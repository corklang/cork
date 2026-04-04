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
        // Constant folding: if the expression can be evaluated at compile time, emit as immediate
        if (TryFoldConstant(expr, out var folded))
        {
            ctx.Buffer.EmitLdaImmediate((byte)folded);
            return;
        }

        switch (expr)
        {
            case IntLiteralExpr intLit:
                ctx.Buffer.EmitLdaImmediate((byte)intLit.Value);
                break;

            case UnaryExpr { Op: TokenKind.Minus } neg when neg.Operand is IntLiteralExpr negInt:
                ctx.Buffer.EmitLdaImmediate((byte)(-negInt.Value & 0xFF));
                break;

            case IdentifierExpr ident:
                if (ctx.ForEachVar is { } fev && fev.Name == ident.Name)
                {
                    ctx.Buffer.EmitLdxZeroPage(fev.IndexZp);
                    ctx.Buffer.EmitLdaAbsoluteX(fev.DataAddr);
                }
                // Inside for-each struct method: bare field names resolve to indexed access
                else if (ctx.ForEachStructVar is { } fesv2 &&
                         fesv2.FieldBases.TryGetValue(ident.Name, out var fieldBase2))
                {
                    ctx.Buffer.EmitLdxZeroPage(fesv2.IndexZp);
                    ctx.Buffer.EmitByte(0xB5); ctx.Buffer.EmitByte(fieldBase2); // LDA zp,X
                }
                else if (ctx.Symbols.TryGetConstant(ident.Name, out var constVal))
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

            // String/array ref param indexing: text[i] → LDA ($FB),Y via pointer
            case IndexExpr { Receiver: IdentifierExpr refName } refIndexExpr
                when ctx.Symbols.TryGetRefParam(refName.Name, out var refP):
                // Load pointer into $FB/$FC
                ctx.Buffer.EmitLdaZeroPage(refP.PtrLo);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
                ctx.Buffer.EmitLdaZeroPage(refP.PtrHi);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);
                // Index into Y
                EmitExprToA(refIndexExpr.Index);
                ctx.Buffer.EmitTay();
                // LDA ($FB),Y
                ctx.Buffer.EmitLdaIndirectY(EmitContext.ZpPointerLo);
                break;

            case IndexExpr { Receiver: IdentifierExpr arrName } indexExpr
                when ctx.DataAddresses.TryGetValue(arrName.Name, out var dataAddr):
                EmitExprToA(indexExpr.Index);
                ctx.Buffer.EmitTax();
                ctx.Buffer.EmitLdaAbsoluteX(dataAddr);
                break;

            case MemberAccessExpr member:
                // text.length for ref params
                if (member.MemberName == "length" && member.Receiver is IdentifierExpr lenIdent &&
                    ctx.Symbols.TryGetRefParam(lenIdent.Name, out var lenRef))
                {
                    ctx.Buffer.EmitLdaZeroPage(lenRef.LenZp);
                    break;
                }
                // text.length for string variables
                if (member.MemberName == "length" && member.Receiver is IdentifierExpr strLenIdent &&
                    ctx.Symbols.TryGetStringVar(strLenIdent.Name, out var strLenInfo))
                {
                    ctx.Buffer.EmitLdaImmediate((byte)strLenInfo.Length);
                    break;
                }
                if (TryResolveForEachStructField(member))
                    break;
                if (TryResolveStructField(member, out var fieldZp))
                    ctx.Buffer.EmitLdaZeroPage(fieldZp);
                else
                    ctx.Buffer.EmitLdaImmediate(ResolveMemberConstant(member));
                break;

            case CastExpr cast:
                EmitCast(cast);
                break;

            case MessageSendExpr msgExpr:
                // Emit as a method call (JSR) — return value is left in A
                ctx.Intrinsics.EmitMessageSend(new MessageSendStmt(
                    msgExpr.Receiver, msgExpr.Segments, msgExpr.Location));
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

    /// <summary>
    /// Attempts to evaluate an expression at compile time.
    /// Handles arithmetic on literals and constants.
    /// </summary>
    public bool TryFoldConstant(ExprNode expr, out long result)
    {
        result = 0;
        switch (expr)
        {
            case IntLiteralExpr intLit:
                result = intLit.Value;
                return true;
            case IdentifierExpr ident when ctx.Symbols.TryGetConstant(ident.Name, out var cv):
                result = cv;
                return true;
            case MemberAccessExpr member when member.Receiver is IdentifierExpr ri:
                if (ctx.Symbols.TryGetEnumMember(ri.Name, member.MemberName, out var ev))
                { result = ev; return true; }
                if (ri.Name == "Color")
                { result = EvalConstExpr(expr); return true; }
                return false;
            case BinaryExpr bin when TryFoldConstant(bin.Left, out var left) && TryFoldConstant(bin.Right, out var right):
                result = bin.Op switch
                {
                    TokenKind.Plus => left + right,
                    TokenKind.Minus => left - right,
                    TokenKind.Star => left * right,
                    TokenKind.Slash when right != 0 => left / right,
                    TokenKind.Percent when right != 0 => left % right,
                    TokenKind.Ampersand => left & right,
                    TokenKind.Pipe => left | right,
                    TokenKind.Caret => left ^ right,
                    TokenKind.ShiftLeft => left << (int)right,
                    TokenKind.ShiftRight => left >> (int)right,
                    _ => 0
                };
                return bin.Op is TokenKind.Plus or TokenKind.Minus or TokenKind.Star or
                    TokenKind.Slash or TokenKind.Percent or TokenKind.Ampersand or
                    TokenKind.Pipe or TokenKind.Caret or TokenKind.ShiftLeft or TokenKind.ShiftRight;
            case UnaryExpr { Op: TokenKind.Minus } neg when TryFoldConstant(neg.Operand, out var inner):
                result = -inner;
                return true;
            default:
                return false;
        }
    }

    public byte EvalConstExpr(ExprNode expr) => expr switch
    {
        IntLiteralExpr intLit => (byte)intLit.Value,
        MemberAccessExpr member => ResolveMemberConstant(member),
        _ => throw new InvalidOperationException($"Cannot evaluate constant: {expr.GetType().Name}")
    };

    /// <summary>
    /// Handle e.x where e is a for-each struct variable.
    /// Emits LDX idx; LDA fieldBase,X using zero-page indexed addressing.
    /// </summary>
    private bool TryResolveForEachStructField(MemberAccessExpr member)
    {
        if (ctx.ForEachStructVar is { } fesv &&
            member.Receiver is IdentifierExpr ident &&
            ident.Name == fesv.Name &&
            fesv.FieldBases.TryGetValue(member.MemberName, out var fieldBase))
        {
            ctx.Buffer.EmitLdxZeroPage(fesv.IndexZp);
            // LDA zp,X — zero-page indexed
            ctx.Buffer.EmitByte(0xB5); // LDA zp,X opcode
            ctx.Buffer.EmitByte(fieldBase);
            return true;
        }
        return false;
    }

    public bool TryResolveStructField(MemberAccessExpr member, out byte zpAddr)
    {
        zpAddr = 0;
        // Direct field: hero.health
        if (member.Receiver is IdentifierExpr ident &&
            ctx.Symbols.TryGetStructInstance(ident.Name, out var instance) &&
            instance.Fields.TryGetValue(member.MemberName, out zpAddr))
        {
            return true;
        }
        // Nested field chain: hero.pos.x → look up "hero" instance, key "pos.x"
        if (member.Receiver is MemberAccessExpr nested &&
            nested.Receiver is IdentifierExpr rootIdent &&
            ctx.Symbols.TryGetStructInstance(rootIdent.Name, out var rootInst) &&
            rootInst.Fields.TryGetValue($"{nested.MemberName}.{member.MemberName}", out zpAddr))
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
