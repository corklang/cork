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
        var a = entries[i];
        var b = entries[i + 1];

        if (a.Kind != StreamEntryKind.Instruction || b.Kind != StreamEntryKind.Instruction)
            return 0;

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
            entries.RemoveAt(i + 1);
            return 2;
        }

        // Rule: PHA; PLA → remove both (stack round-trip with nothing between)
        if (ai.Opcode == 0x48 && bi.Opcode == 0x68)
        {
            entries.RemoveRange(i, 2);
            return 2;
        }

        // Rule: TAX; TXA → remove both (transfer round-trip)
        if (ai.Opcode == 0xAA && bi.Opcode == 0x8A)
        {
            entries.RemoveRange(i, 2);
            return 2;
        }

        // Rule: TAY; TYA → remove both (transfer round-trip)
        if (ai.Opcode == 0xA8 && bi.Opcode == 0x98)
        {
            entries.RemoveRange(i, 2);
            return 2;
        }

        // Rule: LDA zp; STA zp (same addr) → LDA zp (remove redundant store-back)
        if (ai.Opcode == 0xA5 && bi.Opcode == 0x85 && ai.Operand == bi.Operand)
        {
            entries.RemoveAt(i + 1);
            return 2;
        }

        // Rule: STA zp; STA zp (same addr) → STA zp (duplicate store)
        if (ai.Opcode == 0x85 && bi.Opcode == 0x85 && ai.Operand == bi.Operand)
        {
            entries.RemoveAt(i);
            return 2;
        }

        // Rule: CLC; ADC #0 → remove both (addition of zero)
        if (ai.Opcode == 0x18 && bi.Opcode == 0x69 && bi.Operand == 0)
        {
            entries.RemoveRange(i, 2);
            return 3;
        }

        // Rule: SEC; SBC #0 → remove both (subtraction of zero)
        if (ai.Opcode == 0x38 && bi.Opcode == 0xE9 && bi.Operand == 0)
        {
            entries.RemoveRange(i, 2);
            return 3;
        }

        return 0;
    }

    /// <summary>
    /// 3-instruction peephole rules.
    /// </summary>
    private static int TryWindow3(List<StreamEntry> entries, int i)
    {
        if (i + 2 >= entries.Count) return 0;

        var a = entries[i];
        var b = entries[i + 1];
        var c = entries[i + 2];

        if (a.Kind != StreamEntryKind.Instruction ||
            b.Kind != StreamEntryKind.Instruction ||
            c.Kind != StreamEntryKind.Instruction)
            return 0;

        var ai = a.Instruction;
        var bi = b.Instruction;
        var ci = c.Instruction;

        // Rule: LDA #X; STA zp; LDA #X (same value) → LDA #X; STA zp (remove redundant re-load)
        if (ai.Opcode == 0xA9 && bi.Opcode == 0x85 && ci.Opcode == 0xA9 &&
            ai.Operand == ci.Operand)
        {
            entries.RemoveAt(i + 2);
            return 2;
        }

        // Rule: LDA #0; CLC; ADC zp → LDA zp (loading 0 and adding is just loading)
        if (ai.Opcode == 0xA9 && ai.Operand == 0 &&
            bi.Opcode == 0x18 && ci.Opcode == 0x65)
        {
            entries[i] = StreamEntry.Instr(a.Id, 0xA5, AddressMode.ZeroPage, ci.Operand);
            entries.RemoveRange(i + 1, 2);
            return 3;
        }

        return 0;
    }
}
