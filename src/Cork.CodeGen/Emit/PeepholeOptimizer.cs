namespace Cork.CodeGen.Emit;

/// <summary>
/// Multi-pass peephole optimizer operating on the instruction stream.
/// Scans with a sliding window, never matching across label boundaries.
/// Repeats until fixed-point (no more changes).
/// </summary>
public static class PeepholeOptimizer
{
    public static int Optimize(List<StreamEntry> entries)
    {
        int totalRemoved = 0;
        bool changed;
        do
        {
            changed = false;
            for (var i = 0; i < entries.Count - 1; i++)
            {
                if (entries[i].Kind != StreamEntryKind.Instruction) continue;
                var removed = TryOptimizeAt(entries, i);
                if (removed > 0)
                {
                    totalRemoved += removed;
                    changed = true;
                    i--; // re-examine from same position
                }
            }
        } while (changed);
        return totalRemoved;
    }

    /// <summary>
    /// Find the next instruction entry after index i, skipping labels and debug markers.
    /// Returns -1 if no instruction found before a data or branch-to-label entry.
    /// </summary>
    /// <summary>
    /// Find the next instruction entry after index, skipping only debug markers.
    /// Labels, data, and branch-to-label entries are barriers — returns -1.
    /// </summary>
    private static int NextInstr(List<StreamEntry> entries, int after)
    {
        for (var j = after + 1; j < entries.Count; j++)
        {
            switch (entries[j].Kind)
            {
                case StreamEntryKind.Instruction: return j;
                case StreamEntryKind.DebugMarker: continue;
                default: return -1; // label, data, branch — barrier
            }
        }
        return -1;
    }

    private static int TryOptimizeAt(List<StreamEntry> entries, int i)
    {
        // Try 3-instruction window first, then 2-instruction
        var result = TryWindow3(entries, i);
        if (result > 0) return result;
        return TryWindow2(entries, i);
    }

    /// <summary>
    /// 2-instruction peephole rules.
    /// </summary>
    private static int TryWindow2(List<StreamEntry> entries, int i)
    {
        var j = NextInstr(entries, i);
        if (j < 0) return 0;

        var a = entries[i];
        var b = entries[j];
        var ai = a.Instruction;
        var bi = b.Instruction;

        // Rule: LDA #X; LDA #Y → LDA #Y (remove first)
        if (ai.Opcode == 0xA9 && bi.Opcode == 0xA9)
        {
            entries.RemoveAt(i);
            return 2;
        }

        // Rule: STA zp; LDA zp (same addr) → STA zp (remove LDA)
        if (ai.Opcode == 0x85 && bi.Opcode == 0xA5 && ai.Operand == bi.Operand)
        {
            entries.RemoveAt(j);
            return 2;
        }

        // Rule: PHA; PLA → remove both (stack round-trip with nothing between)
        if (ai.Opcode == 0x48 && bi.Opcode == 0x68)
        {
            entries.RemoveAt(j);
            entries.RemoveAt(i);
            return 2;
        }

        // Rule: TAX; TXA → remove both (transfer round-trip)
        if (ai.Opcode == 0xAA && bi.Opcode == 0x8A)
        {
            entries.RemoveAt(j);
            entries.RemoveAt(i);
            return 2;
        }

        // Rule: TAY; TYA → remove both (transfer round-trip)
        if (ai.Opcode == 0xA8 && bi.Opcode == 0x98)
        {
            entries.RemoveAt(j);
            entries.RemoveAt(i);
            return 2;
        }

        // Rule: LDA zp; STA zp (same addr) → LDA zp (remove redundant store-back)
        if (ai.Opcode == 0xA5 && bi.Opcode == 0x85 && ai.Operand == bi.Operand)
        {
            entries.RemoveAt(j);
            return 2;
        }

        // Rule: STA zp; STA zp (same addr) → STA zp (duplicate store)
        if (ai.Opcode == 0x85 && bi.Opcode == 0x85 && ai.Operand == bi.Operand)
        {
            entries.RemoveAt(i);
            return 2;
        }

        // Note: CLC; ADC #0 and SEC; SBC #0 are NOT safe to remove.
        // In 16-bit arithmetic (lo then hi), the carry from the lo-byte
        // add/sub propagates to the hi-byte ADC/SBC. Removing the lo-byte
        // operation also removes the carry setup (CLC/SEC).

        return 0;
    }

    /// <summary>
    /// 3-instruction peephole rules.
    /// </summary>
    private static int TryWindow3(List<StreamEntry> entries, int i)
    {
        var j = NextInstr(entries, i);
        if (j < 0) return 0;
        var k = NextInstr(entries, j);
        if (k < 0) return 0;

        var ai = entries[i].Instruction;
        var bi = entries[j].Instruction;
        var ci = entries[k].Instruction;

        // Rule: LDA #X; STA zp; LDA #X (same value) → LDA #X; STA zp (remove redundant re-load)
        if (ai.Opcode == 0xA9 && bi.Opcode == 0x85 && ci.Opcode == 0xA9 &&
            ai.Operand == ci.Operand)
        {
            entries.RemoveAt(k);
            return 2;
        }

        // Rule: LDA #0; CLC; ADC zp → LDA zp (loading 0 and adding is just loading)
        if (ai.Opcode == 0xA9 && ai.Operand == 0 &&
            bi.Opcode == 0x18 && ci.Opcode == 0x65)
        {
            entries[i] = StreamEntry.Instr(entries[i].Id, 0xA5, AddressMode.ZeroPage, ci.Operand);
            entries.RemoveAt(k);
            entries.RemoveAt(j);
            return 3;
        }

        return 0;
    }
}
