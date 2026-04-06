namespace Cork.CodeGen.Emit;

/// <summary>
/// Emits the runtime library routines (multiply, fixed-point, debug hex)
/// at the end of the binary. Only includes routines that were actually requested.
/// </summary>
public sealed class RuntimeLibrary(EmitContext ctx)
{
    public void EmitRuntimeLibrary()
    {
        if (ctx.Runtime.Count == 0) return;

        EmitMultiplyRoutines();
        EmitSignedMultiply();
        EmitMultiply16x8();
        EmitDivideRoutine();
        EmitDivide16x8();
        EmitFixedDivide();
        EmitSignedFixedDivide();
        EmitDebugRoutines();
        EmitPlotPixelRoutine();
    }

    private void EmitMultiplyRoutines()
    {
        if (!ctx.Runtime.Contains("mul8x8")) return;

        // 8x8->16 unsigned multiply
        ctx.Buffer.DefineLabel("_rt_mul8x8");
        ctx.Buffer.EmitLdaImmediate(0);
        ctx.Buffer.EmitLdxImmediate(8);
        ctx.Buffer.EmitLsrZeroPage(EmitContext.ZpMulB);
        ctx.Buffer.EmitBcc(3);
        ctx.Buffer.EmitClc();
        ctx.Buffer.EmitAdcZeroPage(EmitContext.ZpMulA);
        ctx.Buffer.EmitRorAccumulator();
        ctx.Buffer.EmitRorZeroPage(EmitContext.ZpMulB);
        ctx.Buffer.EmitDex();
        ctx.Buffer.EmitBne(unchecked((sbyte)(-11)));
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulResultHi);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulB);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulResultLo);
        ctx.Buffer.EmitRts();

        // Fixed 8.8 x 8.8 -> 8.8 multiply
        ctx.Buffer.DefineLabel("_rt_fixmul");

        ctx.Buffer.EmitLdaImmediate(0);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB0);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB1);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB2);

        // 1. Al * Bl -> bytes 0,1
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg2Lo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
        ctx.Buffer.EmitJsrAbsolute(ctx.Buffer.GetLabel("_rt_mul8x8"));
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulResultLo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB0);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulResultHi);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB1);

        // 2. Al * Bh -> bytes 1,2
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg2Hi);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
        ctx.Buffer.EmitJsrAbsolute(ctx.Buffer.GetLabel("_rt_mul8x8"));
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedResB1);
        ctx.Buffer.EmitClc();
        ctx.Buffer.EmitAdcZeroPage(EmitContext.ZpMulResultLo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB1);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedResB2);
        ctx.Buffer.EmitAdcZeroPage(EmitContext.ZpMulResultHi);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB2);

        // 3. Ah * Bl -> bytes 1,2
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg2Lo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
        ctx.Buffer.EmitJsrAbsolute(ctx.Buffer.GetLabel("_rt_mul8x8"));
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedResB1);
        ctx.Buffer.EmitClc();
        ctx.Buffer.EmitAdcZeroPage(EmitContext.ZpMulResultLo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB1);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedResB2);
        ctx.Buffer.EmitAdcZeroPage(EmitContext.ZpMulResultHi);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB2);

        // 4. Ah * Bh -> byte 2
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg2Hi);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
        ctx.Buffer.EmitJsrAbsolute(ctx.Buffer.GetLabel("_rt_mul8x8"));
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedResB2);
        ctx.Buffer.EmitClc();
        ctx.Buffer.EmitAdcZeroPage(EmitContext.ZpMulResultLo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB2);

        ctx.Buffer.EmitRts();
    }

    private void EmitMultiply16x8()
    {
        if (!ctx.Runtime.Contains("mul16x8")) return;

        // 16×8 unsigned multiply: ZpFixedArg1(lo/hi) × ZpMulB → result in ZpFixedArg1(lo/hi)
        // Uses two 8×8 multiplies: (lo × B) + (hi × B) << 8
        ctx.Buffer.DefineLabel("_rt_mul16x8");
        // Save high byte and multiplier (mul8x8 destroys ZpMulB)
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
        ctx.Buffer.EmitPha();
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulB);
        ctx.Buffer.EmitPha();
        // lo × B
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
        ctx.Buffer.EmitJsrForward("_rt_mul8x8");
        // Result: lo in ZpMulResultLo, hi in ZpMulResultHi
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulResultLo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Lo); // result lo
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulResultHi);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Hi); // partial result hi
        // hi × B (restore multiplier, then original hi)
        ctx.Buffer.EmitPla(); // restore multiplier
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
        ctx.Buffer.EmitPla(); // restore original hi
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
        ctx.Buffer.EmitJsrForward("_rt_mul8x8");
        // Add low byte of (hi×B) to result high byte
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
        ctx.Buffer.EmitClc();
        ctx.Buffer.EmitAdcZeroPage(EmitContext.ZpMulResultLo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Hi);
        ctx.Buffer.EmitRts();
    }

    private void EmitDivide16x8()
    {
        if (!ctx.Runtime.Contains("div16x8")) return;

        // 16÷8 unsigned divide: ZpFixedArg1(lo/hi) / ZpMulB
        // → quotient in ZpFixedArg1(lo/hi), remainder in ZpDivRemainder
        ctx.Buffer.DefineLabel("_rt_div16x8");
        ctx.Buffer.EmitLdaImmediate(0);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpDivRemainder);
        ctx.Buffer.EmitLdxImmediate(16);
        // Loop: shift dividend left into remainder
        ctx.Buffer.EmitAslZeroPage(EmitContext.ZpFixedArg1Lo);
        ctx.Buffer.EmitRolZeroPage(EmitContext.ZpFixedArg1Hi);
        ctx.Buffer.EmitRolZeroPage(EmitContext.ZpDivRemainder);
        // Try subtract divisor
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpDivRemainder);
        ctx.Buffer.EmitSec();
        ctx.Buffer.EmitSbcZeroPage(EmitContext.ZpMulB);
        ctx.Buffer.EmitBcc(4); // if remainder < divisor, skip
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpDivRemainder);
        ctx.Buffer.EmitIncZeroPage(EmitContext.ZpFixedArg1Lo); // INC zpLo (set quotient bit)
        ctx.Buffer.EmitDex();
        // Loop body: ASL(2)+ROL(2)+ROL(2)+LDA(2)+SEC(1)+SBC(2)+BCC(2)+STA(2)+INC(2)+DEX(1)+BNE(2) = 20
        ctx.Buffer.EmitBne(unchecked((sbyte)(-20)));
        ctx.Buffer.EmitRts();
    }

    private void EmitFixedDivide()
    {
        if (!ctx.Runtime.Contains("fixdiv")) return;

        // 8.8 ÷ 8.8 → 8.8 unsigned fixed-point divide
        // Dividend in ZpFixedArg1(lo/hi), Divisor in ZpFixedArg2(lo/hi)
        // Result in ZpFixedArg1(lo/hi)
        //
        // Algorithm: 24÷16→24 divide with 24 iterations.
        // Dividend = original << 8 = [B2:B1:B0] = [hi:lo:00] (24 bits).
        // Quotient builds in [ResB2:Arg1Hi:Arg1Lo] (24 bits, we take lo 16).
        // Remainder in [MulA:DivRemainder] (16 bits).
        ctx.Buffer.DefineLabel("_rt_fixdiv");

        // Set up 24-bit dividend: [B2:B1:B0] = [Arg1Hi:Arg1Lo:0]
        ctx.Buffer.EmitLdaImmediate(0);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB0);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB1);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB2);

        // Clear remainder and quotient
        ctx.Buffer.EmitLdaImmediate(0);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpDivRemainder);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Lo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Hi);

        ctx.Buffer.EmitLdxImmediate(24);

        // --- Loop ---
        // Shift dividend left into remainder                       bytes
        ctx.Buffer.EmitAslZeroPage(EmitContext.ZpFixedResB0);   // 2
        ctx.Buffer.EmitRolZeroPage(EmitContext.ZpFixedResB1);   // 2
        ctx.Buffer.EmitRolZeroPage(EmitContext.ZpFixedResB2);   // 2
        ctx.Buffer.EmitRolZeroPage(EmitContext.ZpDivRemainder); // 2
        ctx.Buffer.EmitRolZeroPage(EmitContext.ZpMulA);          // 2 = 10
        // Shift quotient left
        ctx.Buffer.EmitAslZeroPage(EmitContext.ZpFixedArg1Lo);  // 2
        ctx.Buffer.EmitRolZeroPage(EmitContext.ZpFixedArg1Hi);  // 2 = 14
        // Try subtract divisor from remainder
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpDivRemainder); // 2
        ctx.Buffer.EmitSec();                                    // 1
        ctx.Buffer.EmitSbcZeroPage(EmitContext.ZpFixedArg2Lo);  // 2
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);          // 2
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulA);          // 2
        ctx.Buffer.EmitSbcZeroPage(EmitContext.ZpFixedArg2Hi);  // 2 = 25
        // BCC +8: skip commit if remainder < divisor
        ctx.Buffer.EmitBcc(8);                                   // 2 = 27
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);          // 2
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulB);          // 2
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpDivRemainder); // 2
        ctx.Buffer.EmitIncZeroPage(EmitContext.ZpFixedArg1Lo); // 2 = 35
        ctx.Buffer.EmitDex();                                    // 1 = 36
        ctx.Buffer.EmitBne(unchecked((sbyte)(-38)));             // 2 = 38

        // Result: 24-bit quotient in [??:Arg1Hi:Arg1Lo]
        // We want the bottom 16 bits = Arg1Hi:Arg1Lo — already in place
        ctx.Buffer.EmitRts();
    }

    private void EmitSignedFixedDivide()
    {
        if (!ctx.Runtime.Contains("sfixdiv")) return;

        // Signed 8.8 ÷ 8.8: check signs, make positive, divide, negate if needed
        ctx.Buffer.DefineLabel("_rt_sfixdiv");

        // Clear sign flag
        ctx.Buffer.EmitLdaImmediate(0);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpSignFlag);

        // Check arg1 sign (hi byte bit 7)
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
        // BPL: skip negate block (INC(2)+LDA(2)+EOR(2)+CLC(1)+ADC(2)+STA(2)+LDA(2)+EOR(2)+ADC(2)+STA(2)=19)
        ctx.Buffer.EmitBpl(19);
        // Negate arg1: flip sign flag, two's complement
        ctx.Buffer.EmitIncZeroPage(EmitContext.ZpSignFlag); // INC sign
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
        ctx.Buffer.EmitEorImmediate(0xFF); // EOR #$FF
        ctx.Buffer.EmitClc();
        ctx.Buffer.EmitAdcImmediate(1);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Lo);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
        ctx.Buffer.EmitEorImmediate(0xFF);
        ctx.Buffer.EmitAdcImmediate(0);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Hi);

        // Check arg2 sign
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg2Hi);
        ctx.Buffer.EmitBpl(19);
        ctx.Buffer.EmitIncZeroPage(EmitContext.ZpSignFlag);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg2Lo);
        ctx.Buffer.EmitEorImmediate(0xFF);
        ctx.Buffer.EmitClc();
        ctx.Buffer.EmitAdcImmediate(1);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg2Lo);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg2Hi);
        ctx.Buffer.EmitEorImmediate(0xFF);
        ctx.Buffer.EmitAdcImmediate(0);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg2Hi);

        // Do unsigned divide
        ctx.Buffer.EmitJsrForward("_rt_fixdiv");

        // If sign flag is odd, negate result
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpSignFlag);
        ctx.Buffer.EmitAndImmediate(0x01);
        // BEQ: skip negate (LDA(2)+EOR(2)+CLC(1)+ADC(2)+STA(2)+LDA(2)+EOR(2)+ADC(2)+STA(2)=17)
        ctx.Buffer.EmitBeq(17);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
        ctx.Buffer.EmitEorImmediate(0xFF);
        ctx.Buffer.EmitClc();
        ctx.Buffer.EmitAdcImmediate(1);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Lo);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
        ctx.Buffer.EmitEorImmediate(0xFF);
        ctx.Buffer.EmitAdcImmediate(0);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Hi);

        ctx.Buffer.EmitRts();
    }

    private void EmitDivideRoutine()
    {
        if (!ctx.Runtime.Contains("div8")) return;

        // 8÷8 unsigned divide: ZpMulA / ZpMulB → A=quotient, ZpDivRemainder=remainder
        // Standard shift-subtract algorithm, 8 iterations
        ctx.Buffer.DefineLabel("_rt_div8");
        ctx.Buffer.EmitLdaImmediate(0);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpDivRemainder);
        ctx.Buffer.EmitLdxImmediate(8);
        // Loop: shift dividend left into remainder, try subtract divisor
        // ASL ZpMulA (shift dividend, carry = next bit into remainder)
        ctx.Buffer.EmitAslZeroPage(EmitContext.ZpMulA);
        // ROL remainder (shift carry into remainder)
        ctx.Buffer.EmitRolZeroPage(EmitContext.ZpDivRemainder);
        // LDA remainder; SEC; SBC divisor
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpDivRemainder);
        ctx.Buffer.EmitSec();
        ctx.Buffer.EmitSbcZeroPage(EmitContext.ZpMulB);
        // BCC +4 (if remainder < divisor, don't subtract)
        ctx.Buffer.EmitBcc(4);
        // remainder = remainder - divisor; set bit 0 of quotient
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpDivRemainder);
        // INC ZpMulA (set low bit of quotient — ASL already shifted it)
        ctx.Buffer.EmitIncZeroPage(EmitContext.ZpMulA);
        // DEX; BNE loop
        ctx.Buffer.EmitDex();
        // Loop body: ASL(2) + ROL(2) + LDA(2) + SEC(1) + SBC(2) + BCC(2) + STA(2) + INC(2) + DEX(1) + BNE(2) = 18
        ctx.Buffer.EmitBne(unchecked((sbyte)(-18)));
        // Result: quotient in ZpMulA, remainder in ZpDivRemainder
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulA);
        ctx.Buffer.EmitRts();
    }

    private void EmitDebugRoutines()
    {
        if (ctx.Runtime.Contains("debughex"))
        {
            ctx.Buffer.DefineLabel("_rt_debughex");
            ctx.Buffer.EmitStxZeroPage(EmitContext.ZpPointerLo);
            ctx.Buffer.EmitStyZeroPage(EmitContext.ZpPointerHi);

            // Digit 0
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
            ctx.Buffer.EmitLsrAccumulator();
            ctx.Buffer.EmitLsrAccumulator();
            ctx.Buffer.EmitLsrAccumulator();
            ctx.Buffer.EmitLsrAccumulator();
            ctx.Buffer.EmitJsrForward("_rt_hexchar");
            ctx.Buffer.EmitLdyImmediate(0);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);

            // Digit 1
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
            ctx.Buffer.EmitAndImmediate(0x0F);
            ctx.Buffer.EmitJsrForward("_rt_hexchar");
            ctx.Buffer.EmitLdyImmediate(1);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);

            // Digit 2
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitLsrAccumulator();
            ctx.Buffer.EmitLsrAccumulator();
            ctx.Buffer.EmitLsrAccumulator();
            ctx.Buffer.EmitLsrAccumulator();
            ctx.Buffer.EmitJsrForward("_rt_hexchar");
            ctx.Buffer.EmitLdyImmediate(2);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);

            // Digit 3
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitAndImmediate(0x0F);
            ctx.Buffer.EmitJsrForward("_rt_hexchar");
            ctx.Buffer.EmitLdyImmediate(3);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);
            ctx.Buffer.EmitRts();

            // hex_char subroutine
            ctx.Buffer.DefineLabel("_rt_hexchar");
            ctx.Buffer.EmitCmpImmediate(10);
            ctx.Buffer.EmitBcc(4);
            ctx.Buffer.EmitSec();
            ctx.Buffer.EmitSbcImmediate(9);
            ctx.Buffer.EmitRts();
            ctx.Buffer.EmitClc();
            ctx.Buffer.EmitAdcImmediate(48);
            ctx.Buffer.EmitRts();
        }

    }

    private void EmitSignedMultiply()
    {
        if (!ctx.Runtime.Contains("sfixmul")) return;
        ctx.Buffer.DefineLabel("_rt_sfixmul");

            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpSignFlag);

            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
            ctx.Buffer.EmitBpl(15);
            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitSec();
            ctx.Buffer.EmitSbcZeroPage(EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitSbcZeroPage(EmitContext.ZpFixedArg1Hi);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Hi);
            ctx.Buffer.EmitIncZeroPage(EmitContext.ZpSignFlag);

            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg2Hi);
            ctx.Buffer.EmitBpl(15);
            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitSec();
            ctx.Buffer.EmitSbcZeroPage(EmitContext.ZpFixedArg2Lo);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg2Lo);
            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitSbcZeroPage(EmitContext.ZpFixedArg2Hi);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg2Hi);
            ctx.Buffer.EmitIncZeroPage(EmitContext.ZpSignFlag);

            ctx.Buffer.EmitJsrAbsolute(ctx.Buffer.GetLabel("_rt_fixmul"));

            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpSignFlag);
            ctx.Buffer.EmitAndImmediate(0x01);
            ctx.Buffer.EmitBeq(13);
            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitSec();
            ctx.Buffer.EmitSbcZeroPage(EmitContext.ZpFixedResB1);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB1);
            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitSbcZeroPage(EmitContext.ZpFixedResB2);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedResB2);

        ctx.Buffer.EmitRts();
    }

    private void EmitPlotPixelRoutine()
    {
        if (!ctx.Runtime.Contains("plotPixel")) return;

        // Input: $F0 = x_lo, $F1 = x_hi, $0F = y
        // Uses: $FB/$FC as pointer, trashes A/X/Y
        // Split tables: rowTableLo/Hi[25] indexed by charRow, bitTable[8]

        // _rt_plotPixel: set pixel (ORA)
        ctx.Buffer.DefineLabel("_rt_plotPixel");
        ctx.Buffer.EmitClc(); // carry clear = set mode
        ctx.Buffer.EmitBcc(1); // skip SEC (always taken)

        // _rt_clearPixel: clear pixel (AND ~mask)
        ctx.Buffer.DefineLabel("_rt_clearPixel");
        ctx.Buffer.EmitSec(); // carry set = clear mode

        // Shared body
        ctx.Buffer.EmitPhp();

        // charRow = y >> 3
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpTemp);
        ctx.Buffer.EmitLsrAccumulator();
        ctx.Buffer.EmitLsrAccumulator();
        ctx.Buffer.EmitLsrAccumulator();
        ctx.Buffer.EmitTax();

        // Row base address from split tables
        ctx.Buffer.EmitLdaAbsoluteXForward("_rt_plotRowLo");
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
        ctx.Buffer.EmitLdaAbsoluteXForward("_rt_plotRowHi");
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);

        // Add (x & ~7) — character column offset
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulA); // x_lo
        ctx.Buffer.EmitAndImmediate(0xF8);
        ctx.Buffer.EmitClc();
        ctx.Buffer.EmitAdcZeroPage(EmitContext.ZpPointerLo);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulB); // x_hi
        ctx.Buffer.EmitAdcZeroPage(EmitContext.ZpPointerHi);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);

        // pixelRow = y & 7 → Y register
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpTemp);
        ctx.Buffer.EmitAndImmediate(0x07);
        ctx.Buffer.EmitTay();

        // Bit mask from x & 7
        ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulA); // x_lo
        ctx.Buffer.EmitAndImmediate(0x07);
        ctx.Buffer.EmitTax();
        ctx.Buffer.EmitLdaAbsoluteXForward("_rt_plotBitTable");

        // Set or clear based on saved carry flag
        ctx.Buffer.EmitPlp();
        var clearLabel = ctx.NextLabel("_plotclr");
        ctx.Buffer.EmitBcs(5); // BCS → clear path (skip ORA(2)+STA(2)+RTS(1) = 5 bytes)

        // Set pixel: ORA existing byte
        ctx.Buffer.EmitOraIndirectY(EmitContext.ZpPointerLo);
        ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);
        ctx.Buffer.EmitRts();

        // Clear pixel: invert mask, AND existing byte
        ctx.Buffer.EmitEorImmediate(0xFF); // EOR #$FF
        ctx.Buffer.EmitAndIndirectY(EmitContext.ZpPointerLo);
        ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);
        ctx.Buffer.EmitRts();

        // Row address tables (25 entries each, split lo/hi)
        // Row N address = $2000 + N * 320
        ctx.Buffer.DefineLabel("_rt_plotRowLo");
        for (var row = 0; row < 25; row++)
            ctx.Buffer.EmitByte((byte)((0x2000 + row * 320) & 0xFF));
        ctx.Buffer.DefineLabel("_rt_plotRowHi");
        for (var row = 0; row < 25; row++)
            ctx.Buffer.EmitByte((byte)((0x2000 + row * 320) >> 8));

        // Bit mask table (8 entries: bit 7 down to bit 0)
        ctx.Buffer.DefineLabel("_rt_plotBitTable");
        ctx.Buffer.EmitBytes([0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01]);
    }
}
