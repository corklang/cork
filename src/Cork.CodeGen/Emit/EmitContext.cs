namespace Cork.CodeGen.Emit;

/// <summary>
/// Shared context object holding all state needed during code emission.
/// Passed to each sub-emitter so they can access the buffer, symbol table,
/// data addresses, and other emitters.
/// </summary>
public sealed class EmitContext
{
    public AssemblyBuffer Buffer { get; }
    public SymbolTable Symbols { get; }
    public HashSet<string> Runtime { get; } = [];
    public Stack<(string BreakLabel, string ContinueLabel)> LoopStack { get; } = [];
    public Dictionary<string, ushort> DataAddresses { get; }

    private int _labelCounter;

    // ZP constants for runtime routines
    public const byte ZpPointerLo = 0xFB;
    public const byte ZpPointerHi = 0xFC;
    public const byte ZpMulA = 0xF0;
    public const byte ZpMulB = 0xF1;
    public const byte ZpMulResultLo = 0xF2;
    public const byte ZpMulResultHi = 0xF3;
    public const byte ZpFixedArg1Lo = 0xF4;
    public const byte ZpFixedArg1Hi = 0xF5;
    public const byte ZpFixedArg2Lo = 0xF6;
    public const byte ZpFixedArg2Hi = 0xF7;
    public const byte ZpFixedResB0 = 0xF8;
    public const byte ZpFixedResB1 = 0xF9;
    public const byte ZpFixedResB2 = 0xFA;
    public const byte ZpSignFlag = 0xEF;

    // Sub-emitters (set after construction to break circular dependencies)
    public ExpressionEmitter Expressions { get; set; } = null!;
    public StatementEmitter Statements { get; set; } = null!;
    public ControlFlowEmitter ControlFlow { get; set; } = null!;
    public SceneEmitter Scenes { get; set; } = null!;
    public IntrinsicEmitter Intrinsics { get; set; } = null!;
    public RuntimeLibrary RuntimeLib { get; set; } = null!;

    public EmitContext(ushort codeBase, Dictionary<string, ushort> dataAddresses)
    {
        Buffer = new AssemblyBuffer(codeBase);
        Symbols = new SymbolTable();
        DataAddresses = dataAddresses;
    }

    public string NextLabel(string prefix) => $"{prefix}_{_labelCounter++}";
}
