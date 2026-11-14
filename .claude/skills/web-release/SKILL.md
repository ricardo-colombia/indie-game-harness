---
name: web-release
description: Build the game for the browser (WebGL), test it locally, and publish it to GitHub Pages as a shareable link. Use for "play it on my phone", "share a link", "put it on the web", "publish to GitHub Pages".
---
1. **Build.** Close the editor if it has `game/` open (`unity close game`), then `./hx web`. Output: `game/Builds/Web` (release build, no harness, uncompressed so any static host serves it). First build takes several minutes.
2. **Check locally.** `./hx web --serve` → http://localhost:8000. Open it in the built-in browser, look at a screenshot, press the controls. Check a phone-shaped viewport too (375×812): UI must fit, touch controls must work, nothing scrolls or zooms.
3. **Web gotchas to fix before publishing:** UI font (`LegacyRuntime.ttf` built-in under `#if UNITY_WEBGL`), no Quit button, camera/UI adapt to the aspect ratio, audio starts after the first tap (browsers block it earlier: start music on first input).
4. **Publish — only with the director's explicit go-ahead in chat.** Tell them what happens first: the build is force-pushed to the `gh-pages` branch of `origin` (only the built game, never the source) and becomes a public page at `https://<owner>.github.io/<repo>/`. Then `bash scripts/publish_web.sh`. Give them the URL it prints (the page may take a minute to appear).
