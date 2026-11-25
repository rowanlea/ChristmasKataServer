using System.Net;
using WebAPI.API;
using WebAPI.Kata.Puzzles;

namespace Tests.Api;

public class TeamPageTests
{
    private KataApiFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new KataApiFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task<string> Page() => _client.GetStringAsync("/team");

    [Test]
    public async Task Team_page_loads_without_credentials()
    {
        var response = await _client.GetAsync("/team");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
    }

    [Test]
    public async Task Team_page_lists_every_puzzle_with_name_and_size()
    {
        var html = WebUtility.HtmlDecode(await Page());

        foreach (var puzzle in PuzzleCatalogue.All)
        {
            Assert.That(html, Does.Contain($"data-puzzle=\"{puzzle.Id}\""));
            Assert.That(html, Does.Contain(puzzle.Name));
            Assert.That(html, Does.Contain($">{puzzle.Size}<"));
        }
    }

    [Test]
    public async Task Team_page_explains_light_shows_with_server_limits()
    {
        var html = WebUtility.HtmlDecode(await Page());

        Assert.That(html, Does.Contain($"SIZE {ShowEndpoints.FrameRows}"));
        Assert.That(html, Does.Contain($"{ShowEndpoints.MaxFrames} steps"));
        Assert.That(html, Does.Contain("hth show"));
        Assert.That(html, Does.Contain("Unlocks with full marks on 7.1"));
    }

    [Test]
    public async Task Team_script_is_served_without_credentials()
    {
        var response = await _client.GetAsync("/team.js");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
