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
}
