namespace WebAPI.Kata;

[Flags]
public enum Quirks
{
    None = 0,
    CounterClockwiseRotate = 1,
    MirrorWrongAxis = 2,
    AnswerOrderWiring = 4,
}

public sealed record EmulatorOptions(Quirks Quirks = Quirks.None, bool DetectCycles = true);

public sealed record LoopStats(long Count, int? PrePeriod, int? Period);

public sealed record RunResult(Tree Final, long InstructionsExecuted, IReadOnlyList<LoopStats> Loops);

public sealed class Emulator(EmulatorOptions? options = null)
{
    private readonly EmulatorOptions _options = options ?? new EmulatorOptions();

    public RunResult Run(Script script)
    {
        var run = new Execution(script.Size, _options);
        run.Execute(script.Body);
        return new RunResult(run.Tree, run.Executed, run.Loops);
    }

    private sealed class Execution(int size, EmulatorOptions options)
    {
        private readonly HashSet<Repeat> _seen = new(ReferenceEqualityComparer.Instance);

        private readonly IReadOnlyList<(int r, int p)> _wiring = options.Quirks.HasFlag(Quirks.AnswerOrderWiring)
            ? Geometry.AnswerOrder(size)
            : Geometry.Wiring(size);

        public Tree Tree { get; private set; } = new(size);
        public long Executed { get; private set; }
        public List<LoopStats> Loops { get; } = [];

        public void Execute(IReadOnlyList<Instruction> body)
        {
            foreach (var instruction in body) Execute(instruction);
        }

        private void Execute(Instruction instruction)
        {
            if (instruction is Repeat repeat)
            {
                if (_seen.Add(repeat)) Loops.Add(new LoopStats(repeat.Count, null, null));
                for (long i = 0; i < repeat.Count; i++) Execute(repeat.Body);
                return;
            }

            Executed++;
            switch (instruction)
            {
                case Set set: Tree[set.Row, set.Position] = set.State; break;
                case Row row:
                    for (var p = 0; p <= row.Index; p++) Tree[row.Index, p] = row.State;
                    break;
                case Fill fill: ForEachBulb((r, p) => Tree[r, p] = fill.State); break;
                case Rotate:
                    Transform(options.Quirks.HasFlag(Quirks.CounterClockwiseRotate)
                        ? Geometry.RotateCounterClockwise
                        : Geometry.Rotate);
                    break;
                case Mirror:
                    Transform(options.Quirks.HasFlag(Quirks.MirrorWrongAxis)
                        ? Geometry.MirrorWrongAxis
                        : (_, r, p) => Geometry.Mirror(r, p));
                    break;
                case Grow grow: ApplyGrow(grow.State); break;
                case Cycle: ApplyCycle(); break;
                case Sequence sequence: ApplySequence(sequence.State); break;
                case Chase: ApplyChase(); break;
                case Fall: ApplyFall(); break;
                default:
                    throw new NotSupportedException($"{instruction.GetType().Name} is not supported yet");
            }
        }

        private void ForEachBulb(Action<int, int> action)
        {
            for (var r = 0; r < size; r++)
                for (var p = 0; p <= r; p++) action(r, p);
        }

        private void Transform(Func<int, int, int, (int r, int p)> map)
        {
            var next = new Tree(size);
            ForEachBulb((r, p) =>
            {
                var (nr, np) = map(size, r, p);
                next[nr, np] = Tree[r, p];
            });
            Tree = next;
        }

        private void ApplyGrow(Bulb state)
        {
            var before = Tree.Clone();
            ForEachBulb((r, p) =>
            {
                if (before[r, p] != Bulb.K) return;
                foreach (var (nr, np) in Geometry.Neighbours(size, r, p))
                {
                    if (before[nr, np] != state) continue;
                    Tree[r, p] = state;
                    return;
                }
            });
        }

        private void ApplySequence(Bulb state)
        {
            foreach (var (r, p) in _wiring)
            {
                if (Tree[r, p] != Bulb.K) continue;
                Tree[r, p] = state;
                return;
            }
        }

        private void ApplyChase()
        {
            var next = new Tree(size);
            for (var i = 0; i < _wiring.Count; i++)
            {
                var (r, p) = _wiring[i];
                var (nr, np) = _wiring[(i + 1) % _wiring.Count];
                next[nr, np] = Tree[r, p];
            }
            Tree = next;
        }

        private void ApplyFall()
        {
            var next = new Tree(size);
            ForEachBulb((r, p) =>
            {
                if (r + 2 < size) next[r + 2, p + 1] = Tree[r, p];
            });
            Tree = next;
        }

        private void ApplyCycle() => ForEachBulb((r, p) =>
        {
            Tree[r, p] = Tree[r, p] switch
            {
                Bulb.R => Bulb.G,
                Bulb.G => Bulb.B,
                Bulb.B => Bulb.W,
                Bulb.W => Bulb.R,
                _ => Bulb.K,
            };
        });
    }
}
