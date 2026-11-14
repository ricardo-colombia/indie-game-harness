---
name: steam-release
description: Build a release player and upload it to a Steam beta branch with steamcmd. Use for "upload build", "push to Steam beta", "release candidate".
---
1. Clean main, `./hx verify` green on HEAD.
2. Bump version (`PlayerSettings.bundleVersion` via `unity command script eval` or an Editor script), tag `vX.Y.Z` locally, draft `steam/CHANGELOG.md` from `backlog/done/` cards merged since the last tag.
3. Commit, then the director runs `./hx release --to-steam`: builds without the harness, scans the output for harness code, stages it in `steam/content/` and records the commit. Never copy a harness build there.
4. Ask the director: "Upload vX.Y.Z to branch <beta>?" — wait for yes.
5. `bash steam/upload.sh` (refuses unless `steam/content` is a scanned release build of the current clean commit; renders `app_build.vdf`, runs steamcmd). The director logs in to steamcmd once themselves; never handle their password or Steam Guard code.
6. Report build ID; the director sets it live in Steamworks.
