using SkiaSharp;

namespace WebAPI.Scoring;

public sealed class InvalidImageException(string message) : Exception(message);

public static class PngSampler
{
    private static readonly (char State, int R, int G, int B)[] Palette =
    [
        ('R', 0xFF, 0x00, 0x00),
        ('G', 0x00, 0xFF, 0x00),
        ('B', 0x00, 0x00, 0xFF),
        ('W', 0xFF, 0xFF, 0xFF),
        ('K', 0x30, 0x30, 0x30),
        ('.', 0x00, 0x00, 0x00),
    ];

    public static string Sample(ReadOnlySpan<byte> png, int size)
    {
        using var bitmap = Decode(png);
        if (bitmap.Width != RenderLayout.Canvas || bitmap.Height != RenderLayout.Canvas)
            throw new InvalidImageException($"Image must be {RenderLayout.Canvas} x {RenderLayout.Canvas}");

        var pixels = bitmap.Pixels;
        var radius = RenderLayout.SampleRadius(size);
        var sb = new System.Text.StringBuilder(size * (size + 1) / 2);
        for (var r = 0; r < size; r++)
        for (var p = 0; p <= r; p++)
        {
            var (cx, cy) = RenderLayout.Centre(size, r, p);
            sb.Append(SnapAverage(pixels, cx, cy, radius));
        }
        return sb.ToString();
    }

    private static SKBitmap Decode(ReadOnlySpan<byte> png)
    {
        SKBitmap? bitmap = null;
        try
        {
            bitmap = png.IsEmpty ? null : SKBitmap.Decode(png);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            bitmap = null;
        }
        return bitmap ?? throw new InvalidImageException("Not a decodable PNG");
    }

    private static char SnapAverage(SKColor[] pixels, double cx, double cy, double radius)
    {
        var x0 = Math.Max((int)Math.Floor(cx - radius) - 1, 0);
        var x1 = Math.Min((int)Math.Ceiling(cx + radius) + 1, RenderLayout.Canvas - 1);
        var y0 = Math.Max((int)Math.Floor(cy - radius) - 1, 0);
        var y1 = Math.Min((int)Math.Ceiling(cy + radius) + 1, RenderLayout.Canvas - 1);
        double sr = 0, sg = 0, sbl = 0;
        var count = 0;
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
        {
            double dx = x + 0.5 - cx, dy = y + 0.5 - cy;
            if (dx * dx + dy * dy > radius * radius) continue;
            var c = pixels[y * RenderLayout.Canvas + x];
            sr += c.Red;
            sg += c.Green;
            sbl += c.Blue;
            count++;
        }
        if (count == 0) return '.';
        double ar = sr / count, ag = sg / count, ab = sbl / count;

        var best = '.';
        var bestDist = double.MaxValue;
        foreach (var (state, r, g, b) in Palette)
        {
            var d = (ar - r) * (ar - r) + (ag - g) * (ag - g) + (ab - b) * (ab - b);
            if (d < bestDist) { bestDist = d; best = state; }
        }
        return best;
    }
}
