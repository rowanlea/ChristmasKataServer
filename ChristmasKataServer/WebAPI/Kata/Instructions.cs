namespace WebAPI.Kata;

public abstract record Instruction;

public sealed record Set(int Row, int Position, Bulb State) : Instruction;

public sealed record Row(int Index, Bulb State) : Instruction;

public sealed record Fill(Bulb State) : Instruction;

public sealed record Rotate : Instruction;

public sealed record Mirror : Instruction;

public sealed record Grow(Bulb State) : Instruction;

public sealed record Cycle : Instruction;

public sealed record Sequence(Bulb State) : Instruction;

public sealed record Chase : Instruction;

public sealed record Fall : Instruction;

public sealed record Repeat(long Count, IReadOnlyList<Instruction> Body) : Instruction;

public sealed record Script(int Size, IReadOnlyList<Instruction> Body);
