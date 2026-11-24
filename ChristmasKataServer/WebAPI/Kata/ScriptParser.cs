namespace WebAPI.Kata;

public sealed class ScriptFormatException(int lineNumber, string message)
    : Exception($"Line {lineNumber}: {message}")
{
    public int LineNumber { get; } = lineNumber;
}

public static class ScriptParser
{
    public const int MinSize = 1;
    public const int MaxSize = 48;
    public const long MaxRepeat = 1_000_000_000;

    private sealed record OpenBlock(int LineNumber, long Count, List<Instruction> Parent);

    public static Script Parse(string text)
    {
        var lines = text.Split('\n');
        int? size = null;
        var stack = new Stack<OpenBlock>();
        var root = new List<Instruction>();
        var current = root;
        var lastLine = 1;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var line = lines[i].TrimEnd('\r');
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#')) continue;
            lastLine = lineNumber;

            var tokens = line.Split(' ');
            var op = tokens[0];

            if (size is null)
            {
                if (op != "SIZE") throw new ScriptFormatException(lineNumber, "the script must start with SIZE");
                Expect(tokens, 2, lineNumber);
                var n = ParseNumber(tokens[1], lineNumber);
                if (n < MinSize || n > MaxSize)
                    throw new ScriptFormatException(lineNumber, $"SIZE must be between {MinSize} and {MaxSize}");
                size = (int)n;
                continue;
            }

            switch (op)
            {
                case "SIZE":
                    throw new ScriptFormatException(lineNumber, "SIZE must be the first line and appear once");
                case "SET":
                    Expect(tokens, 4, lineNumber);
                    var row = ParseNumber(tokens[1], lineNumber);
                    var pos = ParseNumber(tokens[2], lineNumber);
                    if (row >= size || pos > row)
                        throw new ScriptFormatException(lineNumber, $"no bulb at row {tokens[1]}, position {tokens[2]}");
                    current.Add(new Set((int)row, (int)pos, ParseState(tokens[3], lineNumber)));
                    break;
                case "ROW":
                    Expect(tokens, 3, lineNumber);
                    var index = ParseNumber(tokens[1], lineNumber);
                    if (index >= size)
                        throw new ScriptFormatException(lineNumber, $"no row {tokens[1]}");
                    current.Add(new Row((int)index, ParseState(tokens[2], lineNumber)));
                    break;
                case "FILL":
                    Expect(tokens, 2, lineNumber);
                    current.Add(new Fill(ParseState(tokens[1], lineNumber)));
                    break;
                case "GROW":
                    Expect(tokens, 2, lineNumber);
                    current.Add(new Grow(ParseState(tokens[1], lineNumber)));
                    break;
                case "SEQUENCE":
                    Expect(tokens, 2, lineNumber);
                    current.Add(new Sequence(ParseState(tokens[1], lineNumber)));
                    break;
                case "ROTATE":
                    Expect(tokens, 1, lineNumber);
                    current.Add(new Rotate());
                    break;
                case "MIRROR":
                    Expect(tokens, 1, lineNumber);
                    current.Add(new Mirror());
                    break;
                case "CYCLE":
                    Expect(tokens, 1, lineNumber);
                    current.Add(new Cycle());
                    break;
                case "CHASE":
                    Expect(tokens, 1, lineNumber);
                    current.Add(new Chase());
                    break;
                case "FALL":
                    Expect(tokens, 1, lineNumber);
                    current.Add(new Fall());
                    break;
                case "REPEAT":
                    Expect(tokens, 2, lineNumber);
                    var count = ParseNumber(tokens[1], lineNumber, allowNegative: true);
                    if (count < 0 || count > MaxRepeat)
                        throw new ScriptFormatException(lineNumber, $"REPEAT count must be between 0 and {MaxRepeat}");
                    stack.Push(new OpenBlock(lineNumber, count, current));
                    current = [];
                    break;
                case "END":
                    Expect(tokens, 1, lineNumber);
                    if (stack.Count == 0) throw new ScriptFormatException(lineNumber, "END without a matching REPEAT");
                    var open = stack.Pop();
                    open.Parent.Add(new Repeat(open.Count, current));
                    current = open.Parent;
                    break;
                default:
                    throw new ScriptFormatException(lineNumber, $"unknown instruction '{op}'");
            }
        }

        if (size is null) throw new ScriptFormatException(lastLine, "the script must start with SIZE");
        if (stack.Count > 0) throw new ScriptFormatException(stack.Peek().LineNumber, "REPEAT is never closed with END");
        return new Script(size.Value, root);
    }

    private static void Expect(string[] tokens, int count, int lineNumber)
    {
        if (tokens.Length != count)
            throw new ScriptFormatException(lineNumber, $"{tokens[0]} takes {count - 1} argument(s)");
    }

    private static long ParseNumber(string token, int lineNumber, bool allowNegative = false)
    {
        var negative = allowNegative && token.StartsWith('-');
        var digits = negative ? token[1..] : token;
        if (digits.Length == 0 || digits.Length > 12 || !digits.All(char.IsAsciiDigit))
            throw new ScriptFormatException(lineNumber, $"'{token}' is not a valid number");
        var value = long.Parse(digits);
        return negative ? -value : value;
    }

    private static Bulb ParseState(string token, int lineNumber)
    {
        if (token.Length != 1 || "RGBWK".IndexOf(token[0]) < 0)
            throw new ScriptFormatException(lineNumber, $"'{token}' is not a state (expected one of RGBWK)");
        return BulbChars.Parse(token[0]);
    }
}
