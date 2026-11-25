using Microsoft.AspNetCore.Mvc;
using WebAPI.Kata.Puzzles;
using WebAPI.Scoring;
using WebAPI.Service;

namespace WebAPI.API;

public sealed record LeaderboardEntry(int Rank, string TeamName, int Total);

public static class Leaderboard
{
    public static List<LeaderboardEntry> Build(IEnumerable<Participant> participants)
    {
        var totals = participants
            .Select(participant => (participant.TeamName, Total: PuzzleCatalogue.All
                .Where(puzzle => participant.BestCorrect.ContainsKey(puzzle.Id))
                .Sum(puzzle => Points.For(participant.BestCorrect[puzzle.Id], puzzle.Size * (puzzle.Size + 1) / 2))))
            .OrderByDescending(team => team.Total)
            .ThenBy(team => team.TeamName, StringComparer.Ordinal)
            .ToList();
        return totals
            .Select(team => new LeaderboardEntry(1 + totals.Count(other => other.Total > team.Total), team.TeamName, team.Total))
            .ToList();
    }
}

public static class KataEndpoints
{
    public const int MaxPngBytes = 1024 * 1024;

    public static void MapKataEndpoints(this WebApplication app)
    {
        app.MapPost("/puzzles/{puzzle}/challenges", async (
                string puzzle, HttpContext context, IParticipantRepository repository,
                IChallengeGenerator generator, ChallengeStore store) =>
            {
                if (PuzzleCatalogue.Find(puzzle) is not { } found)
                    return Bodies.Error(StatusCodes.Status404NotFound, $"unknown puzzle {puzzle}");

                var participant = await repository.GetParticipantBy(Bodies.Team(context));
                if (participant is null)
                    return Bodies.Error(StatusCodes.Status401Unauthorized, "Invalid team credentials");

                if (!participant.IsUnlocked(found))
                {
                    var previous = PuzzleCatalogue.Previous(found)!;
                    return Bodies.Error(StatusCodes.Status403Forbidden,
                        $"puzzle {found.Id} is locked: reach 80% on {previous.Id} first");
                }

                var generated = generator.Generate(found, Random.Shared);
                var challenge = store.Issue(participant.TeamName, found, generated);
                return Results.Json(new
                {
                    challengeId = challenge.Id,
                    puzzle = challenge.Puzzle,
                    size = challenge.Size,
                    script = generated.Script,
                    expiresAt = challenge.ExpiresAt,
                }, statusCode: StatusCodes.Status201Created);
            })
            .RequireRateLimiting("challenges")
            .WithName("IssueChallenge");

        app.MapPost("/challenges/{challengeId}/submission", async (
                string challengeId, HttpContext context, IParticipantRepository repository,
                ChallengeStore store, Feed feed, LeaderboardCache leaderboard) =>
            {
                var team = Bodies.Team(context);
                var png = await Bodies.ReadBounded(context.Request, MaxPngBytes, context.RequestAborted);
                if (png is null)
                    return Bodies.Error(StatusCodes.Status413PayloadTooLarge, "image must be at most 1 MiB");

                if (!Guid.TryParse(challengeId, out var id))
                    return Bodies.Error(StatusCodes.Status404NotFound, "unknown challenge");

                var (outcome, challenge) = store.Take(id, team);
                switch (outcome)
                {
                    case TakeOutcome.NotFound:
                        return Bodies.Error(StatusCodes.Status404NotFound, "unknown challenge");
                    case TakeOutcome.AlreadySubmitted:
                        return Bodies.Error(StatusCodes.Status409Conflict, "challenge already submitted");
                    case TakeOutcome.Expired:
                        return Bodies.Error(StatusCodes.Status410Gone, "challenge expired");
                }

                string sampled;
                try
                {
                    sampled = PngSampler.Sample(png, challenge!.Size);
                }
                catch (InvalidImageException e)
                {
                    store.Release(id);
                    return Bodies.Error(StatusCodes.Status400BadRequest, e.Message);
                }

                var puzzle = PuzzleCatalogue.Find(challenge.Puzzle)!;
                var bulbCount = puzzle.Size * (puzzle.Size + 1) / 2;
                var expected = challenge.Answer.ToAnswerLine();
                var correct = Enumerable.Range(0, bulbCount).Count(i => sampled[i] == expected[i]);

                ScoreUpdate update;
                try
                {
                    (_, update) = await repository.RecordScore(team, puzzle, correct);
                }
                catch
                {
                    store.Release(id);
                    throw;
                }
                leaderboard.Invalidate();
                feed.Publish(team, puzzle, correct, sampled);

                return Results.Ok(new
                {
                    puzzle = puzzle.Id,
                    correct,
                    bulbCount,
                    points = Points.For(correct, bulbCount),
                    bestPoints = update.BestPoints,
                    unlocked = update.Unlocked?.Id,
                });
            })
            .WithMetadata(new RequestSizeLimitAttribute(MaxPngBytes))
            .RequireRateLimiting("submissions")
            .WithName("Submit");

        app.MapGet("/leaderboard", async (HttpContext context, LeaderboardCache leaderboard) =>
            {
                var cached = await leaderboard.Current();
                context.Response.Headers.ETag = cached.ETag;
                if (context.Request.Headers.IfNoneMatch == cached.ETag)
                    return Results.StatusCode(StatusCodes.Status304NotModified);
                return Results.Bytes(cached.Json, "application/json");
            })
            .WithName("Leaderboard")
            .WithMetadata(new SkipTeamValidationAttribute());

        app.MapGet("/progress", async (HttpContext context, IParticipantRepository repository) =>
            {
                var participant = await repository.GetParticipantBy(Bodies.Team(context));
                if (participant is null)
                    return Bodies.Error(StatusCodes.Status401Unauthorized, "Invalid team credentials");
                return Results.Ok(Progress.Build(participant));
            })
            .WithName("Progress");
    }
}
