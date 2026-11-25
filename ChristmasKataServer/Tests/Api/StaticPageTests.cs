using System.Net;
using System.Text.RegularExpressions;
using WebAPI.API;

namespace Tests.Api;

public class StaticPageTests
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

    [Test]
    public async Task Root_serves_the_scoreboard_page_without_auth()
    {
        var response = await _client.GetAsync("/");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
        var html = await response.Content.ReadAsStringAsync();
        Assert.That(html, Does.Contain("id=\"scoreboard\""));
        Assert.That(html, Does.Contain("id=\"feed\""));
        Assert.That(html, Does.Contain("id=\"shows\""));
        Assert.That(html, Does.Contain("id=\"ladder\""));
    }

    [Test]
    public async Task Scoreboard_header_has_hide_and_show_buttons_and_no_status()
    {
        var html = await _client.GetStringAsync("/");
        Assert.That(html, Does.Contain("id=\"hide-standings\"").And.Contain(">Hide leaderboard<"));
        Assert.That(html, Does.Contain("id=\"show-standings\"").And.Contain(">Show leaderboard<"));
        Assert.That(html, Does.Not.Contain("id=\"status\"").And.Not.Contain("id=\"frozen\""));
    }

    [TestCase("/art/bulbs-16.json")]
    [TestCase("/art/bulbs-48.json")]
    [TestCase("/art/tree-16.png")]
    [TestCase("/art/tree-48.png")]
    public async Task Art_assets_are_served_without_auth(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Scoreboard_draws_shows_on_the_grid_the_server_accepts()
    {
        var script = await _client.GetStringAsync("/scoreboard.js");

        var match = Regex.Match(script, @"const SHOW_SIZE = (\d+);");

        Assert.That(match.Success, Is.True, "scoreboard.js must declare SHOW_SIZE");
        Assert.That(int.Parse(match.Groups[1].Value), Is.EqualTo(ShowEndpoints.FrameRows));
    }
}
