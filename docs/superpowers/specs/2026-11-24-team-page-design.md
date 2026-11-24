# Team page, leaner leaderboard and no freeze — design

## Intent

Teams need one place to see where they are in the kata: the puzzle they are on now, its grid size, and the full list of puzzles from 1.1 to 7.1. Each puzzle shows as locked until the team passes the one before it. After the puzzles, the page sets out the next step: once a team has full marks on Secret Santa, they can design their own light show and have it play on the big screen. The page sits beside the `hth` CLI during the event. It never replaces the CLI.

In the same change, the score freeze goes away. The host hides the standings on the big screen with a button instead, and reveals the live final standings at prize-giving with another. `/leaderboard/final` is no longer needed.

Also in the same change, `/ladder` goes away. The ladder is leaderboard data, so `/leaderboard` carries it. The leaderboard should be as cheap as possible to serve, with all the work done on the server.

## Goals

- A per-team page at `/team`, rendered by ASP.NET as a Razor Page.
- A small script on that page that sends `X-Team-Name` and `X-Unique-Key` on its backend requests.
- A light-shows section that tells teams they can create their own patterns to appear on the big screen, and how to do it.
- One `/leaderboard` response that holds both the ranked teams and the ladder. The server computes everything, and the scoreboard JS only draws it.
- `/ladder` and the separate `ladder` SSE event are removed.
- Two buttons in the scoreboard header, "Hide leaderboard" and "Show leaderboard", replace the connection status text.
- The freeze is removed. Scores are live on the server for the whole event.

## Non-goals

- The team page never fetches a challenge. A fetch issues a single-use script that expires after 5 minutes, so showing it on the page would burn challenges. Teams keep using `hth run`.
- No server-side sessions or cookies. The existing header auth stays the only auth.
- Hiding scores from teams through the API. Teams are never told about `/leaderboard`, so it stays live and public. (User decision.)
- No restyle of the scoreboard. Its layout stays as it is (see the polish-not-redesign preference).

## Anti-goals

- Moving logic into JS. Ranks, unlock state, percentages and the ladder grouping are all computed in .NET. The JS only puts values into the DOM.
- The team page showing another team's private progress. `/progress` only ever answers for the team whose credentials it gets.

## Approaches considered

1. **Static `wwwroot/team.html` with JS doing all the rendering.** Rejected. This is a .NET site, and the fixed content (puzzle order, names, grid sizes) belongs in server-rendered markup.
2. **Razor Page plus a small script for the team-specific parts (chosen).** The server renders everything that is the same for every team. A browser can't add custom headers to a normal page load, so the script fetches the team's own status with the headers and fills it in.
3. **Razor Page with a login form and a cookie.** This would be fully server-rendered, but it adds a second auth mechanism next to the headers. Rejected; the user asked for the script-sets-headers route.

For the freeze, the user chose to drop it entirely and hide the standings only on the projector. The alternative kept the server-side freeze and made "Show" fetch `/leaderboard/final`, which would have put an admin key prompt on a public page.

For the leaderboard shape, the user chose maximum server-side work over a smaller per-team payload that the JS would have to group.

## Design

### `/leaderboard`

The response becomes:

```json
{
  "teams":  [{ "rank": 1, "teamName": "team-07", "total": 1234 }],
  "ladder": [{ "level": "santa", "name": "Found Santa", "teams": [{ "teamName": "team-07", "percent": 100 }] }]
}
```

- `rank` is computed on the server. Tied totals share a rank, and the next rank skips (1, 1, 3). This matches what `renderLeaderboard` computes today.
- `ladder` is exactly what `Ladder.Build` returns today.
- The per-team `puzzles` map is dropped from this response. The scoreboard does not use it, and `/leaderboard/final` is unchanged. (My call. It is a public contract change, so it is documented in `docs/api.md`.)
- `LeaderboardCache` builds the snapshot once per change, as it does now, and also serializes it once to compact camelCase UTF-8 JSON. It must be compact: the SSE `data:` line cannot contain newlines. The GET response uses the same compact bytes, so it loses its indentation. (My call.) `GET /leaderboard` writes those cached bytes. The SSE `leaderboard` event writes the same cached JSON. No request or subscriber serializes again.
- `GET /leaderboard` sends an `ETag` derived from the snapshot and answers `304` to a matching `If-None-Match`.
- `GET /ladder` and the `ladder` SSE event are deleted. `scoreboard.js` reads `teams` and `ladder` from the one payload. `renderLeaderboard` uses the server's `rank` instead of computing it.

### `GET /progress`

This route needs team auth (the usual headers). An unknown team or key gets `401`, the same as other team routes.

```json
{
  "teamName": "team-07",
  "next": "6.3",
  "showsUnlocked": false,
  "puzzles": [{ "id": "1.1", "status": "passed", "percent": 100 }]
}
```

- `puzzles` lists every catalogue puzzle in order.
- `status` is `passed`, `current` or `locked`. A puzzle is `passed` when the team's best score on it passes (`Points.Passes`). It is `current` when it is unlocked but not passed (`Participant.IsUnlocked`). Otherwise it is `locked`.
- `percent` is the best percentage of bulbs correct, or `null` if the team has never submitted on that puzzle.
- `next` is the id of the first `current` puzzle, or `"santa"` when everything has passed.
- `showsUnlocked` is `Participant.MayShow`. That rule is full marks on 7.1, and it is the same check `/shows` uses. Once the freeze is gone, it reads live best scores like everything else. So the page never says shows are open when `/shows` would refuse. (My call: the page reflects the real gate rather than changing it.)
- The route uses the team's best scores (`BestCorrect`), the same ones that decide unlocking.

### `/team` Razor Page

- `Program.cs` adds `AddRazorPages()` and `MapRazorPages()`. The page lives at `Pages/Team.cshtml`.
- The page and its script must load without team headers. `AuthenticationMiddleware` only skips endpoints carrying `SkipTeamValidationAttribute`, so `MapRazorPages()` gets `.WithMetadata(new SkipTeamValidationAttribute())`. That is safe because Razor Pages here only ever render public, team-neutral markup. `team.js` is served by `UseStaticFiles`, which runs before the middleware. (My call.)
- The server renders, from `PuzzleCatalogue`, one row per puzzle: id, name, and grid size (16, or 48 for 7.1). It also renders the light-shows section described below. Every row starts in a neutral "loading" state.
- It reuses the scoreboard's fonts and colour tokens, so the page looks like part of the same event.

Sketch:

```
 Hack the Halls · team-07                         [sign out]
 NEXT UP   6.3 Deep Sleep   16 rows   best 41% of 80%
 ───────────────────────────────────────────────────────────
 ✓ 1.1 Paint by Numbers   16   100%
   …
 ▶ 6.3 Deep Sleep         16    41%
 🔒 7.1 Secret Santa       48     —
 ───────────────────────────────────────────────────────────
 🔒 LIGHT SHOWS · unlocks with full marks on 7.1
 Design your own pattern and it plays on the big screen.
 Script starts SIZE 48, up to 500 steps, one frame each.
   hth show myshow.txt -- <your program>
```

### Light-shows section

The server renders this section below the puzzle list, with fixed copy from the kata rules:

- **What it is:** design your own pattern on the tree and it plays in the light-show panel of the big screen. Only your latest show is kept. Shows are not scored.
- **How:** write a script that starts with `SIZE 48` (the same grid as Secret Santa), up to 500 steps, one frame per step. Run `hth show <file> -- <your program>`. The CLI runs your program on every step, saves a GIF locally, and uploads the show.
- **Locked state:** until `showsUnlocked` is true, the section is dimmed and headed "Unlocks with full marks on 7.1". The how-to stays readable, so teams can plan a show early.
- **Unlocked state:** the script removes the dimming and changes the heading to "Light shows unlocked: your pattern goes on the big screen".

The numbers in the copy (48, 500) come from `ShowEndpoints.FrameRows` and `ShowEndpoints.MaxFrames`, so the page can't drift from what the server accepts.

### Team script (`wwwroot/team.js`)

- With no stored credentials, it shows a small form for team name and key. It saves them in `localStorage` and then loads progress.
- It calls `GET /progress` with `X-Team-Name` and `X-Unique-Key`. On `200` it sets each row's status and percent and fills in the "next up" line from the server values.
- On `401` it clears the stored credentials and shows the form again with "Team name or key not recognised".
- "Sign out" clears the stored credentials.
- Live updates: `EventSource` can't send headers, so the script subscribes to the public `/feed/events`. It re-fetches `/progress` when a `tile` event arrives for its own team name. 
### Rate limiting

`/progress` gets no rate limit. The script only re-fetches when one of its own team's tiles arrives, and submissions are already rate-limited. Other authenticated GETs, such as `/shows/permission`, are not limited either. (My call.)

### Scoreboard header buttons

- In `index.html`, the `#status` span ("connecting" / "live") is replaced by two buttons: "Hide leaderboard" and "Show leaderboard". Only the one that applies is visible at a time.
- "Hide leaderboard" hides the Leaderboard and Challenge ladder panels. The Latest trees feed and the Light shows panel then fill the screen. This is a layout state on the `<main class="board">` element, for example a `standings-hidden` class, with the CSS grid rearranged for it.
- "Show leaderboard" brings both panels back with the current live data. The panels keep receiving updates while hidden, so they are up to date the moment they reappear.
- The state is stored in `localStorage`, so a reload of the projector page keeps the standings hidden. It only affects that browser.
- The buttons must stay unobtrusive on the projector. They use the header's existing type size and colours, not big call-to-action styling.
- Connection problems still need to be visible. The existing reconnect handling stays, and a lost connection shows as a small "reconnecting" note next to the buttons.

### Freeze removal

Remove the freeze entirely:

- `Event:FreezesAt`, `Event:AdminKey`, `EventClock.Frozen` and anything that exists only to support them.
- `GET /leaderboard/final`.
- `Participant.PublicBestCorrect` and the `frozen` argument to `RecordScore`. One best score per puzzle remains (`BestCorrect`), used by the leaderboard, the ladder, unlocking, `MayShow` and `/progress`.
- Feed: tiles always carry `correct` and `banner`. `LeaderboardChanged` is always broadcast. `/feed` drops its `frozen` field.
- `scoreboard.js`: the `freeze()` function, the `#frozen` "Scores frozen" label, the 30-second freeze poll and every `if (frozen)` branch.
- Stored participant documents may still hold a `PublicBestCorrect` field. Reading must ignore it, so existing Cosmos data still loads.
- Production settings: `Event__FreezesAt` and `Event__AdminKey` are no longer needed. Remove them from deployment config and docs.

## Tests (test pyramid)

- **Unit:**
  - `Leaderboard` ranking: ties share a rank, and the next rank skips.
  - A progress builder: covers passed, current and locked, `next` including santa, `percent` null versus a value, and `showsUnlocked`.
- **API:**
  - `/leaderboard` returns the new shape.
  - `ETag` and `304` work.
  - `/ladder` returns `404`.
  - The SSE `leaderboard` event carries `teams` and `ladder`, and no `ladder` event is sent.
  - `/progress` returns `401` without credentials, and the right statuses for a seeded team.
  - `/team` returns `200` without headers and lists every catalogue puzzle with its size.
  - `/team` shows the light-show copy with the size and step limit taken from `ShowEndpoints`.
- **Freeze removal:** delete freeze-specific tests, such as `Ladder_frozen_ignores_post_freeze_scores` and the `/leaderboard/final` tests. Add one test that a stored participant document carrying the old `PublicBestCorrect` field still loads.
- **Scoreboard:** a static-page test that the served `index.html` has both buttons and no `#status` or `#frozen` element.
- **Existing ladder tests** move to the `/leaderboard` payload.

## Docs

- `docs/api.md`: new `/leaderboard` shape, `/progress`, `/team`; remove `/ladder` and the `ladder` event.
- Specs that mention `/ladder`: point them at `/leaderboard`.
- `docs/spec.md` (both repos) and the root kata rules: replace the 20:45 freeze with "the host hides the standings on the big screen; the final standings are revealed at prize-giving". Remove `/leaderboard/final` and the freeze settings from `docs/api.md`.

## Left to the builder

- Names of the C# types behind the progress response.
- How the ETag is derived (a snapshot counter or a hash of the bytes).
- Exact styling of the team page, within the scoreboard's tokens.
