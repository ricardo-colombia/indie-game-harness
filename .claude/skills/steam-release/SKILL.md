---
name: steam-release
description: Build a release player and upload it to a Steam beta branch with steamcmd. Use for "upload build", "push to Steam beta", "release candidate".
---
1. Clean main, `python harness/hx.py verify` green on HEAD.
2. Bump version (`PlayerSettings.bundleVersion` via Funplay or an Editor script), tag `vX.Y.Z` locally, draft `steam/CHANGELOG.md` from `backlog/done/` cards merged since the last tag.
3. Release build into `steam/content/` (Unity `-release` flag): set `game.player_binary` or pass `-harnessBuildPath`.
4. Ask the director: "Upload vX.Y.Z to branch <beta>?" — wait for yes.
5. `bash steam/upload.sh` (renders `app_build.vdf` from config, runs steamcmd). The director logs in to steamcmd once themselves; never handle their password or Steam Guard code.
6. Report build ID; the director sets it live in Steamworks.
