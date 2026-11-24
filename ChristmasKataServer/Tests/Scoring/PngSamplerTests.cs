using WebAPI.Scoring;

namespace Tests.Scoring;

public class PngSamplerTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "render", name);

    private static byte[] Png(string name) => File.ReadAllBytes(FixturePath(name + ".png"));

    private static string Line(string name) => File.ReadAllText(FixturePath(name + ".txt")).TrimEnd();

    [TestCase("tree-3-mixed", 3)]
    [TestCase("tree-16-cycle", 16)]
    [TestCase("tree-48-cycle", 48)]
    public void Samples_harness_fixture_to_its_line(string name, int size)
    {
        Assert.That(PngSampler.Sample(Png(name), size), Is.EqualTo(Line(name)));
    }

    [Test]
    public void Blank_render_samples_to_background_only()
    {
        Assert.That(PngSampler.Sample(Png("blank-16"), 16), Is.EqualTo(new string('.', 136)));
    }

    [Test]
    public void Random_bytes_are_rejected()
    {
        var bytes = new byte[2000];
        new Random(1).NextBytes(bytes);
        Assert.Throws<InvalidImageException>(() => PngSampler.Sample(bytes, 16));
    }

    [Test]
    public void Empty_input_is_rejected()
    {
        Assert.Throws<InvalidImageException>(() => PngSampler.Sample([], 16));
    }

    [Test]
    public void Wrongly_sized_png_is_rejected()
    {
        using var bitmap = new SkiaSharp.SKBitmap(100, 100);
        using var data = SkiaSharp.SKImage.FromBitmap(bitmap).Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var png = data.ToArray();
        Assert.Throws<InvalidImageException>(() => PngSampler.Sample(png, 16));
    }

    [Test]
    public void Header_declaring_huge_dimensions_is_rejected_without_decoding()
    {
        var png = Png("tree-16-cycle");
        WriteBigEndian(png, 16, 100000);
        WriteBigEndian(png, 20, 100000);
        var crc = Crc32(png.AsSpan(12, 17));
        WriteBigEndian(png, 29, crc);

        var started = System.Diagnostics.Stopwatch.StartNew();
        Assert.Throws<InvalidImageException>(() => PngSampler.Sample(png, 16));
        Assert.That(started.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public void Png_truncated_mid_data_is_rejected()
    {
        var png = Png("tree-16-cycle");
        var truncated = png.AsSpan(0, png.Length / 2).ToArray();
        Assert.Throws<InvalidImageException>(() => PngSampler.Sample(truncated, 16));
    }

    [Test]
    public void Imperfect_colours_snap_to_nearest_palette_entry()
    {
        using var bitmap = new SkiaSharp.SKBitmap(1200, 1200);
        bitmap.Erase(new SkiaSharp.SKColor(0x05, 0x05, 0x05));
        var colours = new[]
        {
            new SkiaSharp.SKColor(0xF0, 0x10, 0x10),
            new SkiaSharp.SKColor(0x2A, 0x2A, 0x2A),
            new SkiaSharp.SKColor(0x05, 0x05, 0x05),
        };
        for (var r = 0; r < 3; r++)
        for (var p = 0; p <= r; p++)
        {
            var (cx, cy) = RenderLayout.Centre(3, r, p);
            var colour = colours[(r + p) % 3];
            for (var y = (int)cy - 80; y <= (int)cy + 80; y++)
            for (var x = (int)cx - 80; x <= (int)cx + 80; x++)
                bitmap.SetPixel(x, y, colour);
        }
        using var data = SkiaSharp.SKImage.FromBitmap(bitmap).Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);

        var expected = string.Concat(Enumerable.Range(0, 3).SelectMany(r =>
            Enumerable.Range(0, r + 1).Select(p => "RK."[(r + p) % 3])));
        Assert.That(PngSampler.Sample(data.ToArray(), 3), Is.EqualTo(expected));
    }

    private static void WriteBigEndian(byte[] buffer, int offset, uint value)
    {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset), value);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value) => WriteBigEndian(buffer, offset, (uint)value);

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }
}
