using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Tests.Api;

public class FeedApiTests
{
    private KataApiFactory _factory = null!;
    private HttpClient _team = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new KataApiFactory();
        _team = await _factory.RegisterTeam("team-07");
    }

    [TearDown]
    public void TearDown()
    {
        _team.Dispose();
        _factory.Dispose();
    }

    private static string Line() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "render", "tree-16-cycle.txt")).TrimEnd();

    private async Task SubmitPerfect()
    {
        _factory.Generator.Override = new FixedAnswerGenerator(Line(), 16);
        var issued = await _team.PostAsync("/puzzles/1.1/challenges", null);
        var id = JsonDocument.Parse(await issued.Content.ReadAsStringAsync()).RootElement.GetProperty("challengeId").GetString();
        var content = new ByteArrayContent(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "render", "tree-16-cycle.png")));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var response = await _team.PostAsync($"/challenges/{id}/submission", content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Feed_matches_the_contract_shape()
    {
        await SubmitPerfect();

        var feed = JsonDocument.Parse(await _factory.CreateClient().GetStringAsync("/feed")).RootElement;

        Assert.That(feed.GetProperty("frozen").GetBoolean(), Is.False);
        var tile = feed.GetProperty("tiles")[0];
        Assert.That(tile.GetProperty("teamName").GetString(), Is.EqualTo("team-07"));
        Assert.That(tile.GetProperty("puzzle").GetString(), Is.EqualTo("1.1"));
        Assert.That(tile.GetProperty("correct").GetInt32(), Is.EqualTo(136));
        Assert.That(tile.GetProperty("bulbCount").GetInt32(), Is.EqualTo(136));
        Assert.That(tile.GetProperty("size").GetInt32(), Is.EqualTo(16));
        Assert.That(tile.GetProperty("states").GetString(), Is.EqualTo(Line()));
        Assert.That(tile.GetProperty("banner").GetProperty("kind").GetString(), Is.EqualTo("first-full-marks"));
    }

    [Test]
    public async Task Events_stream_a_tile_after_a_submission()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/feed/events", HttpCompletionOption.ResponseHeadersRead);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/event-stream"));
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        await SubmitPerfect();

        var events = await ReadEvents(reader, "tile", "leaderboard");
        Assert.That(JsonDocument.Parse(events["tile"]).RootElement.GetProperty("teamName").GetString(), Is.EqualTo("team-07"));
        Assert.That(JsonDocument.Parse(events["leaderboard"]).RootElement[0].GetProperty("total").GetInt32(), Is.EqualTo(100));
    }

    internal static async Task<Dictionary<string, string>> ReadEvents(StreamReader reader, params string[] wanted)
    {
        var found = new Dictionary<string, string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        string? name = null;
        while (!wanted.All(found.ContainsKey))
        {
            var line = await reader.ReadLineAsync(timeout.Token) ?? throw new EndOfStreamException();
            if (line.StartsWith("event: ")) name = line["event: ".Length..];
            else if (line.StartsWith("data: ") && name is not null) found[name] = line["data: ".Length..];
        }
        return found;
    }
}
