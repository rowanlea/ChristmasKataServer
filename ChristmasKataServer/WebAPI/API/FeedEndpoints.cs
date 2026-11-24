using System.Text.Json;
using System.Threading.Channels;
using WebAPI.Service;

namespace WebAPI.API;

public static class FeedEndpoints
{
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);
    private static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web);

    public static void MapFeedEndpoints(this WebApplication app)
    {
        app.MapGet("/feed", (Feed feed) => Results.Ok(new { tiles = feed.Latest }))
            .WithName("Feed")
            .WithMetadata(new SkipTeamValidationAttribute());

        app.MapGet("/feed/events", async (HttpContext context, Feed feed, LeaderboardCache leaderboard) =>
            {
                var cancellation = context.RequestAborted;
                var reader = feed.Subscribe(cancellation);

                context.Response.ContentType = "text/event-stream";
                context.Response.Headers.CacheControl = "no-cache";
                context.Response.Headers["X-Accel-Buffering"] = "no";

                try
                {
                    await Write(context, ": connected\n\n");
                    while (!cancellation.IsCancellationRequested)
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                        timeout.CancelAfter(PingInterval);
                        FeedEvent feedEvent;
                        try
                        {
                            feedEvent = await reader.ReadAsync(timeout.Token);
                        }
                        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                        {
                            await Write(context, ": ping\n\n");
                            continue;
                        }

                        switch (feedEvent)
                        {
                            case TileAdded added:
                                await Send(context, "tile", added.Tile);
                                break;
                            case LeaderboardChanged:
                                await Send(context, "leaderboard", await leaderboard.Public());
                                await Send(context, "ladder", await leaderboard.Ladder());
                                break;
                            case ShowAdded show:
                                await Send(context, "show", new { teamName = show.TeamName, first = show.First });
                                break;
                        }
                    }
                }
                catch (Exception e) when (e is OperationCanceledException or ChannelClosedException)
                {
                }
            })
            .WithName("FeedEvents")
            .WithMetadata(new SkipTeamValidationAttribute());
    }

    private static Task Send(HttpContext context, string name, object data) =>
        Write(context, $"event: {name}\ndata: {JsonSerializer.Serialize(data, Compact)}\n\n");

    private static async Task Write(HttpContext context, string text)
    {
        await context.Response.WriteAsync(text, context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }
}
