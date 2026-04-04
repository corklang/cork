namespace Cork.CodeGen.Emit;

using Cork.Language.Ast;
using Cork.Language.Lexing;

/// <summary>
/// Emits 6510 code for expression evaluation.
/// </summary>
public sealed class ExpressionEmitter(EmitContext ctx)
{
    public void EmitExprToA(ExprNode expr)
    {
        switch (expr)
        {
            case IntLiteralExpr intLit:
                ctx.Buffer.EmitLdaImmediate((byte)intLit.Value);
                break;

            case UnaryExpr { Op: TokenKind.Minus } neg when neg.Operand is IntLiteralExpr negInt:
                ctx.Buffer.EmitLdaImmediate((byte)(-negInt.Value & 0xFF));
                break;

            case IdentifierExpr ident:
                if (ctx.Symbols.TryGetConstant(ident.Name, out var constVal))
                    ctx.Buffer.EmitLdaImmediate((byte)constVal);
                else if (ctx.Symbols.IsWordVar(ident.Name))
                    ctx.Buffer.EmitLdaZeroPage((byte)(ctx.Symbols.GetLocal(ident.Name) + 1));
                else
                    ctx.Buffer.EmitLdaZeroPage(ctx.Symbols.GetLocal(ident.Name));
                break;

            case BinaryExpr { Op: TokenKind.Plus } bin:
                EmitExprToA(bin.Left);
                ctx.Buffer.EmitClc();
                EmitAdcValue(bin.Right);
                break;

            case BinaryExpr { Op: TokenKind.Minus } bin:
                EmitExprToA(bin.Left);
                ctx.Buffer.EmitSec();
                EmitSbcValue(bin.Right);
                break;

            case IndexExpr { Receiver: IdentifierExpr arrName } indexExpr
                when ctx.DataAddresses.TryGetValue(arrName.Name, out var dataAddr):
                EmitExprToA(indexExpr.Index);
                ctx.Buffer.EmitTax();
                ctx.Buffer.EmitLdaAbsoluteX(dataAddr);
                break;

            case MemberAccessExpr member:
                if (TryResolveStructField(member, out var fieldZp))
                    ctx.Buffer.EmitLdaZeroPage(fieldZp);
                else
                    ctx.Buffer.EmitLdaImmediate(ResolveMemberConstant(member));
                break;

            case CastExpr cast:
                EmitCast(cast);
                break;

            default:
                throw new InvalidOperationException($"Phase 1: unsupported expression: {expr.GetType().Name}");
        }
    }

    private void EmitCast(CastExpr cast)
    {
        var srcIsWord = cast.Operand is IdentifierExpr id && ctx.Symbols.IsWordVar(id.Name);

        switch (cast.TargetType)
        {
            case "byte" or "sbyte" when srcIsWord:
                // word/fixed/sfixed → byte: load high byte (integer part for fixed, low byte for word)
                var srcId = (IdentifierExpr)cast.Operand;
                var srcType = ctx.Symbols.GetVarType(srcId.Name);
                if (srcType is "fixed" or "sfixed")
                    ctx.Buffer.EmitLdaZeroPage((byte)(ctx.Symbols.GetLocal(srcId.Name) + 1)); // high = integer
                else
                    ctx.Buffer.EmitLdaZeroPage(ctx.Symbols.GetLocal(srcId.Name)); // low byte for word
                break;

            case "word" or "sword":
                // byte → word: load byte, store as low, zero high
                // Result needs to be in A... but word needs two bytes.
                // For "as word" in a byte context (EmitExprToA returns A), just load the byte.
                // The actual widening happens at the assignment level.
                EmitExprToA(cast.Operand);
                break;

            case "fixed" or "sfixed":
                // byte → fixed: load byte as high byte (integer part), zero fractional
                // In EmitExprToA (returns byte in A), just load the byte value.
                EmitExprToA(cast.Operand);
                break;

            default:
                // Same type or unknown — just pass through
                EmitExprToA(cast.Operand);
                break;
        }
    }

    public void EmitAdcValue(ExprNode expr)
    {
        switch (expr)
        {
            case IntLiteralExpr intLit: ctx.Buffer.EmitAdcImmediate((byte)intLit.Value); break;
            case IdentifierExpr ident: ctx.Buffer.EmitAdcZeroPage(ctx.Symbols.GetLocal(ident.Name)); break;
            default: throw new InvalidOperationException("Phase 1: ADC operand must be simple");
        }
    }

    public void EmitSbcValue(ExprNode expr)
    {
        switch (expr)
        {
            case IntLiteralExpr intLit: ctx.Buffer.EmitSbcImmediate((byte)intLit.Value); break;
            case IdentifierExpr ident: ctx.Buffer.EmitSbcZeroPage(ctx.Symbols.GetLocal(ident.Name)); break;
            default: throw new InvalidOperationException("Phase 1: SBC operand must be simple");
        }
    }

    public byte ResolveMemberConstant(MemberAccessExpr member)
    {
        if (member.Receiver is IdentifierExpr ident)
        {
            if (ctx.Symbols.TryGetEnumMember(ident.Name, member.MemberName, out var value))
                return (byte)value;

            if (ident.Name == "Color")
            {
                return member.MemberName switch
                {
                    "black" => 0, "white" => 1, "red" => 2, "cyan" => 3,
                    "purple" => 4, "green" => 5, "blue" => 6, "yellow" => 7,
                    "orange" => 8, "brown" => 9, "lightRed" => 10, "darkGrey" => 11,
                    "mediumGrey" => 12, "lightGreen" => 13, "lightBlue" => 14, "lightGrey" => 15,
                    _ => throw new InvalidOperationException($"Unknown color: {member.MemberName}")
                };
            }
        }
        throw new InvalidOperationException($"Unknown constant: {member.Receiver}");
    }

    public byte EvalConstExpr(ExprNode expr) => expr switch
    {
        IntLiteralExpr intLit => (byte)intLit.Value,
        MemberAccessExpr member => ResolveMemberConstant(member),
        _ => throw new InvalidOperationException($"Cannot evaluate constant: {expr.GetType().Name}")
    };

    public bool TryResolveStructField(MemberAccessExpr member, out byte zpAddr)
    {
        zpAddr = 0;
        if (member.Receiver is IdentifierExpr ident &&
            ctx.Symbols.TryGetStructInstance(ident.Name, out var instance) &&
            instance.Fields.TryGetValue(member.MemberName, out zpAddr))
        {
            return true;
        }
        return false;
    }

    public static ushort Resolve16BitInitializer(string typeName, ExprNode? init)
    {
        if (init == null) return 0;
        if (init is IntLiteralExpr intLit) return (ushort)intLit.Value;
        if (init is FixedLiteralExpr fixLit)
        {
            var intPart = (int)fixLit.Value;
            var fracPart = (int)((fixLit.Value - intPart) * 256);
            if (fixLit.Value < 0)
            {
                var raw = (int)(fixLit.Value * 256);
                return (ushort)(raw & 0xFFFF);
            }
            return (ushort)((intPart << 8) | (fracPart & 0xFF));
        }
        if (init is UnaryExpr { Op: TokenKind.Minus } neg && neg.Operand is FixedLiteralExpr negFix)
        {
            var raw = (int)(-negFix.Value * 256);
            return (ushort)(raw & 0xFFFF);
        }
        return 0;
    }
}
