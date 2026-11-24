using Microsoft.Extensions.Options;

namespace WebAPI.Service;

public sealed class EventOptions
{
    public DateTimeOffset FreezesAt { get; set; }

    public string AdminKey { get; set; } = "";
}

public sealed class EventClock(TimeProvider time, IOptions<EventOptions> options)
{
    public bool Frozen => time.GetUtcNow() >= options.Value.FreezesAt;
}
