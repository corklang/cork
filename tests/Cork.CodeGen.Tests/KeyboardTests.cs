using Cork.CodeGen.Emit;
using Cork.Language.Ast;
using Cork.Language.Lexing;

namespace Cork.CodeGen.Tests;

public class KeyboardTests
{
    private static readonly SourceLocation Loc = new("<test>", 1, 1, 0, 0);

    [Test]
    public async Task IsKeyboardCheck_detects_space()
    {
        var expr = new MemberAccessExpr(
            new IdentifierExpr("keyboard", Loc), "space", Loc);

        var result = ControlFlowEmitter.IsKeyboardCheck(expr, out var colSelect, out var rowMask);

        await Assert.That(result).IsTrue();
        // Space is column 4, row 7 → colSelect = ~(1<<4) = 0xEF, rowMask = 1<<7 = 0x80
        await Assert.That(colSelect).IsEqualTo((byte)0xEF);
        await Assert.That(rowMask).IsEqualTo((byte)0x80);
    }

    [Test]
    public async Task IsKeyboardCheck_detects_letter_a()
    {
        var expr = new MemberAccessExpr(
            new IdentifierExpr("keyboard", Loc), "a", Loc);

        var result = ControlFlowEmitter.IsKeyboardCheck(expr, out var colSelect, out var rowMask);

        await Assert.That(result).IsTrue();
        // A is column 2, row 1 → colSelect = ~(1<<2) = 0xFB, rowMask = 1<<1 = 0x02
        await Assert.That(colSelect).IsEqualTo((byte)0xFB);
        await Assert.That(rowMask).IsEqualTo((byte)0x02);
    }

    [Test]
    public async Task IsKeyboardCheck_detects_return()
    {
        var expr = new MemberAccessExpr(
            new IdentifierExpr("keyboard", Loc), "return", Loc);

        var result = ControlFlowEmitter.IsKeyboardCheck(expr, out var colSelect, out var rowMask);

        await Assert.That(result).IsTrue();
        // Return is column 1, row 0 → colSelect = ~(1<<1) = 0xFD, rowMask = 1<<0 = 0x01
        await Assert.That(colSelect).IsEqualTo((byte)0xFD);
        await Assert.That(rowMask).IsEqualTo((byte)0x01);
    }

    [Test]
    public async Task IsKeyboardCheck_rejects_unknown_key()
    {
        var expr = new MemberAccessExpr(
            new IdentifierExpr("keyboard", Loc), "nonexistent", Loc);

        var result = ControlFlowEmitter.IsKeyboardCheck(expr, out _, out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsKeyboardCheck_rejects_non_keyboard_receiver()
    {
        var expr = new MemberAccessExpr(
            new IdentifierExpr("mouse", Loc), "space", Loc);

        var result = ControlFlowEmitter.IsKeyboardCheck(expr, out _, out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsKeyboardCheck_detects_f1()
    {
        var expr = new MemberAccessExpr(
            new IdentifierExpr("keyboard", Loc), "f1", Loc);

        var result = ControlFlowEmitter.IsKeyboardCheck(expr, out var colSelect, out var rowMask);

        await Assert.That(result).IsTrue();
        // F1 is column 4, row 0 → colSelect = ~(1<<4) = 0xEF, rowMask = 1<<0 = 0x01
        await Assert.That(colSelect).IsEqualTo((byte)0xEF);
        await Assert.That(rowMask).IsEqualTo((byte)0x01);
    }

    [Test]
    public async Task IsKeyboardCheck_detects_number_keys()
    {
        // n1 is column 0, row 7
        var expr = new MemberAccessExpr(
            new IdentifierExpr("keyboard", Loc), "n1", Loc);

        var result = ControlFlowEmitter.IsKeyboardCheck(expr, out var colSelect, out var rowMask);

        await Assert.That(result).IsTrue();
        await Assert.That(colSelect).IsEqualTo((byte)0xFE); // ~(1<<0)
        await Assert.That(rowMask).IsEqualTo((byte)0x80);    // 1<<7
    }

    [Test]
    public async Task Keyboard_check_emits_correct_instructions()
    {
        var ctx = new EmitContext(0x0800, new Dictionary<string, ushort>());
        ctx.Expressions = new ExpressionEmitter(ctx);
        ctx.Statements = new StatementEmitter(ctx);
        ctx.ControlFlow = new ControlFlowEmitter(ctx);
        ctx.Scenes = new SceneEmitter(ctx);
        ctx.Intrinsics = new IntrinsicEmitter(ctx);
        ctx.RuntimeLib = new RuntimeLibrary(ctx);

        // Emit a keyboard.space condition with 3 skip bytes (JMP size)
        var condition = new MemberAccessExpr(
            new IdentifierExpr("keyboard", Loc), "space", Loc);
        ctx.ControlFlow.EmitConditionBranchTrue(condition, 3);

        var bytes = ctx.Buffer.ToArray();
        // Should emit: LDA #$FF, STA $DC00, LDA #$EF, STA $DC00, NOP×4, LDA $DC01, AND #$80, BEQ
        await Assert.That(bytes[0]).IsEqualTo((byte)0xA9); // LDA #$FF (deselect all)
        await Assert.That(bytes[1]).IsEqualTo((byte)0xFF);
        await Assert.That(bytes[2]).IsEqualTo((byte)0x8D); // STA $DC00
        await Assert.That(bytes[5]).IsEqualTo((byte)0xA9); // LDA #$EF (select column 4)
        await Assert.That(bytes[6]).IsEqualTo((byte)0xEF);
        await Assert.That(bytes[7]).IsEqualTo((byte)0x8D); // STA $DC00
        // NOPs at bytes 10-13
        await Assert.That(bytes[10]).IsEqualTo((byte)0xEA); // NOP
        // LDA $DC01 at byte 14
        await Assert.That(bytes[14]).IsEqualTo((byte)0xAD); // LDA abs
        await Assert.That(bytes[15]).IsEqualTo((byte)0x01); // $DC01 lo
        await Assert.That(bytes[16]).IsEqualTo((byte)0xDC); // $DC01 hi
        await Assert.That(bytes[17]).IsEqualTo((byte)0x29); // AND #imm
        await Assert.That(bytes[18]).IsEqualTo((byte)0x80); // row mask for space
    }
}
