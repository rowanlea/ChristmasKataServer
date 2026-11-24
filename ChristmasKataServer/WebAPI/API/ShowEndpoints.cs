using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Service;

namespace WebAPI.API;

public static class ShowEndpoints
{
    public const int MaxBodyBytes = 256 * 1024;
    private const int FrameLength = 136;
    private const int MaxFrames = 500;
    private const string SantaFirst = "Santa comes first: get full marks on 7.1 to unlock light shows";

    private sealed record ShowRequest(List<string?>? Frames);

    public static void MapShowEndpoints(this WebApplication app)
    {
        app.MapGet("/shows/permission", async (HttpContext context, IParticipantRepository repository) =>
                await repository.GetParticipantBy(Bodies.Team(context)) is { MayShow: true }
                    ? Results.NoContent()
                    : Bodies.Error(StatusCodes.Status403Forbidden, SantaFirst))
            .WithName("ShowPermission");

        app.MapPost("/shows", async (
                HttpContext context, IParticipantRepository repository, Feed feed, TimeProvider time) =>
            {
                var team = Bodies.Team(context);
                if (await repository.GetParticipantBy(team) is not { MayShow: true })
                    return Bodies.Error(StatusCodes.Status403Forbidden, SantaFirst);

                var body = await Bodies.ReadBounded(context.Request, MaxBodyBytes, context.RequestAborted);
                if (body is null)
                    return Bodies.Error(StatusCodes.Status413PayloadTooLarge, "show must be at most 256 KiB");

                ShowRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<ShowRequest>(body, JsonSerializerOptions.Web);
                }
                catch (JsonException)
                {
                    return Bodies.Error(StatusCodes.Status400BadRequest, "body must be a JSON object with a frames array");
                }

                if (request?.Frames is not { Count: >= 1 and <= MaxFrames } frames)
                    return Bodies.Error(StatusCodes.Status400BadRequest, $"a show has 1 to {MaxFrames} frames");
                if (frames.Any(frame => !IsFrame(frame)))
                    return Bodies.Error(StatusCodes.Status400BadRequest,
                        $"each frame must be exactly {FrameLength} characters from R, G, B, W, K");

                var first = await repository.SaveShow(team, new Show(frames!, time.GetUtcNow()));
                feed.PublishShow(team, first);
                return Results.Json(new { frames = frames.Count }, statusCode: StatusCodes.Status201Created);
            })
            .WithMetadata(new RequestSizeLimitAttribute(MaxBodyBytes))
            .RequireRateLimiting("shows")
            .WithName("PostShow");

        app.MapGet("/shows", async (IParticipantRepository repository) =>
            {
                var participants = await repository.GetAllParticipants();
                return Results.Ok(participants
                    .Where(p => p.Show is not null)
                    .OrderBy(p => p.Show!.SubmittedAt)
                    .Select(p => new { teamName = p.TeamName, frames = p.Show!.Frames, submittedAt = p.Show.SubmittedAt })
                    .ToList());
            })
            .WithName("Shows")
            .WithMetadata(new SkipTeamValidationAttribute());
    }

    private static bool IsFrame(string? frame) =>
        frame is { Length: FrameLength } && frame.All(c => c is 'R' or 'G' or 'B' or 'W' or 'K');
}
