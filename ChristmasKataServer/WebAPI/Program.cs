using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebAPI.API;
using WebAPI.Database;
using WebAPI.Kata.Puzzles;
using WebAPI.Scoring;
using WebAPI.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

if (builder.Configuration["Storage:Provider"] == "InMemory")
{
    builder.Services.AddSingleton<IParticipantRepository, ParticipantRepository>();
}
else
{
    builder.Services.AddSingleton<ICosmosDbClient, CosmosDbClient>();
    builder.Services.AddSingleton<CosmosParticipantRepository>();
    builder.Services.AddSingleton<IParticipantRepository>(sp => sp.GetRequiredService<CosmosParticipantRepository>());
}

builder.Services.AddSingleton<ChallengeStore>();
builder.Services.AddOptions<EventOptions>()
    .Bind(builder.Configuration.GetSection("Event"))
    .Validate(o => o.FreezesAt != default, "Event:FreezesAt must be set")
    .Validate(o => !string.IsNullOrWhiteSpace(o.AdminKey), "Event:AdminKey must be set")
    .ValidateOnStart();
builder.Services.AddSingleton<IChallengeGenerator, ChallengeGenerator>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Too many requests, slow down" }, token);
    AddTeamLimit(options, "challenges", 30);
    AddTeamLimit(options, "submissions", 30);
    AddTeamLimit(options, "shows", 10);
});
builder.Services.AddSingleton<EventClock>();
builder.Services.AddSingleton<Feed>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

if (app.Services.GetService<CosmosParticipantRepository>() is { } cosmosRepository)
{
    await cosmosRepository.EnsureCreated();
}

await app.Services.GetRequiredService<Feed>().InitialiseAsync(app.Services.GetRequiredService<IParticipantRepository>());

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<AuthenticationMiddleware>();
app.UseRateLimiter();

app.UseHttpsRedirection();

app.MapGet("/register/{teamName}", async (IParticipantRepository participantRepository, [FromRoute] string teamName) =>
{
    if (!TeamNames.IsValid(teamName))
    {
        return Results.BadRequest(new { error = "Team name must be 1-24 characters: letters, digits and hyphens, starting with a letter or digit" });
    }

    var participantIsAlreadyRegistered = await participantRepository.GetParticipantBy(teamName) != null;
    var secret = participantIsAlreadyRegistered ? null : await participantRepository.Create(teamName);

    return secret is null
        ? Results.BadRequest(new { error = "Team name already exists" })
        : Results.Ok(secret);
})
.WithName("Register")
.WithMetadata(new SkipTeamValidationAttribute());

app.MapKataEndpoints();
app.MapFeedEndpoints();
app.MapShowEndpoints();

app.Run();

static void AddTeamLimit(RateLimiterOptions options, string policy, int permits) =>
    options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Request.Headers["X-Team-Name"].ToString(),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        }));

public partial class Program;

public static partial class TeamNames
{
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9-]{0,23}\z")]
    private static partial Regex Pattern();

    public static bool IsValid(string name) => Pattern().IsMatch(name);
}
