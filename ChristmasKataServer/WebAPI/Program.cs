using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using WebAPI.API;
using WebAPI.Database;
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
    builder.Services.AddSingleton<IParticipantRepository, CosmosParticipantRepository>();
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

app.UseMiddleware<AuthenticationMiddleware>();

app.UseHttpsRedirection();

app.MapGet("/leaderboard", async (IParticipantRepository participantRepository) =>
    {
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
.WithMetadata(new SkipTeamValidationAttribute());

app.MapGet("/register/{teamName}", async (IParticipantRepository participantRepository, [FromRoute] string teamName) =>
{
    if (!TeamNames.IsValid(teamName))
    {
        return Results.BadRequest(new { error = "Team name must be 1-24 characters: letters, digits and hyphens, starting with a letter or digit" });
    }

    var participantIsAlreadyRegistered = await participantRepository.GetParticipantBy(teamName) != null;

    if (participantIsAlreadyRegistered)
    {
        return Results.BadRequest(new { error = "Team name already exists" });
    }

    var result = await participantRepository.Create(teamName);
    return Results.Ok(result);
})
.WithName("Register")
.WithMetadata(new SkipTeamValidationAttribute());

app.Run();

public partial class Program;

public static partial class TeamNames
{
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9-]{0,23}\z")]
    private static partial Regex Pattern();

    public static bool IsValid(string name) => Pattern().IsMatch(name);
}
