namespace WebAPI.Kata;

[Flags]
public enum Quirks
{
    None = 0,
    CounterClockwiseRotate = 1,
    MirrorWrongAxis = 2,
    AnswerOrderWiring = 4,
}

public sealed record EmulatorOptions(
    Quirks Quirks = Quirks.None,
    bool DetectCycles = true,
    long? InstructionBudget = null);

public sealed class InstructionBudgetExceededException(long budget)
    : Exception($"Run exceeded the budget of {budget} steps");

public sealed record LoopStats(long Count, int? PrePeriod, int? Period);

public sealed record RunResult(Tree Final, long InstructionsExecuted, IReadOnlyList<LoopStats> Loops);

public sealed class Emulator(EmulatorOptions? options = null)
{
    private readonly EmulatorOptions _options = options ?? new EmulatorOptions();

    public RunResult Run(Script script, Tree? start = null)
    {
        var run = new Execution(script.Size, _options, start?.Clone());
        run.Execute(script.Body);
        return new RunResult(run.Tree, run.Executed, run.Loops);
    }

    private sealed class Execution(int size, EmulatorOptions options, Tree? start)
    {
        private readonly HashSet<Repeat> _seen = new(ReferenceEqualityComparer.Instance);

        private readonly Dictionary<Delegate, int[]> _permutations = [];
        private int[]? _chase;

        private readonly IReadOnlyList<(int r, int p)> _wiring = options.Quirks.HasFlag(Quirks.AnswerOrderWiring)
            ? Geometry.AnswerOrder(size)
            : Geometry.Wiring(size);

        public Tree Tree { get; private set; } = start ?? new(size);
        public long Executed { get; private set; }
        private long _work;
        public List<LoopStats> Loops { get; } = [];

        public void Execute(IReadOnlyList<Instruction> body)
        {
            foreach (var instruction in body) Execute(instruction);
        }

        private void ExecuteRepeat(Repeat repeat)
        {
            var slot = -1;
            if (_seen.Add(repeat))
            {
                slot = Loops.Count;
                Loops.Add(new LoopStats(repeat.Count, null, null));
            }

            if (options.DetectCycles && repeat.Count > 0 && TryRepeatBijection(repeat, slot)) return;

            var detecting = options.DetectCycles;
            var visited = new Dictionary<string, long>();

            for (long i = 0; i < repeat.Count; i++)
            {
                if (detecting)
                {
                    var state = Tree.ToAnswerLine();
                    if (visited.TryGetValue(state, out var first))
                    {
                        var period = i - first;
                        if (slot >= 0) Loops[slot] = Loops[slot] with { PrePeriod = (int)first, Period = (int)period };
                        i += (repeat.Count - i) / period * period;
                        detecting = false;
                        if (i >= repeat.Count) break;
                    }
                    else visited[state] = i;
                }

                Spend();
                Execute(repeat.Body);
            }
        }

        private void Execute(Instruction instruction)
        {
            if (instruction is Repeat repeat)
            {
                ExecuteRepeat(repeat);
                return;
            }

            Executed++;
            Spend();
            switch (instruction)
            {
                case Set set: Tree[set.Row, set.Position] = set.State; break;
                case Row row:
                    for (var p = 0; p <= row.Index; p++) Tree[row.Index, p] = row.State;
                    break;
                case Fill fill: ForEachBulb((r, p) => Tree[r, p] = fill.State); break;
                case Rotate: Transform(RotateMap); break;
                case Mirror: Transform(MirrorMap); break;
                case Grow grow: ApplyGrow(grow.State); break;
                case Cycle: ApplyCycle(); break;
                case Sequence sequence: ApplySequence(sequence.State); break;
                case Chase: ApplyChase(); break;
                case Fall: ApplyFall(); break;
                default:
                    throw new NotSupportedException($"{instruction.GetType().Name} is not supported yet");
            }
        }

        private void Spend()
        {
            if (options.InstructionBudget is { } budget && ++_work > budget)
                throw new InstructionBudgetExceededException(budget);
        }

        private void ForEachBulb(Action<int, int> action)
        {
            for (var r = 0; r < size; r++)
                for (var p = 0; p <= r; p++) action(r, p);
        }

        private void Transform(Func<int, int, int, (int r, int p)> map) => Tree = Tree.Permute(Destination(map));

        private int[] Destination(Func<int, int, int, (int r, int p)> map)
        {
            if (!_permutations.TryGetValue(map, out var destination))
            {
                destination = new int[size * (size + 1) / 2];
                ForEachBulb((r, p) =>
                {
                    var (nr, np) = map(size, r, p);
                    destination[Tree.Index(r, p)] = Tree.Index(nr, np);
                });
                _permutations[map] = destination;
            }
            return destination;
        }

        private Func<int, int, int, (int r, int p)> RotateMap => options.Quirks.HasFlag(Quirks.CounterClockwiseRotate)
            ? Geometry.RotateCounterClockwise
            : Geometry.Rotate;

        private Func<int, int, int, (int r, int p)> MirrorMap => options.Quirks.HasFlag(Quirks.MirrorWrongAxis)
            ? Geometry.MirrorWrongAxis
            : (_, r, p) => Geometry.Mirror(r, p);

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

        private void ApplyChase() => Tree = Tree.Permute(ChaseDestination());

        private int[] ChaseDestination()
        {
            if (_chase is null)
            {
                _chase = new int[_wiring.Count];
                for (var i = 0; i < _wiring.Count; i++)
                {
                    var (r, p) = _wiring[i];
                    var (nr, np) = _wiring[(i + 1) % _wiring.Count];
                    _chase[Tree.Index(r, p)] = Tree.Index(nr, np);
                }
            }
            return _chase;
        }

        /// <summary>
        /// A body of only ROTATE, MIRROR, CHASE and CYCLE moves every bulb along one fixed permutation and shifts its
        /// colour by a fixed amount per pass, so any number of passes is applied through the permutation's cycles
        /// instead of one pass at a time. Returns false for any other body.
        /// </summary>
        private bool TryRepeatBijection(Repeat repeat, int slot)
        {
            if (repeat.Body.Count == 0) return false;
            var bulbs = size * (size + 1) / 2;
            var destination = Enumerable.Range(0, bulbs).ToArray();
            var shift = 0;
            foreach (var instruction in repeat.Body)
            {
                int[] step;
                switch (instruction)
                {
                    case Rotate: step = Destination(RotateMap); break;
                    case Mirror: step = Destination(MirrorMap); break;
                    case Chase: step = ChaseDestination(); break;
                    case Cycle: shift++; continue;
                    default: return false;
                }
                for (var i = 0; i < bulbs; i++) destination[i] = step[destination[i]];
            }

            var before = Tree;
            var after = new Tree(size);
            var visited = new bool[bulbs];
            long period = 1;
            var periodKnown = true;
            for (var start = 0; start < bulbs; start++)
            {
                if (visited[start]) continue;
                var cycle = new List<int>();
                for (var i = start; !visited[i]; i = destination[i])
                {
                    visited[i] = true;
                    cycle.Add(i);
                }
                var moved = (int)(repeat.Count % cycle.Count);
                var recoloured = (int)(repeat.Count % 4 * shift % 4);
                for (var j = 0; j < cycle.Count; j++)
                    after.SetByIndex(cycle[(j + moved) % cycle.Count], Recolour(before.GetByIndex(cycle[j]), recoloured));

                if (!periodKnown) continue;
                period = Lcm(period, CyclePeriod(before, cycle, shift));
                if (period >= repeat.Count) periodKnown = false;
            }
            Tree = after;
            if (slot >= 0 && periodKnown) Loops[slot] = Loops[slot] with { PrePeriod = 0, Period = (int)period };
            return true;
        }

        private static int CyclePeriod(Tree tree, List<int> cycle, int shift)
        {
            var length = cycle.Count;
            for (var candidate = 1; candidate < 4 * length; candidate++)
            {
                if (4 * length % candidate != 0) continue;
                var moved = candidate % length;
                var recoloured = candidate % 4 * shift % 4;
                var holds = true;
                for (var j = 0; j < length && holds; j++)
                    holds = Recolour(tree.GetByIndex(cycle[j]), recoloured) == tree.GetByIndex(cycle[(j + moved) % length]);
                if (holds) return candidate;
            }
            return 4 * length;
        }

        private static readonly Bulb[] ColourOrder = [Bulb.R, Bulb.G, Bulb.B, Bulb.W];

        private static Bulb Recolour(Bulb bulb, int steps) =>
            bulb == Bulb.K ? Bulb.K : ColourOrder[(Array.IndexOf(ColourOrder, bulb) + steps) % 4];

        private static long Lcm(long a, long b)
        {
            var (x, y) = (a, b);
            while (y != 0) (x, y) = (y, x % y);
            return a / x * b;
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
