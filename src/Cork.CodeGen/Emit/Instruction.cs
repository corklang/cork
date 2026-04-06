namespace Cork.CodeGen.Emit;

/// <summary>
/// 6502 addressing modes relevant to the Cork compiler's instruction set.
/// </summary>
public enum AddressMode : byte
{
    Implied,        // e.g., RTS, PHA, TAX, CLC        (1 byte)
    Immediate,      // e.g., LDA #$42                   (2 bytes)
    ZeroPage,       // e.g., LDA $02                    (2 bytes)
    ZeroPageX,      // e.g., LDA $02,X                  (2 bytes)
    Absolute,       // e.g., LDA $D020                  (3 bytes)
    AbsoluteX,      // e.g., LDA $D020,X                (3 bytes)
    AbsoluteY,      // e.g., LDA $D020,Y                (3 bytes)
    IndirectY,      // e.g., LDA ($FB),Y                (2 bytes)
    Relative,       // e.g., BNE offset                 (2 bytes)
    Accumulator,    // e.g., ASL A, LSR A               (1 byte)
}

/// <summary>
/// A single 6502 instruction in the IR. Immutable value type.
/// Operand interpretation depends on AddressMode:
///   Implied/Accumulator: operand unused (0)
///   Immediate/ZeroPage/ZeroPageX/IndirectY/Relative: low byte of operand
///   Absolute/AbsoluteX/AbsoluteY: full 16-bit operand
/// </summary>
public readonly record struct Instruction(byte Opcode, AddressMode Mode, ushort Operand)
{
    /// <summary>Size in bytes when encoded.</summary>
    public int Size => Mode switch
    {
        AddressMode.Implied or AddressMode.Accumulator => 1,
        AddressMode.Absolute or AddressMode.AbsoluteX or AddressMode.AbsoluteY => 3,
        _ => 2
    };

    public bool IsLda => Opcode is 0xA9 or 0xA5 or 0xAD or 0xBD or 0xB9 or 0xB1 or 0xB5;
    public bool IsSta => Opcode is 0x85 or 0x8D or 0x9D or 0x91 or 0x95;
    public bool IsLoad => IsLda || Opcode is 0xA2 or 0xA6 or 0xA0 or 0xA4;
    public bool IsStore => IsSta || Opcode is 0x86 or 0x84;
    public bool IsBranch => Opcode is 0xD0 or 0xF0 or 0x90 or 0xB0 or 0x30 or 0x10;
    public bool IsJump => Opcode is 0x4C or 0x20;
    public bool IsTransfer => Opcode is 0xAA or 0xA8 or 0x8A or 0x98;
    public bool IsPush => Opcode is 0x48 or 0x08;
    public bool IsPull => Opcode is 0x68 or 0x28;
}

/// <summary>
/// Entry kinds in the instruction stream.
/// </summary>
public enum StreamEntryKind : byte
{
    Instruction,
    Label,
    Data,
    /// <summary>
    /// A relative branch to a named label. Resolved to a 2-byte branch instruction
    /// (or 2+3 byte inverted-branch-over-JMP if out of range) during fixup resolution.
    /// </summary>
    BranchToLabel,
}

/// <summary>
/// An entry in the instruction stream: a typed instruction, label marker, or raw data blob.
/// Each entry has a unique Id for stable reference across optimizations.
/// </summary>
public readonly struct StreamEntry
{
    public int Id { get; private init; }
    public StreamEntryKind Kind { get; private init; }
    public Instruction Instruction { get; private init; }
    public string? LabelName { get; private init; }
    public byte[]? RawData { get; private init; }
    /// <summary>For BranchToLabel: the branch opcode (BNE, BEQ, etc.).</summary>
    public byte BranchOpcode { get; private init; }
    /// <summary>For BranchToLabel: the target label name.</summary>
    public string? BranchTarget { get; private init; }

    /// <summary>Size in bytes when encoded.</summary>
    public int Size => Kind switch
    {
        StreamEntryKind.Instruction => Instruction.Size,
        StreamEntryKind.Label => 0,
        StreamEntryKind.Data => RawData?.Length ?? 0,
        StreamEntryKind.BranchToLabel => 2, // branch opcode + offset (may expand during resolution)
        _ => 0
    };

    public static StreamEntry Instr(int id, byte opcode, AddressMode mode, ushort operand = 0)
        => new() { Id = id, Kind = StreamEntryKind.Instruction, Instruction = new(opcode, mode, operand) };

    public static StreamEntry LabelDef(int id, string name)
        => new() { Id = id, Kind = StreamEntryKind.Label, LabelName = name };

    public static StreamEntry DataBlob(int id, byte[] data)
        => new() { Id = id, Kind = StreamEntryKind.Data, RawData = data };

    /// <summary>
    /// A relative branch to a named label. During finalization, resolved to a 2-byte branch
    /// or expanded to inverted-branch(2) + JMP(3) = 5 bytes if out of range.
    /// </summary>
    public static StreamEntry Branch(int id, byte opcode, string targetLabel)
        => new() { Id = id, Kind = StreamEntryKind.BranchToLabel, BranchOpcode = opcode, BranchTarget = targetLabel };
}
