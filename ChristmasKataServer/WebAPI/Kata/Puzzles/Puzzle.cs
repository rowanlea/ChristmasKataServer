namespace WebAPI.Kata.Puzzles;

public sealed record Puzzle(string Id, string Name, int Round, int Size, Func<Random, string> Template);

public sealed record GeneratedChallenge(string Script, Tree Answer);
