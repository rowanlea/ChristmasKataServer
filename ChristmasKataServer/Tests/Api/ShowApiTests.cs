using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WebAPI.Kata.Puzzles;
using WebAPI.Service;

namespace Tests.Api;

public class ShowApiTests
{
    private const string SantaMessage = "Santa comes first: get full marks on 7.1 to unlock light shows";

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

    private static string Frame(char c = 'R') => new(c, 136);

    private static HttpContent Body(params string[] frames) => JsonContent.Create(new { frames });

    [Test]
    public async Task Show_refused_before_santa()
    {
        var permission = await _team.GetAsync("/shows/permission");
        var post = await _team.PostAsync("/shows", Body(Frame()));

        Assert.That(permission.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(JsonDocument.Parse(await permission.Content.ReadAsStringAsync()).RootElement
            .GetProperty("error").GetString(), Is.EqualTo(SantaMessage));
        Assert.That(post.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(JsonDocument.Parse(await post.Content.ReadAsStringAsync()).RootElement
            .GetProperty("error").GetString(), Is.EqualTo(SantaMessage));
    }

    [Test]
    public async Task Show_accepted_after_full_marks_santa()
    {
        await _factory.UnlockThrough("team-07", "7.1");
        using var anonymous = _factory.CreateClient();
        using var response = await anonymous.GetAsync("/feed/events", HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        Assert.That((await _team.GetAsync("/shows/permission")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var created = await _team.PostAsync("/shows", Body(Frame('R'), Frame('G'), Frame('B')));
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement
            .GetProperty("frames").GetInt32(), Is.EqualTo(3));

        var listed = JsonDocument.Parse(await anonymous.GetStringAsync("/shows")).RootElement;
        Assert.That(listed.GetArrayLength(), Is.EqualTo(1));
        Assert.That(listed[0].GetProperty("teamName").GetString(), Is.EqualTo("team-07"));
        Assert.That(listed[0].GetProperty("frames").GetArrayLength(), Is.EqualTo(3));

        var first = await FeedApiTests.ReadEvents(reader, "show");
        Assert.That(JsonDocument.Parse(first["show"]).RootElement.GetProperty("first").GetBoolean(), Is.True);

        await _team.PostAsync("/shows", Body(Frame('W')));
        var second = await FeedApiTests.ReadEvents(reader, "show");
        Assert.That(JsonDocument.Parse(second["show"]).RootElement.GetProperty("first").GetBoolean(), Is.False);

        var replaced = JsonDocument.Parse(await anonymous.GetStringAsync("/shows")).RootElement;
        Assert.That(replaced.GetArrayLength(), Is.EqualTo(1));
        Assert.That(replaced[0].GetProperty("frames").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task Show_refused_when_santa_full_marks_came_after_the_freeze()
    {
        _factory.Time.SetUtcNow(KataApiFactory.FreezesAt.AddMinutes(1));
        var repository = _factory.Services.GetRequiredService<IParticipantRepository>();
        await repository.RecordScore("team-07", PuzzleCatalogue.Find("7.1")!, 1176, frozen: true);

        Assert.That((await _team.GetAsync("/shows/permission")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await _team.PostAsync("/shows", Body(Frame()))).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Show_allowed_after_freeze_when_santa_full_marks_came_before()
    {
        await _factory.UnlockThrough("team-07", "7.1");
        _factory.Time.SetUtcNow(KataApiFactory.FreezesAt.AddMinutes(1));

        Assert.That((await _team.PostAsync("/shows", Body(Frame()))).StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Show_rejects_bad_frames()
    {
        await _factory.UnlockThrough("team-07", "7.1");

        var none = await _team.PostAsync("/shows", Body());
        var tooMany = await _team.PostAsync("/shows", Body(Enumerable.Repeat(Frame(), 501).ToArray()));
        var short135 = await _team.PostAsync("/shows", Body(new string('R', 135)));
        var badChar = await _team.PostAsync("/shows", Body(new string('X', 136)));
        var notJson = await _team.PostAsync("/shows", new StringContent("nope", Encoding.UTF8, "application/json"));
        var nullFrame = await _team.PostAsync("/shows", new StringContent("{\"frames\":[null]}", Encoding.UTF8, "application/json"));

        Assert.That(none.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(tooMany.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(short135.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(badChar.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(notJson.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(nullFrame.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Show_of_exactly_500_frames_is_accepted()
    {
        await _factory.UnlockThrough("team-07", "7.1");

        var response = await _team.PostAsync("/shows", Body(Enumerable.Repeat(Frame(), 500).ToArray()));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement
            .GetProperty("frames").GetInt32(), Is.EqualTo(500));
    }

    [Test]
    public async Task Show_over_256KiB_is_413()
    {
        await _factory.UnlockThrough("team-07", "7.1");
        var big = new ByteArrayContent(new byte[300 * 1024]);

        var response = await _team.PostAsync("/shows", big);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
    }
}
