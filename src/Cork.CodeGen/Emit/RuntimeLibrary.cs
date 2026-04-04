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
        EmitDivideRoutine();
        EmitDebugRoutines();
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
        ctx.Buffer.EmitByte(0x6A); // ROR A
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
        ctx.Buffer.EmitByte(0xE6); ctx.Buffer.EmitByte(EmitContext.ZpMulA); // INC zp
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
            ctx.Buffer.EmitByte(0x84); ctx.Buffer.EmitByte(EmitContext.ZpPointerHi); // STY zp

            // Digit 0
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
            ctx.Buffer.EmitByte(0x4A); ctx.Buffer.EmitByte(0x4A);
            ctx.Buffer.EmitByte(0x4A); ctx.Buffer.EmitByte(0x4A);
            ctx.Buffer.EmitJsrForward("_rt_hexchar");
            ctx.Buffer.EmitLdyImmediate(0);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);

            // Digit 1
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
            ctx.Buffer.EmitByte(0x29); ctx.Buffer.EmitByte(0x0F);
            ctx.Buffer.EmitJsrForward("_rt_hexchar");
            ctx.Buffer.EmitLdyImmediate(1);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);

            // Digit 2
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitByte(0x4A); ctx.Buffer.EmitByte(0x4A);
            ctx.Buffer.EmitByte(0x4A); ctx.Buffer.EmitByte(0x4A);
            ctx.Buffer.EmitJsrForward("_rt_hexchar");
            ctx.Buffer.EmitLdyImmediate(2);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);

            // Digit 3
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitByte(0x29); ctx.Buffer.EmitByte(0x0F);
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
            ctx.Buffer.EmitByte(0x29); ctx.Buffer.EmitByte(0x01); // AND #1
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
}
