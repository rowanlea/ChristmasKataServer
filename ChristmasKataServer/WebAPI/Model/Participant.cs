using Newtonsoft.Json;
using WebAPI.Kata.Puzzles;
using WebAPI.Scoring;

namespace WebAPI.Service;

public sealed record Show(IReadOnlyList<string> Frames, DateTimeOffset SubmittedAt);

public sealed record ScoreUpdate(int BestPoints, Puzzle? Unlocked);

public record Participant
{
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; }

    [JsonProperty(PropertyName = "teamName")]
    public string TeamName { get; set; }

    public string UserSecret { get; set; }

    public Dictionary<string, int> BestCorrect { get; set; } = new();

    public Dictionary<string, int> PublicBestCorrect { get; set; } = new();

    public Show? Show { get; set; }

    [JsonProperty(PropertyName = "_etag")]
    public string? ETag { get; set; }

    private const string SecretSantaId = "7.1";

    private static int BulbCount(Puzzle p) => p.Size * (p.Size + 1) / 2;

    public ScoreUpdate RecordScore(Puzzle puzzle, int correct, bool frozen)
    {
        var bulbs = BulbCount(puzzle);
        var before = BestCorrect.GetValueOrDefault(puzzle.Id);
        var best = Math.Max(before, correct);
        BestCorrect[puzzle.Id] = best;

        if (!frozen)
            PublicBestCorrect[puzzle.Id] = Math.Max(PublicBestCorrect.GetValueOrDefault(puzzle.Id), correct);

        var opened = !Points.Passes(before, bulbs) && Points.Passes(best, bulbs);
        return new ScoreUpdate(Points.For(best, bulbs), opened ? PuzzleCatalogue.Next(puzzle) : null);
    }

    public bool IsUnlocked(Puzzle p)
    {
        var previous = PuzzleCatalogue.Previous(p);
        return previous is null || Points.Passes(BestCorrect.GetValueOrDefault(previous.Id), BulbCount(previous));
    }

    [JsonIgnore]
    public bool MayShow
    {
        get
        {
            var santa = PuzzleCatalogue.Find(SecretSantaId)!;
            return PublicBestCorrect.GetValueOrDefault(SecretSantaId) == BulbCount(santa);
        }
    }
}
