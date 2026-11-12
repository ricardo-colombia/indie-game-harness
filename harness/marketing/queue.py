"""Posting queue over marketing/schedule.yaml. Human approval is required per item (approved: true).

  python harness/marketing/queue.py list
  python harness/marketing/queue.py schedule          # give approved items the next free slots
  python harness/marketing/queue.py post --due        # dry-run: show what would post now
  python harness/marketing/queue.py post --due --live # actually post (cron this, e.g. every 15 min)
"""
from __future__ import annotations

import argparse
import datetime as dt
import sys
from pathlib import Path

import yaml

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from core import ROOT  # noqa: E402

SCHED = ROOT / "marketing" / "schedule.yaml"
DAYS = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"]


def slots_from(rules, start: dt.datetime, horizon_days=60):
    for d in range(horizon_days):
        day = (start + dt.timedelta(days=d)).date()
        for r in rules:
            if DAYS[day.weekday()] in r["days"]:
                hh, mm = map(int, r["time"].split(":"))
                t = dt.datetime.combine(day, dt.time(hh, mm))
                if t > start:
                    yield t, r.get("platforms")


# ---------- adapters ----------
def post_youtube(item, cfg, live):
    if not live:
        return "dry-run"
    from google.oauth2.credentials import Credentials
    from google_auth_oauthlib.flow import InstalledAppFlow
    from googleapiclient.discovery import build
    from googleapiclient.http import MediaFileUpload
    scopes = ["https://www.googleapis.com/auth/youtube.upload"]
    tok = ROOT / cfg.get("token", "marketing/secrets/youtube_token.json")
    if tok.exists():
        creds = Credentials.from_authorized_user_file(str(tok), scopes)
    else:  # first run is interactive, by the director
        creds = InstalledAppFlow.from_client_secrets_file(str(ROOT / cfg["client_secret"]), scopes).run_local_server(port=0)
        tok.parent.mkdir(parents=True, exist_ok=True)
        tok.write_text(creds.to_json())
    yt = build("youtube", "v3", credentials=creds)
    body = {"snippet": {"title": item["title"][:100], "description": item.get("description", "") + "\n#shorts",
                        "tags": item.get("tags", []), "categoryId": "20"},
            "status": {"privacyStatus": cfg.get("privacy", "private"), "selfDeclaredMadeForKids": False}}
    r = yt.videos().insert(part="snippet,status", body=body, media_body=MediaFileUpload(str(ROOT / item["file"]))).execute()
    return f"https://youtube.com/shorts/{r['id']}"


def post_manual(item, cfg, live):
    # TikTok/Instagram/X APIs need app review; until wired, surface a reminder instead of failing.
    return "manual: upload " + item["file"]


ADAPTERS = {"youtube": post_youtube}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["list", "schedule", "post"])
    ap.add_argument("--due", action="store_true")
    ap.add_argument("--live", action="store_true")
    a = ap.parse_args()
    data = yaml.safe_load(SCHED.read_text())
    items = data.get("items", []) or []
    now = dt.datetime.now()

    if a.cmd == "list":
        for i in items:
            print(f"{i['id']:28} approved={i['approved']!s:5} {i['status']:18} {i.get('slot') or ''}")
    elif a.cmd == "schedule":
        taken = {i["slot"] for i in items if i.get("slot")}
        free = (s for s in slots_from(data["slots"], now) if s[0].isoformat(timespec="minutes") not in taken)
        for i in items:
            if i["approved"] and not i.get("slot") and i["status"] in ("pending_approval", "approved"):
                t, plats = next(free)
                i["slot"], i["status"] = t.isoformat(timespec="minutes"), "scheduled"
                if plats:
                    i["platforms"] = [p for p in i["platforms"] if p in plats] or i["platforms"]
                print(f"{i['id']} -> {i['slot']}")
    elif a.cmd == "post":
        for i in items:
            if i["status"] != "scheduled" or not i["approved"]:
                continue
            if a.due and dt.datetime.fromisoformat(i["slot"]) > now:
                continue
            results = {}
            for p in i["platforms"]:
                try:
                    results[p] = ADAPTERS.get(p, post_manual)(i, data.get("platforms", {}).get(p, {}), a.live)
                except Exception as e:  # keep going; record failure
                    results[p] = f"error: {e}"
            print(i["id"], results)
            if a.live:
                i["status"] = "failed" if any(str(v).startswith("error") for v in results.values()) else "posted"
                i["results"] = results
    if a.live or a.cmd == "schedule":
        SCHED.write_text(yaml.safe_dump(data, sort_keys=False))


if __name__ == "__main__":
    main()
