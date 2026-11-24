using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Tests.Api;

public class EventConfigTests
{
    [TestCase("Event:AdminKey", "")]
    [TestCase("Event:FreezesAt", "0001-01-01T00:00:00+00:00")]
    public void Startup_fails_when_event_config_is_missing(string key, string value)
    {
        using var factory = new KataApiFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })));

        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}
