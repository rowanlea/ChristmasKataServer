using System;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using WebAPI;
using WebAPI.Model;
using WebAPI.Requests;
using WebAPI.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransient<IScoringService, ScoringService>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRateLimiter(limiter =>
{
    limiter.AddPolicy("naughtyornice", context =>
    {
        var teamName = context.Items["teamName"] as string;
        var teamSecret = context.Items["teamSecret"] as string;
        var key = $"{teamName}:${teamSecret}";
        
        //TODO validate the tema name exists etc
        

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        });
    });
});



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

Determiner determiner = new();

app.MapGet("/naughtyornice", (Person person) =>
{
    return determiner.DetermineNiceness(person);
})
.WithName("NaughtyOrNice")
.WithOpenApi();

//TODO validate tokens on each request
app.MapPost("/score", async (NaughtyNiceCandidateSubmissionRequest request) =>
    {
        
        var scoringService = app.Services.GetRequiredService<IScoringService>();
        return await scoringService.Score(request);
    })
    .RequireRateLimiting("naughtyornice")
.WithName("Score")
.WithOpenApi();

app.Run();