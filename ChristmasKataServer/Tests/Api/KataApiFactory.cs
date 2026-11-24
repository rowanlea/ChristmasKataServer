using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WebAPI.Kata;
using WebAPI.Kata.Puzzles;
using WebAPI.Service;

namespace Tests.Api;

public class KataApiFactory : WebApplicationFactory<Program>
{
    public const string AdminKey = "test-admin-key";

    public static readonly DateTimeOffset FreezesAt = new(2026, 12, 10, 20, 45, 0, TimeSpan.Zero);

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 12, 10, 18, 0, 0, TimeSpan.Zero));

    public SwitchableGenerator Generator { get; } = new();

    public SpyRepository Spy => Services.GetRequiredService<SpyRepository>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "InMemory",
                ["Event:FreezesAt"] = FreezesAt.ToString("o"),
                ["Event:AdminKey"] = AdminKey,
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
            services.RemoveAll<IParticipantRepository>();
            services.AddSingleton<ParticipantRepository>();
            services.AddSingleton(sp => new SpyRepository(sp.GetRequiredService<ParticipantRepository>()));
            services.AddSingleton<IParticipantRepository>(sp => sp.GetRequiredService<SpyRepository>());
            services.RemoveAll<IChallengeGenerator>();
            services.AddSingleton<IChallengeGenerator>(Generator);
        });
    }

    public async Task<HttpClient> RegisterTeam(string name)
    {
        var client = CreateClient();
        var secret = await client.GetFromJsonAsync<string>($"/register/{name}");
        client.DefaultRequestHeaders.Add("X-Team-Name", name);
        client.DefaultRequestHeaders.Add("X-Unique-Key", secret);
        return client;
    }

    public async Task UnlockThrough(string team, string puzzleId)
    {
        var repository = Services.GetRequiredService<IParticipantRepository>();
        foreach (var puzzle in PuzzleCatalogue.All)
        {
            await repository.RecordScore(team, puzzle, puzzle.Size * (puzzle.Size + 1) / 2, false);
            if (puzzle.Id == puzzleId) return;
        }
    }
}

public sealed class SwitchableGenerator : IChallengeGenerator
{
    private readonly ChallengeGenerator _real = new();

    public IChallengeGenerator? Override { get; set; }

    public GeneratedChallenge Generate(Puzzle puzzle, Random rng) =>
        (Override ?? _real).Generate(puzzle, rng);
}

public sealed class FixedAnswerGenerator(string answerLine, int size) : IChallengeGenerator
{
    public GeneratedChallenge Generate(Puzzle puzzle, Random rng) =>
        new($"SIZE {size}\n", Tree.FromAnswerLine(answerLine, size));
}
