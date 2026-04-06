namespace Cork.CodeGen.Emit;

/// <summary>
/// Drop-in replacement for AssemblyBuffer that builds a List&lt;StreamEntry&gt;
/// instead of raw bytes. Same public API — emitters don't need changes.
/// Supports peephole optimization passes before serialization to bytes.
/// </summary>
public sealed class InstructionBuffer(ushort baseAddress)
{
    private readonly List<StreamEntry> _entries = [];
    private readonly List<(int entryId, string label, FixupKind kind)> _fixups = [];
    private readonly Dictionary<string, int> _labelEntryIds = []; // label name → entry ID
    private readonly Dictionary<ushort, string> _labelAddresses = []; // addr → label name (for backward jump detection)

    private enum FixupKind { Word, LoByte, HiByte }

    private int _nextId;
    // Running byte total — updated on every append to avoid O(n) per CurrentAddress call
    private int _byteLength;

    public ushort BaseAddress => baseAddress;
    public ushort CurrentAddress => (ushort)(baseAddress + _byteLength);
    public int Length => _byteLength;
    public int PeepholeRemovals { get; set; }

    // Expose entries for the peephole optimizer
    internal List<StreamEntry> Entries => _entries;

    private int NextId() => _nextId++;

    private void Append(StreamEntry entry)
    {
        _byteLength += entry.Size;
        _entries.Add(entry);
    }

    // --- Label management ---

    public void DefineLabel(string name)
    {
        var id = NextId();
        _labelEntryIds[name] = id;
        _labelAddresses[CurrentAddress] = name;
        Append(StreamEntry.LabelDef(id, name));
    }

    // --- Raw data emission (for backward compat with EmitByte callers) ---

    public void EmitByte(byte value) => Append(StreamEntry.DataBlob(NextId(), [value]));

    public void EmitWord(ushort value)
        => Append(StreamEntry.DataBlob(NextId(), [(byte)(value & 0xFF), (byte)(value >> 8)]));

    public void EmitBytes(ReadOnlySpan<byte> data)
        => Append(StreamEntry.DataBlob(NextId(), data.ToArray()));

    // --- 6510 Instructions ---

    // LDA
    public void EmitLdaImmediate(byte value)
        => Append(StreamEntry.Instr(NextId(), 0xA9, AddressMode.Immediate, value));

    public void EmitLdaZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0xA5, AddressMode.ZeroPage, addr));

    public void EmitLdaAbsolute(ushort addr)
        => Append(StreamEntry.Instr(NextId(), 0xAD, AddressMode.Absolute, addr));

    public void EmitLdaAbsoluteX(ushort addr)
        => Append(StreamEntry.Instr(NextId(), 0xBD, AddressMode.AbsoluteX, addr));

    public void EmitLdaAbsoluteY(ushort addr)
        => Append(StreamEntry.Instr(NextId(), 0xB9, AddressMode.AbsoluteY, addr));

    public void EmitLdaIndirectY(byte zpAddr)
        => Append(StreamEntry.Instr(NextId(), 0xB1, AddressMode.IndirectY, zpAddr));
    public void EmitLdaZeroPageX(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0xB5, AddressMode.ZeroPageX, addr));
    public void EmitStaZeroPageX(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0x95, AddressMode.ZeroPageX, addr));

    // LDY
    public void EmitLdyZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0xA4, AddressMode.ZeroPage, addr));

    public void EmitLdyImmediate(byte value)
        => Append(StreamEntry.Instr(NextId(), 0xA0, AddressMode.Immediate, value));

    // LDX
    public void EmitLdxImmediate(byte value)
        => Append(StreamEntry.Instr(NextId(), 0xA2, AddressMode.Immediate, value));

    public void EmitLdxZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0xA6, AddressMode.ZeroPage, addr));

    // STA
    public void EmitStaZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0x85, AddressMode.ZeroPage, addr));

    public void EmitStaAbsolute(ushort addr)
        => Append(StreamEntry.Instr(NextId(), 0x8D, AddressMode.Absolute, addr));

    public void EmitStaAbsoluteX(ushort addr)
        => Append(StreamEntry.Instr(NextId(), 0x9D, AddressMode.AbsoluteX, addr));

    public void EmitStaIndirectY(byte zpAddr)
        => Append(StreamEntry.Instr(NextId(), 0x91, AddressMode.IndirectY, zpAddr));

    // STX
    public void EmitStxZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0x86, AddressMode.ZeroPage, addr));

    // STY
    public void EmitStyZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0x84, AddressMode.ZeroPage, addr));

    // Arithmetic
    public void EmitClc() => Append(StreamEntry.Instr(NextId(), 0x18, AddressMode.Implied));
    public void EmitSec() => Append(StreamEntry.Instr(NextId(), 0x38, AddressMode.Implied));
    public void EmitAdcImmediate(byte value)
        => Append(StreamEntry.Instr(NextId(), 0x69, AddressMode.Immediate, value));
    public void EmitAdcZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0x65, AddressMode.ZeroPage, addr));
    public void EmitAdcAbsolute(ushort addr)
        => Append(StreamEntry.Instr(NextId(), 0x6D, AddressMode.Absolute, addr));
    public void EmitSbcImmediate(byte value)
        => Append(StreamEntry.Instr(NextId(), 0xE9, AddressMode.Immediate, value));
    public void EmitSbcZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0xE5, AddressMode.ZeroPage, addr));

    // Bitwise
    public void EmitAndImmediate(byte value)
        => Append(StreamEntry.Instr(NextId(), 0x29, AddressMode.Immediate, value));
    public void EmitOraImmediate(byte value)
        => Append(StreamEntry.Instr(NextId(), 0x09, AddressMode.Immediate, value));
    public void EmitEorImmediate(byte value)
        => Append(StreamEntry.Instr(NextId(), 0x49, AddressMode.Immediate, value));
    public void EmitAndZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0x25, AddressMode.ZeroPage, addr));
    public void EmitOraZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0x05, AddressMode.ZeroPage, addr));
    public void EmitEorZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0x45, AddressMode.ZeroPage, addr));
    public void EmitOraIndirectY(byte zpAddr)
        => Append(StreamEntry.Instr(NextId(), 0x11, AddressMode.IndirectY, zpAddr));
    public void EmitAndIndirectY(byte zpAddr)
        => Append(StreamEntry.Instr(NextId(), 0x31, AddressMode.IndirectY, zpAddr));

    // Increment / Decrement
    public void EmitInx() => Append(StreamEntry.Instr(NextId(), 0xE8, AddressMode.Implied));
    public void EmitIny() => Append(StreamEntry.Instr(NextId(), 0xC8, AddressMode.Implied));
    public void EmitDex() => Append(StreamEntry.Instr(NextId(), 0xCA, AddressMode.Implied));
    public void EmitDey() => Append(StreamEntry.Instr(NextId(), 0x88, AddressMode.Implied));
    public void EmitIncZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0xE6, AddressMode.ZeroPage, addr));
    public void EmitDecZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0xC6, AddressMode.ZeroPage, addr));

    // Shift
    public void EmitAslAccumulator()
        => Append(StreamEntry.Instr(NextId(), 0x0A, AddressMode.Accumulator));
    public void EmitLsrAccumulator()
        => Append(StreamEntry.Instr(NextId(), 0x4A, AddressMode.Accumulator));
    public void EmitRorAccumulator()
        => Append(StreamEntry.Instr(NextId(), 0x6A, AddressMode.Accumulator));
    public void EmitLsrZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0x46, AddressMode.ZeroPage, addr));
    public void EmitRorZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0x66, AddressMode.ZeroPage, addr));
    public void EmitAslZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0x06, AddressMode.ZeroPage, addr));
    public void EmitRolZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0x26, AddressMode.ZeroPage, addr));

    // Compare
    public void EmitCmpImmediate(byte value)
        => Append(StreamEntry.Instr(NextId(), 0xC9, AddressMode.Immediate, value));
    public void EmitCmpZeroPage(byte addr)
        => Append(StreamEntry.Instr(NextId(), 0xC5, AddressMode.ZeroPage, addr));
    public void EmitCpxImmediate(byte value)
        => Append(StreamEntry.Instr(NextId(), 0xE0, AddressMode.Immediate, value));
    public void EmitCpyImmediate(byte value)
        => Append(StreamEntry.Instr(NextId(), 0xC0, AddressMode.Immediate, value));

    // Branch
    public void EmitBne(sbyte offset)
        => Append(StreamEntry.Instr(NextId(), 0xD0, AddressMode.Relative, (ushort)(byte)offset));
    public void EmitBeq(sbyte offset)
        => Append(StreamEntry.Instr(NextId(), 0xF0, AddressMode.Relative, (ushort)(byte)offset));
    public void EmitBcc(sbyte offset)
        => Append(StreamEntry.Instr(NextId(), 0x90, AddressMode.Relative, (ushort)(byte)offset));
    public void EmitBcs(sbyte offset)
        => Append(StreamEntry.Instr(NextId(), 0xB0, AddressMode.Relative, (ushort)(byte)offset));
    public void EmitBmi(sbyte offset)
        => Append(StreamEntry.Instr(NextId(), 0x30, AddressMode.Relative, (ushort)(byte)offset));
    public void EmitBpl(sbyte offset)
        => Append(StreamEntry.Instr(NextId(), 0x10, AddressMode.Relative, (ushort)(byte)offset));

    // Jump — auto-detect backward label references and convert to fixups
    public void EmitJmpAbsolute(ushort addr)
    {
        if (_labelAddresses.TryGetValue(addr, out var label))
        {
            var id = NextId();
            _fixups.Add((id, label, FixupKind.Word));
            Append(StreamEntry.Instr(id, 0x4C, AddressMode.Absolute, addr));
        }
        else
            Append(StreamEntry.Instr(NextId(), 0x4C, AddressMode.Absolute, addr));
    }
    public void EmitJsrAbsolute(ushort addr)
    {
        if (_labelAddresses.TryGetValue(addr, out var label))
        {
            var id = NextId();
            _fixups.Add((id, label, FixupKind.Word));
            Append(StreamEntry.Instr(id, 0x20, AddressMode.Absolute, addr));
        }
        else
            Append(StreamEntry.Instr(NextId(), 0x20, AddressMode.Absolute, addr));
    }
    public void EmitRts() => Append(StreamEntry.Instr(NextId(), 0x60, AddressMode.Implied));

    // Transfer
    public void EmitTax() => Append(StreamEntry.Instr(NextId(), 0xAA, AddressMode.Implied));
    public void EmitTay() => Append(StreamEntry.Instr(NextId(), 0xA8, AddressMode.Implied));
    public void EmitTxa() => Append(StreamEntry.Instr(NextId(), 0x8A, AddressMode.Implied));
    public void EmitTya() => Append(StreamEntry.Instr(NextId(), 0x98, AddressMode.Implied));

    // Stack
    public void EmitPha() => Append(StreamEntry.Instr(NextId(), 0x48, AddressMode.Implied));
    public void EmitPla() => Append(StreamEntry.Instr(NextId(), 0x68, AddressMode.Implied));
    public void EmitPhp() => Append(StreamEntry.Instr(NextId(), 0x08, AddressMode.Implied));
    public void EmitPlp() => Append(StreamEntry.Instr(NextId(), 0x28, AddressMode.Implied));

    // Misc
    public void EmitNop() => Append(StreamEntry.Instr(NextId(), 0xEA, AddressMode.Implied));
    public void EmitSei() => Append(StreamEntry.Instr(NextId(), 0x78, AddressMode.Implied));
    public void EmitCli() => Append(StreamEntry.Instr(NextId(), 0x58, AddressMode.Implied));

    // --- Forward references ---

    public void EmitJmpForward(string label)
    {
        var id = NextId();
        _fixups.Add((id, label, FixupKind.Word));
        Append(StreamEntry.Instr(id, 0x4C, AddressMode.Absolute, 0x0000));
    }

    public void EmitJsrForward(string label)
    {
        var id = NextId();
        _fixups.Add((id, label, FixupKind.Word));
        Append(StreamEntry.Instr(id, 0x20, AddressMode.Absolute, 0x0000));
    }

    public void EmitStoreAddrForward(string label, ushort destLo, ushort destHi)
    {
        // LDA #<label
        var id1 = NextId();
        _fixups.Add((id1, label, FixupKind.LoByte));
        Append(StreamEntry.Instr(id1, 0xA9, AddressMode.Immediate, 0x00));
        // STA destLo
        Append(StreamEntry.Instr(NextId(), 0x8D, AddressMode.Absolute, destLo));
        // LDA #>label
        var id2 = NextId();
        _fixups.Add((id2, label, FixupKind.HiByte));
        Append(StreamEntry.Instr(id2, 0xA9, AddressMode.Immediate, 0x00));
        // STA destHi
        Append(StreamEntry.Instr(NextId(), 0x8D, AddressMode.Absolute, destHi));
    }

    public void EmitLdaAbsoluteXForward(string label, int offset = 0)
    {
        var id = NextId();
        _fixups.Add((id, label, FixupKind.Word));
        Append(StreamEntry.Instr(id, 0xBD, AddressMode.AbsoluteX, (ushort)offset));
    }

    // --- Label resolution ---

    public sbyte BranchOffset(string label)
    {
        var targetAddr = GetLabel(label);
        var offset = targetAddr - CurrentAddress;
        if (offset is < -128 or > 127)
            throw new InvalidOperationException($"Branch to '{label}' out of range: {offset}");
        return (sbyte)offset;
    }

    public ushort GetLabel(string name)
    {
        if (!_labelEntryIds.TryGetValue(name, out var labelId))
            throw new InvalidOperationException($"Undefined label: {name}");
        // Sum sizes of all entries before the label entry
        ushort addr = baseAddress;
        foreach (var entry in _entries)
        {
            if (entry.Id == labelId) break;
            addr += (ushort)entry.Size;
        }
        return addr;
    }

    // --- Fixup resolution ---

    public void ResolveFixups()
    {
        // Build ID → index map
        var idToIndex = new Dictionary<int, int>();
        for (var i = 0; i < _entries.Count; i++)
            idToIndex[_entries[i].Id] = i;

        // Build entry-index-to-byte-offset map
        var entryOffsets = new int[_entries.Count];
        var offset = 0;
        for (var i = 0; i < _entries.Count; i++)
        {
            entryOffsets[i] = offset;
            offset += _entries[i].Size;
        }

        foreach (var (entryId, label, kind) in _fixups)
        {
            // Find the label's byte offset
            if (!_labelEntryIds.TryGetValue(label, out var labelId))
                throw new InvalidOperationException($"Unresolved label: {label}");
            if (!idToIndex.TryGetValue(labelId, out var labelIdx))
                throw new InvalidOperationException($"Label entry not found: {label}");
            var labelAddr = (ushort)(baseAddress + entryOffsets[labelIdx]);

            // Find the fixup instruction
            if (!idToIndex.TryGetValue(entryId, out var instrIdx))
                throw new InvalidOperationException($"Fixup entry not found for label: {label}");
            var entry = _entries[instrIdx];
            if (entry.Kind != StreamEntryKind.Instruction)
                throw new InvalidOperationException($"Fixup entry {entryId} is not an instruction");

            var instr = entry.Instruction;
            var newOperand = kind switch
            {
                FixupKind.Word => labelAddr,
                FixupKind.LoByte => (ushort)(labelAddr & 0xFF),
                FixupKind.HiByte => (ushort)(labelAddr >> 8),
                _ => throw new InvalidOperationException($"Unknown fixup kind: {kind}")
            };

            _entries[instrIdx] = StreamEntry.Instr(entry.Id, instr.Opcode, instr.Mode, newOperand);
        }
    }

    // --- Optimization ---

    /// <summary>
    /// Run peephole optimization passes over the instruction stream.
    /// Recalculates _byteLength after modifications.
    /// </summary>
    public void Optimize()
    {
        bool changed;
        do
        {
            changed = false;

            // Scan for instruction pairs, never crossing labels or data
            for (var i = 0; i < _entries.Count - 1; i++)
            {
                var a = _entries[i];
                var b = _entries[i + 1];

                if (a.Kind != StreamEntryKind.Instruction || b.Kind != StreamEntryKind.Instruction)
                    continue;

                var ai = a.Instruction;
                var bi = b.Instruction;

                // Rule 1: LDA #X; LDA #Y → LDA #Y (remove first)
                if (ai.Opcode == 0xA9 && bi.Opcode == 0xA9)
                {
                    _entries.RemoveAt(i);
                    PeepholeRemovals += 2;
                    changed = true;
                    i--;
                    continue;
                }

                // Rule 2: STA zp; LDA zp (same addr) → STA zp (remove LDA)
                if (ai.Opcode == 0x85 && bi.Opcode == 0xA5 &&
                    ai.Operand == bi.Operand)
                {
                    _entries.RemoveAt(i + 1);
                    PeepholeRemovals += 2;
                    changed = true;
                    i--;
                    continue;
                }
            }
        } while (changed);

        // Recalculate byte length
        _byteLength = 0;
        foreach (var entry in _entries)
            _byteLength += entry.Size;
    }

    // --- Serialization ---

    public byte[] ToArray()
    {
        var result = new List<byte>(_byteLength);
        foreach (var entry in _entries)
        {
            switch (entry.Kind)
            {
                case StreamEntryKind.Instruction:
                    EncodeInstruction(entry.Instruction, result);
                    break;
                case StreamEntryKind.Data:
                    if (entry.RawData != null)
                        result.AddRange(entry.RawData);
                    break;
                case StreamEntryKind.Label:
                    break;
            }
        }
        return [.. result];
    }

    private static void EncodeInstruction(Instruction instr, List<byte> output)
    {
        output.Add(instr.Opcode);
        switch (instr.Mode)
        {
            case AddressMode.Implied:
            case AddressMode.Accumulator:
                break;
            case AddressMode.Immediate:
            case AddressMode.ZeroPage:
            case AddressMode.ZeroPageX:
            case AddressMode.IndirectY:
            case AddressMode.Relative:
                output.Add((byte)(instr.Operand & 0xFF));
                break;
            case AddressMode.Absolute:
            case AddressMode.AbsoluteX:
            case AddressMode.AbsoluteY:
                output.Add((byte)(instr.Operand & 0xFF));
                output.Add((byte)(instr.Operand >> 8));
                break;
        }
    }
}
