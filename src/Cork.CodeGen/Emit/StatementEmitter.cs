namespace Cork.CodeGen.Emit;

using Cork.Language.Ast;
using Cork.Language.Lexing;

/// <summary>
/// Emits 6510 code for statements: variable declarations, assignments, blocks.
/// </summary>
public sealed class StatementEmitter(EmitContext ctx)
{
    public void EmitStatement(StmtNode stmt)
    {
        switch (stmt)
        {
            case VarDeclStmt varDecl: EmitVarDecl(varDecl); break;
            case AssignmentStmt assign: EmitAssignment(assign); break;
            case WhileStmt whileStmt: ctx.ControlFlow.EmitWhile(whileStmt); break;
            case IfStmt ifStmt: ctx.ControlFlow.EmitIf(ifStmt); break;
            case ForStmt forStmt: ctx.ControlFlow.EmitFor(forStmt); break;
            case ForEachStmt forEach: ctx.ControlFlow.EmitForEach(forEach); break;
            case SwitchStmt switchStmt: ctx.ControlFlow.EmitSwitch(switchStmt); break;
            case MessageSendStmt msgSend: ctx.Intrinsics.EmitMessageSend(msgSend); break;
            case GoStmt goStmt: ctx.Buffer.EmitJmpForward($"_scene_{goStmt.SceneName}"); break;
            case ReturnStmt ret: EmitReturn(ret); break;
            case BreakStmt: ctx.ControlFlow.EmitBreak(); break;
            case ContinueStmt: ctx.ControlFlow.EmitContinue(); break;
            default: throw new InvalidOperationException($"Unsupported statement: {stmt.GetType().Name}");
        }
    }

    public void EmitBlock(BlockNode block)
    {
        foreach (var stmt in block.Statements)
        {
            EmitStatement(stmt);
            // Dead code elimination: stop emitting after terminal statements
            if (stmt is ReturnStmt or BreakStmt or ContinueStmt or GoStmt)
                break;
        }
    }

    private void EmitReturn(ReturnStmt ret)
    {
        if (ret.Value != null)
            ctx.Expressions.EmitExprToA(ret.Value);
        ctx.Buffer.EmitRts();
    }

    public void EmitVarDecl(VarDeclStmt decl)
    {
        if (decl.IsConst && decl.Initializer is IntLiteralExpr constLit)
        {
            ctx.Symbols.AddConstant(decl.Name, constLit.Value);
            return;
        }
        EmitTypedVarInit(decl.TypeName, decl.Name, decl.Initializer);
    }

    public void EmitTypedVarInit(string typeName, string name, ExprNode? initializer)
    {
        if (SymbolTable.Is16BitType(typeName))
        {
            var zp = ctx.Symbols.AllocWordZeroPage(name);
            ctx.Symbols.SetVarType(name, typeName);

            if (initializer is IdentifierExpr srcIdent && ctx.Symbols.IsWordVar(srcIdent.Name))
            {
                var srcZp = ctx.Symbols.GetLocal(srcIdent.Name);
                ctx.Buffer.EmitLdaZeroPage(srcZp);
                ctx.Buffer.EmitStaZeroPage(zp);
                ctx.Buffer.EmitLdaZeroPage((byte)(srcZp + 1));
                ctx.Buffer.EmitStaZeroPage((byte)(zp + 1));
            }
            else if (initializer is CastExpr { TargetType: "word" or "sword" } cast &&
                     cast.Operand is IdentifierExpr byteId && !ctx.Symbols.IsWordVar(byteId.Name))
            {
                // byte as word: widen — low byte = value, high byte = 0
                ctx.Buffer.EmitLdaZeroPage(ctx.Symbols.GetLocal(byteId.Name));
                ctx.Buffer.EmitStaZeroPage(zp);
                ctx.Buffer.EmitLdaImmediate(0);
                ctx.Buffer.EmitStaZeroPage((byte)(zp + 1));
            }
            else
            {
                var val = ExpressionEmitter.Resolve16BitInitializer(typeName, initializer);
                ctx.Buffer.EmitLdaImmediate((byte)(val & 0xFF));
                ctx.Buffer.EmitStaZeroPage(zp);
                ctx.Buffer.EmitLdaImmediate((byte)(val >> 8));
                ctx.Buffer.EmitStaZeroPage((byte)(zp + 1));
            }
        }
        else
        {
            var zp = ctx.Symbols.AllocZeroPage(name);
            ctx.Symbols.SetVarType(name, typeName);
            if (initializer != null)
            {
                ctx.Expressions.EmitExprToA(initializer);
                ctx.Buffer.EmitStaZeroPage(zp);
            }
            else
            {
                ctx.Buffer.EmitLdaImmediate(0);
                ctx.Buffer.EmitStaZeroPage(zp);
            }
        }
    }

    public void EmitAssignment(AssignmentStmt assign)
    {
        byte zp;
        var varName = "";
        if (assign.Target is IdentifierExpr ident)
        {
            zp = ctx.Symbols.GetLocal(ident.Name);
            varName = ident.Name;
        }
        else if (assign.Target is MemberAccessExpr member && ctx.Expressions.TryResolveStructField(member, out var fieldZp))
        {
            zp = fieldZp;
        }
        // Indexed struct array field: enemies[0].x = 5;
        else if (assign.Target is MemberAccessExpr { Receiver: IndexExpr { Receiver: IdentifierExpr arrIdent, Index: IntLiteralExpr idxLit } } arrMember &&
                 ctx.Symbols.TryGetStructArray(arrIdent.Name, out var arrInfo) &&
                 arrInfo.FieldBases.TryGetValue(arrMember.MemberName, out var fieldBase))
        {
            zp = (byte)(fieldBase + (int)idxLit.Value);
        }
        else
        {
            throw new InvalidOperationException("Unsupported assignment target");
        }

        if (varName != "" && ctx.Symbols.IsWordVar(varName))
        {
            EmitWordAssignment(zp, assign.Op, assign.Value);
            return;
        }

        switch (assign.Op)
        {
            case TokenKind.Equal:
                ctx.Expressions.EmitExprToA(assign.Value);
                ctx.Buffer.EmitStaZeroPage(zp);
                break;
            case TokenKind.PlusEqual:
                ctx.Buffer.EmitLdaZeroPage(zp);
                ctx.Buffer.EmitClc();
                ctx.Expressions.EmitAdcValue(assign.Value);
                ctx.Buffer.EmitStaZeroPage(zp);
                break;
            case TokenKind.MinusEqual:
                ctx.Buffer.EmitLdaZeroPage(zp);
                ctx.Buffer.EmitSec();
                ctx.Expressions.EmitSbcValue(assign.Value);
                ctx.Buffer.EmitStaZeroPage(zp);
                break;
            default:
                throw new InvalidOperationException($"Phase 1: unsupported assignment op {assign.Op}");
        }
    }

    public void EmitWordAssignment(byte zpLo, TokenKind op, ExprNode value)
    {
        var zpHi = (byte)(zpLo + 1);

        if (value is IdentifierExpr varExpr && ctx.Symbols.IsWordVar(varExpr.Name))
        {
            var srcZp = ctx.Symbols.GetLocal(varExpr.Name);
            var srcHi = (byte)(srcZp + 1);
            if (op == TokenKind.PlusEqual)
            {
                ctx.Buffer.EmitLdaZeroPage(zpLo);
                ctx.Buffer.EmitClc();
                ctx.Buffer.EmitAdcZeroPage(srcZp);
                ctx.Buffer.EmitStaZeroPage(zpLo);
                ctx.Buffer.EmitLdaZeroPage(zpHi);
                ctx.Buffer.EmitAdcZeroPage(srcHi);
                ctx.Buffer.EmitStaZeroPage(zpHi);
            }
            else if (op == TokenKind.MinusEqual)
            {
                ctx.Buffer.EmitLdaZeroPage(zpLo);
                ctx.Buffer.EmitSec();
                ctx.Buffer.EmitSbcZeroPage(srcZp);
                ctx.Buffer.EmitStaZeroPage(zpLo);
                ctx.Buffer.EmitLdaZeroPage(zpHi);
                ctx.Buffer.EmitSbcZeroPage(srcHi);
                ctx.Buffer.EmitStaZeroPage(zpHi);
            }
            else if (op == TokenKind.Equal)
            {
                ctx.Buffer.EmitLdaZeroPage(srcZp);
                ctx.Buffer.EmitStaZeroPage(zpLo);
                ctx.Buffer.EmitLdaZeroPage(srcHi);
                ctx.Buffer.EmitStaZeroPage(zpHi);
            }
            return;
        }

        var val = ExpressionEmitter.Resolve16BitInitializer("", value);

        if (op == TokenKind.Equal)
        {
            ctx.Buffer.EmitLdaImmediate((byte)(val & 0xFF));
            ctx.Buffer.EmitStaZeroPage(zpLo);
            ctx.Buffer.EmitLdaImmediate((byte)(val >> 8));
            ctx.Buffer.EmitStaZeroPage(zpHi);
        }
        else if (op == TokenKind.PlusEqual)
        {
            ctx.Buffer.EmitLdaZeroPage(zpLo);
            ctx.Buffer.EmitClc();
            ctx.Buffer.EmitAdcImmediate((byte)(val & 0xFF));
            ctx.Buffer.EmitStaZeroPage(zpLo);
            ctx.Buffer.EmitLdaZeroPage(zpHi);
            ctx.Buffer.EmitAdcImmediate((byte)(val >> 8));
            ctx.Buffer.EmitStaZeroPage(zpHi);
        }
        else if (op == TokenKind.MinusEqual)
        {
            ctx.Buffer.EmitLdaZeroPage(zpLo);
            ctx.Buffer.EmitSec();
            ctx.Buffer.EmitSbcImmediate((byte)(val & 0xFF));
            ctx.Buffer.EmitStaZeroPage(zpLo);
            ctx.Buffer.EmitLdaZeroPage(zpHi);
            ctx.Buffer.EmitSbcImmediate((byte)(val >> 8));
            ctx.Buffer.EmitStaZeroPage(zpHi);
        }
        else if (op == TokenKind.StarEqual)
        {
            var varType = ctx.Symbols.GetVarTypeForZp(zpLo);
            var isSigned = varType is "sfixed" or "sword";
            ctx.Runtime.Add("mul8x8");
            ctx.Runtime.Add(isSigned ? "sfixmul" : "fixmul");
            ctx.Buffer.EmitLdaZeroPage(zpLo);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitLdaZeroPage(zpHi);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Hi);

            if (value is IdentifierExpr mulVar && ctx.Symbols.IsWordVar(mulVar.Name))
            {
                var srcZp = ctx.Symbols.GetLocal(mulVar.Name);
                ctx.Buffer.EmitLdaZeroPage(srcZp);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg2Lo);
                ctx.Buffer.EmitLdaZeroPage((byte)(srcZp + 1));
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg2Hi);
            }
            else
            {
                var mulVal = ExpressionEmitter.Resolve16BitInitializer("", value);
                ctx.Buffer.EmitLdaImmediate((byte)(mulVal & 0xFF));
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg2Lo);
                ctx.Buffer.EmitLdaImmediate((byte)(mulVal >> 8));
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg2Hi);
            }

            ctx.Buffer.EmitJsrForward(isSigned ? "_rt_sfixmul" : "_rt_fixmul");

            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedResB1);
            ctx.Buffer.EmitStaZeroPage(zpLo);
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedResB2);
            ctx.Buffer.EmitStaZeroPage(zpHi);
        }
        else if (op == TokenKind.ShiftRightEqual && value is IntLiteralExpr shrLit)
        {
            for (var i = 0; i < (int)shrLit.Value; i++)
            {
                ctx.Buffer.EmitLsrZeroPage(zpHi);
                ctx.Buffer.EmitRorZeroPage(zpLo);
            }
        }
        else if (op == TokenKind.ShiftLeftEqual && value is IntLiteralExpr shlLit)
        {
            for (var i = 0; i < (int)shlLit.Value; i++)
            {
                ctx.Buffer.EmitAslZeroPage(zpLo);
                ctx.Buffer.EmitRolZeroPage(zpHi);
            }
        }
        else
        {
            throw new InvalidOperationException($"Unsupported word assignment: {op}");
        }
    }
}
