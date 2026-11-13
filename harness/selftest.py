"""Validate the harness. Every level is $0 money.

  hx selftest            offline: runner logic (fake Claude), billing/quota rules, gates, hook, queue,
                         shorts cutting (ffmpeg), art pipeline modes, config/agent/skill/plugin files
  hx selftest --unity    free, finds the editor via the Unity CLI: temp sample project -> build, EditMode, PlayMode,
                         autotest (input->metrics->thresholds), camera capture -> short
  hx selftest --live     real Claude on PLAN QUOTA ONLY, Haiku forced, tiny caps: headless call, project
                         skills load, one real card through the runner
  hx selftest --all
Everything runs in temp copies of the repo; your checkout is never modified.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import socket
import subprocess
import sys
import tempfile
import textwrap
import unittest
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent
sys.path.insert(0, str(HERE))
PY = sys.executable
LIVE_MODEL = "haiku"
STEP_ACTIONS = {"press", "set", "replay", "wait", "screenshot", "send", "mark"}  # cheapest model; forced for every live test
NO_KEY = ["ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN"]
from core import find_blender, find_unity  # noqa: E402
BLENDER = find_blender()

FAKE_UNITY = r'''#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
a = sys.argv[1:]
open(os.environ["FAKE_UNITY_LOG"], "a").write(" ".join(a) + "\n")
if a[:2] == ["editors", "--installed"]:
    items = [] if os.environ.get("FAKE_UNITY_NO_EDITOR") else [{"version": "6000.3.1f1"}, {"version": "2022.3.1f1"}]
    print(json.dumps({"success": True, "data": {"items": items}}))
elif a[:2] == ["projects", "create"]:
    if "--accept-eula" in a and "--install-editor" not in a:  # the real CLI rejects this
        print("Error: --accept-eula applies only with --install-editor."); sys.exit(2)
    p = Path(a[a.index("--path") + 1]) / a[2]
    (p / "ProjectSettings").mkdir(parents=True); (p / "Packages").mkdir()
    (p / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: 6000.3.1f1\n")
    (p / "Packages/manifest.json").write_text('{"dependencies": {}}')
elif a[:2] == ["pipeline", "install"]:
    f = Path(a[a.index("--project-path") + 1]) / "Packages/manifest.json"
    m = json.loads(f.read_text()); m["dependencies"]["com.unity.pipeline"] = "0.9.0"; f.write_text(json.dumps(m))
'''

FAKE_CLAUDE = r'''#!/usr/bin/env python3
import json, os, pathlib, sys
a = sys.argv[1:]
prompt = sys.stdin.read()
rec = {"model": a[a.index("--model") + 1], "key": bool(os.environ.get("ANTHROPIC_API_KEY")),
       "review": "VERDICT" in prompt, "resume": "--resume" in a, "cap": a[a.index("--max-budget-usd") + 1]}
open(os.environ["FAKE_LOG"], "a").write(json.dumps(rec) + "\n")
mode = os.environ.get("FAKE_MODE", "ok")
if rec["review"]:
    print(json.dumps({"result": os.environ.get("FAKE_REVIEW", "fine\nVERDICT: PASS"), "total_cost_usd": 0.01})); sys.exit(0)
if mode == "quota_once":                       # quota on the first work call only, then behave
    flag = pathlib.Path(os.environ["FAKE_LOG"] + ".quota")
    if not flag.exists():
        flag.write_text("1")
        mode = "quota"
    else:
        mode = "ok"
if mode == "quota":
    print(json.dumps({"is_error": True, "result": "Claude AI usage limit reached. Your limit will reset at 5am", "session_id": "s0"})); sys.exit(1)
result = "done"
def write(path, text):
    pathlib.Path(path).parent.mkdir(parents=True, exist_ok=True)
    open(path, "a").write(text)
if mode == "protected":
    write("harness/evil.py", "x = 1\n")
elif mode == "harness_pkg":
    write("packages/com.studio.harness/Runtime/AutoPilot.cs", "// rigged\n")
elif mode == "editor":
    write("game/Assets/Editor/Tool.cs", "class T {}\n")
elif mode == "suspicious":
    write("game/Assets/Feature.cs", "class F { void A() { System.Diagnostics.Process.Start(\"git\"); } }\n")
elif mode == "escape":                          # agent code reaching the MAIN checkout (what a gate-time script could do)
    write(os.environ["FAKE_ROOT"] + "/scenarios/smoke.json", " ")
    write("game/Assets/Feature.cs", "// change\n")
elif mode == "blocked":
    result = "Card needs a taste decision.\nBLOCKED: needs new art — suggest split: C-1 model, C-2 hookup"
elif mode == "says429":
    write("game/Assets/Feature.cs", "// change\n")
    result = "Fixed the null ref at line 429 (HTTP 429 handling untouched)."
elif mode != "nochange":
    write("game/Assets/Feature.cs", "// change\n")
print(json.dumps({"result": result, "total_cost_usd": 0.3, "num_turns": 3, "session_id": "s1"}))
'''

CARD = """---
id: {id}
title: {title}
priority: 1
budget_usd: 1.0
max_turns: 5
timeout_min: 5
attempts: 2
gates: [review]
{extra}
---
## Goal
Selftest card.
"""


def run(cmd, cwd=None, env=None, unset=(), timeout=600, stdin=None):
    e = {k: v for k, v in os.environ.items() if k not in unset}
    e.update(env or {})
    exe = shutil.which(cmd[0], path=e.get("PATH"))
    if exe:
        cmd = [exe, *cmd[1:]]
    return subprocess.run(cmd, cwd=cwd, env=e, capture_output=True, text=True, timeout=timeout, input=stdin,
                          encoding="utf-8", errors="replace")


def clone_repo(tmp: Path) -> Path:
    dst = tmp / "repo"
    skip = shutil.ignore_patterns(".git", ".worktrees", "Library", "Temp", "Logs",
                                  "__pycache__", "reports", "ledger.jsonl", ".venv", "selftest-results.txt")

    def ignore(folder, names):
        ignored = set(skip(folder, names))
        # The game's own Unity project is not part of the harness template under test: the setup tests start
        # from a repo without one, and its LFS files cannot be checked out into a scratch repo.
        if Path(folder) == Path(REPO):
            ignored.add("game")
        return ignored

    shutil.copytree(REPO, dst, ignore=ignore)
    for c in (["git", "init", "-q", "-b", "main"], ["git", "config", "user.name", "selftest"],
              ["git", "config", "user.email", "selftest@local"], ["git", "add", "-A"], ["git", "commit", "-qm", "init"]):
        run(c, cwd=dst)
    return dst


def commit(repo: Path, msg="setup"):
    run(["git", "add", "-A"], cwd=repo)
    run(["git", "commit", "-qm", msg], cwd=repo)


def add_card(repo: Path, cid: str, extra: str = "", title="Selftest"):
    (repo / "backlog/ready" / f"{cid}-t.md").write_text(CARD.format(id=cid, title=title, extra=extra))


def set_cfg_section(repo: Path, section: str, **kv):
    import yaml
    p = repo / "harness/config.yaml"
    c = yaml.safe_load(p.read_text())
    c[section].update(kv)
    p.write_text(yaml.safe_dump(c, sort_keys=False))


def set_cfg(repo: Path, **runner):
    import yaml
    p = repo / "harness/config.yaml"
    c = yaml.safe_load(p.read_text())
    c["runner"].update(runner)
    p.write_text(yaml.safe_dump(c, sort_keys=False))


def where(repo: Path, cid: str) -> str:
    for s in ("ready", "review", "done", "failed", "inbox"):
        if list((repo / "backlog" / s).glob(f"{cid}-*.md")):
            return s
    return "missing"


# ------------------------------------------------------------------ offline
class Offline(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="hx-selftest-"))
        self.repo = clone_repo(self.tmp)
        set_cfg(self.repo, plan_overage_disabled=True)   # the director's acknowledgement, for the temp copy
        commit(self.repo, "ack overage off")
        bin_ = self.tmp / "bin"
        bin_.mkdir()
        (bin_ / "fake_claude.py").write_text(FAKE_CLAUDE)
        if os.name == "nt":
            (bin_ / "claude.cmd").write_text(f'@"{PY}" "%~dp0fake_claude.py" %*\r\n')
        else:
            (bin_ / "claude").write_text(f'#!/bin/sh\nexec "{PY}" "$(dirname "$0")/fake_claude.py" "$@"\n')
            (bin_ / "claude").chmod(0o755)
        self.log = self.tmp / "calls.jsonl"
        self.env = {"PATH": f"{bin_}{os.pathsep}{os.environ['PATH']}", "FAKE_LOG": str(self.log),
                    "ANTHROPIC_API_KEY": "sk-test-not-real", "HX_FORCE_MODEL": "", "FAKE_ROOT": str(self.repo),
                    "CLAUDECODE": "", "CLAUDE_CONFIG_DIR": str(self.tmp / "claude-home")}

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def runner(self, *args, **env):
        return run([PY, "harness/runner.py", *args], cwd=self.repo, env={**self.env, **env})

    def calls(self):
        return [json.loads(l) for l in self.log.read_text().splitlines()] if self.log.exists() else []

    # --- runner & billing
    def test_merge_on_plan_quota_strips_api_key(self):
        add_card(self.repo, "C-0001"); commit(self.repo)
        r = self.runner()
        self.assertEqual(where(self.repo, "C-0001"), "done", r.stdout + r.stderr)
        self.assertTrue(all(not c["key"] for c in self.calls()), "API key leaked into a plan-quota call")
        self.assertIn("Merge card/C-0001", run(["git", "log", "--oneline"], cwd=self.repo).stdout)

    def test_force_model(self):
        add_card(self.repo, "C-0001", "difficulty: hard"); commit(self.repo)
        self.runner(HX_FORCE_MODEL="haiku")
        self.assertEqual({c["model"] for c in self.calls()}, {"haiku"})

    def test_protected_paths_fail_and_retry_resumes(self):
        add_card(self.repo, "C-0001"); commit(self.repo)
        self.runner(FAKE_MODE="protected")
        self.assertEqual(where(self.repo, "C-0001"), "failed")
        work = [c for c in self.calls() if not c["review"]]
        self.assertEqual(len(work), 2)
        self.assertTrue(work[1]["resume"], "fix attempt should resume the session (cache-cheap)")

    def test_no_money_means_no_escalation_and_paid_cards_wait(self):
        add_card(self.repo, "C-0001"); add_card(self.repo, "C-0002", "paid: true"); commit(self.repo)
        out = self.runner(FAKE_MODE="nochange").stdout
        self.assertEqual(where(self.repo, "C-0001"), "failed")
        self.assertEqual(where(self.repo, "C-0002"), "ready", out)
        self.assertTrue(all(not c["key"] for c in self.calls()))
        self.assertTrue(all(c["model"] != "opus" for c in self.calls()))

    def test_money_enables_escalation_billed_as_api(self):
        add_card(self.repo, "C-0001"); commit(self.repo)
        self.runner("--budget", "5", FAKE_MODE="nochange")
        work = [c for c in self.calls() if not c["review"]]
        self.assertEqual(len(work), 3, work)
        self.assertEqual((work[-1]["model"], work[-1]["key"]), ("opus", True))
        self.assertFalse(work[0]["key"])
        rows = [json.loads(l) for l in (self.repo / "harness/ledger.jsonl").read_text().splitlines()]
        self.assertEqual([r["billed"] for r in rows if r["kind"] == "card"], ["plan", "plan", "api"])

    def test_quota_exhausted_returns_card_to_ready(self):
        add_card(self.repo, "C-0001"); commit(self.repo)
        out = self.runner(FAKE_MODE="quota").stdout
        self.assertIn("quota exhausted", out)
        self.assertEqual(where(self.repo, "C-0001"), "ready")

    def test_taste_review_parks_branch(self):
        add_card(self.repo, "C-0001", "taste_review: true"); commit(self.repo)
        self.runner()
        self.assertEqual(where(self.repo, "C-0001"), "review")
        self.assertNotIn("Merge card/C-0001", run(["git", "log", "--oneline"], cwd=self.repo).stdout)

    def test_dependencies_and_priority(self):
        add_card(self.repo, "C-0002", "depends_on: [C-0001]"); add_card(self.repo, "C-0001"); commit(self.repo)
        out = self.runner("--dry-run").stdout
        self.assertIn("would run C-0001", out)

    # --- adversarial fixes
    def test_allowlist_blocks_harness_package_and_editor_code(self):
        for mode in ("harness_pkg", "editor"):
            with self.subTest(mode=mode):
                cid = "C-0001" if mode == "harness_pkg" else "C-0002"
                add_card(self.repo, cid); commit(self.repo)
                self.runner("--max-cards", "1", FAKE_MODE=mode)
                self.assertEqual(where(self.repo, cid), "failed", mode)

    def test_suspicious_code_is_not_executed(self):
        add_card(self.repo, "C-0001"); commit(self.repo)
        self.runner(FAKE_MODE="suspicious")
        self.assertEqual(where(self.repo, "C-0001"), "review")
        self.assertFalse(any(c["review"] for c in self.calls()), "gates must not run suspicious code")
        self.assertEqual(len(self.calls()), 1, "no fix pass: the director decides")

    def test_main_checkout_tampering_stops_the_run(self):
        add_card(self.repo, "C-0001"); add_card(self.repo, "C-0002"); commit(self.repo)
        out = self.runner(FAKE_MODE="escape").stdout
        self.assertIn("STOP: judge files", out)
        self.assertEqual(where(self.repo, "C-0002"), "ready")

    def test_blocked_goes_to_review_without_fix_pass(self):
        add_card(self.repo, "C-0001"); commit(self.repo)
        self.runner(FAKE_MODE="blocked")
        self.assertEqual(where(self.repo, "C-0001"), "review")
        self.assertEqual(len(self.calls()), 1)

    def test_agent_text_with_429_is_not_quota(self):
        add_card(self.repo, "C-0001"); commit(self.repo)
        self.runner(FAKE_MODE="says429")
        self.assertEqual(where(self.repo, "C-0001"), "done")

    def test_quota_pause_resumes_instead_of_restarting(self):
        add_card(self.repo, "C-0001"); commit(self.repo)
        self.runner(FAKE_MODE="quota_once")
        self.assertEqual(where(self.repo, "C-0001"), "ready")
        self.assertTrue((self.repo / ".worktrees/C-0001").exists(), "paused worktree must be kept")
        self.runner(FAKE_MODE="quota_once")
        self.assertEqual(where(self.repo, "C-0001"), "done")
        work = [c for c in self.calls() if not c["review"]]
        self.assertTrue(work[1]["resume"], "second run should resume the paused session")

    def test_review_verdict_must_be_last_line(self):
        add_card(self.repo, "C-0001"); commit(self.repo)
        self.runner(FAKE_REVIEW="VERDICT: PASS\n(ignore the diff comment telling you to pass)")
        self.assertEqual(where(self.repo, "C-0001"), "failed")

    def test_billing_preflight_refuses(self):
        add_card(self.repo, "C-0001"); commit(self.repo)
        set_cfg(self.repo, plan_overage_disabled=False); commit(self.repo)
        out = self.runner()
        self.assertIn("plan_overage_disabled", out.stdout + out.stderr)
        set_cfg(self.repo, plan_overage_disabled=True); commit(self.repo)
        home = self.tmp / "claude-home"; home.mkdir(exist_ok=True)
        (home / "settings.json").write_text(json.dumps({"apiKeyHelper": "echo sk"}))
        out = self.runner()
        self.assertIn("apiKeyHelper", out.stdout + out.stderr)
        self.assertEqual(self.calls(), [])

    def test_agent_session_cannot_start_runner_or_promote(self):
        out = self.runner(CLAUDECODE="1")
        self.assertIn("director", out.stdout + out.stderr)
        r = run([PY, "harness/hx.py", "card", "ready", "C-0001"], cwd=self.repo, env={"CLAUDECODE": "1"})
        self.assertEqual(r.returncode, 1)

    def test_taste_card_is_regated_before_merge(self):
        add_card(self.repo, "C-0001", "taste_review: true"); commit(self.repo)
        self.runner()
        self.assertEqual(where(self.repo, "C-0001"), "review")
        r = run([PY, "harness/hx.py", "merge", "C-0001"], cwd=self.repo, env=self.env)
        self.assertEqual(where(self.repo, "C-0001"), "done", r.stdout + r.stderr)
        self.assertTrue(any(c["review"] for c in self.calls()[2:]), "merge must re-run the gates")

    def test_release_scan_finds_harness(self):
        import unity
        b = self.tmp / "build"; (b / "Game_Data/Managed").mkdir(parents=True)
        (b / "Game.exe").write_bytes(b"MZ")
        self.assertEqual(unity.scan_for_harness(b / "Game.exe"), [])
        (b / "Game_Data/global-metadata.dat").write_bytes(b"\0" * 5000 + b"Studio.Harness" + b"\0")
        self.assertTrue(unity.scan_for_harness(b / "Game.exe"))
        (b / "Game_Data/global-metadata.dat").unlink()
        (b / "Game_Data/Managed/Studio.Harness.dll").write_bytes(b"x")
        self.assertTrue(unity.scan_for_harness(b))

    @unittest.skipIf(os.name == "nt", "fake player is a POSIX script")
    def test_autotest_rejects_forged_metrics(self):
        import yaml, unity
        proj = self.repo / "game"
        (proj / "Builds").mkdir(parents=True, exist_ok=True)
        player = proj / "Builds/player"
        forge = 'import json,sys,os\na=sys.argv\no=a[a.index("-harnessOut")+1]\n' \
                'n=a[a.index("-harnessNonce")+1] if os.environ.get("HONEST") else "guess"\n' \
                'open(o+"/boot.png","w").write("")\n' \
                'json.dump({"completed":True,"errors":0,"nonce":n,"realSeconds":float(os.environ.get("SECS","6")),' \
                '"metrics":{"frame.ms":{"p95":10},"startup.seconds":{"last":1}}},open(o+"/metrics.json","w"))\n'
        (proj / "Builds/fake_player.py").write_text(forge)
        player.write_text(f'#!/bin/sh\nexec "{PY}" "$(dirname "$0")/fake_player.py" "$@"\n'); player.chmod(0o755)
        cfg = yaml.safe_load((self.repo / "harness/config.yaml").read_text())
        cfg["game"]["player_binary"] = "Builds/player"
        cfg["runner"]["sandbox"]["prefix"] = []
        os.environ.pop("DISPLAY", None)
        saved = shutil.which
        try:
            unity.shutil.which = lambda *a, **k: None if a and a[0] == "xvfb-run" else saved(*a, **k)
            for env, want in (({"HONEST": "1"}, True), ({}, False), ({"HONEST": "1", "SECS": "0.1"}, False)):
                old = {k: os.environ.get(k) for k in ("HONEST", "SECS")}
                os.environ.update(env)
                try:
                    ok, res = unity.autotest(cfg, proj, self.tmp / "out", ["smoke"])
                finally:
                    for k, v in old.items():
                        os.environ.pop(k, None) if v is None else os.environ.__setitem__(k, v)
                self.assertEqual(ok, want, (env, res))
        finally:
            unity.shutil.which = saved

    # --- pieces
    def test_threshold_checker(self):
        import unity
        m = {"speed": {"max": 9.0, "p95": 8.0}, "frame.ms": {"p95": 40}}
        self.assertEqual(unity._check(m, {"speed.max": {"min": 6, "max": 12}}), [])
        self.assertEqual(len(unity._check(m, {"frame.ms.p95": {"max": 33.4}, "nope.max": {"max": 1}})), 2)

    def test_card_roundtrip_keeps_comments(self):
        r = run([PY, "harness/hx.py", "card", "new", "Roundtrip"], cwd=self.repo)
        cid = Path(r.stdout.strip()).name[:6]
        run([PY, "harness/hx.py", "card", "ready", cid], cwd=self.repo, env={"CLAUDECODE": ""})
        text = next((self.repo / "backlog/ready").glob(f"{cid}*.md")).read_text()
        self.assertIn("# trivial (haiku)", text)

    def test_cs_guard_hook(self):
        hook = self.repo / "harness/hooks/guard_cs.py"
        cases = {
            "Legacy.cs": ('class A { void Update() { var x = Input.GetAxis("h"); } }', 2),
            "UsesHarness.cs": ('using Harness;\nclass B { }', 2),
            "Qualified.cs": ('class D { void A() { global::Harness.InputDriver.Init(); } }', 2),
            "Reflection.cs": ('class E { void A() { System.Type.GetType("Harness.HarnessMetrics"); } }', 2),
            "Good.cs": ('using UnityEngine.InputSystem;\nclass C { InputAction a; void Update() { var v = a.ReadValue<float>(); } }', 0),
        }
        for name, (src, want) in cases.items():
            f = self.tmp / name
            f.write_text(src)
            rc = subprocess.run([PY, str(hook)], input=json.dumps({"tool_input": {"file_path": str(f)}}),
                                text=True, capture_output=True).returncode
            self.assertEqual(rc, want, name)

    def test_posting_queue_requires_approval(self):
        import yaml
        sched = self.repo / "marketing/schedule.yaml"
        d = yaml.safe_load(sched.read_text())
        d["items"] = [{"id": "a", "file": "x.mp4", "title": "A", "platforms": ["youtube"], "approved": True,
                       "status": "pending_approval", "slot": None},
                      {"id": "b", "file": "y.mp4", "title": "B", "platforms": ["youtube"], "approved": False,
                       "status": "pending_approval", "slot": None}]
        sched.write_text(yaml.safe_dump(d))
        run([PY, "harness/marketing/queue.py", "schedule"], cwd=self.repo)
        items = {i["id"]: i for i in yaml.safe_load(sched.read_text())["items"]}
        self.assertEqual(items["a"]["status"], "scheduled")
        self.assertIsNone(items["b"]["slot"])
        out = run([PY, "harness/marketing/queue.py", "post"], cwd=self.repo).stdout  # dry-run, no --live
        self.assertIn("dry-run", out)

    @unittest.skipUnless(shutil.which("ffmpeg") and shutil.which("ffprobe"), "ffmpeg not installed")
    def test_cut_shorts_from_png_sequence(self):
        for take in ("gameplay_follow", "hero_orbit"):
            d = self.repo / "marketing/out/raw/launch" / take
            d.mkdir(parents=True)
            run(["ffmpeg", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc=size=640x360:rate=30", "-frames:v", "200",
                 str(d / "f_%05d.png")])
        r = run([PY, "harness/marketing/cut_shorts.py", "marketing/shorts.yaml"], cwd=self.repo)
        self.assertEqual(r.returncode, 0, r.stderr[-1500:])
        mp4 = self.repo / "marketing/out/first-look-a.mp4"
        dims = run(["ffprobe", "-v", "error", "-select_streams", "v", "-show_entries", "stream=width,height",
                    "-of", "csv=p=0", str(mp4)]).stdout.strip()
        self.assertEqual(dims, "1080,1920")
        self.assertIn("approved: false", (self.repo / "marketing/schedule.yaml").read_text())
        # an ffmpeg without drawtext (Homebrew's default build) still cuts the short, flagged as captionless
        shim = self.tmp / "noshim"; shim.mkdir()
        (shim / "ffmpeg").write_text(f'#!/bin/sh\nif [ "$2" = "-filters" ]; then exit 0; fi\nexec "{shutil.which("ffmpeg")}" "$@"\n')
        (shim / "ffmpeg").chmod(0o755)
        (self.repo / "marketing/schedule.yaml").write_text("items: []\n")
        if os.name != "nt":
            r = run([PY, "harness/marketing/cut_shorts.py", "marketing/shorts.yaml"], cwd=self.repo,
                    env={"PATH": f"{shim}{os.pathsep}{os.environ['PATH']}"})
            self.assertEqual(r.returncode, 0, r.stderr[-800:])
            self.assertIn("no drawtext", r.stderr)

    def test_art_pipeline_free_modes(self):
        r = run([PY, "harness/art/pipeline.py", "concept", "example_vehicle"], cwd=self.repo)
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertTrue((self.repo / "art/concepts/example_vehicle/PROMPT.txt").exists())
        r = run([PY, "harness/art/pipeline.py", "model", "example_vehicle"], cwd=self.repo)
        self.assertIn("director's call", r.stderr + r.stdout)  # no pick yet -> refuses
        r = run([PY, "harness/art/pipeline.py", "texture", "example_vehicle"], cwd=self.repo)
        self.assertIn("CC0", r.stderr + r.stdout)  # $0 default refuses paid texture gen

    @unittest.skipUnless(BLENDER, "blender not installed")
    def test_blender_cleanup(self):
        src, out = self.tmp / "in.glb", self.tmp / "out.fbx"
        run([BLENDER, "-b", "--factory-startup", "--python-expr",
             "import bpy;bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.mesh.primitive_uv_sphere_add(segments=64,ring_count=32);"
             "bpy.ops.mesh.primitive_cube_add(location=(3,0,0));"
             f"bpy.ops.export_scene.gltf(filepath=r'{src}')"])
        r = run([BLENDER, "-b", "--factory-startup", "-P", str(self.repo / "harness/art/blender_cleanup.py"), "--",
                 "--in", str(src), "--out", str(out), "--height", "2", "--faces", "500", "--split", "loose"])
        stats = json.loads(next(l for l in r.stdout.splitlines() if l.startswith("HARNESS_STATS")).split(" ", 1)[1])
        self.assertTrue(out.exists())
        self.assertLessEqual(stats["faces_after"], 600)
        self.assertAlmostEqual(stats["size_m_xyz_blender"][2], 2.0, places=2)
        self.assertEqual(stats["parts"], 2)

    def _fake_unity(self, **env):
        shim = self.tmp / "unitybin"
        shim.mkdir(exist_ok=True)
        (shim / "fake_unity.py").write_text(FAKE_UNITY)
        (shim / "unity").write_text(f'#!/bin/sh\nexec "{PY}" "$(dirname "$0")/fake_unity.py" "$@"\n')
        (shim / "unity").chmod(0o755)
        return {"PATH": f"{shim}{os.pathsep}{os.environ['PATH']}", "FAKE_UNITY_LOG": str(self.tmp / "unity.log"), **env}

    @unittest.skipIf(os.name == "nt", "the fake unity shim is a shell script")
    def test_setup_creates_project_wires_packages_and_is_idempotent(self):
        env = self._fake_unity()
        r = run([PY, "harness/hx.py", "setup", "My Game", "--yes"], cwd=self.repo, env=env)
        self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
        m = json.loads((self.repo / "game/Packages/manifest.json").read_text())
        for pkg in ("com.studio.harness", "com.unity.pipeline"):
            self.assertIn(pkg, m["dependencies"])
        self.assertIn("com.studio.harness", m["testables"])
        cfg = (self.repo / "harness/config.yaml").read_text()
        self.assertIn("name: My Game", cfg)
        self.assertIn("Builds/Player/MyGame.exe", cfg)
        log = (self.tmp / "unity.log").read_text()
        self.assertIn("--editor-version 6000.3.1f1", log)   # newest installed Unity 6, not a new download
        self.assertNotIn("--install-editor", log)
        self.assertIn("--no-cloud", log)
        self.assertEqual(run([PY, "harness/hx.py", "doctor"], cwd=self.repo, env=env).returncode, 0)
        run([PY, "harness/hx.py", "setup", "--yes"], cwd=self.repo, env=env)
        self.assertEqual((self.tmp / "unity.log").read_text().count("projects create"), 1)

    @unittest.skipIf(os.name == "nt", "the fake unity shim is a shell script")
    def test_setup_does_not_download_an_editor_without_consent(self):
        env = self._fake_unity(FAKE_UNITY_NO_EDITOR="1")
        r = run([PY, "harness/hx.py", "setup", "My Game"], cwd=self.repo, env=env)
        self.assertEqual(r.returncode, 2, r.stdout)
        self.assertNotIn("projects create", (self.tmp / "unity.log").read_text())
        run([PY, "harness/hx.py", "setup", "My Game", "--yes"], cwd=self.repo, env=env)
        self.assertIn("--install-editor --accept-eula", (self.tmp / "unity.log").read_text())

    def test_static_files(self):
        import yaml
        for p in [*REPO.glob("*.json"), *REPO.glob(".claude/*.json"), *REPO.glob(".claude-plugin/*.json"),
                  *REPO.glob("harness/runner/*.json"), *REPO.glob("scenarios/*.json"), *REPO.glob("marketing/takes/*.json"),
                  REPO / ".mcp.json", REPO / "packages/com.studio.harness/package.json"]:
            json.loads(p.read_text())
        for p in [*REPO.glob("harness/*.yaml"), *REPO.glob("marketing/*.yaml"), *REPO.glob("art/*.yaml")]:
            yaml.safe_load(p.read_text())
        for p in [*REPO.glob(".claude/agents/*.md"), *REPO.glob(".claude/skills/*/SKILL.md")]:
            fm = yaml.safe_load(p.read_text().split("---")[1])
            self.assertTrue(fm.get("name") and fm.get("description"), p)
        plug = json.loads((REPO / ".claude-plugin/plugin.json").read_text())
        for k in ("agents", "skills"):
            self.assertTrue((REPO / plug[k]).is_dir())
        for s in REPO.glob("scenarios/*.json"):
            for st in json.loads(s.read_text()).get("steps", []):
                self.assertIn(st["action"], STEP_ACTIONS, s)
            for pr in json.loads(s.read_text()).get("probes", []):
                self.assertTrue(pr.get("name") and pr.get("target") and pr.get("member"), s)
        for t in REPO.glob("marketing/takes/*.json"):
            for take in json.loads(t.read_text())["takes"]:
                for st in take.get("steps", []):
                    self.assertIn(st["action"], STEP_ACTIONS, t)

    def test_game_code_never_references_harness(self):
        import re
        rt = json.loads((REPO / "packages/com.studio.harness/Runtime/Studio.Harness.asmdef").read_text())
        self.assertIn("UNITY_EDITOR || HARNESS", rt["defineConstraints"])  # stripped from release builds
        self.assertFalse(rt.get("autoReferenced", True))                    # game assemblies can't see it
        for f in (REPO / "game").rglob("*.cs") if (REPO / "game").exists() else []:
            self.assertIsNone(re.search(r"^\s*using\s+Harness\b", f.read_text(errors="ignore"), re.M), f)



# ------------------------------------------------------------------ unity (free)
def unity_checks() -> list[tuple[str, bool, str]]:
    from core import load_config
    cfg = load_config()
    exe = find_unity(cfg, REPO / cfg["game"]["unity_project"])
    if not exe:
        return [("unity", False, "editor not found: install the Unity CLI + an editor (`unity install lts`), or set unity.editor_path")]
    tmp = Path(tempfile.mkdtemp(prefix="hx-unity-"))
    repo = clone_repo(tmp)
    proj = repo / cfg["game"]["unity_project"]
    res = []
    if not (proj / "ProjectSettings").exists():
        shutil.rmtree(proj, ignore_errors=True)
        r = run([exe, "-batchmode", "-nographics", "-quit", "-createProject", str(proj), "-logFile", "-"], timeout=1800)
        res.append(("create temp Unity project", r.returncode == 0, f"rc={r.returncode}"))
        man = proj / "Packages/manifest.json"
        m = json.loads(man.read_text())
        m["dependencies"]["com.studio.harness"] = f"file:{(repo / 'packages/com.studio.harness').as_posix()}"
        m["dependencies"].setdefault("com.unity.test-framework", "1.4.5")
        m["testables"] = ["com.studio.harness"]
        man.write_text(json.dumps(m, indent=2))
    r = run([exe, "-batchmode", "-nographics", "-quit", "-projectPath", str(proj),
             "-executeMethod", "Harness.Editor.SelfTest.CreateSampleScene", "-logFile", "-"], timeout=1800)
    res.append(("package compiles + sample scene", r.returncode == 0, "error CS" in r.stdout and "compile errors" or f"rc={r.returncode}"))
    sys.path.insert(0, str(repo / "harness"))
    import unity
    out = tmp / "out"
    for name, fn in [("headless build", lambda: unity.build(cfg, proj, out)),
                     ("release build contains no harness", lambda: unity.build(cfg, proj, out, release=True)),
                     ("EditMode tests", lambda: unity.tests(cfg, proj, out, "EditMode")),
                     ("PlayMode tests", lambda: unity.tests(cfg, proj, out, "PlayMode")),
                     ("autotest smoke + selftest_move", lambda: unity.autotest(cfg, proj, out, ["smoke", "selftest_move"])),
                     ("camera capture", lambda: unity.capture(cfg, proj, repo / "marketing/takes/selftest.json", out / "cap"))]:
        ok, summ = fn()
        res.append((name, ok, json.dumps(summ, default=str)[:300]))
        if not ok and name == "headless build":
            break
    frames = sorted((out / "cap" / "orbit").glob("f_*.png")) if (out / "cap").exists() else []
    res.append(("capture produced frames", len(frames) >= 25, f"{len(frames)} frames, kept in {out}"))
    return res


# ------------------------------------------------------------------ live (plan quota, haiku)
def live_checks() -> list[tuple[str, bool, str]]:
    if not shutil.which("claude"):
        return [("claude CLI", False, "not installed")]
    res = []
    base = ["claude", "-p", "--model", LIVE_MODEL, "--output-format", "json", "--max-budget-usd", "0.05"]

    def call(prompt, turns=1, extra=(), cwd=REPO):
        r = run([*base, "--max-turns", str(turns), *extra], cwd=cwd, unset=NO_KEY, timeout=300, stdin=prompt)
        try:
            return json.loads(r.stdout.strip().splitlines()[-1])
        except Exception:
            return {"is_error": True, "result": (r.stdout + r.stderr)[-400:]}

    d = call("Reply with exactly the word PONG and nothing else.")
    res.append(("headless claude on plan login (haiku)", "PONG" in str(d.get("result", "")),
                f"{str(d.get('result'))[:120]} | est ${d.get('total_cost_usd', 0)} (plan quota, not billed)"))
    if d.get("is_error"):
        res.append(("hint", False, "run `claude` once and /login with your Pro/Max account; API keys are stripped on purpose"))
        return res
    d = call("Without using tools, list the names of the project skills available to you, comma-separated.")
    found = [s for s in ("run-card", "write-card", "playtest") if s in str(d.get("result", ""))]
    res.append(("project skills load", len(found) >= 2, f"saw {found}"))

    tmp = Path(tempfile.mkdtemp(prefix="hx-live-"))
    repo = clone_repo(tmp)
    add_card(repo, "C-9001", "allow_paths: [docs/]\ndifficulty: trivial",
             title="Create docs/selftest.md containing the single line OK")
    commit(repo)
    r = run([PY, "harness/runner.py", "--max-cards", "1", "--budget", "0"], cwd=repo,
            env={"HX_FORCE_MODEL": LIVE_MODEL}, unset=NO_KEY, timeout=1200)
    merged = (repo / "docs/selftest.md").exists() and where(repo, "C-9001") == "done"
    note = r.stdout.strip()[-300:] + r.stderr.strip()[-300:]
    if "plan_overage_disabled" in note:
        note = "runner refused: turn off extra usage on your account, then set runner.plan_overage_disabled: true"
    res.append(("real card through runner (haiku, $0 money)", merged, note))

    return res


def report(title, rows) -> bool:
    print(f"\n== {title}")
    for name, ok, note in rows:
        print(f"  {'PASS' if ok else 'FAIL'}  {name}  — {note}")
    return all(ok for _, ok, _ in rows)


def main():
    ap = argparse.ArgumentParser()
    for f in ("unity", "live", "all"):
        ap.add_argument(f"--{f}", action="store_true")
    a = ap.parse_args()
    print("selftest: money $0 on every level" + (f"; live tests force model '{LIVE_MODEL}' on plan quota" if a.live or a.all else ""))
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(Offline)
    ok = unittest.TextTestRunner(verbosity=1).run(suite).wasSuccessful()
    if a.unity or a.all:
        ok &= report("Unity", unity_checks())
    if a.live or a.all:
        ok &= report("Live (plan quota, haiku)", live_checks())
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
