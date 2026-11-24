using WebAPI.Kata;
using WebAPI.Kata.Puzzles;

namespace Tests.Kata.Puzzles;

public class CatalogueTests
{
    [Test]
    public void Catalogue_has_23_puzzles_in_7_rounds()
    {
        string[] ids =
        [
            "1.1", "1.2", "1.3", "1.4", "2.1", "2.2", "2.3", "2.4", "3.1", "3.2", "3.3", "3.4",
            "4.1", "4.2", "4.3", "4.4", "5.1", "5.2", "5.3", "6.1", "6.2", "6.3", "7.1",
        ];
        string[] names =
        [
            "Paint by Numbers", "Fill Your Boots", "Turn of Events", "Looking Glass",
            "Turn, Then Reflect", "Second Thoughts", "Round in Circles", "Three Sides to Every Story",
            "Growing Pains", "Turf War", "Cornered", "Change of Heart",
            "Switched On", "Chasing Tail", "Falling Star", "Light Show",
            "The Long List", "Wheels Within Wheels", "Nothing Doing",
            "Round the Clock", "False Start", "Deep Sleep", "Secret Santa",
        ];

        Assert.That(PuzzleCatalogue.All.Select(p => p.Id), Is.EqualTo(ids));
        Assert.That(PuzzleCatalogue.All.Select(p => p.Name), Is.EqualTo(names));
        Assert.That(PuzzleCatalogue.All.Select(p => p.Round).Distinct(), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7 }));
        Assert.That(PuzzleCatalogue.All.All(p => p.Size == (p.Id == "7.1" ? 48 : 16)));
        Assert.That(PuzzleCatalogue.InRound(5).Select(p => p.Id), Is.EqualTo(new[] { "5.1", "5.2", "5.3" }));
        Assert.That(PuzzleCatalogue.Find("9.9"), Is.Null);
    }

    [Test]
    public void Previous_and_next_follow_catalogue_order()
    {
        Puzzle P(string id) => PuzzleCatalogue.Find(id)!;

        Assert.That(PuzzleCatalogue.Previous(P("1.1")), Is.Null);
        Assert.That(PuzzleCatalogue.Previous(P("2.1"))!.Id, Is.EqualTo("1.4"));
        Assert.That(PuzzleCatalogue.Next(P("1.4"))!.Id, Is.EqualTo("2.1"));
        Assert.That(PuzzleCatalogue.Next(P("6.3"))!.Id, Is.EqualTo("7.1"));
        Assert.That(PuzzleCatalogue.Next(P("7.1")), Is.Null);
    }

    [Test]
    public void Templates_are_placeholders_until_written()
    {
        Assert.Throws<NotImplementedException>(() => PuzzleCatalogue.All[0].Template(new Random(1)));
    }

    [Test]
    public void Generator_gives_up_after_50_attempts()
    {
        var calls = 0;
        var puzzle = new Puzzle("1.1", "x", 1, 16, _ =>
        {
            calls++;
            return "SIZE 16\n";
        });

        Assert.Throws<InvalidOperationException>(() => new ChallengeGenerator().Generate(puzzle, new Random(1)));
        Assert.That(calls, Is.EqualTo(50));
    }

    [Test]
    public void Generator_returns_the_answer_of_an_accepted_script()
    {
        var puzzle = new Puzzle("1.1", "x", 1, 16, _ => "SIZE 16\nSET 3 2 R\n");

        var challenge = new ChallengeGenerator().Generate(puzzle, new Random(1));

        Assert.That(challenge.Answer[3, 2], Is.EqualTo(Bulb.R));
    }

    [Test]
    public void Script_builder_writes_lines_and_nested_repeats()
    {
        var text = new ScriptBuilder().Size(16).Line("FILL R").Repeat(3, b => b.Line("CYCLE")).ToString();

        Assert.That(text, Is.EqualTo("SIZE 16\nFILL R\nREPEAT 3\nCYCLE\nEND\n"));
        Assert.That(ScriptParser.Parse(new ScriptBuilder().Size(16).RandomSet(new Random(4), 30).ToString()).Body,
            Has.Count.EqualTo(30));
    }
}
