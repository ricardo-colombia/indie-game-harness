"""Overnight card runner.

For each ready card (priority order, deps satisfied):
  worktree on card/<id> -> headless Claude implements it (hard $ + turn + time caps)
  -> independent gates (protected paths, build, tests, autotest metrics, cheap review)
  -> one fix attempt with the gate report (resumed session, cache-cheap)
  -> merge to main, or park in backlog/review (taste call) or backlog/failed.
Nothing is pushed. You review in the morning with `hx report`.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import shutil
import time
from pathlib import Path

import unity
from core import REPORTS, ROOT, Card, ledger_add, ledger_total, load_config, next_card, sh

# Agents may not change their own judges. A card can whitelist specific paths via `allow_paths`.
PROTECTED = ["harness/", ".claude/", "backlog/", "scenarios/", "design/PILLARS.md", "CLAUDE.md", ".mcp.json"]
RUNNER_DIR = ROOT / "harness" / "runner"


def git(*args, cwd=ROOT):
    return sh(["git", *args], cwd=cwd)


def claude(prompt: str, cwd: Path, model: str, budget: float, turns: int, timeout_min: float,
           resume: str | None = None, tools: str | None = None) -> dict:
    cmd = ["claude", "-p", prompt, "--model", model, "--output-format", "json",
           "--max-budget-usd", f"{budget:.2f}", "--max-turns", str(turns),
           "--permission-mode", "acceptEdits",
           "--settings", str(RUNNER_DIR / "runner.settings.json"),
           "--strict-mcp-config", "--mcp-config", str(RUNNER_DIR / "mcp.runner.json")]
    if resume:
        cmd += ["--resume", resume]
    if tools:
        cmd += ["--allowedTools", tools]
    r = sh(cmd, cwd=cwd, timeout=timeout_min * 60)
    try:
        data = json.loads(r.stdout.strip().splitlines()[-1])
    except (json.JSONDecodeError, IndexError):
        data = {"is_error": True, "result": r.stdout[-2000:], "total_cost_usd": budget if r.returncode == 124 else 0}
    data["rc"] = r.returncode
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


def run_gates(card: Card, cfg, wt: Path, out: Path) -> tuple[bool, dict]:
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
        d = claude(prompt, wt, rv["model"], rv["budget_usd"], rv["max_turns"], 10,
                   tools="Read,Grep,Glob,Bash(git diff:*),Bash(git log:*)")
        ledger_add("review", d.get("total_cost_usd", 0), card=card.id)
        text = str(d.get("result", ""))
        verdict = next((l for l in reversed(text.splitlines()) if l.startswith("VERDICT:")), "VERDICT: FAIL - no verdict")
        res["review"] = {"ok": verdict.startswith("VERDICT: PASS"), "verdict": verdict, "notes": text[-3000:]}
        if not res["review"]["ok"]:
            return False, res
    return True, res


def write_report(card: Card, out: Path, attempts: list, gates: dict, outcome: str) -> Path:
    lines = [f"# {card.id} — {card.meta.get('title', '')}", f"**Outcome:** {outcome}", ""]
    for i, a in enumerate(attempts, 1):
        lines += [f"## Attempt {i} — ${a.get('total_cost_usd', 0):.2f}, {a.get('num_turns', '?')} turns",
                  str(a.get("result", ""))[-2500:], ""]
    lines += ["## Gates", "```json", json.dumps(gates, indent=2, default=str)[:12000], "```"]
    shots = sorted(out.glob("autotest/*/*.png"))
    if shots:
        lines += ["## Screenshots"] + [f"![{p.stem}]({p.relative_to(out)})" for p in shots]
    rp = out / "report.md"
    rp.write_text("\n".join(lines))
    return rp


def process(card: Card, cfg, remaining: float) -> tuple[str, float]:
    main = cfg["runner"]["main_branch"]
    budget = min(float(card.get("budget_usd", cfg)), remaining)
    out = REPORTS / card.id / dt.datetime.now().strftime("%Y%m%d-%H%M")
    out.mkdir(parents=True, exist_ok=True)
    wt = prepare_worktree(card, cfg)
    model, turns, tmin = card.get("model", cfg), int(card.get("max_turns", cfg)), float(card.get("timeout_min", cfg))
    attempts, gates, spent, session = [], {}, 0.0, None
    prompt = (f"You are running UNATTENDED on backlog card {card.id} in an isolated git worktree. "
              f"Follow the `run-card` skill exactly. The card:\n\n{card.text()}")

    ok = False
    for attempt in range(int(card.meta.get("attempts", 2))):
        left = budget - spent
        if left < 0.25:
            break
        d = claude(prompt, wt, model, left, turns, tmin, resume=session)
        attempts.append(d)
        cost = float(d.get("total_cost_usd", 0) or 0)
        spent += cost
        session = d.get("session_id") or session
        ledger_add("card", cost, card=card.id, attempt=attempt + 1, model=model)
        git("add", "-A", cwd=wt)
        git("commit", "-m", f"{card.id}: {card.meta.get('title', '')} (runner wip)", cwd=wt)
        try:
            ok, gates = run_gates(card, cfg, wt, out)
        except Exception as e:  # env problems (no Unity, missing scenario) fail the card, not the night
            ok, gates = False, {"error": f"{type(e).__name__}: {e}"}
        if ok:
            break
        prompt = ("The runner's independent gates FAILED. Report:\n```json\n"
                  + json.dumps(gates, indent=1, default=str)[:6000]
                  + "\n```\nFix the root cause. Do NOT weaken tests, thresholds or scenarios, and do not touch "
                    "protected paths. Commit when done.")

    if ok and card.meta.get("taste_review"):
        outcome = f"PASSED gates — awaiting your taste review on branch card/{card.id}"
        state = "review"
    elif ok:
        m = git("merge", "--no-ff", "-m", f"Merge card/{card.id}: {card.meta.get('title', '')}", f"card/{card.id}")
        if m.returncode:
            git("merge", "--abort")
            outcome, state = "PASSED gates but merge conflicted — rebase needed", "failed"
        else:
            outcome, state = f"MERGED into {main}", "done"
    else:
        outcome, state = f"FAILED (spent ${spent:.2f}) — branch card/{card.id} kept", "failed"

    rp = write_report(card, out, attempts, gates, outcome)
    card.move(state, f"- {dt.datetime.now():%Y-%m-%d %H:%M} {outcome}. Report: {rp.relative_to(ROOT)}")
    git("add", "-A", "backlog")
    git("commit", "-m", f"backlog: {card.id} -> {state}")
    git("worktree", "remove", "--force", str(wt))
    return state, spent


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--budget", type=float, help="override nightly budget (USD)")
    ap.add_argument("--max-cards", type=int)
    ap.add_argument("--dry-run", action="store_true", help="show what would run")
    a = ap.parse_args()
    cfg = load_config()
    if git("status", "--porcelain").stdout.strip() and not a.dry_run:
        raise SystemExit("Main checkout is dirty; commit or stash before running cards.")
    if not a.dry_run and ("${" in str(cfg["unity"]["editor_path"]) or not cfg["unity"]["editor_path"]):
        print("warning: UNITY_PATH not set — cards whose gates need Unity will fail")
    nightly = a.budget or cfg["runner"]["nightly_budget_usd"]
    max_cards = a.max_cards or cfg["runner"]["max_cards_per_run"]
    start = dt.datetime.now().replace(microsecond=0)  # ledger timestamps are second-precision
    fails = done = 0
    while done < max_cards:
        remaining = nightly - ledger_total(since=start)
        card = next_card()
        if not card or remaining < 0.5:
            print("stop:", "no ready cards" if not card else f"budget exhausted (${remaining:.2f} left)")
            break
        if a.dry_run:
            print(f"would run {card.id} [{card.get('model', cfg)}, ${card.get('budget_usd', cfg)}] {card.meta.get('title')}")
            break
        t0 = time.time()
        state, spent = process(card, cfg, remaining)
        print(f"{card.id}: {state} in {(time.time() - t0) / 60:.0f} min, ${spent:.2f}")
        done += 1
        fails = fails + 1 if state == "failed" else 0
        if fails >= cfg["runner"]["stop_after_consecutive_failures"]:
            print("stop: too many consecutive failures — something systemic is wrong")
            break
    print(f"session spend: ${ledger_total(since=start):.2f}")


if __name__ == "__main__":
    main()
