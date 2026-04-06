namespace Cork.CodeGen.Emit;

/// <summary>
/// Shared context object holding all state needed during code emission.
/// Passed to each sub-emitter so they can access the buffer, symbol table,
/// data addresses, and other emitters.
/// </summary>
public sealed class EmitContext
{
    public InstructionBuffer Buffer { get; }
    public SymbolTable Symbols { get; }
    public HashSet<string> Runtime { get; } = [];
    public Stack<(string BreakLabel, string ContinueLabel)> LoopStack { get; } = [];
    public Dictionary<string, ushort> DataAddresses { get; }

    private int _labelCounter;

    // ZP constants for runtime routines
    public const byte ZpTemp = 0xED;      // scratch byte (was 0x0F — must not be in allocatable range)
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
    public const byte ZpDivRemainder = 0xEE;

    // Sub-emitters (set after construction to break circular dependencies)
    public ExpressionEmitter Expressions { get; set; } = null!;
    public StatementEmitter Statements { get; set; } = null!;
    public ControlFlowEmitter ControlFlow { get; set; } = null!;
    public SceneEmitter Scenes { get; set; } = null!;
    public IntrinsicEmitter Intrinsics { get; set; } = null!;
    public RuntimeLibrary RuntimeLib { get; set; } = null!;

    public EmitContext(ushort codeBase, Dictionary<string, ushort> dataAddresses)
    {
        Buffer = new InstructionBuffer(codeBase);
        Symbols = new SymbolTable();
        DataAddresses = dataAddresses;
    }

    // Collected errors for multi-error reporting
    public List<CompileError> Errors { get; } = [];

    // Debug symbol information (populated during final emission pass)
    public DebugInfo? Debug { get; set; }

    // For-each over const byte array: (varName, dataAddr, indexZp)
    public (string Name, ushort DataAddr, byte IndexZp)? ForEachVar { get; set; }
    // For-each over struct array
    public (string Name, string StructType, Dictionary<string, byte> FieldBases, byte IndexZp)? ForEachStructVar { get; set; }
    // For-each over ref param (string/array passed by reference)
    public (byte PtrLo, byte PtrHi, byte LenZp)? ForEachRefParam { get; set; }

    // Mutable data arrays: large mutable arrays stored in absolute memory (not ZP)
    // Maps array name → absolute address (resolved after code size is known)
    public Dictionary<string, ushort> MutableDataAddresses { get; } = [];
    private readonly List<(string Name, int Size)> _mutableDataArrays = [];
    public int MutableDataSize => _mutableDataArrays.Sum(a => a.Size);

    public void RegisterMutableDataArray(string name, int size)
    {
        _mutableDataArrays.Add((name, size));
    }

    public void ResolveMutableDataAddresses(ushort baseAddr)
    {
        var offset = 0;
        foreach (var (name, size) in _mutableDataArrays)
        {
            MutableDataAddresses[name] = (ushort)(baseAddr + offset);
            offset += size;
        }
    }

    // Const array sizes (populated during code generation)
    private readonly Dictionary<string, int> _constArraySizes = [];

    // Inline data (string literals etc.) — accumulated during emission
    private readonly List<(string Name, byte[] Data)> _inlineData = [];
    private readonly Dictionary<string, ushort> _inlineDataAddresses = [];

    public void RegisterConstArraySize(string name, int size) => _constArraySizes[name] = size;
    public int GetConstArraySize(string name) =>
        _constArraySizes.TryGetValue(name, out var s) ? s
            : throw new InvalidOperationException($"Unknown const array: {name}");

    public void RegisterInlineData(string name, byte[] data)
    {
        _inlineData.Add((name, data));
    }

    public ushort GetInlineDataAddress(string name)
    {
        if (_inlineDataAddresses.TryGetValue(name, out var addr))
            return addr;
        // During first pass, return a placeholder
        return 0xFFFF;
    }

    /// <summary>
    /// After code emission, resolve inline data addresses and return the combined inline data bytes.
    /// </summary>
    public byte[] FinalizeInlineData(ushort dataStart)
    {
        var offset = 0;
        foreach (var (name, data) in _inlineData)
        {
            _inlineDataAddresses[name] = (ushort)(dataStart + offset);
            offset += data.Length;
        }

        var result = new byte[offset];
        offset = 0;
        foreach (var (_, data) in _inlineData)
        {
            data.CopyTo(result, offset);
            offset += data.Length;
        }
        return result;
    }

    public int InlineDataSize => _inlineData.Sum(d => d.Data.Length);

    // Active method selector (set during method body emission for caller-save param protection)
    public string? ActiveMethodSelector { get; set; }
    // Active method locals base ZP address (for caller-save of locals at call sites)
    public byte ActiveMethodLocalsBase { get; set; }

    // Current scene's graphics mode (set during hardware block emission)
    public bool IsBitmapMode { get; set; }

    // Sprite VIC-II registers dirtied by the current scene (cleared on go)
    public HashSet<ushort> DirtySpriteRegs { get; } = [];

    public string NextLabel(string prefix) => $"{prefix}_{_labelCounter++}";
}
