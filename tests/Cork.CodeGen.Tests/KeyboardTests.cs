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
        // Space is PA7, PB4 → colSelect = ~(1<<7) = 0x7F, rowMask = 1<<4 = 0x10
        await Assert.That(colSelect).IsEqualTo((byte)0x7F);
        await Assert.That(rowMask).IsEqualTo((byte)0x10);
    }

    [Test]
    public async Task IsKeyboardCheck_detects_letter_a()
    {
        var expr = new MemberAccessExpr(
            new IdentifierExpr("keyboard", Loc), "a", Loc);

        var result = ControlFlowEmitter.IsKeyboardCheck(expr, out var colSelect, out var rowMask);

        await Assert.That(result).IsTrue();
        // A is PA1, PB2 → colSelect = ~(1<<1) = 0xFD, rowMask = 1<<2 = 0x04
        await Assert.That(colSelect).IsEqualTo((byte)0xFD);
        await Assert.That(rowMask).IsEqualTo((byte)0x04);
    }

    [Test]
    public async Task IsKeyboardCheck_detects_return()
    {
        var expr = new MemberAccessExpr(
            new IdentifierExpr("keyboard", Loc), "return", Loc);

        var result = ControlFlowEmitter.IsKeyboardCheck(expr, out var colSelect, out var rowMask);

        await Assert.That(result).IsTrue();
        // Return is PA0, PB1 → colSelect = ~(1<<0) = 0xFE, rowMask = 1<<1 = 0x02
        await Assert.That(colSelect).IsEqualTo((byte)0xFE);
        await Assert.That(rowMask).IsEqualTo((byte)0x02);
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
        // F1 is PA0, PB4 → colSelect = ~(1<<0) = 0xFE, rowMask = 1<<4 = 0x10
        await Assert.That(colSelect).IsEqualTo((byte)0xFE);
        await Assert.That(rowMask).IsEqualTo((byte)0x10);
    }

    [Test]
    public async Task IsKeyboardCheck_detects_number_keys()
    {
        // n1 is PA7, PB0
        var expr = new MemberAccessExpr(
            new IdentifierExpr("keyboard", Loc), "n1", Loc);

        var result = ControlFlowEmitter.IsKeyboardCheck(expr, out var colSelect, out var rowMask);

        await Assert.That(result).IsTrue();
        await Assert.That(colSelect).IsEqualTo((byte)0x7F); // ~(1<<7)
        await Assert.That(rowMask).IsEqualTo((byte)0x01);    // 1<<0
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
        // Should emit: LDA #$7F, STA $DC00, LDA $DC01, AND #$10, BEQ +3
        // Space is PA7 ($DC00 = ~(1<<7) = $7F), PB4 ($DC01 mask = 1<<4 = $10)
        await Assert.That(bytes[0]).IsEqualTo((byte)0xA9); // LDA #imm
        await Assert.That(bytes[1]).IsEqualTo((byte)0x7F); // PA7 drive value
        await Assert.That(bytes[2]).IsEqualTo((byte)0x8D); // STA abs
        await Assert.That(bytes[3]).IsEqualTo((byte)0x00); // $DC00 lo
        await Assert.That(bytes[4]).IsEqualTo((byte)0xDC); // $DC00 hi
        await Assert.That(bytes[5]).IsEqualTo((byte)0xAD); // LDA abs
        await Assert.That(bytes[6]).IsEqualTo((byte)0x01); // $DC01 lo
        await Assert.That(bytes[7]).IsEqualTo((byte)0xDC); // $DC01 hi
        await Assert.That(bytes[8]).IsEqualTo((byte)0x29); // AND #imm
        await Assert.That(bytes[9]).IsEqualTo((byte)0x10); // PB4 mask for space
        await Assert.That(bytes[10]).IsEqualTo((byte)0xF0); // BEQ
        await Assert.That(bytes[11]).IsEqualTo((byte)0x03); // +3 skip
    }
}
