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
            case GoStmt goStmt: EmitGo(goStmt); break;
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

    private void EmitGo(GoStmt goStmt)
    {
        // Clear sprite VIC-II registers dirtied by this scene before leaving
        if (ctx.DirtySpriteRegs.Count > 0)
        {
            ctx.Buffer.EmitLdaImmediate(0);
            foreach (var reg in ctx.DirtySpriteRegs)
                ctx.Buffer.EmitStaAbsolute(reg);
        }
        ctx.Buffer.EmitJmpForward($"_scene_{goStmt.SceneName}");
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
        // For-each struct field assignment: bare field name inside method
        if (assign.Target is IdentifierExpr feFieldIdent &&
            ctx.ForEachStructVar is { } fesv &&
            fesv.FieldBases.TryGetValue(feFieldIdent.Name, out var feFieldBase))
        {
            ctx.Expressions.EmitExprToA(assign.Value);
            ctx.Buffer.EmitLdxZeroPage(fesv.IndexZp);
            ctx.Buffer.EmitByte(0x95); ctx.Buffer.EmitByte(feFieldBase); // STA zp,X
            return;
        }

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
            // Check if the resolved field is a word-width variable (e.g., sprite X position)
            var fieldVarType = ctx.Symbols.GetVarTypeForZp(zp);
            if (fieldVarType != null && SymbolTable.Is16BitType(fieldVarType))
                varName = member.Receiver is IdentifierExpr ri ? $"{ri.Name}${member.MemberName}" : "";
        }
        // Indexed struct array field: enemies[0].x = 5;
        else if (assign.Target is MemberAccessExpr { Receiver: IndexExpr { Receiver: IdentifierExpr arrIdent, Index: IntLiteralExpr idxLit } } arrMember &&
                 ctx.Symbols.TryGetStructArray(arrIdent.Name, out var arrInfo) &&
                 arrInfo.FieldBases.TryGetValue(arrMember.MemberName, out var fieldBase))
        {
            zp = (byte)(fieldBase + (int)idxLit.Value);
        }
        // String indexed assignment: score[8] = 49;
        else if (assign.Target is IndexExpr { Receiver: IdentifierExpr strIdent, Index: IntLiteralExpr strIdx } &&
                 ctx.Symbols.TryGetStringVar(strIdent.Name, out var strInfo))
        {
            zp = (byte)(strInfo.ZpBase + (int)strIdx.Value);
        }
        else
        {
            throw new InvalidOperationException("Unsupported assignment target");
        }

        if (varName != "" && ctx.Symbols.IsWordVar(varName))
        {
            EmitWordAssignment(zp, assign.Op, assign.Value);
            // Sprite auto-sync for word X: write low byte + update MSB in $D010
            if (ctx.Symbols.TryGetSpriteSync(zp, out var wordVicReg))
            {
                var sprIdx = (wordVicReg - 0xD000) / 2;
                var sprBit = (byte)(1 << sprIdx);
                // Write X low byte to VIC-II
                ctx.Buffer.EmitLdaZeroPage(zp);
                ctx.Buffer.EmitStaAbsolute(wordVicReg);
                // Update MSB bit: check high byte, set or clear bit in $D010
                var endLabel = ctx.NextLabel("_xmsb_end");
                var setLabel = ctx.NextLabel("_xmsb_set");
                ctx.Buffer.EmitLdaZeroPage((byte)(zp + 1));
                // BNE → set path: skip clear(3+2=5) + JMP(3) = 8 bytes
                ctx.Buffer.EmitBne(8);
                // Clear bit: LDA $D010; AND #~bit; JMP end
                ctx.Buffer.EmitLdaAbsolute(0xD010);
                ctx.Buffer.EmitByte(0x29); ctx.Buffer.EmitByte((byte)~sprBit);
                ctx.Buffer.EmitJmpForward(endLabel);
                // Set bit: LDA $D010; ORA #bit
                ctx.Buffer.EmitLdaAbsolute(0xD010);
                ctx.Buffer.EmitByte(0x09); ctx.Buffer.EmitByte(sprBit);
                // Store result
                ctx.Buffer.DefineLabel(endLabel);
                ctx.Buffer.EmitStaAbsolute(0xD010);
            }
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
            case TokenKind.StarEqual:
                ctx.Runtime.Add("mul8x8");
                ctx.Buffer.EmitLdaZeroPage(zp);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
                ctx.Expressions.EmitExprToA(assign.Value);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
                ctx.Buffer.EmitJsrForward("_rt_mul8x8");
                ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpMulResultLo);
                ctx.Buffer.EmitStaZeroPage(zp);
                break;
            case TokenKind.SlashEqual:
                ctx.Runtime.Add("div8");
                ctx.Buffer.EmitLdaZeroPage(zp);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
                ctx.Expressions.EmitExprToA(assign.Value);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
                ctx.Buffer.EmitJsrForward("_rt_div8");
                ctx.Buffer.EmitStaZeroPage(zp); // A = quotient
                break;
            case TokenKind.PercentEqual:
                ctx.Runtime.Add("div8");
                ctx.Buffer.EmitLdaZeroPage(zp);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
                ctx.Expressions.EmitExprToA(assign.Value);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
                ctx.Buffer.EmitJsrForward("_rt_div8");
                ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpDivRemainder);
                ctx.Buffer.EmitStaZeroPage(zp); // remainder
                break;
            default:
                throw new InvalidOperationException($"Phase 1: unsupported assignment op {assign.Op}");
        }

        // Sprite auto-sync: if this ZP has a paired VIC-II register, update it
        if (ctx.Symbols.TryGetSpriteSync(zp, out var vicReg))
        {
            ctx.Buffer.EmitLdaZeroPage(zp);
            ctx.Buffer.EmitStaAbsolute(vicReg);
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
            if (varType is "fixed" or "sfixed")
            {
                // Fixed-point 8.8 × 8.8 → 8.8 multiply
                var isSigned = varType == "sfixed";
                ctx.Runtime.Add("mul8x8");
                ctx.Runtime.Add(isSigned ? "sfixmul" : "fixmul");
                EmitLoad16ToFixedArgs(zpLo, zpHi, value, 1);
                ctx.Buffer.EmitJsrForward(isSigned ? "_rt_sfixmul" : "_rt_fixmul");
                ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedResB1);
                ctx.Buffer.EmitStaZeroPage(zpLo);
                ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedResB2);
                ctx.Buffer.EmitStaZeroPage(zpHi);
            }
            else
            {
                // Word 16×8 → 16 multiply (multiply by byte value)
                ctx.Runtime.Add("mul8x8");
                ctx.Runtime.Add("mul16x8");
                ctx.Buffer.EmitLdaZeroPage(zpLo);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Lo);
                ctx.Buffer.EmitLdaZeroPage(zpHi);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Hi);
                ctx.Expressions.EmitExprToA(value);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
                ctx.Buffer.EmitJsrForward("_rt_mul16x8");
                ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
                ctx.Buffer.EmitStaZeroPage(zpLo);
                ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
                ctx.Buffer.EmitStaZeroPage(zpHi);
            }
        }
        else if (op == TokenKind.SlashEqual)
        {
            var varType = ctx.Symbols.GetVarTypeForZp(zpLo);
            if (varType is "fixed" or "sfixed")
            {
                var isSigned = varType == "sfixed";
                ctx.Runtime.Add("fixdiv");
                if (isSigned) ctx.Runtime.Add("sfixdiv");
                // Load dividend (arg1) and divisor (arg2)
                EmitLoad16ToFixedArgs(zpLo, zpHi, value, 1);
                ctx.Buffer.EmitJsrForward(isSigned ? "_rt_sfixdiv" : "_rt_fixdiv");
                // Result: quotient hi in ZpFixedArg1Hi, lo in ZpFixedArg1Lo
                // 8.8 format: hi = integer part, lo = fractional part
                ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
                ctx.Buffer.EmitStaZeroPage(zpLo);
                ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
                ctx.Buffer.EmitStaZeroPage(zpHi);
                return; // skip word path
            }
            // Word 16÷8 → 16 quotient
            ctx.Runtime.Add("div16x8");
            ctx.Buffer.EmitLdaZeroPage(zpLo);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitLdaZeroPage(zpHi);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Hi);
            ctx.Expressions.EmitExprToA(value);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
            ctx.Buffer.EmitJsrForward("_rt_div16x8");
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitStaZeroPage(zpLo);
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpFixedArg1Hi);
            ctx.Buffer.EmitStaZeroPage(zpHi);
        }
        else if (op == TokenKind.PercentEqual)
        {
            // Word 16÷8 → remainder in A
            ctx.Runtime.Add("div16x8");
            ctx.Buffer.EmitLdaZeroPage(zpLo);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitLdaZeroPage(zpHi);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Hi);
            ctx.Expressions.EmitExprToA(value);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
            ctx.Buffer.EmitJsrForward("_rt_div16x8");
            // Remainder is in ZpDivRemainder, store as word (lo only, hi=0)
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpDivRemainder);
            ctx.Buffer.EmitStaZeroPage(zpLo);
            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitStaZeroPage(zpHi);
        }
        else if (op is TokenKind.AmpEqual or TokenKind.PipeEqual or TokenKind.CaretEqual)
        {
            // Word bitwise: operate on both bytes
            var immOp = op switch
            {
                TokenKind.AmpEqual => (byte)0x29,  // AND
                TokenKind.PipeEqual => (byte)0x09,  // ORA
                _ => (byte)0x49                      // EOR
            };
            var zpOp = op switch
            {
                TokenKind.AmpEqual => (byte)0x25,
                TokenKind.PipeEqual => (byte)0x05,
                _ => (byte)0x45
            };
            if (ctx.Expressions.TryFoldConstant(value, out var bwVal))
            {
                var lo = (byte)(bwVal & 0xFF);
                var hi = (byte)(bwVal >> 8);
                ctx.Buffer.EmitLdaZeroPage(zpLo);
                ctx.Buffer.EmitByte(immOp); ctx.Buffer.EmitByte(lo);
                ctx.Buffer.EmitStaZeroPage(zpLo);
                ctx.Buffer.EmitLdaZeroPage(zpHi);
                ctx.Buffer.EmitByte(immOp); ctx.Buffer.EmitByte(hi);
                ctx.Buffer.EmitStaZeroPage(zpHi);
            }
            else
            {
                throw new InvalidOperationException("Word bitwise assignment requires constant operand");
            }
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

    private void EmitLoad16ToFixedArgs(byte zpLo, byte zpHi, ExprNode value, int argNum)
    {
        var dstLo = argNum == 1 ? EmitContext.ZpFixedArg1Lo : EmitContext.ZpFixedArg2Lo;
        var dstHi = argNum == 1 ? EmitContext.ZpFixedArg1Hi : EmitContext.ZpFixedArg2Hi;

        ctx.Buffer.EmitLdaZeroPage(zpLo);
        ctx.Buffer.EmitStaZeroPage(dstLo);
        ctx.Buffer.EmitLdaZeroPage(zpHi);
        ctx.Buffer.EmitStaZeroPage(dstHi);

        if (value is IdentifierExpr mulVar && ctx.Symbols.IsWordVar(mulVar.Name))
        {
            var srcZp = ctx.Symbols.GetLocal(mulVar.Name);
            ctx.Buffer.EmitLdaZeroPage(srcZp);
            ctx.Buffer.EmitStaZeroPage(argNum == 1 ? EmitContext.ZpFixedArg2Lo : EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitLdaZeroPage((byte)(srcZp + 1));
            ctx.Buffer.EmitStaZeroPage(argNum == 1 ? EmitContext.ZpFixedArg2Hi : EmitContext.ZpFixedArg1Hi);
        }
        else
        {
            var v = ExpressionEmitter.Resolve16BitInitializer("", value);
            ctx.Buffer.EmitLdaImmediate((byte)(v & 0xFF));
            ctx.Buffer.EmitStaZeroPage(argNum == 1 ? EmitContext.ZpFixedArg2Lo : EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitLdaImmediate((byte)(v >> 8));
            ctx.Buffer.EmitStaZeroPage(argNum == 1 ? EmitContext.ZpFixedArg2Hi : EmitContext.ZpFixedArg1Hi);
        }
    }
}
