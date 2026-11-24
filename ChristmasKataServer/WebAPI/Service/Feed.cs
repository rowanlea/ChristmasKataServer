using System.Threading.Channels;
using WebAPI.Kata.Puzzles;

namespace WebAPI.Service;

public sealed record Banner(string Kind, string Text);

public sealed record Tile(string TeamName, string Puzzle, int? Correct, int BulbCount, int Size, string States, Banner? Banner);

public abstract record FeedEvent;

public sealed record TileAdded(Tile Tile) : FeedEvent;

public sealed record LeaderboardChanged : FeedEvent;

public sealed record ShowAdded(string TeamName, bool First) : FeedEvent;

public sealed class Feed(EventClock clock)
{
    private const string SecretSantaId = "7.1";
    private const int LatestCount = 8;
    private const int SubscriberCapacity = 64;

    private readonly object _gate = new();
    private readonly LinkedList<Tile> _latest = new();
    private readonly List<Channel<FeedEvent>> _subscribers = [];
    private readonly HashSet<string> _announcedPuzzles = [];
    private readonly HashSet<int> _announcedRounds = [];
    private readonly Dictionary<string, HashSet<string>> _fullMarksByTeam = new();

    public IReadOnlyList<Tile> Latest
    {
        get
        {
            lock (_gate) return _latest.ToList();
        }
    }

    private static int BulbCount(Puzzle p) => p.Size * (p.Size + 1) / 2;

    public async Task InitialiseAsync(IParticipantRepository repository)
    {
        var participants = await repository.GetAllParticipants();
        lock (_gate)
        {
            foreach (var participant in participants)
            {
                foreach (var (id, best) in participant.BestCorrect)
                {
                    if (PuzzleCatalogue.Find(id) is not { } puzzle || best != BulbCount(puzzle)) continue;
                    _announcedPuzzles.Add(id);
                    var done = RecordFullMarks(participant.TeamName, puzzle);
                    if (RoundDone(done, puzzle.Round)) _announcedRounds.Add(puzzle.Round);
                }
            }
        }
    }

    public Tile Publish(string team, Puzzle p, int correct, string sampledStates)
    {
        var frozen = clock.Frozen;
        lock (_gate)
        {
            var decided = correct == BulbCount(p) ? DecideBanner(team, p) : null;
            var banner = frozen ? null : decided;
            var tile = new Tile(team, p.Id, frozen ? null : correct, BulbCount(p), p.Size, sampledStates, banner);
            _latest.AddFirst(tile);
            while (_latest.Count > LatestCount) _latest.RemoveLast();

            Broadcast(new TileAdded(tile));
            if (!frozen) Broadcast(new LeaderboardChanged());
            return tile;
        }
    }

    public void PublishShow(string team, bool first)
    {
        lock (_gate) Broadcast(new ShowAdded(team, first));
    }

    public ChannelReader<FeedEvent> Subscribe(CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<FeedEvent>(new BoundedChannelOptions(SubscriberCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

        lock (_gate) _subscribers.Add(channel);

        cancellationToken.Register(() =>
        {
            lock (_gate) _subscribers.Remove(channel);
            channel.Writer.TryComplete();
        });

        return channel.Reader;
    }

    private Banner? DecideBanner(string team, Puzzle p)
    {
        var firstOnPuzzle = _announcedPuzzles.Add(p.Id);
        var done = RecordFullMarks(team, p);
        var firstOfRound = RoundDone(done, p.Round) && _announcedRounds.Add(p.Round);

        if (p.Id == SecretSantaId) return new Banner("secret-santa", $"{team} found Santa!");
        if (firstOfRound) return new Banner("first-round-finish", $"{team} first to finish round {p.Round}");
        if (firstOnPuzzle) return new Banner("first-full-marks", $"{team} first to solve {p.Id} {p.Name}");
        return null;
    }

    private HashSet<string> RecordFullMarks(string team, Puzzle p)
    {
        if (!_fullMarksByTeam.TryGetValue(team, out var done))
            _fullMarksByTeam[team] = done = [];
        done.Add(p.Id);
        return done;
    }

    private static bool RoundDone(HashSet<string> done, int round) =>
        PuzzleCatalogue.InRound(round).All(x => done.Contains(x.Id));

    private void Broadcast(FeedEvent feedEvent)
    {
        foreach (var subscriber in _subscribers) subscriber.Writer.TryWrite(feedEvent);
    }
}
