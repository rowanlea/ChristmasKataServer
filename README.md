# ChristmasKataServer

Server for the LSCC Christmas kata, "Hack the Halls" (.NET 10 Web API). The kata spec is in [docs/spec.md](docs/spec.md), the HTTP contract is in [docs/api.md](docs/api.md), and the implementation plan is in `docs/superpowers/plans/`.

## What it does

Teams register, then fetch a puzzle script (a program that lights a Christmas tree of bulbs), run it with their own interpreter, and submit a 1200 x 1200 PNG of the result. The server samples the bulbs from the image, scores it against the expected tree, and keeps each team's best per puzzle. It also:

- serves a big-screen scoreboard at `/` with a live feed of submissions (server-sent events) and a live leaderboard that the host can hide on the big screen;
- serves a page for each team at `/team` showing its next puzzle, its status and best score on every puzzle, and how to make a light show;
- unlocks light shows for teams that get full marks on Santa (7.1), and plays the latest show per team on the scoreboard.

## Run locally

```bash
cd ChristmasKataServer
dotnet run --project WebAPI --launch-profile http
```

`appsettings.Development.json` selects the in-memory store, so this needs no other setup. Data is lost when the process stops. Open `http://localhost:5289/` for the scoreboard, `http://localhost:5289/team` for the team page, and see [docs/api.md](docs/api.md) for the API.

Tests: `dotnet test` in `ChristmasKataServer/`.

## Configuration

Any key can be set in `appsettings*.json`, user secrets, or an environment variable. Use `__` for `:` in environment variable names.

| Key | Env var | Purpose |
| --- | --- | --- |
| `Storage:Provider` | `Storage__Provider` | `Cosmos` (default) or `InMemory` |
| `accountEndpoint` | `accountEndpoint` | Cosmos account endpoint (Cosmos only) |
| `accountKey` | `accountKey` | Cosmos account key (Cosmos only, secret) |

Never commit a real Cosmos key.

Cosmos data lives in database `ChristmasKataServer`, container `participants`. The server creates them if missing.

## Docker

Build from the solution folder (`ChristmasKataServer/ChristmasKataServer/`), then run. The image listens on port 8080 and uses `SkiaSharp.NativeAssets.Linux.NoDependencies`, so it needs no extra system packages.

```bash
docker build -f WebAPI/Dockerfile -t hack-the-halls .
docker run --rm -p 8080:8080 \
  -e Storage__Provider=InMemory \
  hack-the-halls
```

For Cosmos, replace `Storage__Provider` with `-e accountEndpoint=... -e accountKey=...`.

## Pre-event checklist

- [ ] Empty the Cosmos `participants` container (database `ChristmasKataServer`), so dry-run teams and scores are gone. The server seeds its "first" banners from persisted scores at startup, so stale data would show up as old firsts.
- [ ] Re-copy the harness art after any change to the pixel-art pass:
  - `christmas-2025/internal/viewer/static/tree.js` to `ChristmasKataServer/WebAPI/wwwroot/tree.js`
  - everything in `christmas-2025/internal/art/assets/` to `ChristmasKataServer/WebAPI/wwwroot/art/`

  The two are byte-identical at the time of writing. Check with `diff` before the event.
- [ ] Run the dry run: register a team, get full marks on a puzzle, check it appears on the scoreboard, then restart the server and check nothing replays. Check that "Hide leaderboard" and "Show leaderboard" on the scoreboard hide and bring back the standings.
- [ ] Empty the container again after the dry run.

## Behaviour worth knowing

- A challenge lasts 5 minutes and takes one submission. A submission whose PNG is rejected (400 or 413) does not use up the challenge, so the team can retry. After expiry the challenge answers 410 for one hour, then 404.
- Rate limits are per team, per 5 minutes: 30 challenge requests, 30 submissions, 10 show uploads. Beyond that the server returns 429.
- Light shows unlock on full marks on 7.1.
- Scores stay live for the whole event. Near the end the host presses "Hide leaderboard" on the scoreboard, which hides the standings on that screen only, and presses "Show leaderboard" at prize-giving. `/leaderboard` itself is public and always live; teams are not told about it.
