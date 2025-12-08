using System;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebAPI;
using WebAPI.API;
using WebAPI.Model;
using WebAPI.Requests;
using WebAPI.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IParticipantRepository, ParticipantRepository>();
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
            PermitLimit = 10,
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
// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }

app.UseHttpsRedirection();

Determiner determiner = new();

app.MapGet("/naughtyornice", ([FromBody] Person person) => determiner.DetermineNiceness(person))
.WithName("NaughtyOrNice")
.WithOpenApi();

app.MapGet("trysmashingurl", () =>
{
    return Results.Ok("Hello World!");
}).RequireRateLimiting("naughtyornice");

app.MapPost("/score", async (HttpContext context, [FromBody] NaughtyNiceCandidateSubmissionRequest request) =>
    {
        var teamName = context.Request.Headers["X-Team-Name"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return Results.BadRequest("X-Team-Name header is required");
        }
        
        var teamSecret = context.Request.Headers["X-Unique-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(teamSecret))
        {
            return Results.BadRequest("X-Unique-Key header is required");
        }
        
        try
        {
            var scoringService = app.Services.GetRequiredService<IScoringService>();
            var result = await scoringService.Score(teamName, teamSecret, request);
            return Results.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                detail: ex.Message
            );
        }
    })
.RequireRateLimiting("naughtyornice")
.WithName("Score")
.WithOpenApi();

app.MapGet("/leaderboard", () =>
    {
        var participantRepository = app.Services.GetRequiredService<IParticipantRepository>();
        var participants = participantRepository.GetAllParticipants();
        
        var leaderboard = participants.Select(p => new
        {
            TeamName = p.TeamName,
            Score = p.GetParticipantScore()
        })
        .OrderByDescending(x => x.Score)
        .ToList();
        
        return Results.Ok(leaderboard);
    })
.WithName("Leaderboard")
.WithMetadata(new SkipTeamValidationAttribute())
.WithOpenApi();


app.MapPost("/register", (HttpContext context, [FromBody] string teamName) =>
{
    var participantRepository = app.Services.GetRequiredService<IParticipantRepository>();

    var participantIsAlreadyRegistered = participantRepository.GetParticipantBy(teamName) != null;

    if (participantIsAlreadyRegistered)
    {
        return Results.BadRequest("Team name already exists");
    }

    var result = participantRepository.Create(teamName);
    return Results.Ok(result);
})
.WithName("Register")
.WithMetadata(new SkipTeamValidationAttribute())
.WithOpenApi();

app.Run();