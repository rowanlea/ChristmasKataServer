using System.Net;
using System.Net.Http.Json;

namespace Tests.Api;

public class RegistrationTests
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
    public async Task Register_returns_secret_then_rejects_duplicate()
    {
        var first = await _client.GetAsync("/register/team-07");
        var second = await _client.GetAsync("/register/team-07");

        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Register_returns_a_32_char_lowercase_hex_secret()
    {
        var first = await _client.GetFromJsonAsync<string>("/register/team-07");
        var second = await _client.GetFromJsonAsync<string>("/register/team-08");

        Assert.That(first, Does.Match("^[0-9a-f]{32}$"));
        Assert.That(second, Does.Match("^[0-9a-f]{32}$"));
        Assert.That(second, Is.Not.EqualTo(first));
    }

    [Test]
    public async Task Register_race_on_the_same_name_is_400()
    {
        await _client.GetAsync("/register/team-07");
        _factory.Spy.HideOnLookup = true;

        var second = await _client.GetAsync("/register/team-07");

        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await second.Content.ReadFromJsonAsync<Dictionary<string, string>>(),
            Is.EqualTo(new Dictionary<string, string> { ["error"] = "Team name already exists" }));
    }

    [TestCase("%3Cscript%3E")]
    [TestCase("%20")]
    [TestCase("a%20b")]
    [TestCase("abcdefghijklmnopqrstuvwxy")]
    [TestCase("team%0A")]
    [TestCase("abcdefghijklmnopqrstuvwx%0A")]
    public async Task Register_rejects_invalid_names(string name)
    {
        var response = await _client.GetAsync($"/register/{name}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Register_accepts_24_character_name()
    {
        var response = await _client.GetAsync("/register/abcdefghijklmnopqrstuvwx");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Register_rejects_empty_name()
    {
        var response = await _client.GetAsync("/register/");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Protected_route_without_headers_is_401()
    {
        var response = await _client.PostAsync("/puzzles/1.1/challenges", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
