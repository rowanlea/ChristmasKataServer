using WebAPI.Kata;

// Reference solver for the smoke test: reads a challenge script on stdin and prints the
// answer line using the server's own emulator. An optional argument flips that many bulbs,
// so the smoke test can submit a partly wrong answer. 7919 is coprime with every
// bulb count below 7919 (it is prime), so each flip lands on a different bulb.
var script = ScriptParser.Parse(Console.In.ReadToEnd());
var line = new Emulator().Run(script).Final.ToAnswerLine().ToCharArray();
var wrong = args.Length > 0 ? int.Parse(args[0]) : 0;
for (var i = 0; i < wrong; i++)
{
    var j = (i * 7919 + 3) % line.Length;
    line[j] = line[j] == 'K' ? 'W' : 'K';
}
Console.WriteLine(new string(line));
