namespace Cork.CodeGen.Emit;

/// <summary>
/// Growable byte buffer for 6510 machine code emission.
/// Tracks current address for label resolution and forward references.
/// </summary>
public sealed class AssemblyBuffer(ushort baseAddress)
{
    private readonly List<byte> _bytes = [];
    private readonly Dictionary<string, ushort> _labels = [];
    private readonly List<(int offset, string label, FixupKind kind)> _fixups = [];

    private enum FixupKind { Word, LoByte, HiByte }

    public ushort BaseAddress => baseAddress;
    public ushort CurrentAddress => (ushort)(baseAddress + _bytes.Count);
    public int Length => _bytes.Count;

    public void DefineLabel(string name) => _labels[name] = CurrentAddress;

    public void EmitByte(byte value) => _bytes.Add(value);

    public void EmitWord(ushort value)
    {
        _bytes.Add((byte)(value & 0xFF));
        _bytes.Add((byte)(value >> 8));
    }

    public void EmitBytes(ReadOnlySpan<byte> data)
    {
        foreach (var b in data) _bytes.Add(b);
    }

    // --- 6510 Instructions ---

    // LDA
    public void EmitLdaImmediate(byte value) { EmitByte(0xA9); EmitByte(value); }
    public void EmitLdaZeroPage(byte addr) { EmitByte(0xA5); EmitByte(addr); }
    public void EmitLdaAbsolute(ushort addr) { EmitByte(0xAD); EmitWord(addr); }
    public void EmitLdaAbsoluteX(ushort addr) { EmitByte(0xBD); EmitWord(addr); }
    public void EmitLdaAbsoluteY(ushort addr) { EmitByte(0xB9); EmitWord(addr); }

    // LDX
    public void EmitLdxImmediate(byte value) { EmitByte(0xA2); EmitByte(value); }
    public void EmitLdxZeroPage(byte addr) { EmitByte(0xA6); EmitByte(addr); }

    // LDY
    public void EmitLdyImmediate(byte value) { EmitByte(0xA0); EmitByte(value); }

    // STA
    public void EmitStaZeroPage(byte addr) { EmitByte(0x85); EmitByte(addr); }
    public void EmitStaAbsolute(ushort addr) { EmitByte(0x8D); EmitWord(addr); }
    public void EmitStaAbsoluteX(ushort addr) { EmitByte(0x9D); EmitWord(addr); }

    // STX
    public void EmitStxZeroPage(byte addr) { EmitByte(0x86); EmitByte(addr); }

    // Arithmetic
    public void EmitClc() => EmitByte(0x18);
    public void EmitSec() => EmitByte(0x38);
    public void EmitAdcImmediate(byte value) { EmitByte(0x69); EmitByte(value); }
    public void EmitAdcZeroPage(byte addr) { EmitByte(0x65); EmitByte(addr); }
    public void EmitAdcAbsolute(ushort addr) { EmitByte(0x6D); EmitWord(addr); }
    public void EmitSbcImmediate(byte value) { EmitByte(0xE9); EmitByte(value); }
    public void EmitSbcZeroPage(byte addr) { EmitByte(0xE5); EmitByte(addr); }

    // Increment / Decrement
    public void EmitInx() => EmitByte(0xE8);
    public void EmitIny() => EmitByte(0xC8);
    public void EmitDex() => EmitByte(0xCA);
    public void EmitDey() => EmitByte(0x88);
    public void EmitIncZeroPage(byte addr) { EmitByte(0xE6); EmitByte(addr); }
    public void EmitDecZeroPage(byte addr) { EmitByte(0xC6); EmitByte(addr); }

    // Compare
    public void EmitCmpImmediate(byte value) { EmitByte(0xC9); EmitByte(value); }
    public void EmitCmpZeroPage(byte addr) { EmitByte(0xC5); EmitByte(addr); }
    public void EmitCpxImmediate(byte value) { EmitByte(0xE0); EmitByte(value); }

    // Branch
    public void EmitBne(sbyte offset) { EmitByte(0xD0); EmitByte((byte)offset); }
    public void EmitBeq(sbyte offset) { EmitByte(0xF0); EmitByte((byte)offset); }
    public void EmitBcc(sbyte offset) { EmitByte(0x90); EmitByte((byte)offset); }
    public void EmitBcs(sbyte offset) { EmitByte(0xB0); EmitByte((byte)offset); }
    public void EmitBmi(sbyte offset) { EmitByte(0x30); EmitByte((byte)offset); }
    public void EmitBpl(sbyte offset) { EmitByte(0x10); EmitByte((byte)offset); }

    // Jump
    public void EmitJmpAbsolute(ushort addr) { EmitByte(0x4C); EmitWord(addr); }
    public void EmitJsrAbsolute(ushort addr) { EmitByte(0x20); EmitWord(addr); }
    public void EmitRts() => EmitByte(0x60);

    // Transfer
    public void EmitTax() => EmitByte(0xAA);
    public void EmitTay() => EmitByte(0xA8);
    public void EmitTxa() => EmitByte(0x8A);
    public void EmitTya() => EmitByte(0x98);

    // Stack
    public void EmitPha() => EmitByte(0x48);
    public void EmitPla() => EmitByte(0x68);

    // Misc
    public void EmitNop() => EmitByte(0xEA);
    public void EmitSei() => EmitByte(0x78);
    public void EmitCli() => EmitByte(0x58);

    // --- Fixup / Label resolution ---

    /// <summary>
    /// Emit a JMP with a forward reference to be patched later.
    /// </summary>
    public void EmitJmpForward(string label)
    {
        EmitByte(0x4C);
        _fixups.Add((_bytes.Count, label, FixupKind.Word));
        EmitWord(0x0000);
    }

    public void EmitJsrForward(string label)
    {
        EmitByte(0x20);
        _fixups.Add((_bytes.Count, label, FixupKind.Word));
        EmitWord(0x0000);
    }

    /// <summary>
    /// Emit LDA #&lt;label; STA destLo; LDA #&gt;label; STA destHi
    /// Used for writing a label address to a memory location (e.g., IRQ vector).
    /// </summary>
    public void EmitStoreAddrForward(string label, ushort destLo, ushort destHi)
    {
        EmitByte(0xA9); // LDA #<label
        _fixups.Add((_bytes.Count, label, FixupKind.LoByte));
        EmitByte(0x00);
        EmitByte(0x8D); EmitWord(destLo); // STA destLo

        EmitByte(0xA9); // LDA #>label
        _fixups.Add((_bytes.Count, label, FixupKind.HiByte));
        EmitByte(0x00);
        EmitByte(0x8D); EmitWord(destHi); // STA destHi
    }

    /// <summary>
    /// Calculate branch offset from current position to a label.
    /// Must be called AFTER the branch opcode+operand bytes are reserved.
    /// </summary>
    public sbyte BranchOffset(string label)
    {
        if (!_labels.TryGetValue(label, out var target))
            throw new InvalidOperationException($"Undefined label: {label}");
        var offset = target - CurrentAddress;
        if (offset is < -128 or > 127)
            throw new InvalidOperationException($"Branch to '{label}' out of range: {offset}");
        return (sbyte)offset;
    }

    /// <summary>
    /// Resolve all forward reference fixups.
    /// </summary>
    public void ResolveFixups()
    {
        foreach (var (offset, label, kind) in _fixups)
        {
            if (!_labels.TryGetValue(label, out var addr))
                throw new InvalidOperationException($"Unresolved label: {label}");
            switch (kind)
            {
                case FixupKind.Word:
                    _bytes[offset] = (byte)(addr & 0xFF);
                    _bytes[offset + 1] = (byte)(addr >> 8);
                    break;
                case FixupKind.LoByte:
                    _bytes[offset] = (byte)(addr & 0xFF);
                    break;
                case FixupKind.HiByte:
                    _bytes[offset] = (byte)(addr >> 8);
                    break;
            }
        }
    }

    public ushort GetLabel(string name) =>
        _labels.TryGetValue(name, out var addr) ? addr : throw new InvalidOperationException($"Undefined label: {name}");

    public byte[] ToArray() => [.. _bytes];
}
