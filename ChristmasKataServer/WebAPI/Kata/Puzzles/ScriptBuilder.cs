using System.Text;

namespace WebAPI.Kata.Puzzles;

public sealed class ScriptBuilder
{
    private const string Colours = "RGBW";
    private readonly StringBuilder _text = new();
    private int _size = 16;

    public ScriptBuilder Size(int n)
    {
        _size = n;
        return Line($"SIZE {n}");
    }

    public ScriptBuilder Line(string line)
    {
        _text.Append(line).Append('\n');
        return this;
    }

    public ScriptBuilder Repeat(long count, Action<ScriptBuilder> body)
    {
        Line($"REPEAT {count}");
        body(this);
        return Line("END");
    }

    public ScriptBuilder RandomSet(Random rng, int n)
    {
        for (var i = 0; i < n; i++)
        {
            var row = rng.Next(_size);
            Line($"SET {row} {rng.Next(row + 1)} {Colours[rng.Next(Colours.Length)]}");
        }
        return this;
    }

    public override string ToString() => _text.ToString();
}
