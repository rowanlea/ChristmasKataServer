using System.Text;
using System.Text.Json;
using WebAPI.API;
using WebAPI.Kata.Puzzles;

namespace Tests.Service;

public class ShowFrameTests
{
    [Test]
    public void Shows_use_the_secret_santa_grid()
    {
        var santa = PuzzleCatalogue.Find("7.1")!;

        Assert.That(ShowEndpoints.FrameRows, Is.EqualTo(santa.Size));
        Assert.That(ShowEndpoints.FrameRows, Is.EqualTo(48));
        Assert.That(ShowEndpoints.FrameLength, Is.EqualTo(1176));
    }

    [TestCase('R')]
    [TestCase('G')]
    [TestCase('B')]
    [TestCase('W')]
    [TestCase('K')]
    public void A_full_frame_of_one_state_is_valid(char state) =>
        Assert.That(ShowEndpoints.IsFrame(new string(state, 1176)), Is.True);

    [TestCase(0)]
    [TestCase(136)]
    [TestCase(1175)]
    [TestCase(1177)]
    public void A_frame_of_the_wrong_length_is_invalid(int length) =>
        Assert.That(ShowEndpoints.IsFrame(new string('R', length)), Is.False);

    [TestCase('X')]
    [TestCase('r')]
    [TestCase('.')]
    [TestCase(' ')]
    public void A_frame_with_any_other_character_is_invalid(char bad) =>
        Assert.That(ShowEndpoints.IsFrame(new string('R', 1175) + bad), Is.False);

    [Test]
    public void A_null_frame_is_invalid() =>
        Assert.That(ShowEndpoints.IsFrame(null), Is.False);

    [Test]
    public void The_largest_valid_show_fits_the_body_limit()
    {
        var frames = Enumerable.Repeat(new string('W', ShowEndpoints.FrameLength), ShowEndpoints.MaxFrames);

        var bytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(new { frames }));

        Assert.That(bytes, Is.LessThan(ShowEndpoints.MaxBodyBytes));
    }
}
