using WebAPI.Scoring;

namespace Tests.Scoring;

public class PointsTests
{
    [TestCase(136, 100)]
    [TestCase(135, 78)]
    [TestCase(109, 40)]
    [TestCase(108, 0)]
    [TestCase(0, 0)]
    public void Points_on_136_bulbs(int correct, int expected) =>
        Assert.That(Points.For(correct, 136), Is.EqualTo(expected));

    [Test]
    public void Points_on_1176_bulbs() =>
        Assert.That(Points.For(1175, 1176), Is.EqualTo(78));

    [TestCase(109, true)]
    [TestCase(108, false)]
    public void Passes_at_eighty_percent(int correct, bool expected) =>
        Assert.That(Points.Passes(correct, 136), Is.EqualTo(expected));
}
