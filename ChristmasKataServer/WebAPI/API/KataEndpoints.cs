using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebAPI.Kata.Puzzles;
using WebAPI.Scoring;
using WebAPI.Service;

namespace WebAPI.API;

public sealed record LeaderboardEntry(string TeamName, int Total, Dictionary<string, int> Puzzles);

public static class Leaderboard
{
    public static List<LeaderboardEntry> Build(IEnumerable<Participant> participants, bool live) =>
        participants
            .Select(participant =>
            {
                var best = live ? participant.BestCorrect : participant.PublicBestCorrect;
                var puzzles = new Dictionary<string, int>();
                foreach (var puzzle in PuzzleCatalogue.All)
                {
                    if (best.TryGetValue(puzzle.Id, out var correct))
                        puzzles[puzzle.Id] = Points.For(correct, puzzle.Size * (puzzle.Size + 1) / 2);
                }
                return new LeaderboardEntry(participant.TeamName, puzzles.Values.Sum(), puzzles);
            })
            .OrderByDescending(entry => entry.Total)
            .ThenBy(entry => entry.TeamName, StringComparer.Ordinal)
            .ToList();
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
                ChallengeStore store, EventClock clock, Feed feed) =>
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

                var (_, update) = await repository.RecordScore(team, puzzle, correct, clock.Frozen);
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

        app.MapGet("/leaderboard", async (IParticipantRepository repository) =>
                Results.Ok(Leaderboard.Build(await repository.GetAllParticipants(), live: false)))
            .WithName("Leaderboard")
            .WithMetadata(new SkipTeamValidationAttribute());

        app.MapGet("/leaderboard/final", async (
                HttpContext context, IParticipantRepository repository, IOptions<EventOptions> options) =>
            {
                var supplied = Encoding.UTF8.GetBytes(context.Request.Headers["X-Admin-Key"].ToString());
                var expected = Encoding.UTF8.GetBytes(options.Value.AdminKey);
                if (supplied.Length == 0 || !CryptographicOperations.FixedTimeEquals(supplied, expected))
                    return Bodies.Error(StatusCodes.Status401Unauthorized, "Invalid admin key");

                return Results.Ok(Leaderboard.Build(await repository.GetAllParticipants(), live: true));
            })
            .WithName("FinalLeaderboard")
            .WithMetadata(new SkipTeamValidationAttribute());
    }
}
