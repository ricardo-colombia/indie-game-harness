"""Overnight card runner.

Billing model (indie-first):
  * Default: everything runs on your Claude plan's included quota (ANTHROPIC_API_KEY is stripped so
    Claude Code uses your logged-in subscription). $0 is a fully supported way to run.
  * `--budget N` / runner.money_budget_usd > 0 lets the runner ALSO spend real API money, only for:
    cards marked `paid: true`, models your plan doesn't include, or one escalation attempt after a
    card fails on plan quota. Money is never spent when the budget is 0.
  * When plan quota runs out, the current card goes back to ready (not failed) and the runner either
    waits for the quota window to reset (until `run_until`) or stops.

Per card: worktree card/<id> -> headless Claude (turn cap, wall-clock cap, per-card spend cap — on plan
quota this cap is an estimate that keeps one runaway card from eating the whole night's quota)
-> independent gates -> one fix pass -> merge | backlog/review (taste) | backlog/failed. Never pushes.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import shutil
import time
from pathlib import Path

import unity
from core import REPORTS, ROOT, Card, ledger_add, ledger_total, load_config, next_card, sh

# Agents may not change their own judges. A card can whitelist specific paths via `allow_paths`.
PROTECTED = ["harness/", ".claude/", "backlog/", "scenarios/", "design/PILLARS.md", "CLAUDE.md", ".mcp.json"]
RUNNER_DIR = ROOT / "harness" / "runner"
QUOTA_RE = re.compile(r"(usage|rate|session|weekly)[ _-]?limit|limit (reached|will reset|resets)|out of (credits|quota)"
                      r"|credit balance is too low|quota exceeded|429", re.I)


class QuotaExhausted(Exception):
    pass


def git(*args, cwd=ROOT):
    return sh(["git", *args], cwd=cwd)


class Billing:
    """Decides, per call, whether we run on plan quota or paid API, and tracks money."""

    def __init__(self, cfg, money_budget: float, start: dt.datetime):
        r = cfg["runner"]
        self.money_budget, self.start = money_budget, start
        self.plan_models = r.get("plan_models", ["haiku", "sonnet"])
        self.key = os.environ.get(r.get("api_key_env", "ANTHROPIC_API_KEY"), "")
        self.escalation = r.get("escalation", {})

    def money_left(self) -> float:
        return self.money_budget - ledger_total(since=self.start, billed="api")

    def can_pay(self, need=0.25) -> bool:
        return bool(self.key) and self.money_left() >= need

    def env(self, paid: bool) -> tuple[dict, list]:
        """(env to set, env vars to unset) for a claude call."""
        if paid:
            return {"ANTHROPIC_API_KEY": self.key}, []
        return {}, ["ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN"]  # force subscription login

    def choose(self, card: Card, cfg) -> tuple[str, bool] | None:
        """(model, paid) for the first attempt, or None if the card can't run under current funds."""
        forced = os.environ.get("HX_FORCE_MODEL") or cfg["runner"].get("force_model")
        if forced:
            return forced, False
        model = card.get("model", cfg)
        if card.meta.get("paid") or model not in self.plan_models:
            if self.can_pay(float(card.get("budget_usd", cfg)) * 0.5):
                return model, True
            if card.meta.get("paid"):
                return None                          # explicitly needs money; wait for a funded run
            return self.plan_models[-1], False       # downgrade to best plan model
        return model, False


def claude(prompt: str, cwd: Path, model: str, cap: float, turns: int, timeout_min: float, billing: Billing,
           paid: bool, resume: str | None = None, tools: str | None = None) -> dict:
    # prompt goes over stdin: no OS command-line length limits, no quoting issues on Windows
    cmd = ["claude", "-p", "--model", model, "--output-format", "json",
           "--max-budget-usd", f"{cap:.2f}", "--max-turns", str(turns),
           "--permission-mode", "acceptEdits",
           "--settings", str(RUNNER_DIR / "runner.settings.json"),
           "--strict-mcp-config", "--mcp-config", str(RUNNER_DIR / "mcp.runner.json")]
    if resume:
        cmd += ["--resume", resume]
    if tools:
        cmd += ["--allowedTools", tools]
    env, unset = billing.env(paid)
    r = sh(cmd, cwd=cwd, timeout=timeout_min * 60, env=env, unset=unset, stdin=prompt)
    try:
        data = json.loads(r.stdout.strip().splitlines()[-1])
    except (json.JSONDecodeError, IndexError):
        data = {"is_error": True, "result": r.stdout[-2000:], "total_cost_usd": 0}
    data["rc"], data["billed"], data["model"] = r.returncode, "api" if paid else "plan", model
    if (data.get("is_error") or r.returncode) and QUOTA_RE.search(str(data.get("result", "")) + r.stdout[-3000:]):
        raise QuotaExhausted(str(data.get("result", ""))[:300])
    return data


def prepare_worktree(card: Card, cfg) -> Path:
    wt = ROOT / cfg["runner"]["worktrees_dir"] / card.id
    if wt.exists():
        git("worktree", "remove", "--force", str(wt))
    r = git("worktree", "add", "-B", f"card/{card.id}", str(wt), cfg["runner"]["main_branch"])
    if r.returncode:
        raise RuntimeError(r.stdout)
    lib = ROOT / cfg["game"]["unity_project"] / "Library"
    if cfg["runner"]["seed_library_cache"] and lib.exists():
        shutil.copytree(lib, wt / cfg["game"]["unity_project"] / "Library", dirs_exist_ok=True)
    return wt


def changed_files(wt: Path, main: str) -> list[str]:
    return [f for f in git("diff", "--name-only", f"{main}...HEAD", cwd=wt).stdout.splitlines() if f]


def run_gates(card: Card, cfg, wt: Path, out: Path, billing: Billing) -> tuple[bool, dict]:
    main = cfg["runner"]["main_branch"]
    project = wt / cfg["game"]["unity_project"]
    gates = card.get("gates", cfg)
    res: dict = {}

    files = changed_files(wt, main)
    allowed = card.meta.get("allow_paths", [])
    bad = [f for f in files if any(f.startswith(p) for p in PROTECTED) and not any(f.startswith(a) for a in allowed)]
    res["diff"] = {"ok": bool(files) and not bad, "files": len(files),
                   "protected_touched": bad, "note": "" if files else "no changes committed"}
    if not res["diff"]["ok"]:
        return False, res

    if "build" in gates:
        ok, res["build"] = unity.build(cfg, project, out)
        if not ok:
            return False, res
    for plat in ("editmode", "playmode"):
        if plat in gates:
            ok, res[plat] = unity.tests(cfg, project, out, "EditMode" if plat == "editmode" else "PlayMode")
            if not ok:
                return False, res
    if "autotest" in gates:
        scen = ["smoke"] + [s for s in card.meta.get("scenarios", []) or [] if s != "smoke"]
        ok, res["autotest"] = unity.autotest(cfg, project, out, scen)
        if not ok:
            return False, res
    if "review" in gates:
        rv = cfg["runner"]["review"]
        prompt = (f"Review `git diff {main}...HEAD` for backlog card {card.id}. Card:\n\n{card.text()}\n\n"
                  "Use the code-reviewer rules in .claude/agents/code-reviewer.md. "
                  "End with exactly one line: `VERDICT: PASS` or `VERDICT: FAIL - <reason>`.")
        forced = os.environ.get("HX_FORCE_MODEL") or cfg["runner"].get("force_model")
        d = claude(prompt, wt, forced or rv["model"], rv["budget_usd"], rv["max_turns"], 10, billing, paid=False,
                   tools="Read,Grep,Glob,Bash(git diff:*),Bash(git log:*)")
        ledger_add("review", d.get("total_cost_usd", 0) or 0, card=card.id, billed="plan")
        text = str(d.get("result", ""))
        verdict = next((l for l in reversed(text.splitlines()) if l.startswith("VERDICT:")), "VERDICT: FAIL - no verdict")
        res["review"] = {"ok": verdict.startswith("VERDICT: PASS"), "verdict": verdict, "notes": text[-3000:]}
        if not res["review"]["ok"]:
            return False, res
    return True, res


def write_report(card: Card, out: Path, attempts: list, gates: dict, outcome: str) -> Path:
    lines = [f"# {card.id} — {card.meta.get('title', '')}", f"**Outcome:** {outcome}", ""]
    for i, a in enumerate(attempts, 1):
        lines += [f"## Attempt {i} — {a.get('model')} on {a.get('billed')}, ~${a.get('total_cost_usd', 0) or 0:.2f}"
                  f"{'' if a.get('billed') == 'api' else ' (plan quota, not billed)'}, {a.get('num_turns', '?')} turns",
                  str(a.get("result", ""))[-2500:], ""]
    lines += ["## Gates", "```json", json.dumps(gates, indent=2, default=str)[:12000], "```"]
    shots = sorted(out.glob("autotest/*/*.png"))
    if shots:
        lines += ["## Screenshots"] + [f"![{p.stem}]({p.relative_to(out)})" for p in shots]
    rp = out / "report.md"
    rp.write_text("\n".join(lines))
    return rp


def finish(card: Card, wt: Path, state: str, note: str):
    card.move(state, note)
    git("add", "-A", "backlog")
    git("commit", "-m", f"backlog: {card.id} -> {state}")
    git("worktree", "remove", "--force", str(wt))


def process(card: Card, cfg, billing: Billing) -> tuple[str, float]:
    """Returns (state, money_spent). Raises QuotaExhausted after putting the card back to ready."""
    main = cfg["runner"]["main_branch"]
    choice = billing.choose(card, cfg)
    if choice is None:
        return "skipped", 0.0
    model, paid = choice
    cap = float(card.get("budget_usd", cfg))
    out = REPORTS / card.id / dt.datetime.now().strftime("%Y%m%d-%H%M")
    out.mkdir(parents=True, exist_ok=True)
    wt = prepare_worktree(card, cfg)
    turns, tmin = int(card.get("max_turns", cfg)), float(card.get("timeout_min", cfg))
    attempts, gates, used, money, session = [], {}, 0.0, 0.0, None
    prompt = (f"You are running UNATTENDED on backlog card {card.id} in an isolated git worktree. "
              f"Follow the `run-card` skill exactly. The card:\n\n{card.text()}")
    plan = [(model, paid)] * int(card.meta.get("attempts", 2))
    esc = billing.escalation
    forced = os.environ.get("HX_FORCE_MODEL") or cfg["runner"].get("force_model")
    if esc.get("enabled") and card.meta.get("escalate", True) and not paid and not forced:
        plan.append((esc.get("model", "opus"), True))  # used only if money is available at that point

    ok = False
    try:
        for i, (m, p) in enumerate(plan):
            is_escalation = i == len(plan) - 1 and p and not paid
            if is_escalation:
                if not billing.can_pay(float(esc.get("budget_usd", 3))):
                    break
                left = min(float(esc.get("budget_usd", 3)), billing.money_left())
            else:
                left = cap - used
                if p:
                    left = min(left, billing.money_left())
            if left < 0.25:
                break
            d = claude(prompt, wt, m, left, turns, tmin, billing, p, resume=session)
            attempts.append(d)
            cost = float(d.get("total_cost_usd", 0) or 0)
            used += cost
            money += cost if p else 0
            session = d.get("session_id") or session
            ledger_add("card", cost, card=card.id, attempt=i + 1, model=m, billed="api" if p else "plan")
            git("add", "-A", cwd=wt)
            git("commit", "-m", f"{card.id}: {card.meta.get('title', '')} (runner wip)", cwd=wt)
            try:
                ok, gates = run_gates(card, cfg, wt, out, billing)
            except QuotaExhausted:
                raise
            except Exception as e:  # env problems (no Unity, missing scenario) fail the card, not the night
                ok, gates = False, {"error": f"{type(e).__name__}: {e}"}
            if ok:
                break
            prompt = ("The runner's independent gates FAILED. Report:\n```json\n"
                      + json.dumps(gates, indent=1, default=str)[:6000]
                      + "\n```\nFix the root cause. Do NOT weaken tests, thresholds or scenarios, and do not touch "
                        "protected paths. Commit when done.")
    except QuotaExhausted as e:
        write_report(card, out, attempts, gates, f"PAUSED — plan quota exhausted ({e})")
        git("worktree", "remove", "--force", str(wt))
        raise

    if ok and card.meta.get("taste_review"):
        outcome, state = f"PASSED gates — awaiting your taste review on branch card/{card.id}", "review"
    elif ok:
        mres = git("merge", "--no-ff", "-m", f"Merge card/{card.id}: {card.meta.get('title', '')}", f"card/{card.id}")
        if mres.returncode:
            git("merge", "--abort")
            outcome, state = "PASSED gates but merge conflicted — rebase needed", "failed"
        else:
            outcome, state = f"MERGED into {main}", "done"
    else:
        outcome, state = f"FAILED — branch card/{card.id} kept", "failed"
    outcome += f" · money ${money:.2f}" if money else " · $0 (plan quota)"
    rp = write_report(card, out, attempts, gates, outcome)
    finish(card, wt, state, f"- {dt.datetime.now():%Y-%m-%d %H:%M} {outcome}. Report: {rp.relative_to(ROOT)}")
    return state, money


def past(hhmm: str | None, start: dt.datetime) -> bool:
    if not hhmm:
        return False
    h, m = map(int, hhmm.split(":"))
    stop = start.replace(hour=h, minute=m, second=0)
    if stop <= start:
        stop += dt.timedelta(days=1)
    return dt.datetime.now() >= stop


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--budget", type=float, help="real money (USD) this session may spend; default config money_budget_usd (0)")
    ap.add_argument("--max-cards", type=int)
    ap.add_argument("--until", help="HH:MM local; stop starting new cards after this (default config run_until)")
    ap.add_argument("--dry-run", action="store_true", help="show what would run")
    a = ap.parse_args()
    cfg = load_config()
    r = cfg["runner"]
    if git("status", "--porcelain").stdout.strip() and not a.dry_run:
        raise SystemExit("Main checkout is dirty; commit or stash before running cards.")
    if not a.dry_run and ("${" in str(cfg["unity"]["editor_path"]) or not cfg["unity"]["editor_path"]):
        print("warning: UNITY_PATH not set — cards whose gates need Unity will fail")
    start = dt.datetime.now().replace(microsecond=0)  # ledger timestamps are second-precision
    billing = Billing(cfg, a.budget if a.budget is not None else float(r.get("money_budget_usd", 0)), start)
    until = a.until or r.get("run_until")
    max_cards = a.max_cards or r["max_cards_per_run"]
    print(f"mode: plan quota" + (f" + up to ${billing.money_budget:.2f} API" if billing.money_budget > 0 else " only ($0)"))

    fails = done = 0
    skipped: set[str] = set()
    while done < max_cards and not past(until, start):
        card = next_card(exclude=skipped)
        if not card:
            print("stop: no runnable ready cards")
            break
        if a.dry_run:
            ch = billing.choose(card, cfg)
            print(f"would run {card.id} " + (f"[{ch[0]} on {'api' if ch[1] else 'plan'}]" if ch else "[skip: needs money]")
                  + f" {card.meta.get('title')}")
            break
        t0 = time.time()
        try:
            state, money = process(card, cfg, billing)
        except QuotaExhausted as e:
            print(f"plan quota exhausted during {card.id}: {e}")
            if r.get("on_quota_exhausted", "wait") == "wait" and not past(until, start):
                wait = int(r.get("quota_wait_min", 30))
                print(f"waiting {wait} min for the quota window to reset (until {until or 'no limit'})")
                time.sleep(wait * 60)
                continue  # card is still in ready; retried from scratch
            break
        if state == "skipped":
            skipped.add(card.id)
            print(f"{card.id}: skipped (paid: true and no money budget)")
            continue
        print(f"{card.id}: {state} in {(time.time() - t0) / 60:.0f} min, money ${money:.2f}")
        done += 1
        fails = fails + 1 if state == "failed" else 0
        if fails >= r["stop_after_consecutive_failures"]:
            print("stop: too many consecutive failures — something systemic is wrong")
            break
    print(f"session: money ${ledger_total(since=start, billed='api'):.2f}, "
          f"plan-quota usage ≈${ledger_total(since=start, billed='plan'):.2f} equivalent (not billed)")


if __name__ == "__main__":
    main()
