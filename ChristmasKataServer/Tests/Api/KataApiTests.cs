using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using WebAPI.Service;

namespace Tests.Api;

public class KataApiTests
{
    private KataApiFactory _factory = null!;
    private HttpClient _team = null!;

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "render", name);

    private static byte[] Fixture(string name) => File.ReadAllBytes(FixturePath(name));

    private static string Line(string name) => File.ReadAllText(FixturePath(name)).TrimEnd();

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

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private async Task<JsonElement> Issue(HttpClient client, string puzzle)
    {
        var response = await client.PostAsync($"/puzzles/{puzzle}/challenges", null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return await Json(response);
    }

    private static Task<HttpResponseMessage> Submit(HttpClient client, JsonElement challenge, byte[] png)
    {
        var content = new ByteArrayContent(png);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return client.PostAsync($"/challenges/{challenge.GetProperty("challengeId").GetString()}/submission", content);
    }

    private async Task<int> BestFor(string team, string puzzle)
    {
        var participant = await _factory.Services.GetRequiredService<IParticipantRepository>().GetParticipantBy(team);
        return participant!.BestCorrect.GetValueOrDefault(puzzle);
    }

    private void KnownAnswerIsTree16Cycle() =>
        _factory.Generator.Override = new FixedAnswerGenerator(Line("tree-16-cycle.txt"), 16);

    [Test]
    public async Task Progress_without_credentials_is_401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/progress");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Progress_with_wrong_key_is_401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Team-Name", "team-07");
        client.DefaultRequestHeaders.Add("X-Unique-Key", "not-the-key");

        var response = await client.GetAsync("/progress");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Progress_returns_statuses_for_the_calling_team()
    {
        KnownAnswerIsTree16Cycle();
        var challenge = await Issue(_team, "1.1");
        Assert.That((await Submit(_team, challenge, Fixture("tree-16-cycle.png"))).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var progress = await Json(await _team.GetAsync("/progress"));

        Assert.That(progress.GetProperty("teamName").GetString(), Is.EqualTo("team-07"));
        Assert.That(progress.GetProperty("next").GetString(), Is.EqualTo("1.2"));
        Assert.That(progress.GetProperty("showsUnlocked").GetBoolean(), Is.False);
        var puzzles = progress.GetProperty("puzzles").EnumerateArray().ToList();
        Assert.That(puzzles[0].GetProperty("id").GetString(), Is.EqualTo("1.1"));
        Assert.That(puzzles[0].GetProperty("status").GetString(), Is.EqualTo("passed"));
        Assert.That(puzzles[0].GetProperty("percent").GetInt32(), Is.EqualTo(100));
        Assert.That(puzzles[1].GetProperty("status").GetString(), Is.EqualTo("current"));
        Assert.That(puzzles[1].GetProperty("percent").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task Fetch_returns_script_and_expiry()
    {
        var challenge = await Issue(_team, "1.1");

        Assert.That(challenge.GetProperty("puzzle").GetString(), Is.EqualTo("1.1"));
        Assert.That(challenge.GetProperty("size").GetInt32(), Is.EqualTo(16));
        Assert.That(challenge.GetProperty("script").GetString(), Does.StartWith("SIZE 16"));
        Assert.That(challenge.GetProperty("expiresAt").GetDateTimeOffset(),
            Is.EqualTo(_factory.Time.GetUtcNow().AddMinutes(5)));
    }

    [Test]
    public async Task Fetch_unknown_puzzle_is_404()
    {
        var response = await _team.PostAsync("/puzzles/9.9/challenges", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Fetch_locked_puzzle_is_403()
    {
        var locked = await _team.PostAsync("/puzzles/1.2/challenges", null);

        Assert.That(locked.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await Json(locked)).GetProperty("error").GetString(),
            Is.EqualTo("puzzle 1.2 is locked: reach 80% on 1.1 first"));

        await _factory.UnlockThrough("team-07", "1.1");
        var open = await _team.PostAsync("/puzzles/1.2/challenges", null);

        Assert.That(open.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Secret_santa_opens_when_reached()
    {
        await _factory.UnlockThrough("team-07", "6.3");

        var challenge = await Issue(_team, "7.1");

        Assert.That(challenge.GetProperty("size").GetInt32(), Is.EqualTo(48));
    }

    [Test]
    public async Task Submit_scores_against_expected()
    {
        KnownAnswerIsTree16Cycle();
        var challenge = await Issue(_team, "1.1");

        var response = await Submit(_team, challenge, Fixture("tree-16-cycle.png"));
        var result = await Json(response);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(result.GetProperty("puzzle").GetString(), Is.EqualTo("1.1"));
        Assert.That(result.GetProperty("correct").GetInt32(), Is.EqualTo(136));
        Assert.That(result.GetProperty("bulbCount").GetInt32(), Is.EqualTo(136));
        Assert.That(result.GetProperty("points").GetInt32(), Is.EqualTo(100));
        Assert.That(result.GetProperty("bestPoints").GetInt32(), Is.EqualTo(100));
        Assert.That(result.GetProperty("unlocked").GetString(), Is.EqualTo("1.2"));
    }

    [Test]
    public async Task Submit_twice_is_409()
    {
        KnownAnswerIsTree16Cycle();
        var challenge = await Issue(_team, "1.1");
        await Submit(_team, challenge, Fixture("tree-16-cycle.png"));

        var second = await Submit(_team, challenge, Fixture("blank-16.png"));

        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await BestFor("team-07", "1.1"), Is.EqualTo(136));
    }

    [Test]
    public async Task Submit_after_expiry_is_410()
    {
        KnownAnswerIsTree16Cycle();
        var challenge = await Issue(_team, "1.1");
        _factory.Time.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        var response = await Submit(_team, challenge, Fixture("tree-16-cycle.png"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
        Assert.That(await BestFor("team-07", "1.1"), Is.EqualTo(0));
    }

    [Test]
    public async Task Submit_after_expiry_is_410_even_when_another_team_issued_meanwhile()
    {
        KnownAnswerIsTree16Cycle();
        using var other = await _factory.RegisterTeam("team-08");
        var challenge = await Issue(_team, "1.1");
        _factory.Time.Advance(TimeSpan.FromMinutes(6));
        await Issue(other, "1.1");

        var response = await Submit(_team, challenge, Fixture("tree-16-cycle.png"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
    }

    [Test]
    public async Task Submit_other_teams_challenge_is_404()
    {
        KnownAnswerIsTree16Cycle();
        using var other = await _factory.RegisterTeam("team-08");
        var challenge = await Issue(_team, "1.1");

        var response = await Submit(other, challenge, Fixture("tree-16-cycle.png"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await BestFor("team-08", "1.1"), Is.EqualTo(0));
        Assert.That(await BestFor("team-07", "1.1"), Is.EqualTo(0));
    }

    [Test]
    public async Task Submit_garbage_is_400_and_leaves_the_challenge_usable()
    {
        KnownAnswerIsTree16Cycle();
        var challenge = await Issue(_team, "1.1");
        var garbage = new byte[2000];
        new Random(1).NextBytes(garbage);

        var rejected = await Submit(_team, challenge, garbage);
        var retried = await Submit(_team, challenge, Fixture("tree-16-cycle.png"));

        Assert.That(rejected.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(retried.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await BestFor("team-07", "1.1"), Is.EqualTo(136));
    }

    [Test]
    public async Task Submit_wrong_dimensions_is_400()
    {
        var challenge = await Issue(_team, "1.1");
        using var bitmap = new SKBitmap(10, 10);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);

        var response = await Submit(_team, challenge, data.ToArray());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await BestFor("team-07", "1.1"), Is.EqualTo(0));
    }

    [Test]
    public async Task Submit_2MiB_is_413()
    {
        var challenge = await Issue(_team, "1.1");

        var response = await Submit(_team, challenge, new byte[2 * 1024 * 1024]);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
    }

    [Test]
    public async Task Unexpected_failure_is_500_with_an_error_body()
    {
        _factory.Generator.Override = new ThrowingGenerator();

        var response = await _team.PostAsync("/puzzles/1.1/challenges", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That((await Json(response)).GetProperty("error").GetString(), Is.EqualTo("internal error"));
    }

    [Test]
    public async Task Failed_score_write_releases_the_challenge()
    {
        KnownAnswerIsTree16Cycle();
        var challenge = await Issue(_team, "1.1");
        _factory.Spy.FailRecordScore = true;

        var failed = await Submit(_team, challenge, Fixture("tree-16-cycle.png"));
        _factory.Spy.FailRecordScore = false;
        var retried = await Submit(_team, challenge, Fixture("tree-16-cycle.png"));

        Assert.That(failed.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That((await Json(failed)).GetProperty("error").GetString(), Is.EqualTo("internal error"));
        Assert.That(retried.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await BestFor("team-07", "1.1"), Is.EqualTo(136));
    }

    [Test]
    public async Task Leaderboard_sums_best_points_per_puzzle()
    {
        using var other = await _factory.RegisterTeam("team-08");
        await _factory.UnlockThrough("team-07", "1.2");
        await _factory.UnlockThrough("team-08", "1.1");

        var teams = (await Json(await _factory.CreateClient().GetAsync("/leaderboard"))).GetProperty("teams");

        Assert.That(teams.GetArrayLength(), Is.EqualTo(2));
        Assert.That(teams[0].GetProperty("teamName").GetString(), Is.EqualTo("team-07"));
        Assert.That(teams[0].GetProperty("total").GetInt32(), Is.EqualTo(200));
        Assert.That(teams[1].GetProperty("teamName").GetString(), Is.EqualTo("team-08"));
        Assert.That(teams[1].GetProperty("total").GetInt32(), Is.EqualTo(100));
    }

    [Test]
    public async Task Leaderboard_returns_ranked_teams_and_ladder()
    {
        using var other = await _factory.RegisterTeam("team-08");
        await _factory.UnlockThrough("team-07", "1.2");

        var board = await Json(await _factory.CreateClient().GetAsync("/leaderboard"));
        var first = board.GetProperty("teams")[0];
        var ladder = board.GetProperty("ladder");
        var rungs = ladder.EnumerateArray().ToDictionary(r => r.GetProperty("level").GetString()!);

        Assert.That(first.GetProperty("rank").GetInt32(), Is.EqualTo(1));
        Assert.That(first.GetProperty("teamName").GetString(), Is.EqualTo("team-07"));
        Assert.That(first.GetProperty("total").GetInt32(), Is.EqualTo(200));
        Assert.That(first.TryGetProperty("puzzles", out _), Is.False);
        Assert.That(board.GetProperty("teams")[1].GetProperty("rank").GetInt32(), Is.EqualTo(2));
        Assert.That(ladder.GetArrayLength(), Is.EqualTo(24));
        Assert.That(ladder[0].GetProperty("level").GetString(), Is.EqualTo("santa"));
        var on13 = rungs["1.3"].GetProperty("teams")[0];
        Assert.That(on13.GetProperty("teamName").GetString(), Is.EqualTo("team-07"));
        Assert.That(on13.GetProperty("percent").GetInt32(), Is.EqualTo(0));
        Assert.That(rungs["1.1"].GetProperty("teams")[0].GetProperty("teamName").GetString(), Is.EqualTo("team-08"));
        Assert.That(rungs["1.1"].GetProperty("name").GetString(), Is.EqualTo("Paint by Numbers"));
    }

    [Test]
    public async Task Leaderboard_is_compact_json()
    {
        var body = await _factory.CreateClient().GetStringAsync("/leaderboard");

        Assert.That(body, Does.Not.Contain('\n'));
    }

    [Test]
    public async Task Leaderboard_with_no_teams_is_empty_not_an_error()
    {
        using var factory = new KataApiFactory();

        var response = await factory.CreateClient().GetAsync("/leaderboard");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await Json(response)).GetProperty("teams").GetArrayLength(), Is.EqualTo(0));
        Assert.That(response.Headers.ETag, Is.Not.Null);
    }

    [Test]
    public async Task Leaderboard_answers_304_to_matching_If_None_Match()
    {
        using var client = _factory.CreateClient();
        var first = await client.GetAsync("/leaderboard");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/leaderboard");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await client.SendAsync(request);

        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
        Assert.That(second.Headers.ETag, Is.EqualTo(first.Headers.ETag));
    }

    [Test]
    public async Task Leaderboard_ETag_changes_after_a_new_score()
    {
        using var client = _factory.CreateClient();
        var before = await client.GetAsync("/leaderboard");
        KnownAnswerIsTree16Cycle();
        var challenge = await Issue(_team, "1.1");
        await Submit(_team, challenge, Fixture("tree-16-cycle.png"));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/leaderboard");
        request.Headers.IfNoneMatch.Add(before.Headers.ETag!);
        var after = await client.SendAsync(request);

        Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(after.Headers.ETag, Is.Not.EqualTo(before.Headers.ETag));
        Assert.That((await Json(after)).GetProperty("teams")[0].GetProperty("total").GetInt32(), Is.EqualTo(100));
    }

    [Test]
    public async Task Ladder_route_is_gone()
    {
        var response = await _factory.CreateClient().GetAsync("/ladder");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Leaderboard_shows_a_team_registered_after_it_was_last_built()
    {
        using var anonymous = _factory.CreateClient();
        await anonymous.GetAsync("/leaderboard");

        using var other = await _factory.RegisterTeam("team-08");
        var board = await Json(await anonymous.GetAsync("/leaderboard"));

        Assert.That(board.GetProperty("teams").EnumerateArray().Select(e => e.GetProperty("teamName").GetString()),
            Is.EquivalentTo(new[] { "team-07", "team-08" }));
    }

    [Test]
    public async Task Final_leaderboard_route_is_gone()
    {
        var response = await _factory.CreateClient().GetAsync("/leaderboard/final");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
