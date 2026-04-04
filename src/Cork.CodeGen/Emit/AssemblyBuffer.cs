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

    // Peephole state: track last instruction for redundancy elimination
    private byte _prevOp;
    private byte _prevArg;
    private bool _hasPrev;
    public int PeepholeRemovals { get; private set; }

    public ushort BaseAddress => baseAddress;
    public ushort CurrentAddress => (ushort)(baseAddress + _bytes.Count);
    public int Length => _bytes.Count;

    public void DefineLabel(string name)
    {
        _labels[name] = CurrentAddress;
        _hasPrev = false; // branch target — can't optimize across labels
    }

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

    // --- Peephole optimization ---

    private void TrackInstruction(byte op, byte arg)
    {
        _prevOp = op;
        _prevArg = arg;
        _hasPrev = true;
    }

    private void ResetPeephole() => _hasPrev = false;

    // --- 6510 Instructions ---

    // LDA
    public void EmitLdaImmediate(byte value)
    {
        // STA zp; LDA #same → keep (STA needed for side effects, A value differs)
        // LDA #X; LDA #Y → remove first LDA
        if (_hasPrev && _prevOp == 0xA9)
        {
            // Previous was also LDA #imm — remove it (2 bytes)
            _bytes.RemoveRange(_bytes.Count - 2, 2);
            PeepholeRemovals += 2;
        }
        EmitByte(0xA9); EmitByte(value);
        TrackInstruction(0xA9, value);
    }

    public void EmitLdaZeroPage(byte addr)
    {
        // STA zp; LDA zp (same addr) → remove LDA, A already has the value
        if (_hasPrev && _prevOp == 0x85 && _prevArg == addr)
        {
            PeepholeRemovals += 2;
            // Don't emit — A still holds the stored value
            // Track as if we loaded (A has addr's value)
            TrackInstruction(0xA5, addr);
            return;
        }
        EmitByte(0xA5); EmitByte(addr);
        TrackInstruction(0xA5, addr);
    }

    public void EmitLdaAbsolute(ushort addr) { EmitByte(0xAD); EmitWord(addr); ResetPeephole(); }
    public void EmitLdaAbsoluteX(ushort addr) { EmitByte(0xBD); EmitWord(addr); }
    public void EmitLdaAbsoluteY(ushort addr) { EmitByte(0xB9); EmitWord(addr); }

    // LDA indirect indexed
    public void EmitLdaIndirectY(byte zpAddr) { EmitByte(0xB1); EmitByte(zpAddr); }

    // LDY zero page
    public void EmitLdyZeroPage(byte addr) { EmitByte(0xA4); EmitByte(addr); }

    // LDX
    public void EmitLdxImmediate(byte value) { EmitByte(0xA2); EmitByte(value); }
    public void EmitLdxZeroPage(byte addr) { EmitByte(0xA6); EmitByte(addr); }

    // LDY
    public void EmitLdyImmediate(byte value) { EmitByte(0xA0); EmitByte(value); }

    // STA
    public void EmitStaZeroPage(byte addr)
    {
        EmitByte(0x85); EmitByte(addr);
        TrackInstruction(0x85, addr);
    }
    public void EmitStaAbsolute(ushort addr) { EmitByte(0x8D); EmitWord(addr); ResetPeephole(); }
    public void EmitStaAbsoluteX(ushort addr) { EmitByte(0x9D); EmitWord(addr); ResetPeephole(); }
    public void EmitStaIndirectY(byte zpAddr) { EmitByte(0x91); EmitByte(zpAddr); ResetPeephole(); }

    // STX
    public void EmitStxZeroPage(byte addr) { EmitByte(0x86); EmitByte(addr); }

    // Arithmetic — all change A, reset peephole
    public void EmitClc() { EmitByte(0x18); /* don't reset — flag only */ }
    public void EmitSec() { EmitByte(0x38); /* don't reset — flag only */ }
    public void EmitAdcImmediate(byte value) { EmitByte(0x69); EmitByte(value); ResetPeephole(); }
    public void EmitAdcZeroPage(byte addr) { EmitByte(0x65); EmitByte(addr); ResetPeephole(); }
    public void EmitAdcAbsolute(ushort addr) { EmitByte(0x6D); EmitWord(addr); ResetPeephole(); }
    public void EmitSbcImmediate(byte value) { EmitByte(0xE9); EmitByte(value); ResetPeephole(); }
    public void EmitSbcZeroPage(byte addr) { EmitByte(0xE5); EmitByte(addr); ResetPeephole(); }

    // Increment / Decrement
    public void EmitInx() => EmitByte(0xE8);
    public void EmitIny() => EmitByte(0xC8);
    public void EmitDex() => EmitByte(0xCA);
    public void EmitDey() => EmitByte(0x88);
    public void EmitIncZeroPage(byte addr) { EmitByte(0xE6); EmitByte(addr); }
    public void EmitDecZeroPage(byte addr) { EmitByte(0xC6); EmitByte(addr); }

    // Shift
    public void EmitLsrZeroPage(byte addr) { EmitByte(0x46); EmitByte(addr); }
    public void EmitRorZeroPage(byte addr) { EmitByte(0x66); EmitByte(addr); }
    public void EmitAslZeroPage(byte addr) { EmitByte(0x06); EmitByte(addr); }
    public void EmitRolZeroPage(byte addr) { EmitByte(0x26); EmitByte(addr); }

    // Compare
    public void EmitCmpImmediate(byte value) { EmitByte(0xC9); EmitByte(value); }
    public void EmitCmpZeroPage(byte addr) { EmitByte(0xC5); EmitByte(addr); }
    public void EmitCpxImmediate(byte value) { EmitByte(0xE0); EmitByte(value); }

    // Branch — all reset peephole (control flow change)
    public void EmitBne(sbyte offset) { EmitByte(0xD0); EmitByte((byte)offset); ResetPeephole(); }
    public void EmitBeq(sbyte offset) { EmitByte(0xF0); EmitByte((byte)offset); ResetPeephole(); }
    public void EmitBcc(sbyte offset) { EmitByte(0x90); EmitByte((byte)offset); ResetPeephole(); }
    public void EmitBcs(sbyte offset) { EmitByte(0xB0); EmitByte((byte)offset); ResetPeephole(); }
    public void EmitBmi(sbyte offset) { EmitByte(0x30); EmitByte((byte)offset); ResetPeephole(); }
    public void EmitBpl(sbyte offset) { EmitByte(0x10); EmitByte((byte)offset); ResetPeephole(); }

    // Jump
    public void EmitJmpAbsolute(ushort addr) { EmitByte(0x4C); EmitWord(addr); ResetPeephole(); }
    public void EmitJsrAbsolute(ushort addr) { EmitByte(0x20); EmitWord(addr); ResetPeephole(); }
    public void EmitRts() { EmitByte(0x60); ResetPeephole(); }

    // Transfer — changes A/X/Y relationship, reset
    public void EmitTax() { EmitByte(0xAA); ResetPeephole(); }
    public void EmitTay() { EmitByte(0xA8); ResetPeephole(); }
    public void EmitTxa() { EmitByte(0x8A); ResetPeephole(); }
    public void EmitTya() { EmitByte(0x98); ResetPeephole(); }

    // Stack — changes A, reset
    public void EmitPha() { EmitByte(0x48); ResetPeephole(); }
    public void EmitPla() { EmitByte(0x68); ResetPeephole(); }

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
        ResetPeephole();
    }

    public void EmitJsrForward(string label)
    {
        EmitByte(0x20);
        _fixups.Add((_bytes.Count, label, FixupKind.Word));
        EmitWord(0x0000);
        ResetPeephole();
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
