# ChristmasKataServer

Server for the LSCC Christmas kata, "Hack the Halls" (.NET 10 Web API). The kata spec is in [docs/spec.md](docs/spec.md), the HTTP contract is in [docs/api.md](docs/api.md), and the implementation plan is in `docs/superpowers/plans/`.

## What it does

Teams register, then fetch a puzzle script (a program that lights a Christmas tree of bulbs), run it with their own interpreter, and submit a 1200 x 1200 PNG of the result. The server samples the bulbs from the image, scores it against the expected tree, and keeps each team's best per puzzle. It also:

- serves a big-screen scoreboard at `/` with a live feed of submissions (server-sent events) and a leaderboard that freezes at `Event:FreezesAt`;
- unlocks light shows for teams that get full marks on Santa (7.1) before the freeze, and plays the latest show per team on the scoreboard.

## Run locally

```bash
cd ChristmasKataServer
dotnet run --project WebAPI --launch-profile http
```

`appsettings.Development.json` selects the in-memory store and sets a development freeze time and admin key, so this needs no other setup. Data is lost when the process stops. Open `http://localhost:5289/` for the scoreboard and see [docs/api.md](docs/api.md) for the API.

Tests: `dotnet test` in `ChristmasKataServer/`.

## Configuration

Any key can be set in `appsettings*.json`, user secrets, or an environment variable. Use `__` for `:` in environment variable names.

| Key | Env var | Purpose |
| --- | --- | --- |
| `Storage:Provider` | `Storage__Provider` | `Cosmos` (default) or `InMemory` |
| `accountEndpoint` | `accountEndpoint` | Cosmos account endpoint (Cosmos only) |
| `accountKey` | `accountKey` | Cosmos account key (Cosmos only, secret) |
| `Event:FreezesAt` | `Event__FreezesAt` | Freeze time, ISO-8601 with offset, e.g. `2026-12-10T20:45:00+00:00` |
| `Event:AdminKey` | `Event__AdminKey` | Secret for `GET /leaderboard/final` (sent as `X-Admin-Key`) |

**The server refuses to start unless `Event:FreezesAt` and `Event:AdminKey` are both set.** `appsettings.json` deliberately sets neither, so production, which does not load `appsettings.Development.json`, must supply them, for example `Event__FreezesAt` and `Event__AdminKey`. Never commit a real admin key or Cosmos key.

Cosmos data lives in database `ChristmasKataServer`, container `participants`. The server creates them if missing.

## Docker

Build from the solution folder (`ChristmasKataServer/ChristmasKataServer/`), then run. The image listens on port 8080 and uses `SkiaSharp.NativeAssets.Linux.NoDependencies`, so it needs no extra system packages.

```bash
docker build -f WebAPI/Dockerfile -t hack-the-halls .
docker run --rm -p 8080:8080 \
  -e Storage__Provider=InMemory \
  -e Event__FreezesAt=2026-12-10T20:45:00+00:00 \
  -e Event__AdminKey=change-me \
  hack-the-halls
```

For Cosmos, replace `Storage__Provider` with `-e accountEndpoint=... -e accountKey=...`.

## Pre-event checklist

- [ ] Set `Event__FreezesAt` to the confirmed freeze time (20:45 on the night, with the correct UTC offset).
- [ ] Set `Event__AdminKey` to a fresh secret and keep it somewhere the organisers can reach.
- [ ] Empty the Cosmos `participants` container (database `ChristmasKataServer`), so dry-run teams and scores are gone. The server seeds its "first" banners from persisted scores at startup, so stale data would show up as old firsts.
- [ ] Re-copy the harness art after any change to the pixel-art pass:
  - `christmas-2025/internal/viewer/static/tree.js` to `ChristmasKataServer/WebAPI/wwwroot/tree.js`
  - everything in `christmas-2025/internal/art/assets/` to `ChristmasKataServer/WebAPI/wwwroot/art/`

  The two are byte-identical at the time of writing. Check with `diff` before the event.
- [ ] Run the dry run: register a team, get full marks on a puzzle, check it appears on the scoreboard, then restart the server and check nothing replays. After the freeze time, check the scoreboard freezes (it notices within 30 seconds) and `/leaderboard/final` still shows live scores.
- [ ] Empty the container again after the dry run.

## Behaviour worth knowing

- A challenge lasts 5 minutes and takes one submission. A submission whose PNG is rejected (400 or 413) does not use up the challenge, so the team can retry. After expiry the challenge answers 410 for one hour, then 404.
- Rate limits are per team, per 5 minutes: 30 challenge requests, 30 submissions, 10 show uploads. Beyond that the server returns 429.
- Light shows unlock only on full marks on 7.1 counted before the freeze. Full marks earned after the freeze do not unlock shows.
- After the freeze, `/leaderboard` and the feed show pre-freeze scores only, and tiles carry no `correct`. `/leaderboard/final` (admin key) shows live scores.
