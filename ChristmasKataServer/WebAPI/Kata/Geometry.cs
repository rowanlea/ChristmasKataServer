using System.Collections.Concurrent;

namespace WebAPI.Kata;

public static class Geometry
{
    private static readonly ConcurrentDictionary<int, IReadOnlyList<(int r, int p)>> WiringCache = new();

    public static (int r, int p) Rotate(int n, int r, int p) => (n - 1 - r + p, n - 1 - r);

    public static (int r, int p) RotateCounterClockwise(int n, int r, int p) => (n - 1 - p, r - p);

    public static (int r, int p) Mirror(int r, int p) => (r, r - p);

    public static (int r, int p) MirrorWrongAxis(int n, int r, int p) => (n - 1 - p, n - 1 - r);

    public static IEnumerable<(int r, int p)> Neighbours(int n, int r, int p)
    {
        (int r, int p)[] candidates =
        [
            (r, p - 1), (r, p + 1),
            (r - 1, p - 1), (r - 1, p),
            (r + 1, p), (r + 1, p + 1),
        ];
        foreach (var (nr, np) in candidates)
            if (nr >= 0 && nr < n && np >= 0 && np <= nr)
                yield return (nr, np);
    }

    public static IReadOnlyList<(int r, int p)> Wiring(int n) => WiringCache.GetOrAdd(n, BuildWiring);

    public static IReadOnlyList<(int r, int p)> AnswerOrder(int n) =>
        [.. Enumerable.Range(0, n).SelectMany(r => Enumerable.Range(0, r + 1).Select(p => (r, p)))];

    private static IReadOnlyList<(int r, int p)> BuildWiring(int n)
    {
        var order = new List<(int r, int p)>(n * (n + 1) / 2) { (0, 0) };
        for (var r = 1; r < n; r++)
        {
            if (r % 2 == 1)
                for (var p = 0; p <= r; p++) order.Add((r, p));
            else
                for (var p = r; p >= 0; p--) order.Add((r, p));
        }
        return order;
    }
}
