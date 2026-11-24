# Challenge ladder on the scoreboard

## Intent

The big-screen scoreboard should show every challenge and how far each team has got. The organiser asked for a simple challenge ladder with team names beside each level.

## Goals

- Add one ladder showing all 23 challenges, 1.1 at the bottom and 7.1 at the top.
- Put the names of the teams currently on each level beside it.
- Update the ladder live, alongside the existing leaderboard.

## Non-goals

- No browser register or login page. Teams keep registering and logging in with `hth login` or raw HTTP, the 2025 approach that already exists: `GET /register/{teamName}`, then the `X-Team-Name` and `X-Unique-Key` headers.
- No new API endpoint.
- No change to scoring or unlocks.

## Anti-goals

- Anything busy or decorative that makes names hard to read from the back of the room.
- Showing scores in a way that reveals answers.

## Approaches considered

1. A ladder panel on the existing scoreboard page. **Chosen:** one screen shows everything.
2. The ladder replaces the leaderboard panel. Rejected because it loses the ranked list used to pick the winner.
3. A separate `/ladder.html` page. Rejected because it needs a second screen or tab switching.

## Design

**Placement**

- A new "Challenge ladder" panel goes on `wwwroot/index.html`, beside the leaderboard. My call: it sits between the leaderboard and the right-hand column.

**Rungs**

- There is one row per challenge, 23 in total, with 7.1 at the top.
- A thin divider or label separates the rounds.
- Each row shows the challenge id on the left and the team names to its right.

**Who sits on which level**

- A team appears once, beside its current level. The current level is the first challenge, in order, on which it has not yet scored 80 or more.
- Teams that have reached 80 or more on 7.1 sit on a "Found Santa" row above 7.1.
- A registered team with no scores sits beside 1.1.

**Progress**

- Each name chip carries a subtle fill or bar showing the team's best score on that level, from 0 to 100. The 80% unlock point is marked.
- My call: the score is shown on the fill only, with no number. This keeps the rows narrow.

**Data**

- The server builds the ladder in .NET (`Ladder.Build`) from each team's public best correct bulbs, and serves it at `GET /ladder` and as an SSE `ladder` event after each `leaderboard` event. The page only renders it.
- The progress on a level is the percentage of bulbs correct, so the 80 % unlock point matches the unlock rule exactly.
- The puzzle order is the fixed list 1.1–1.4, 2.1–2.4, 3.1–3.4, 4.1–4.4, 5.1–5.3, 6.1–6.3, 7.1.
- It refreshes wherever the leaderboard refreshes now, on SSE events and polling fallback.

**Layout and look**

- The visual design is done with the impeccable skill, matching the existing scoreboard styles in `scoreboard.css`.
- It must stay legible on a 1920x1080 projector with about 25 teams.
- Crowded rows wrap names onto a second line rather than scrolling.

## Left to the builder

- Exact spacing, colours and chip styling, within the impeccable pass.
- Decided during the build: the placement rule lives on the server, not in a `ladder.js` module, so there is one implementation and it is unit tested in .NET.
- Tests: the level-placement rule, covering no scores, partial scores, exactly 80, a finished 7.1, and a score dropping below 80 on an earlier level being ignored because best scores never fall.
