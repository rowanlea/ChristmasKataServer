# API

Base URL: `http://localhost:5289` locally, or `:8080` in Docker. Replies are JSON. Errors are `{"error": "<message>"}`.

Every route needs the headers `X-Team-Name` and `X-Unique-Key` (the secret returned by register), except the routes marked *public*. Missing or wrong credentials give `401`.

Rate limits (per team, fixed 5-minute window, `429` when exceeded): challenges 30, submissions 30, shows 10.

| Route | Request | Success | Failures |
| --- | --- | --- | --- |
| `GET /register/{teamName}` *public* | none | `200` JSON string: the team secret, 32 lowercase hex characters | `400` invalid or taken name |
| `POST /puzzles/{puzzle}/challenges` | none | `201` `{"challengeId","puzzle","size","script","expiresAt"}` | `404` unknown puzzle, `403` locked, `429` |
| `POST /challenges/{challengeId}/submission` | body `image/png`, at most 1 MiB | `200` `{"puzzle","correct","bulbCount","points","bestPoints","unlocked"}` | `404` unknown or another team's, `409` already submitted, `410` expired, `400` not a 1200 x 1200 PNG, `413` too large, `429` |
| `GET /shows/permission` | none | `204` | `403` |
| `POST /shows` | body `{"frames":[...]}`, at most 1 MiB | `201` `{"frames":412}` | `403`, `400`, `413`, `429` |
| `GET /shows` *public* | none | `200` latest show per team, oldest submission first | none |
| `GET /progress` | none | `200` the calling team's progress (see Shapes) | none |
| `GET /leaderboard` *public* | optional `If-None-Match` | `200` `{"teams","ladder"}` with an `ETag` header; `304` when `If-None-Match` matches | none |
| `GET /feed` *public* | none | `200` `{"tiles"}` | none |
| `GET /feed/events` *public* | none | `200` `text/event-stream` | none |

## Rules

- **Names.** 1 to 24 characters, `^[A-Za-z0-9][A-Za-z0-9-]*$`. Names appear on the big screen.
- **Unlocking.** Puzzles open one at a time in order (`1.1` ... `6.3`, `7.1`). `1.1` is always open. A puzzle opens once the best on the previous one is at least 80 % (points above 0). A locked fetch returns `403`.
- **Challenges.** Last 5 minutes and take one submission. A submission whose PNG is rejected (`400` or `413`) does not consume the challenge, so the team can retry. An expired challenge returns `410` for up to 1 hour after it expires, then `404`. The body size check (`413`) runs before the challenge lookup.
- **Scoring.** `pct = floor(100 * correct / bulbCount)`. All bulbs correct scores 100. Otherwise `pct >= 80` scores `40 + 2 * (pct - 80)`, and anything lower scores 0. The best submission per puzzle (most `correct`) counts, and the leaderboard total is the sum of the best points. `unlocked` in the submission reply is the puzzle this submission opened, else `null`.
- **Light shows.** Unlocked by full marks on 7.1. A show is 1 to 500 frames of exactly 1176 characters (a 48-row tree, the same grid as 7.1) from `R G B W K`. Only the latest show per team is kept. Shows are not scored. The permission check (`403`) runs before the body is read.
- **No freeze.** Scores are live for the whole event. The host hides the standings on the big screen with the scoreboard's "Hide leaderboard" button and shows them again at prize-giving.

## Shapes

Leaderboard: `{"teams":[{"rank":1,"teamName":"team-07","total":1234}],"ladder":[...rungs]}`. Teams are ordered by total descending, then team name. `rank` is 1 plus the number of teams with a strictly higher total, so tied totals share a rank and the next rank skips (1, 1, 3). The server builds the body once per score change as compact JSON (no newlines) and serves the same bytes to every request and every SSE subscriber. The `ETag` changes whenever the body does.

Ladder rung (the `ladder` array of the leaderboard, 24 rungs): `{"level":"1.2","name":"Fill Your Boots","teams":[{"teamName":"team-07","percent":50}]}`. The first rung has level `santa` and name `Found Santa`. Each team sits once, on the first puzzle where its best is below 80 %, with `percent` its best there; a team that has passed 7.1 sits on `santa` at 100. Teams on a rung are ordered by `percent` descending, then name.

Progress: `{"teamName":"team-07","next":"1.2","showsUnlocked":false,"puzzles":[{"id":"1.1","status":"passed","percent":100},{"id":"1.2","status":"current","percent":null}, ...]}`. `puzzles` lists every puzzle in catalogue order. `status` is `passed` (best at least 80 %), `current` (open but not passed) or `locked`. `percent` is `floor(100 * best / bulbCount)`, or `null` before any submission. `next` is the first puzzle not passed, or `santa` once all are passed. `showsUnlocked` is true after full marks on 7.1.

Tile: `{"teamName","puzzle","correct": int,"bulbCount","size","states":"<sampled line; '.' = background>","banner": null | {"kind":"first-full-marks"|"first-round-finish"|"secret-santa","text":"..."}}`. `/feed` returns the newest 8 tiles, newest first.

Server-sent events on `/feed/events`:

- `tile`: data is a Tile.
- `leaderboard`: data is the leaderboard, the same body as `GET /leaderboard` (`teams` and `ladder`). Sent after each submission.
- `show`: data is `{"teamName","first":bool}`. The page then reloads `GET /shows`. A team's first show gets its banner from this event.

A `: ping` comment is sent every 15 seconds to keep the connection open.

## Examples

```bash
BASE=http://localhost:5289

# register: the reply is the secret as a JSON string
SECRET=$(curl -s $BASE/register/team-07 | tr -d '"')
H=(-H "X-Team-Name: team-07" -H "X-Unique-Key: $SECRET")

# fetch a challenge
curl -s -X POST "${H[@]}" $BASE/puzzles/1.1/challenges

# submit a PNG for that challenge
curl -s -X POST "${H[@]}" -H "Content-Type: image/png" \
  --data-binary @tree.png $BASE/challenges/<challengeId>/submission

# light shows
curl -i "${H[@]}" $BASE/shows/permission
curl -s -X POST "${H[@]}" -H "Content-Type: application/json" \
  -d '{"frames":["KKKK...1176 characters..."]}' $BASE/shows
curl -s $BASE/shows

# your progress
curl -s "${H[@]}" $BASE/progress

# leaderboard and feed
curl -s $BASE/leaderboard
curl -s $BASE/feed
curl -N $BASE/feed/events
```
