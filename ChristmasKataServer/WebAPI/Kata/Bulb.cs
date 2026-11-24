namespace WebAPI.Kata;

public enum Bulb : byte { K, R, G, B, W }

public static class BulbChars
{
    public static char ToChar(this Bulb bulb) => bulb switch
    {
        Bulb.K => 'K',
        Bulb.R => 'R',
        Bulb.G => 'G',
        Bulb.B => 'B',
        Bulb.W => 'W',
        _ => throw new ArgumentOutOfRangeException(nameof(bulb)),
    };

    public static Bulb Parse(char c) => c switch
    {
        'K' => Bulb.K,
        'R' => Bulb.R,
        'G' => Bulb.G,
        'B' => Bulb.B,
        'W' => Bulb.W,
        _ => throw new FormatException($"'{c}' is not a bulb state (expected one of RGBWK)"),
    };
}
