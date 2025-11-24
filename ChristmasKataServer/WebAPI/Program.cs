using System;
using WebAPI;
using WebAPI.Model;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.MapGet("/score", (List<string = ID, bool = Nice> naughtyNiceList) =>
{
    return false;
})
.WithName("Score")
.WithOpenApi();

app.Run();