# Hack the Halls Server Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 2025 Naughty-or-Nice server with the Hack the Halls online system: reference emulator, per-puzzle script generator, PNG scorer with points and sequential unlocks, team API, light-show uploads, and the big-screen scoreboard page.

**Architecture:** One ASP.NET Core minimal-API app (`WebAPI`). A pure domain core (`Kata/`: tree, parser, emulator, generators, checks) has no I/O and carries most tests. Thin services around it own scoring, persistence (Cosmos DB, in-memory for dev/tests), and the live feed (server-sent events). The scoreboard is static files in `wwwroot`. Single instance: outstanding challenges and the feed live in memory, and everything that must survive a restart is derived from persisted participants.

**Tech Stack:** .NET 10 (LTS; .NET 8 support ended Nov 2026), ASP.NET Core minimal APIs, Microsoft.Azure.Cosmos, SkiaSharp (MIT) for PNG decoding, NUnit + NSubstitute, Microsoft.AspNetCore.Mvc.Testing, Microsoft.Extensions.TimeProvider.Testing.

**Spec:** `C:\Users\edfar\source\repos\codurance\lscc\christmas\Hack the Halls LSCC Christmas kata v1.1.md` (copy into `docs/spec.md` in Task 1 so it travels with the repo).

**Companion plan:** `christmas-2025/docs/superpowers/plans/2026-11-24-hth-harness.md` (Go harness). Both plans share the API contract below and the render layout. Harness Task 2 produces the PNG fixtures this plan's Task 9 consumes, and harness Tasks 8 and 10 produce the viewer art and `tree.js` this plan's Task 13 copies. Run those harness tasks first.

## Global Constraints

- Commits land on `main`. The 2025 code is already archived on another branch.
- Tree: rows `0..n-1`, row r holds r + 1 bulbs, position `0 ≤ p ≤ r`. Answer-line index of (r, p) is `r(r+1)/2 + p`. Bulb count `n(n+1)/2`.
- States: `R` red, `G` green, `B` blue, `W` white, `K` off. Every bulb starts `K`.
- Puzzle sizes: 16 rows for puzzles 1.1–6.3, 48 rows for 7.1.
- `REPEAT n` allows `0 ≤ n ≤ 1,000,000,000`. Blocks nest.
- Challenge lifetime: 5 minutes from issue. Each challenge takes one submission.
- Render layout (shared with harness): canvas 1200 × 1200, background `#000000`, `h = 1000 / (n − 1)`, `v = 0.866 × h`, centre `x = 600 + (p − r/2) × h`, `y = 120 + r × v`, bulb radius `0.32 × h`, sample radius `0.14 × h`. Colours: R `#FF0000`, G `#00FF00`, B `#0000FF`, W `#FFFFFF`, K `#303030`.
- Sampling: average the pixels whose centres lie within the sample radius of each bulb centre. Snap the average to the nearest of the five palette colours or the background, by squared RGB distance. Background snaps to `.`, which never matches. `correct` = number of bulbs that match the expected state.
- Points: `pct = floor(100 × correct / bulbCount)`. `correct == bulbCount` scores 100. Otherwise `pct ≥ 80` scores `40 + 2 × (pct − 80)` (so 99 % scores 78), and `pct < 80` scores 0.
- Unlocking: puzzles unlock one at a time in catalogue order (`1.1`, `1.2`, … `6.3`, `7.1`). `1.1` is always open. Each later puzzle opens once the team's best on the previous puzzle is ≥ 80 % (points > 0). No unlock is time-based.
- A team's best submission per puzzle is the one with most `correct`. Leaderboard total = sum of best points per puzzle.
- Light shows: open to a team once its best on `7.1` is full marks. A show is 1–500 frames of exactly 1176 `RGBWK` chars (a 48-row tree, the same grid as `7.1`). Only the latest show per team is kept. Nothing is scored.
- No freeze and no admin key. Scores are live for the whole event. The host hides the standings on the big screen with the scoreboard's "Hide leaderboard" button and shows them again at prize-giving.
- Team names: 1–24 characters, `^[A-Za-z0-9][A-Za-z0-9-]*$`. They appear on the big screen.
- Drop FluentAssertions: v8 needs a commercial licence. Use NUnit `Assert.That`.

### API contract (identical in harness plan)

`docs/api.md` is the live contract. This table matches it.

Auth headers `X-Team-Name` and `X-Unique-Key` are required on every route except those marked *public*. Errors return `{"error": "<message>"}`. Rate limits (per team, fixed 5-minute window, `429` when exceeded): challenges 30, submissions 30, shows 10.

| Route | Request | Success | Failures |
| --- | --- | --- | --- |
| `GET /register/{teamName}` *public* | — | `200` JSON string: the team secret | `400` invalid or taken name |
| `POST /puzzles/{puzzle}/challenges` | — | `201` `{"challengeId":"<guid>","puzzle":"3.2","size":16,"script":"SIZE 16\n…","expiresAt":"<ISO-8601>"}` | `404` unknown puzzle, `403` locked |
| `POST /challenges/{challengeId}/submission` | body `image/png`, ≤ 1 MiB | `200` `{"puzzle":"3.2","correct":131,"bulbCount":136,"points":72,"bestPoints":72,"unlocked":"3.3"}`. `unlocked` is the puzzle this submission opened, else `null` | `404` unknown or another team's, `409` already submitted, `410` expired, `400` not a 1200 × 1200 PNG, `413` too large |
| `GET /shows/permission` | — | `204` | `403` `Santa comes first: get full marks on 7.1 to unlock light shows` |
| `POST /shows` | body `{"frames":["<1176 chars>",…]}`, ≤ 1 MiB | `201` `{"frames":412}` | `403` as above, `400` no frames, over 500 frames, or a frame not 1176 `RGBWK` chars, `413` too large |
| `GET /shows` *public* | — | `200` `[{"teamName","frames":[…],"submittedAt"}]`, the latest show per team, oldest submission first | — |
| `GET /progress` | — | `200` `{"teamName","next","showsUnlocked","puzzles":[{"id","status","percent"}]}`, the calling team's progress | — |
| `GET /team` *public* | — | `200` HTML page: a team signs in with its name and key (kept in the browser), then sees its progress from `GET /progress`, live | — |
| `GET /leaderboard` *public* | optional `If-None-Match` | `200` `{"teams":[{"rank":1,"teamName":"team-07","total":1234}],"ladder":[…rungs]}` with an `ETag` header, teams by total descending then name; `304` when `If-None-Match` matches | — |
| `GET /feed` *public* | — | `200` `{"tiles":[Tile…]}`, newest first, max 8 | — |
| `GET /feed/events` *public* | — | `text/event-stream`: event `tile` (data Tile), event `leaderboard` (data the same body as `GET /leaderboard`, sent after each submission), and event `show` (data `{"teamName","first":bool}`; the page then reloads `/shows`) | — |

`Tile = {"teamName","puzzle","correct": int,"bulbCount","size","states":"<sampled line; '.' = background>","banner": null | {"kind":"first-full-marks"|"first-round-finish"|"secret-santa","text":"…"}}`. A team's first show gets a banner on the page from the `show` event, not from a tile. `docs/api.md` describes the ladder rung and progress shapes in full.

## Review Focus

1. A team name containing markup such as `<img onerror>` must never reach the big screen as HTML. Expected: register rejects it with `400` (test in Task 1), and the page sets text only with `textContent` (manual check in Task 13).
2. A garbage, truncated, huge, or wrongly sized upload must get `400`/`413` and never a `500` or memory blow-up. Test in Task 12.
3. A replayed, expired, or foreign `challengeId` must not score, and a team must not fetch a puzzle it has not unlocked. Expected: `409`/`410`/`404` with the best unchanged, and `403` on a locked fetch. Test in Task 12.
4. Two submissions from one team at the same moment (a pair on two laptops) must not lose a best score or an unlock. Expected: Cosmos ETag retry, and a lock in memory. Test in Task 10.
5. A server restart mid-event must not replay "first" banners or reset the leaderboard. Expected: firsts are seeded from persisted scores at startup. Test in Task 11.

---

### Task 1: Clear the 2025 domain and move to .NET 10

**Files:**
- Delete: `WebAPI/Determiner.cs`, `WebAPI/FileReader.cs`, `WebAPI/TestHarness.cs`, `WebAPI/Model/Person.cs`, `WebAPI/Model/ValidatedNaughtyOrNicePerson.cs`, `WebAPI/Model/ParticipantScore.cs`, `WebAPI/Requests/`, `WebAPI/Abstractions/IValidationResultReader.cs`, `WebAPI/Service/ValidationResultReader.cs`, `WebAPI/Service/ScoringService.cs`, `WebAPI/Data/`, `Tests/AcceptanceTests.cs`, `Tests/ServiceTests/`
- Modify: `WebAPI/WebAPI.csproj`, `Tests/Tests.csproj`, `WebAPI/Program.cs`, `WebAPI/Dockerfile` (base images `8.0` → `10.0`), `WebAPI/appsettings.Development.json`, `WebAPI/WebAPI.http`, `README.md`
- Create: `docs/spec.md` (copy of spec), `Tests/Api/RegistrationTests.cs`, `Tests/Api/KataApiFactory.cs`

**Interfaces:**
- Produces: `KataApiFactory : WebApplicationFactory<Program>`, which forces `Storage:Provider=InMemory` and exposes a `FakeTimeProvider Time`. Program registers `TimeProvider` as a singleton (`TimeProvider.System`), and the factory swaps in `Time`. `Storage:Provider` is `"Cosmos"` (default) or `"InMemory"` (Development).

- [ ] **Step 1:** Delete the files listed. Retarget both projects to `net10.0` and update packages to their current versions. Remove FluentAssertions. Add `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.Extensions.TimeProvider.Testing` to Tests. Strip the `/naughtyornice` and `/score` routes from `Program.cs`, and add `public partial class Program;`.
- [ ] **Step 2:** Fix the in-memory `ParticipantRepository.GetAllParticipants`, which returns an empty list. Delete `TryGetOrCreateParticipant` from the interface and both implementations, because nothing uses it. Register the repository by `Storage:Provider`.
- [ ] **Step 3: Write the failing tests** in `Tests/Api/RegistrationTests.cs`:
  - `Register_returns_secret_then_rejects_duplicate`: the first `GET /register/team-07` gives `200`, and the second gives `400`.
  - `Register_rejects_invalid_names`: `<script>`, `""`, `a b`, and a 25-character name each give `400`.
  - `Protected_route_without_headers_is_401`: `POST /puzzles/1.1/challenges` with no headers gives `401`.
- [ ] **Step 4:** Run `dotnet test`. Expected: the invalid-names test FAILS (no validation yet). Add the name regex check to the register route. Run again: PASS.
- [ ] **Step 5:** Commit: `chore: clear 2025 kata, move to .NET 10, validate team names`.

### Task 2: Tree, bulbs, and script parser

**Files:**
- Create: `WebAPI/Kata/Bulb.cs`, `WebAPI/Kata/Tree.cs`, `WebAPI/Kata/Instructions.cs`, `WebAPI/Kata/ScriptParser.cs`
- Test: `Tests/Kata/TreeTests.cs`, `Tests/Kata/ScriptParserTests.cs`

**Interfaces:**
- Produces:
  - `enum Bulb : byte { K, R, G, B, W }`, plus `static class BulbChars { char ToChar(this Bulb); Bulb Parse(char) }` (`Parse` throws `FormatException` on other chars).
  - `sealed class Tree(int size)` with `int Size`, `int Count`, `Bulb this[int r, int p] { get; set; }`, `static int Index(int r, int p)`, `bool Contains(int r, int p)`, `Tree Clone()`, `string ToAnswerLine()`, `static Tree FromAnswerLine(string line, int size)`. Bulbs are stored in answer order in a `Bulb[]`.
  - `abstract record Instruction`, with `Set(int Row, int Position, Bulb State)`, `Row(int Index, Bulb State)`, `Fill(Bulb State)`, `Rotate`, `Mirror`, `Grow(Bulb State)`, `Cycle`, `Sequence(Bulb State)`, `Chase`, `Fall`, `Repeat(long Count, IReadOnlyList<Instruction> Body)`.
  - `sealed record Script(int Size, IReadOnlyList<Instruction> Body)`.
  - `static class ScriptParser { Script Parse(string text) }`, which throws `ScriptFormatException(int lineNumber, string message)`.

- [ ] **Step 1: Write failing tests:**
  - `Index_matches_answer_order`: `Tree.Index(0,0)==0`, `Index(1,1)==2`, `Index(2,0)==3`, `Index(15,15)==135`.
  - `New_tree_is_all_off`: `new Tree(16).ToAnswerLine() == new string('K', 136)`.
  - `Answer_line_round_trips`: `FromAnswerLine("RRGRGW", 3).ToAnswerLine() == "RRGRGW"`.
  - `Parses_nested_repeat`: the script `SIZE 4\nREPEAT 3\nCYCLE\nREPEAT 2\nCHASE\nEND\nEND` gives `Body[0]` = `Repeat(3, [Cycle, Repeat(2, [Chase])])`.
  - `Ignores_blank_and_comment_lines`, including inside blocks and before `SIZE`.
  - `Accepts_CRLF_line_endings`.
  - Rejections (each asserts `ScriptFormatException` with the right line number): missing or non-first `SIZE`, unknown instruction, wrong token count, lowercase token, state char outside `RGBWK`, `SET 3 4 R` on a 4-row tree (p > r), `ROW 4 R` on 4 rows, `REPEAT -1`, `REPEAT 1000000001`, unmatched `END`, unclosed `REPEAT`.
- [ ] **Step 2:** Run `dotnet test --filter "FullyQualifiedName~Kata"`. Expected: FAIL (types missing).
- [ ] **Step 3:** Implement. The parser uses a stack of open bodies. Tokens split on single spaces. Row and position ranges are checked at parse time against `SIZE`.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `feat: tree model and script parser`.

### Task 3: Emulator for paint, transform, GROW, and CYCLE

**Files:**
- Create: `WebAPI/Kata/Emulator.cs`, `WebAPI/Kata/Geometry.cs`
- Test: `Tests/Kata/EmulatorTests.cs`, `Tests/Kata/WorkedExamples.cs`

**Interfaces:**
- Consumes: Task 2 types.
- Produces:
  - `[Flags] enum Quirks { None = 0, CounterClockwiseRotate = 1, MirrorWrongAxis = 2, AnswerOrderWiring = 4 }`.
  - `sealed record EmulatorOptions(Quirks Quirks = Quirks.None, bool DetectCycles = true)`.
  - `sealed record LoopStats(long Count, int? PrePeriod, int? Period)`.
  - `sealed record RunResult(Tree Final, long InstructionsExecuted, IReadOnlyList<LoopStats> Loops)`. `Loops` holds the first execution of each `Repeat` node, in script order.
  - `sealed class Emulator(EmulatorOptions? options = null) { RunResult Run(Script script); }`.
  - `static class Geometry { (int r, int p) Rotate(int n, int r, int p); (int r, int p) Mirror(int r, int p); IEnumerable<(int r, int p)> Neighbours(int n, int r, int p); IReadOnlyList<(int r, int p)> Wiring(int n) }`. `Wiring` is used in Task 4 and cached per n.

- [ ] **Step 1: Write failing tests.** `WorkedExamples.cs` holds the eight example scripts from the harness plan's Task 5 table, with their expected answers, as `[TestCase]`s. Mark the ones that need later instructions `[Category("Task4")]` or `[Category("Task5")]`. Run only `1-lights-on` and `2-order-order` now. Unit tests in `EmulatorTests.cs`:
  - `Rotate_moves_star_to_bottom_right_corner`: on 4 rows, `SET 0 0 R` then `ROTATE` lights only (3,3).
  - `Three_rotations_are_identity` and `Mirror_twice_is_identity`, on a random 16-row tree (seeded).
  - `Mirror_rotate_mirror_equals_two_rotations`, on a random 5-row tree.
  - `Mirror_keeps_middle_bulb_of_odd_rows`: (2,1) stays put.
  - `Grow_updates_simultaneously`: on 4 rows, `SET 3 0 R` then `GROW R` gives `KKKRKKRRKK`. Only (2,0) and (3,1) grow, and (3,2) does not.
  - `Grow_only_fills_off_bulbs`: a `G` bulb next to `R` stays `G`.
  - `Cycle_skips_off_bulbs`: `FILL K`, `SET 0 0 W`, `CYCLE` gives `R` at the star and `K` elsewhere.
  - `Counter_clockwise_quirk_differs`: with `Quirks.CounterClockwiseRotate`, `SET 0 0 R`, `ROTATE` lights (3,0).
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement `Geometry` and the paint, transform, `GROW`, and `CYCLE` cases. Barycentric rotation, with N = n − 1: `Rotate(n, r, p) = (N − r + p, N − r)`. The counter-clockwise quirk applies the inverse map `(r, p) → (N − p, r − p)`. The wrong-axis mirror quirk is `(r, p) → (N − p, N − r)`. Transforms build a fresh `Bulb[]`. `GROW` reads from a snapshot. Count each executed non-`REPEAT` instruction in `InstructionsExecuted`.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `feat: emulator paint, transform, grow, cycle`.

### Task 4: Wiring patterns: SEQUENCE, CHASE, FALL

**Files:**
- Modify: `WebAPI/Kata/Emulator.cs`, `WebAPI/Kata/Geometry.cs`
- Test: `Tests/Kata/PatternTests.cs`, and enable the `Task4` worked examples

**Interfaces:**
- Consumes: `Geometry.Wiring(int n)`. Row 0, then row 1 left to right, row 2 right to left, alternating.

- [ ] **Step 1: Write failing tests:**
  - `Wiring_on_four_rows`: equals `(0,0) (1,0) (1,1) (2,2) (2,1) (2,0) (3,0) (3,1) (3,2) (3,3)`.
  - `Sequence_follows_wiring`: on 4 rows, four `SEQUENCE G` light (0,0), (1,0), (1,1), (2,2).
  - `Sequence_on_full_tree_does_nothing`.
  - `Chase_wraps_last_to_star`: on 4 rows, `SET 3 3 R`, `CHASE` lights only (0,0).
  - `Chase_reverses_on_even_rows`: `SET 1 1 B`, `CHASE` lights only (2,2).
  - `Fall_drops_two_rows`: the spec example `SET 0 0 R`, `SET 1 0 G`, `SET 1 1 B`, `FALL` on 4 rows gives `KKKKRKKGBK`.
  - `Fall_loses_bottom_states_and_clears_top_rows`.
  - `Answer_order_quirk_changes_chase`: with `Quirks.AnswerOrderWiring`, `SET 1 1 B`, `CHASE` lights (2,0).
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement. The `AnswerOrderWiring` quirk swaps the wiring list for plain answer order.
- [ ] **Step 4:** Run the tests and the `Task4` worked example. Expected: PASS.
- [ ] **Step 5:** Commit: `feat: wiring patterns sequence, chase, fall`.

### Task 5: REPEAT with cycle detection

**Files:**
- Modify: `WebAPI/Kata/Emulator.cs`
- Test: `Tests/Kata/RepeatTests.cs`, and enable the `Task5` worked examples

**Interfaces:**
- Produces: `RunResult.Loops`, filled as Task 3 defines it. `Period`/`PrePeriod` are `null` when the loop finished without a repeat.

- [ ] **Step 1: Write failing tests:**
  - `Repeat_zero_skips_body`.
  - `Nested_repeat_counts_multiply`: `REPEAT 3 / REPEAT 4 / CHASE` on 16 rows equals 12 plain `CHASE`s.
  - `Billion_rotate_cycle_finishes_fast`: 16 rows, `SET 2 1 R`, `SET 9 4 G`, `SET 5 3 B`, then `REPEAT 1000000000 / ROTATE / CYCLE`. It completes in under 1 s, `Loops[0].Period == 12`, and it equals brute force of `1000000000 % 12` passes.
  - `False_start_pre_period`: `SET 2 1 R`, `SET 9 4 G`, `REPEAT 1000000000 / CYCLE / GROW R / ROTATE` gives `PrePeriod == 43`, `Period == 12`, and matches brute force of `43 + (1e9 − 43) % 12` passes.
  - `Deep_sleep_inner_period`: seeds `SET 2 1 R`, `SET 9 4 G`, `SET 5 3 B`. A body of `ROTATE`, `CHASE`, `CYCLE` has `Period == 372`. A 1000-pass outer loop with a `SET` and that billion-pass inner loop finishes in under 2 s.
  - `Detection_off_matches_on`: for a 5,000-pass loop, `DetectCycles=false` gives the same final tree and `InstructionsExecuted` equal to passes × body length.
- [ ] **Step 2:** Run the tests. Expected: FAIL (or timeout).
- [ ] **Step 3:** Implement. For each `Repeat` execution, keep `Dictionary<string, long>` from the answer line at the start of a pass to the pass index. On a hit, `period = i − seen`. Skip `((count − i) / period) × period` passes, then run the rest plainly. Nested loops get their own dictionary on each execution. That is correct because every instruction is a pure function of tree state.
- [ ] **Step 4:** Run all `Kata` tests, including all eight worked examples. Expected: PASS.
- [ ] **Step 5:** Commit: `feat: repeat blocks with cycle detection`.

### Task 6: Puzzle catalogue and generation checks

**Files:**
- Create: `WebAPI/Kata/Puzzles/Puzzle.cs`, `WebAPI/Kata/Puzzles/PuzzleCatalogue.cs`, `WebAPI/Kata/Puzzles/GenerationChecks.cs`, `WebAPI/Kata/Puzzles/ChallengeGenerator.cs`, `WebAPI/Kata/Puzzles/ScriptBuilder.cs`
- Test: `Tests/Kata/Puzzles/CatalogueTests.cs`, `Tests/Kata/Puzzles/GenerationChecksTests.cs`

**Interfaces:**
- Produces:
  - `sealed record Puzzle(string Id, string Name, int Round, int Size, Func<Random, string> Template)`.
  - `static class PuzzleCatalogue { IReadOnlyList<Puzzle> All; Puzzle? Find(string id); Puzzle? Previous(Puzzle p); Puzzle? Next(Puzzle p); IEnumerable<Puzzle> InRound(int round) }`. `All` is in unlock order.
  - `sealed record GeneratedChallenge(string Script, Tree Answer)`.
  - `static class GenerationChecks { IReadOnlyList<string> Failures(Puzzle p, Script s, RunResult r) }`. An empty list means pass.
  - `interface IChallengeGenerator { GeneratedChallenge Generate(Puzzle p, Random rng) }`, implemented by `sealed class ChallengeGenerator`. It tries up to 50 templates and throws `InvalidOperationException` after that. The interface exists only so API tests can pin the expected answer.
  - `ScriptBuilder`: a fluent string builder (`Size(n)`, `Line(string)`, `Repeat(long, Action<ScriptBuilder>)`, `RandomSet(Random, int n)`, `ToString()`), shared by templates.
  - `static double Similarity(Tree a, Tree b)`: the fraction of equal bulbs (in `GenerationChecks`).

- [ ] **Step 1: Write failing tests:**
  - `Previous_and_next_follow_catalogue_order`: `Previous(1.1)` is null, `Next(1.4)` is `2.1`, `Next(6.3)` is `7.1`, `Next(7.1)` is null.
  - `Catalogue_has_23_puzzles_in_7_rounds`: ids `1.1`–`1.4`, `2.1`–`2.4`, `3.1`–`3.4`, `4.1`–`4.4`, `5.1`–`5.3`, `6.1`–`6.3`, `7.1`; names exactly as the spec's tables; size 16, except 48 for `7.1`.
  - `GenerationChecksTests` (hand-written scripts):
    - A symmetric `FILL R` + `ROTATE` script fails "wrong rotation scores ≥ 60%".
    - A script ending all `K` fails "drains to empty".
    - A round-4 script whose answer under `AnswerOrderWiring` is ≥ 60 % similar fails.
    - A round-6 loop with power-of-two period fails.
    - A 6.2 loop with `PrePeriod < 20` fails.
    - A round-5 script with brute-force `InstructionsExecuted > 200000` fails.
    - A round-2 script where swapping every adjacent `ROTATE`/`MIRROR` pair leaves the answer unchanged fails.
    - A 7.1 answer that is < 90 % different from all-`K` fails (`Similarity(answer, allK) ≥ 0.10`).
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement the checks, from the spec's "Generation checks", applied by round:
  - All puzzles: final tree not all `K`. If the script has `ROTATE`, the `CounterClockwiseRotate` run is < 60 % similar. If it has `MIRROR`, the `MirrorWrongAxis` run is < 60 % similar.
  - Round 2: the adjacent-transform swap changes the answer.
  - Round 4: the `AnswerOrderWiring` run is < 60 % similar.
  - Round 5: a `DetectCycles=false` run executes ≤ 200,000 instructions.
  - Round 6: every loop with a `Period` has a non-power-of-two period. For `6.2`, `PrePeriod ≥ 20`.
  - `7.1`: the all-`K` similarity is < 10 %.
  - Templates are registered as `Func<Random,string>` placeholders that throw `NotImplementedException`. Tasks 7 and 8 fill them.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `feat: puzzle catalogue and generation checks`.

### Task 7: Templates for rounds 1–5

**Files:**
- Create: `WebAPI/Kata/Puzzles/Templates/Round1.cs` … `Round5.cs`
- Modify: `PuzzleCatalogue.cs`
- Test: `Tests/Kata/Puzzles/TemplateTests.cs`

**Interfaces:**
- Consumes: `ScriptBuilder`, `ChallengeGenerator`, `PuzzleCatalogue`.

Template rules (from the spec tables; R = random state from `RGBW`, unless a rule says otherwise):

| Id | Template |
| --- | --- |
| 1.1 | 30 `SET` at random distinct positions |
| 1.2 | `FILL`, then 4–8 `ROW` and 10–20 `SET` lines, mixed, total ≤ 40 |
| 1.3 | 20 `SET`, then `ROTATE` |
| 1.4 | 20 `SET` (≥ 3 on middle bulbs of odd-length rows), then `MIRROR` |
| 2.1 | a 20–30 line paint pattern, then `ROTATE`, `MIRROR` |
| 2.2 | the same pattern style, then `MIRROR`, `ROTATE` |
| 2.3 | a pattern, then 30 random `ROTATE`/`MIRROR` |
| 2.4 | `ROW 15 C1`, `ROTATE`, `ROW 15 C2`, 1–4 `SET`, `ROTATE`, `ROW 15 C3`, optional `MIRROR`, 1–4 `SET` (spec example shape), ≤ 60 lines |
| 3.1 | 2–4 seeds of one colour, then 1–6 `GROW` of it |
| 3.2 | seeds of R and G, then alternating `GROW R`/`GROW G`, 4–8 total |
| 3.3 | seeds on ≥ 2 corners, with `GROW` and `ROTATE` interleaved, 4–8 each |
| 3.4 | 2–4 seeds incl. ≥ 1 corner, then `REPEAT 4..12` of `CYCLE`, `GROW R` (+ optional `ROTATE`) |
| 4.1 | 3–6 seeds, then runs of `SEQUENCE C` (total ≤ 60), changing C between runs |
| 4.2 | a pattern, then `REPEAT 20..200 / CHASE` |
| 4.3 | 3–6 painted rows, then `REPEAT 2..6 / FALL` blocks with `SET` lines between |
| 4.4 | a pattern, then a mix of `SEQUENCE`, `CHASE`, `FALL`, `ROTATE`, `MIRROR`; ≥ 1 `CHASE` or `SEQUENCE` after a `ROTATE` |
| 5.1 | 5,000 lines, no loops, all 10 non-loop instructions, ≥ 1 % `FILL`/`ROW` repaints so `FALL` never drains |
| 5.2 | `REPEAT` nested four deep, counts 1–20, mixed bodies, ≤ 200,000 executed |
| 5.3 | `REPEAT 0` (with painting inside that would change the answer), `REPEAT 1`, empty blocks, comments and blank lines inside blocks |

- [ ] **Step 1: Write the failing test** `Every_round_1_to_5_puzzle_generates_for_seeds_1_to_20`. For each puzzle and seed, `Generate` succeeds, `ScriptParser.Parse` round-trips, the line count is within the table's cap, and `GenerationChecks.Failures` is empty. Add shape assertions per id:
  - 1.4 has ≥ 3 odd-row middle `SET`s.
  - 3.3 has a corner seed.
  - 3.4's `REPEAT` body has both `CYCLE` and `GROW`.
  - 4.4 has `CHASE`/`SEQUENCE` after `ROTATE`.
  - 5.1 has 5,000 instruction lines.
  - 5.2 nests 4 deep.
  - 5.3 contains `REPEAT 0` and a comment inside a block.
- [ ] **Step 2:** Run the test. Expected: FAIL (`NotImplementedException`).
- [ ] **Step 3:** Implement the templates.
- [ ] **Step 4:** Run the test. Expected: PASS. If a check rejects > 50 % of attempts for some puzzle, tune that template rather than raising the retry cap.
- [ ] **Step 5:** Commit: `feat: templates for rounds 1-5`.

### Task 8: Templates for Silent Night and Secret Santa

**Files:**
- Create: `WebAPI/Kata/Puzzles/Templates/Round6.cs`, `WebAPI/Kata/Puzzles/Templates/SecretSanta.cs`, `WebAPI/Kata/Puzzles/Templates/secret-santa.txt` (embedded resource)
- Test: `Tests/Kata/Puzzles/SilentNightTests.cs`, `Tests/Kata/Puzzles/SecretSantaTests.cs`

**Interfaces:**
- Produces:
  - `static class SecretSanta { Tree Picture(Random rng); string Template(Random rng) }`.
  - `static int OrbitPeriod(Tree start, IReadOnlyList<Instruction> body, int cap)`: returns `-1` if the period is over `cap`.

Rules:
- **6.1:** 3–6 seeds, then `REPEAT 999,000,000..1,000,000,000 / ROTATE / CYCLE` (order random).
- **6.2:** as 6.1, plus `GROW <C>` in the body. Seeds are few and far apart so `PrePeriod ≥ 20`.
- **6.3:** 2–4 seeds, then `REPEAT 1000` of { `SET` random, inner `REPEAT 999,000,000..1,000,000,000` of `ROTATE`, `CHASE`, `CYCLE` }.
- **Art file:** 14 lines, each ≤ 35 chars, chars `RGBWK` plus `.` = transparent and `S` = sack. It is a first draft of Santa, sleigh, sack, and red-nosed reindeer. The spec's "pixel-art pass" task refines it later.
- **Picture:** `FILL G`. Sprinkle `W` snow at 8 % of rows 0–33. Paint the art into rows 34–47, left-aligned at `p = (r + 1 − 35) / 2 + offset`, where `offset ∈ [−3, 3]`. Reverse each art line when facing left. Replace `S` with a sack colour from `{R, B, W}`.
- **Backward generation:** pick two loop bodies, each 2–4 instructions from {`ROTATE`, `MIRROR`, `CHASE`, `CYCLE`}, with ≥ 1 `CHASE`, and counts `c1, c2 ∈ [999,000,000, 1,000,000,000]`. Let `P2 = OrbitPeriod(picture, L2, 20000)`, and reject the body if `P2` is −1 or a power of two. `U = L2^((P2 − c2 mod P2) mod P2)(picture)`. Do the same for L1 from U to get start `S`. Emit `SIZE 48`, `FILL G`, `SET` for each non-`G` bulb of `S`, `REPEAT c1 L1 END`, `REPEAT c2 L2 END`. Every body instruction is a bijection on states, so each orbit is purely periodic and running forward undoes the scramble.

- [ ] **Step 1: Write failing tests:**
  - `Round_6_generates_for_seeds_1_to_10`: checks pass, 6.2 has `PrePeriod ≥ 20`, every loop count ≥ 999,000,000, and generation plus emulation finishes in under 5 s per seed.
  - `Secret_santa_script_ends_on_picture` (seeds 1–5): `new Emulator().Run(Parse(script)).Final.ToAnswerLine() == SecretSanta.Picture(sameSeededRng).ToAnswerLine()`. Use two `Random`s with equal seeds, or return both from one call.
  - `Picture_varies`: across seeds 1–20 there is more than one facing, more than one offset, and more than one sack colour.
  - `Art_fits_scene`: 14 lines, each ≤ 35 chars, only allowed chars.
  - `Secret_santa_runs_in_time`: emulation takes < 3 s.
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement. Draft the art file with a clear sleigh silhouette. Check it by printing the picture rows from a scratch test.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `feat: silent night and secret santa generators`.

### Task 9: PNG sampler

**Files:**
- Create: `WebAPI/Scoring/RenderLayout.cs`, `WebAPI/Scoring/PngSampler.cs`
- Create: `Tests/Fixtures/render/*.png`, copied from `christmas-2025/internal/tree/testdata/` (harness Task 2)
- Test: `Tests/Scoring/PngSamplerTests.cs`

**Interfaces:**
- Produces:
  - `static class RenderLayout { double Step(int n); (double x, double y) Centre(int n, int r, int p); double SampleRadius(int n) }`. Constants per Global Constraints. For `n = 1`, use `h = 1000` (avoid division by zero).
  - `static class PngSampler { string Sample(ReadOnlySpan<byte> png, int size) }`. It returns a sampled line of `RGBWK.` and throws `InvalidImageException` on undecodable or non-1200 × 1200 input.
- Add packages `SkiaSharp` and `SkiaSharp.NativeAssets.Linux.NoDependencies` (for the Docker image).

- [ ] **Step 1: Write failing tests:**
  - Each harness fixture `tree-<n>-<name>.png` comes with a `.txt` holding its line. `Sample(png, n) == line` for the 3, 16, and 48-row fixtures.
  - `blank-16.png` samples to 136 `.`.
  - Random bytes, a 100 × 100 PNG, and a zero-length array each throw `InvalidImageException`.
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement. Decode with `SKBitmap.Decode`. Average the pixels whose `(x + 0.5, y + 0.5)` lies within the sample radius. Snap by squared distance to the six colours.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `feat: png bulb sampler`.

### Task 10: Points, unlocks, participant bests, shows, and challenge store

**Files:**
- Modify: `WebAPI/Model/Participant.cs`, `WebAPI/Abstractions/IParticipantRepository.cs`, `WebAPI/Service/CosmosParticipantRepository.cs`, `WebAPI/Service/ParticipantRepository.cs`
- Create: `WebAPI/Scoring/Points.cs`, `WebAPI/Service/ChallengeStore.cs`
- Test: `Tests/Scoring/PointsTests.cs`, `Tests/Service/ParticipantTests.cs`, `Tests/Service/ChallengeStoreTests.cs`, `Tests/Service/InMemoryRepositoryTests.cs`

**Interfaces:**
- Produces:
  - `static class Points { int For(int correct, int bulbCount); bool Passes(int correct, int bulbCount) }`, using the formula in Global Constraints. `Passes` means ≥ 80 %.
  - `Participant`: remove `Score`/`UpdateHighScore`. Add:
    - `Dictionary<string,int> BestCorrect` (puzzle id to best `correct`).
    - `Show? Show`, with `sealed record Show(IReadOnlyList<string> Frames, DateTimeOffset SubmittedAt)`.
    - `string? ETag` (`[JsonProperty("_etag")]`).
    - `ScoreUpdate RecordScore(Puzzle puzzle, int correct)`, with `sealed record ScoreUpdate(int BestPoints, Puzzle? Unlocked)`. It raises `BestCorrect`. `Unlocked` is `PuzzleCatalogue.Next(puzzle)` when this call took the best from below 80 % to at least 80 %.
    - `bool IsUnlocked(Puzzle p)`: true for the first puzzle, or when `Points.Passes` holds for the best on `Previous(p)`.
    - `bool MayShow`: the best on `7.1` is 1176.
  - `IParticipantRepository.RecordScore(string teamName, Puzzle puzzle, int correct) → Task<(Participant, ScoreUpdate)>`. It does a read–modify–replace. Cosmos uses `IfMatchEtag` and retries up to 5 times on `412`. In-memory uses a per-team lock.
  - `IParticipantRepository.SaveShow(string teamName, Show show) → Task<bool>`. It returns true when this is the team's first show. It uses the same retry and lock.
  - `sealed record Challenge(Guid Id, string TeamName, string Puzzle, int Size, Tree Answer, DateTimeOffset ExpiresAt)`.
  - `enum TakeOutcome { Ok, NotFound, AlreadySubmitted, Expired }`.
  - `sealed class ChallengeStore(TimeProvider time) { Challenge Issue(string team, Puzzle p, GeneratedChallenge g); (TakeOutcome, Challenge?) Take(Guid id, string team) }`. Singleton. `Take` is atomic and single-use. Expired entries are pruned on each `Issue`.

- [ ] **Step 1: Write failing tests:**
  - `PointsTests`, `[TestCase]` on 136 bulbs:
    - 136 → 100.
    - 135 (99.26 %) → 78.
    - 109 (80.1 %) → 40.
    - 108 (79.4 %) → 0.
    - 0 → 0.
    - On 1176 bulbs, 1175 → 78.
  - `RecordScore_keeps_best`: 130, then 120 leaves best 130.
  - `RecordScore_reports_unlock_once`: on 1.1, 100 correct (73 %) unlocks nothing. 120 correct unlocks `1.2`. A later 136 unlocks nothing new.
  - `IsUnlocked_follows_previous_puzzle`: a new participant has only `1.1` open. After 120 correct on `1.1`, `1.2` is open and `1.3` is not.
  - `MayShow_needs_full_marks_on_secret_santa`: 1175 on `7.1` gives false, and 1176 gives true.
  - `SaveShow_replaces_and_reports_first`: the first call returns true, the second returns false, and only the second show is kept.
  - `Take_twice_is_AlreadySubmitted`.
  - `Take_by_other_team_is_NotFound`.
  - `Take_after_5_minutes_is_Expired` (advance the `FakeTimeProvider` by 5 min + 1 s).
  - `Concurrent_record_keeps_best`: 50 parallel `RecordScore` calls with correct counts 1..50 on the in-memory repository leave best 50.
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement. Cosmos: run `CreateContainerIfNotExistsAsync` once at startup. Keep the `participants` container and partition key `/id`. No migration is needed, because the event starts with an empty container. A 500-frame show is about 70 KB, well inside the Cosmos 2 MB item limit.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `feat: points, sequential unlocks, best scores, shows, single-use challenges`.

### Task 11: Live feed and banners

**Files:**
- Create: `WebAPI/Service/Feed.cs`
- Test: `Tests/Service/FeedTests.cs`

**Interfaces:**
- Consumes: `IParticipantRepository`, `Puzzle`.
- Produces:
  - `sealed record Banner(string Kind, string Text)`.
  - `sealed record Tile(string TeamName, string Puzzle, int? Correct, int BulbCount, int Size, string States, Banner? Banner)`.
  - `abstract record FeedEvent`, with `TileAdded(Tile)`, `LeaderboardChanged`, and `ShowAdded(string TeamName, bool First)`.
  - `sealed class Feed`. Singleton. Members:
    - `Task InitialiseAsync(IParticipantRepository)`.
    - `Tile Publish(string team, Puzzle p, int correct, string sampledStates)`. It decides the banner, keeps the latest 8, and broadcasts to subscribers.
    - `void PublishShow(string team, bool first)`.
    - `IReadOnlyList<Tile> Latest`.
    - `ChannelReader<FeedEvent> Subscribe(CancellationToken)`.
- Banner rules, checked in this order, one banner per tile:
  - **secret-santa:** any full-marks `7.1`. Text: `"{team} found Santa!"`.
  - **first-full-marks:** the first full marks on a puzzle by any team. Text: `"{team} first to solve {id} {name}"`.
  - **first-round-finish:** the first team with full marks on every puzzle in a round. Text: `"{team} first to finish round {n}"`.
  - "Announced" sets are seeded in `InitialiseAsync` from all participants' `BestCorrect`, where full marks = `Size(Size+1)/2`.
- Every submission sends `LeaderboardChanged`. There is no freeze.

- [ ] **Step 1: Write failing tests:**
  - `First_full_marks_gets_banner_second_does_not`.
  - `Secret_santa_every_solve_gets_banner`.
  - `Round_finish_banner_once`.
  - `Feed_keeps_latest_eight_newest_first`.
  - `Seeded_firsts_survive_restart`: a participant already has full marks on 1.1, so a new full-marks 1.1 gets no banner.
  - `Subscriber_receives_tile_and_show`.
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement. Use one bounded `Channel` per subscriber and drop the oldest when full.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `feat: live feed with banners`.

### Task 12: API endpoints

**Files:**
- Create: `WebAPI/API/KataEndpoints.cs`, `WebAPI/API/FeedEndpoints.cs`, `WebAPI/API/ShowEndpoints.cs`
- Modify:
  - `WebAPI/Program.cs`: map the endpoints and call `Feed.InitialiseAsync` at startup.
  - Rate limits per team, each per 5 min: `challenges` 30, `submissions` 30, `shows` 10.
  - Request body limits: 1 MiB on the submission route, 256 KiB on `POST /shows`.
  - `WebAPI/appsettings*.json` (`Event` section), `WebAPI/WebAPI.http`.
- Test: `Tests/Api/KataApiTests.cs`, `Tests/Api/FeedApiTests.cs`, `Tests/Api/ShowApiTests.cs`

**Interfaces:**
- Consumes:
  - `PuzzleCatalogue`, `IChallengeGenerator`, `ChallengeStore`, `PngSampler`, `Points`.
  - `IParticipantRepository.RecordScore`/`SaveShow`, `Participant.IsUnlocked`/`MayShow`.
  - `Feed.Publish`/`PublishShow`/`Subscribe`.
- Produces: the routes and status codes exactly as the API contract. The locked-puzzle `403` message is `puzzle {id} is locked: reach 80% on {previous} first`.

- [ ] **Step 1: Write failing tests** with `KataApiFactory` (team registered in setup), posting fixture PNG bytes. A helper `UnlockThrough(team, id)` records passing scores straight into the repository.
  - `Fetch_returns_script_and_expiry`: on `1.1`, `201`, `size == 16`, the script starts `SIZE 16`, and `expiresAt = now + 5 min`.
  - `Fetch_unknown_puzzle_is_404` (`9.9`).
  - `Fetch_locked_puzzle_is_403`: a new team fetching `1.2` gets `403` with the locked message. After `UnlockThrough(team, "1.1")` it gets `201`.
  - `Secret_santa_opens_when_reached`: after `UnlockThrough(team, "6.3")`, fetching `7.1` gives `201` with `size == 48`. No clock change is needed.
  - `Submit_scores_against_expected`: substitute the generator with one that returns a known answer equal to the `tree-16-cycle` fixture line. Posting that fixture gives `correct == 136`, `bulbCount == 136`, `points == 100`, `bestPoints == 100`, `unlocked == "1.2"`.
  - `Submit_twice_is_409`, `Submit_after_expiry_is_410`, `Submit_other_teams_challenge_is_404`. The best is unchanged in each.
  - `Submit_garbage_is_400`, `Submit_wrong_dimensions_is_400`, `Submit_2MiB_is_413`.
  - `Leaderboard_sums_best_points_per_puzzle` and sorts descending.
  - `Leaderboard_returns_ranked_teams_and_ladder`: tied totals share a rank (1, 1, 3), and the ladder places each team on its first puzzle below 80 %.
  - `Leaderboard_ETag_changes_after_a_new_score`, and a repeat request with `If-None-Match` set to the current `ETag` gives `304`.
  - `GET /feed` matches the contract shape. `GET /feed/events` streams a `tile` event within 2 s of a submission (read with `HttpClient` and `HttpCompletionOption.ResponseHeadersRead`).
  - `ShowApiTests`:
    - `Show_refused_before_santa`: permission gives `403` with the Santa message, and so does `POST /shows`.
    - `Show_accepted_after_full_marks_santa`: permission gives `204`. Posting 3 frames gives `201`, `GET /shows` lists them, and `/feed/events` streams `show` with `first: true`. A second post streams `first: false` and replaces the frames.
    - `Show_rejects_bad_frames`: 0 frames, 501 frames, and a 135-char frame each give `400`. A 300 KiB body gives `413`.
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement. Production `ChallengeGenerator` uses `Random.Shared`, and the factory substitutes `IChallengeGenerator`. The server builds the leaderboard body (teams and ladder) once per score change and serves the same bytes to every request and SSE subscriber. The SSE endpoint writes a `: ping` comment every 15 s so proxies keep the stream open.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `feat: challenge, submission, leaderboard, feed and show api`.

### Task 13: Scoreboard page

**Files:**
- Create: `WebAPI/wwwroot/index.html`, `WebAPI/wwwroot/scoreboard.css`, `WebAPI/wwwroot/scoreboard.js`
- Create, copied verbatim from the harness (the harness copies are the source of truth, and `wwwroot/art/README.md` says so):
  - `WebAPI/wwwroot/tree.js` from `christmas-2025/internal/viewer/static/tree.js` (harness Task 10).
  - `WebAPI/wwwroot/art/*` from `christmas-2025/internal/art/assets/` (harness Task 8).
- Modify: `WebAPI/Program.cs` (`UseDefaultFiles`, `UseStaticFiles`, with static files exempt from auth)

Layout:
- **Scoreboard (left):** rank, team, and total points from the `teams` array of `/leaderboard`, and the challenge ladder from its `ladder` array. The server computes both; the page only renders them.
- **Feed (right):** tiles with team, puzzle, `correct/bulbCount`, and the decorated tree drawn by `tree.js` from `states`. The newest tile slides in at the top, max 8.
- **Banners:** a banner overlays the feed for 6 s. The tree is enlarged for `first-full-marks` and `secret-santa`. A `show` event with `first: true` shows a `"{team} has a light show!"` banner.
- **Light-show panel (under the feed):** loads `/shows` and plays each team's latest show in turn, at 10 frames a second, with the team name. It reloads `/shows` on each `show` event and keeps its place.
- **Hide leaderboard:** a button hides the standings and the ladder, so the host can keep the final order secret until prize-giving. "Show leaderboard" brings them back.
- **Data:** initial state comes from `GET /feed`, `GET /leaderboard`, and `GET /shows`. Live updates come from `EventSource('/feed/events')`. If `EventSource` errors, poll all three every 5 s.
- **Safety:** all team-supplied text goes through `textContent`.

- [ ] **Step 1:** Write `Tests/Api/StaticPageTests.cs`: `GET /` returns `200` HTML containing `id="scoreboard"`, `id="feed"`, and `id="shows"`, without auth headers. `GET /art/bulbs-16.json` returns `200`.
- [ ] **Step 2:** Run it. Expected: FAIL. Implement the page. Run again. Expected: PASS.
- [ ] **Step 3: Manual check.** Run `dotnet run --project WebAPI` (Development, in-memory). Use `WebAPI.http` to register two teams, fetch, submit fixture PNGs, and post a show (unlock with the test helper or a debug seed). Confirm in a browser at 1920 × 1080:
  - Tiles slide in, and trees match the harness viewer's look.
  - The banner shows on the first full marks.
  - The show panel plays, and the first show gets a banner.
  - A team named `team-<b>x</b>` is rejected at register.
  - "Hide leaderboard" hides the standings and the ladder, and "Show leaderboard" brings them back.
- [ ] **Step 4:** Commit: `feat: big-screen scoreboard page with light shows`.

### Task 14: Deployment and docs

**Files:**
- Modify: `README.md`, `WebAPI/Dockerfile`
- Create: `docs/api.md` (the API contract table above, plus curl examples)

- [ ] **Step 1:** Rewrite `README.md`. Cover:
  - What the server does.
  - Local run (`dotnet run`, in-memory).
  - Configuration keys: `accountEndpoint`, `accountKey`, `Storage:Provider`.
  - A pre-event checklist: empty the Cosmos container, re-copy the harness art and `tree.js` after the pixel-art pass, and run the dry run.
- [ ] **Step 2:** Run `docker build -f WebAPI/Dockerfile .`, then start the container with `Storage__Provider=InMemory`. Run `curl localhost:8080/register/smoke`, fetch `1.1`, and submit `blank-16.png`. Expected: `correct` `0`, `points` `0`, no SkiaSharp native-load error.
- [ ] **Step 3:** Commit: `docs: server readme, api contract, docker smoke`.
