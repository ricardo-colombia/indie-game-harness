# Testing the harness yourself

Every level costs **$0 in money**. The live level uses your Claude plan's included quota with **Haiku forced**, and each call is capped at an estimated $0.05. Tests run in temp copies of the repo, so your checkout is never modified.
Each run also writes **`selftest-results.txt`**. If anything is red, paste that file back to Claude.

## 0. One-time setup
| Need | For level | Get it |
|---|---|---|
| Python 3.10+ and git | all | python.org / git-scm.com |
| ffmpeg | offline (shorts test; skipped if missing), unity | `winget install ffmpeg` · `brew install ffmpeg` · `apt install ffmpeg` |
| Blender 4.x on PATH | offline (cleanup test; skipped if missing) | blender.org |
| Claude Code, logged in **with your plan** | live, mcp | `npm i -g @anthropic-ai/claude-code`, then `claude` → `/login` |
| uv (`uvx`) | mcp (Blender MCP server) | docs.astral.sh/uv |
| Unity 6 + `UNITY_PATH` | unity, live (Funplay check) | Unity Hub; `UNITY_PATH` = path to `Unity.exe` / `Unity.app/Contents/MacOS/Unity` |
| xvfb (headless Linux only) | unity | `apt install xvfb` |

You don't need an API key. Leave `ANTHROPIC_API_KEY` unset, or keep it: the tests strip it from every AI call, so nothing is billed to it.

## 1. Run the levels, in this order
Windows (PowerShell):
```powershell
powershell -ExecutionPolicy Bypass -File scripts\selftest.ps1          # offline
powershell -ExecutionPolicy Bypass -File scripts\selftest.ps1 mcp
powershell -ExecutionPolicy Bypass -File scripts\selftest.ps1 unity
powershell -ExecutionPolicy Bypass -File scripts\selftest.ps1 live
```
macOS / Linux:
```bash
bash scripts/selftest.sh          # offline
bash scripts/selftest.sh mcp
bash scripts/selftest.sh unity
bash scripts/selftest.sh live
```
`all` runs everything. The offline tests always run first.

### Offline (≈20 s, no AI, no Unity)
Expected ending: `Ran 16 tests ... OK` (shows `skipped=N` if Blender or ffmpeg is missing).
What it checks:
- **Runner:** merges a card, strips your API key from agent calls, forces the model, rejects changes to protected paths, and resumes the session on the fix attempt.
- **Billing:** no escalation without money; with money, escalation is logged as `api`; a `paid` card waits; when the quota runs out, the card goes back to `ready`.
- **Workflow:** `taste_review` cards wait for you; dependencies and priority order are respected; metric thresholds are checked.
- **Pieces:** card files keep their comments, the C# input hook works, the posting queue needs approval, a real ffmpeg short comes out at 1080×1920, the art pipeline's free modes work, and Blender cleanup works.
- **Files:** every config, agent, skill and plugin file is valid.

### MCP (≈30 s, no model call)
Before running:
- **Unity:** open any project with the Funplay package installed, then choose Funplay → MCP Server → Start.
- **Blender:** install the BlenderMCP add-on, then open the N-panel → BlenderMCP → Connect.
- **Claude Code:** run `claude` once in this repo and approve the `funplay` and `blender` servers when asked.

Expected: `PASS funplay MCP ... ~N tools listed`, `PASS blender MCP addon`, `PASS claude mcp list`.

### Unity (15–40 min the first time, mostly project import)
This creates a throwaway Unity project in a temp folder and adds the harness package and a generated sample scene. It then runs a headless build, the EditMode and PlayMode tests, autotest (`smoke` plus `selftest_move`, which drives a cube forward for 2 s and expects about 10 m travelled at 5 m/s), and a 2-take camera capture.
Expected: every line `PASS`. The last line shows where the captured frames were kept.

### Live (≈2–5 min, plan quota, Haiku)
Expected PASS lines:
1. `headless claude on plan login (haiku)`: the model replies PONG.
2. `project skills load`: it sees `run-card`, `write-card`, ...
3. `real card through runner`: Haiku writes `docs/selftest.md`, the review gate passes, and the card merges, all in a temp clone.
4. `agent drives Unity via Funplay`: if Unity is open with Funplay running, it returns the editor version.

## 2. If something fails
| Symptom | Fix |
|---|---|
| live: `is_error`, "Invalid API key" or a login prompt | Run `claude` → `/login` with your plan account. The tests strip API keys on purpose. |
| live: "usage limit reached" | Your plan's window is used up. Wait for it to reset; nothing was billed. |
| mcp: funplay connection refused | Unity isn't open or the Funplay server isn't started (port 8765). |
| mcp: blender connection refused | BlenderMCP add-on isn't connected (port 9876). |
| mcp: `Pending approval` in `claude mcp list` | Run `claude` in the repo and approve the servers. |
| unity: `package compiles` FAIL | Open `selftest-results.txt`, find `error CS` lines, and paste them to Claude. |
| unity: autotest `no metrics.json` on Linux | Install xvfb; GPU-less Linux needs a virtual display. |
| offline: cut_shorts drawtext error | Your ffmpeg has no fontconfig. Set `fontfile:` in `marketing/shorts.yaml` to any .ttf. |
| Windows: `claude` not found | Reopen the terminal after installing (PATH), or check `where claude`. |

## 3. Continuous check
`.github/workflows/selftest.yml` runs the offline level on Ubuntu and Windows on every push. That's free on GitHub-hosted runners and needs no secrets.
