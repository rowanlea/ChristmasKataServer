# Challenge Ladder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a simple challenge ladder to the big-screen scoreboard, with the 23 challenges as levels and the team names beside each level.

**Architecture:** A pure ES module, `wwwroot/ladder.js`, turns the existing `/leaderboard` entries into rungs. `scoreboard.js` renders them into a new panel wherever it already renders the leaderboard. There are no server changes.

**Tech Stack:** Vanilla ES modules, CSS and `node --test` (Node 26). The ASP.NET static files stay unchanged.

**Spec:** `docs/superpowers/specs/2026-11-24-challenge-ladder-design.md`

## Global Constraints

- The puzzle order is exactly: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 6.1, 6.2, 6.3, 7.1.
- A team's current level is the first puzzle in that order with a best score below 80. A missing puzzle counts as 0.
- If every puzzle is at 80 or more, the team goes on the "Found Santa" rung above 7.1.
- Each team appears exactly once.
- Rungs render top-down: Found Santa, then 7.1, down to 1.1 at the bottom.
- The progress fill is the team's best score on its current level, 0–100. The 80 mark is visible. No number is shown.
- No new endpoint and no server code change.
- Must be legible on a 1920x1080 projector with about 25 teams. Crowded rungs wrap names, with no scrolling.
- After the freeze, the ladder keeps what it showed, the same rule as `renderLeaderboard`.
- Team names render via `textContent`, never `innerHTML`.

## Review Focus

- A team that registered but never submitted has `puzzles: {}` and must sit on 1.1 with an empty fill.
- A team at exactly 80 on a level has passed that level.
- Names and long team names (24 characters) must not overflow the rung on a projector.
- Leaderboard SSE events arrive often, so rendering must replace the children without leaking nodes.
- The ladder must update on both the SSE `leaderboard` event and the polling fallback.

---

### Task 1: Ladder placement module

**Files:**
- Create: `ChristmasKataServer/WebAPI/wwwroot/ladder.js`
- Test: `ChristmasKataServer/Tests/js/ladder.test.mjs`

**Interfaces:**
- Produces:
  - `export const PUZZLES` (array of 23 ids, in order).
  - `export const UNLOCK = 80`.
  - `export function currentLevel(puzzles) -> string`, which returns a puzzle id or `'santa'`.
  - `export function buildLadder(entries) -> Array<{ level: string, teams: Array<{ teamName: string, score: number }> }>`.
- `buildLadder` returns 24 rungs: `'santa'` first, then 7.1 down to 1.1. Teams keep their input order, which is leaderboard rank.

- [ ] **Step 1: Write failing tests**

Run them with `node --test ChristmasKataServer/Tests/js/` from the repo root. The test imports `../../WebAPI/wwwroot/ladder.js`.

- `currentLevel({})` returns `'1.1'`.
- `currentLevel({'1.1': 79})` returns `'1.1'`.
- `currentLevel({'1.1': 80})` returns `'1.2'`.
- `currentLevel` of all 23 puzzles at 100 returns `'santa'`.
- `currentLevel({'1.1': 100, '1.2': 50, '1.3': 100})` returns `'1.2'`.
- For `buildLadder([{teamName:'a',total:0,puzzles:{}}, {teamName:'b',total:180,puzzles:{'1.1':100,'1.2':80,'1.3':40}}])`:
  - The length is 24.
  - `[0].level` is `'santa'`.
  - `[1].level` is `'7.1'`.
  - `[23].level` is `'1.1'` and its teams are `[{teamName:'a',score:0}]`.
  - The `'1.3'` rung has teams `[{teamName:'b',score:40}]`.
- `buildLadder([])` gives 24 rungs, all with empty teams.

- [ ] **Step 2: Run and see them fail**

Run `node --test ChristmasKataServer/Tests/js/`. The expected failure is that the module is not found.

- [ ] **Step 3: Implement `ladder.js`**

It has no DOM access and no imports.

- [ ] **Step 4: Run and see them pass**

Run `node --test ChristmasKataServer/Tests/js/`. Every test should pass.

- [ ] **Step 5: Commit**

```bash
git add ChristmasKataServer/WebAPI/wwwroot/ladder.js ChristmasKataServer/Tests/js/ladder.test.mjs
git commit -m "feat(scoreboard): ladder placement from leaderboard scores"
```

### Task 2: Ladder panel on the scoreboard (design with impeccable)

**Files:**
- Modify: `ChristmasKataServer/WebAPI/wwwroot/index.html` (add a panel between `section.standings` and `div.right`)
- Modify: `ChristmasKataServer/WebAPI/wwwroot/scoreboard.js` (`renderLadder(entries)`, called beside every `renderLeaderboard` call)
- Modify: `ChristmasKataServer/WebAPI/wwwroot/scoreboard.css`

**Interfaces:**
- Consumes: `buildLadder`, `UNLOCK` and `PUZZLES` from Task 1.

**Markup:** `<section class="panel ladder" aria-labelledby="ladder-title"><h2 id="ladder-title">Challenge ladder</h2><ol id="ladder" class="ladder-rungs"></ol></section>`.

**Each rung:** an `li` with the level label (`7.1`, or `Found Santa` for `'santa'`) and a wrap of name chips.

**Each chip:** carries `--score` (0–100) as an inline custom property. CSS draws the fill to `--score` percent and a tick at 80%. The round boundaries (between x.4/x.3 and the next round) get a visual divider. Add a `data-round` attribute on each rung so the CSS can show the divider.

- [ ] **Step 1: Design pass**

Use the impeccable skill to set the ladder's look within the existing `scoreboard.css` tokens and palette:
- rung height that fits 24 rungs in the panel at 1080p,
- chip style,
- fill and the 80 tick,
- round dividers,
- a Found Santa rung accent.

Keep it simple; the user asked for "a simple challenge ladder".

- [ ] **Step 2: Implement `renderLadder(entries)`**

- It skips when `frozen` and the ladder already has children, the same rule as the leaderboard.
- It builds with `el()` and `replaceChildren`.
- Call it in `loadAll` and in the SSE `leaderboard` listener.

- [ ] **Step 3: Verify in the browser**

Restart the local server (background task `bk991v81y`) and re-run `scratchpad/seed/seed.sh` to reseed. Open http://localhost:5289/ at 1920x1080 and check that:
- elf-squad is on Found Santa,
- tinsel-tigers is on 6.2,
- mince-pies is on 1.1,
- the fills are visible,
- nothing scrolls or overflows.

Take a screenshot.

- [ ] **Step 4: Run the existing tests**

Run `dotnet test` in `ChristmasKataServer/` and `node --test ChristmasKataServer/Tests/js/`. Both should pass, with 252 tests passing in `dotnet test`.

- [ ] **Step 5: Commit**

```bash
git add ChristmasKataServer/WebAPI/wwwroot/index.html ChristmasKataServer/WebAPI/wwwroot/scoreboard.js ChristmasKataServer/WebAPI/wwwroot/scoreboard.css
git commit -m "feat(scoreboard): challenge ladder panel with team names beside each level"
```
