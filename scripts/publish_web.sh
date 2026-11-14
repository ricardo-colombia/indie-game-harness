#!/usr/bin/env bash
# Publish the last `./hx web` build to GitHub Pages (branch gh-pages of `origin`). Needs the director's go-ahead:
# it pushes, and the page is public. Only the built game goes there, never the source.
#   bash scripts/publish_web.sh
set -euo pipefail
cd "$(dirname "$0")/.."
SRC=game/Builds/Web
[ -f "$SRC/index.html" ] || { echo "No web build. Run: ./hx web"; exit 1; }
REMOTE=$(git remote get-url origin)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
cp -R "$SRC/." "$tmp/"
touch "$tmp/.nojekyll"                      # Pages must not run Jekyll over Unity's file names
(
  cd "$tmp"
  git init -q -b gh-pages
  git add -A
  git -c user.name="web-publish" -c user.email="web-publish@local" commit -qm "Web build $(date '+%Y-%m-%d %H:%M')"
  git push -q --force "$REMOTE" gh-pages     # the branch only ever holds the latest build
)
# One-time: turn Pages on for that branch (no-op if already on, or if gh is missing)
if command -v gh >/dev/null 2>&1; then
  gh api -X POST "repos/{owner}/{repo}/pages" -f "source[branch]=gh-pages" -f "source[path]=/" >/dev/null 2>&1 || true
  URL=$(gh api "repos/{owner}/{repo}/pages" --jq .html_url 2>/dev/null || true)
fi
echo "Published. ${URL:-Enable Pages for the gh-pages branch in the repo settings.} (may take a minute to update)"
