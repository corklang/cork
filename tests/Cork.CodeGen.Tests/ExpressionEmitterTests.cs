using Cork.CodeGen.Emit;
using Cork.Language.Ast;
using Cork.Language.Lexing;

namespace Cork.CodeGen.Tests;

public class ExpressionEmitterTests
{
    private static readonly SourceLocation Loc = new("<test>", 1, 1, 0, 0);

    private static (EmitContext Ctx, ExpressionEmitter Expr) CreateEmitter()
    {
        var ctx = new EmitContext(0x0800, new Dictionary<string, ushort>());
        ctx.Expressions = new ExpressionEmitter(ctx);
        ctx.Statements = new StatementEmitter(ctx);
        ctx.ControlFlow = new ControlFlowEmitter(ctx);
        ctx.Scenes = new SceneEmitter(ctx);
        ctx.Intrinsics = new IntrinsicEmitter(ctx);
        ctx.RuntimeLib = new RuntimeLibrary(ctx);
        return (ctx, ctx.Expressions);
    }

    [Test]
    public async Task EmitExprToA_byte_literal_emits_LDA_immediate()
    {
        var (ctx, expr) = CreateEmitter();
        expr.EmitExprToA(new IntLiteralExpr(42, Loc));

        var bytes = ctx.Buffer.ToArray();
        await Assert.That(bytes).Count().IsEqualTo(2);
        await Assert.That(bytes[0]).IsEqualTo((byte)0xA9); // LDA #
        await Assert.That(bytes[1]).IsEqualTo((byte)42);
    }

    [Test]
    public async Task EmitExprToA_variable_emits_LDA_zeropage()
    {
        var (ctx, expr) = CreateEmitter();
        ctx.Symbols.AllocZeroPage("x");

        expr.EmitExprToA(new IdentifierExpr("x", Loc));

        var bytes = ctx.Buffer.ToArray();
        await Assert.That(bytes).Count().IsEqualTo(2);
        await Assert.That(bytes[0]).IsEqualTo((byte)0xA5); // LDA zp
        await Assert.That(bytes[1]).IsEqualTo((byte)0x02); // first ZP slot
    }

    [Test]
    public async Task EmitExprToA_binary_add_constant_folds()
    {
        var (ctx, expr) = CreateEmitter();
        var addExpr = new BinaryExpr(
            new IntLiteralExpr(10, Loc),
            TokenKind.Plus,
            new IntLiteralExpr(20, Loc),
            Loc
        );

        expr.EmitExprToA(addExpr);

        var bytes = ctx.Buffer.ToArray();
        // Constant-folded: LDA #30
        await Assert.That(bytes).Count().IsEqualTo(2);
        await Assert.That(bytes[0]).IsEqualTo((byte)0xA9); // LDA #
        await Assert.That(bytes[1]).IsEqualTo((byte)30);
    }

    [Test]
    public async Task EmitExprToA_binary_sub_constant_folds()
    {
        var (ctx, expr) = CreateEmitter();
        var subExpr = new BinaryExpr(
            new IntLiteralExpr(50, Loc),
            TokenKind.Minus,
            new IntLiteralExpr(30, Loc),
            Loc
        );

        expr.EmitExprToA(subExpr);

        var bytes = ctx.Buffer.ToArray();
        // Constant-folded: LDA #20
        await Assert.That(bytes).Count().IsEqualTo(2);
        await Assert.That(bytes[0]).IsEqualTo((byte)0xA9); // LDA #
        await Assert.That(bytes[1]).IsEqualTo((byte)20);
    }

    [Test]
    public async Task EmitExprToA_constant_uses_immediate()
    {
        var (ctx, expr) = CreateEmitter();
        ctx.Symbols.AddConstant("MAX", 200);

        expr.EmitExprToA(new IdentifierExpr("MAX", Loc));

        var bytes = ctx.Buffer.ToArray();
        await Assert.That(bytes[0]).IsEqualTo((byte)0xA9); // LDA #
        await Assert.That(bytes[1]).IsEqualTo((byte)200);
    }

    [Test]
    public async Task Resolve16BitInitializer_fixed_point()
    {
        var result = ExpressionEmitter.Resolve16BitInitializer("fixed",
            new FixedLiteralExpr(1.5, Loc));
        // 1.5 in 8.8 = 0x0180 (1 in high byte, 0x80 = 128 in low byte)
        await Assert.That(result).IsEqualTo((ushort)0x0180);
    }

    [Test]
    public async Task Resolve16BitInitializer_null_returns_zero()
    {
        var result = ExpressionEmitter.Resolve16BitInitializer("word", null);
        await Assert.That(result).IsEqualTo((ushort)0);
    }

    [Test]
    public async Task EvalConstExpr_integer_literal()
    {
        var (_, expr) = CreateEmitter();
        var result = expr.EvalConstExpr(new IntLiteralExpr(7, Loc));
        await Assert.That(result).IsEqualTo((byte)7);
    }
}
