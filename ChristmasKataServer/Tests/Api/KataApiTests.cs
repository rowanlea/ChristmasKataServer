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
    public async Task Leaderboard_sums_best_points_per_puzzle()
    {
        using var other = await _factory.RegisterTeam("team-08");
        await _factory.UnlockThrough("team-07", "1.2");
        await _factory.UnlockThrough("team-08", "1.1");

        var board = await Json(await _factory.CreateClient().GetAsync("/leaderboard"));

        Assert.That(board.GetArrayLength(), Is.EqualTo(2));
        Assert.That(board[0].GetProperty("teamName").GetString(), Is.EqualTo("team-07"));
        Assert.That(board[0].GetProperty("total").GetInt32(), Is.EqualTo(200));
        Assert.That(board[0].GetProperty("puzzles").GetProperty("1.2").GetInt32(), Is.EqualTo(100));
        Assert.That(board[1].GetProperty("teamName").GetString(), Is.EqualTo("team-08"));
        Assert.That(board[1].GetProperty("total").GetInt32(), Is.EqualTo(100));
    }

    [Test]
    public async Task Leaderboard_frozen_hides_post_freeze_scores()
    {
        var repository = _factory.Services.GetRequiredService<IParticipantRepository>();
        var puzzle = WebAPI.Kata.Puzzles.PuzzleCatalogue.Find("1.1")!;
        await repository.RecordScore("team-07", puzzle, 120, frozen: false);
        await repository.RecordScore("team-07", puzzle, 136, frozen: true);
        var anonymous = _factory.CreateClient();

        var publicBoard = await Json(await anonymous.GetAsync("/leaderboard"));
        Assert.That(publicBoard[0].GetProperty("total").GetInt32(), Is.EqualTo(56));

        var finalBoard = await Json(await AdminGet(anonymous, "/leaderboard/final", KataApiFactory.AdminKey));
        Assert.That(finalBoard[0].GetProperty("total").GetInt32(), Is.EqualTo(100));
    }

    [Test]
    public async Task Final_leaderboard_without_or_with_wrong_key_is_401()
    {
        var anonymous = _factory.CreateClient();

        Assert.That((await anonymous.GetAsync("/leaderboard/final")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That((await AdminGet(anonymous, "/leaderboard/final", "nope")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private static Task<HttpResponseMessage> AdminGet(HttpClient client, string url, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Admin-Key", key);
        return client.SendAsync(request);
    }
}
