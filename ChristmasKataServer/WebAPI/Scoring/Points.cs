namespace WebAPI.Scoring;

public static class Points
{
    public static int For(int correct, int bulbCount)
    {
        if (bulbCount <= 0 || correct <= 0) return 0;
        if (correct >= bulbCount) return 100;
        var pct = 100 * correct / bulbCount;
        return pct >= 80 ? 40 + 2 * (pct - 80) : 0;
    }

    public static bool Passes(int correct, int bulbCount) =>
        bulbCount > 0 && correct * 100 >= 80 * bulbCount;
}
