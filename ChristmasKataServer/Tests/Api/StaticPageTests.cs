using System.Net;

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
    }

    [Test]
    public async Task Art_assets_are_served_without_auth()
    {
        var response = await _client.GetAsync("/art/bulbs-16.json");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
