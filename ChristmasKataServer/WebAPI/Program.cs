using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using WebAPI;
using WebAPI.API;
using WebAPI.Database;
using WebAPI.Model;
using WebAPI.Requests;
using WebAPI.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<ICosmosDbClient, CosmosDbClient>();
builder.Services.AddTransient<IParticipantRepository, CosmosParticipantRepository>();
builder.Services.AddTransient<IValidationResultReader, ValidationResultReader>();
builder.Services.AddTransient<IScoringService, ScoringService>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddRateLimiter(limiter =>
{
    limiter.AddPolicy("naughtyornice", context =>
    {
        var teamName = context.Request.Headers["X-Team-Name"].FirstOrDefault();
        var teamSecret = context.Request.Headers["X-Unique-Key"].FirstOrDefault();
        
        var key = $"{teamName}:${teamSecret}";
        
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        });
    });

    limiter.AddPolicy("score", context =>
    {
        var teamName = context.Request.Headers["X-Team-Name"].FirstOrDefault();
        var teamSecret = context.Request.Headers["X-Unique-Key"].FirstOrDefault();
        
        var key = $"{teamName}:${teamSecret}";
        
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        });
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

app.UseMiddleware<AuthenticationMiddleware>();
app.UseRateLimiter();

app.UseHttpsRedirection();

Determiner determiner = new();

app.MapGet("/naughtyornice", ([FromBody] Person person) => determiner.DetermineNiceness(person))
.WithName("NaughtyOrNice")
.RequireRateLimiting("naughtyornice")
.WithOpenApi();


app.MapPost("/score", async (HttpContext context, [FromBody] NaughtyNiceCandidateSubmissionRequest request) =>
    {
        var teamName = context.Request.Headers["X-Team-Name"].FirstOrDefault();
        
        var scoringService = app.Services.GetRequiredService<IScoringService>();
        var result = await scoringService.Score(teamName!, request);
        return Results.Ok(result);
    })
.RequireRateLimiting("score")
.WithName("Score")
.WithOpenApi();

app.MapGet("/leaderboard", async () =>
    {
        var participantRepository = app.Services.GetRequiredService<IParticipantRepository>();
        var participants = await participantRepository.GetAllParticipants();
        var leaderboard = participants.Select(x => new
                {
                    TeamName = x.TeamName,
                    Score = x.Score
                })
        .OrderByDescending(x => x.Score)
        .ToList();
        return Results.Ok(leaderboard);
    })
.WithName("Leaderboard")
.WithMetadata(new SkipTeamValidationAttribute())
.WithOpenApi();


app.MapGet("/register/{teamName}", async (HttpContext context, [FromRoute] string teamName) =>
{
    var participantRepository = app.Services.GetRequiredService<IParticipantRepository>();

    var participantIsAlreadyRegistered = await participantRepository.GetParticipantBy(teamName) != null;

    if (participantIsAlreadyRegistered)
    {
        return Results.BadRequest("Team name already exists");
    }

    var result = await participantRepository.Create(teamName);
    return Results.Ok(result);
})
.WithName("Register")
.WithMetadata(new SkipTeamValidationAttribute())
.WithOpenApi();

app.Run();