using System.Net;
using System.Text.RegularExpressions;
using WebAPI.API;
using WebAPI.Kata.Puzzles;
using WebAPI.Scoring;
using WebAPI.Service;

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

    private static string? Attribute(string html, string name)
    {
        var match = Regex.Match(html, $"\\s{name}=\"([^\"]*)\"");
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }

    [Test]
    public async Task Team_page_carries_the_pass_mark_from_the_scoring_rules()
    {
        var html = await Page();

        Assert.That(Attribute(html, "data-pass-percent"), Is.EqualTo(Points.PassPercent.ToString()));
    }

    [Test]
    public async Task Team_page_carries_the_name_and_key_rules_the_server_uses()
    {
        var html = await Page();

        Assert.That(Attribute(html, "data-name-pattern"), Is.EqualTo(TeamNames.Pattern));
        Assert.That(Attribute(html, "data-key-pattern"), Is.EqualTo(TeamSecrets.Pattern));
    }

    [Test]
    public void Key_pattern_matches_a_new_secret()
    {
        Assert.That(TeamSecrets.New(), Does.Match($"^{TeamSecrets.Pattern}\\z"));
    }

    [Test]
    public async Task Team_script_is_served_without_credentials()
    {
        var response = await _client.GetAsync("/team.js");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
