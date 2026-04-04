namespace Cork.CodeGen.Emit;

using Cork.Language.Ast;

/// <summary>
/// Emits 6510 code for message sends including built-in intrinsics
/// (poke, pokeScreen, debugHex) and user-defined method calls.
/// </summary>
public sealed class IntrinsicEmitter(EmitContext ctx)
{
    public void EmitMessageSend(MessageSendStmt msgSend)
    {
        // Built-in intrinsic: poke: addr value: val
        if (msgSend.Receiver == null && msgSend.Segments.Count == 2 &&
            msgSend.Segments[0].Name == "poke" && msgSend.Segments[1].Name == "value")
        {
            EmitPoke(msgSend.Segments[0].Argument!, msgSend.Segments[1].Argument!);
            return;
        }

        // Built-in intrinsic: pokeScreen: wordOffset value: byteVal
        if (msgSend.Receiver == null && msgSend.Segments.Count == 2 &&
            msgSend.Segments[0].Name == "pokeScreen" && msgSend.Segments[1].Name == "value")
        {
            EmitPokeScreen(msgSend.Segments[0].Argument!, msgSend.Segments[1].Argument!);
            return;
        }

        // Built-in intrinsic: debugHex: screenAddr value: wordVar
        if (msgSend.Receiver == null && msgSend.Segments.Count == 2 &&
            msgSend.Segments[0].Name == "debugHex" && msgSend.Segments[1].Name == "value")
        {
            EmitDebugHex(msgSend.Segments[0].Argument!, msgSend.Segments[1].Argument!);
            return;
        }

        // Scene method call (no receiver)
        if (msgSend.Receiver == null && msgSend.Segments.Count > 0)
        {
            var selectorName = string.Join("", msgSend.Segments.Select(s => s.Name + ":"));
            var label = $"_method_{selectorName}";

            if (ctx.Symbols.TryGetMethodParams(selectorName, out var methodParams))
            {
                for (var i = 0; i < msgSend.Segments.Count; i++)
                {
                    if (msgSend.Segments[i].Argument != null && i < methodParams.Count && methodParams[i].ParamName != "")
                    {
                        ctx.Expressions.EmitExprToA(msgSend.Segments[i].Argument!);
                        ctx.Buffer.EmitStaZeroPage(ctx.Symbols.GetLocal(methodParams[i].ParamName));
                    }
                }
            }

            ctx.Buffer.EmitJsrForward(label);
            return;
        }

        // Struct method call: receiver methodName:;
        if (msgSend.Receiver is IdentifierExpr receiverIdent &&
            ctx.Symbols.TryGetStructInstance(receiverIdent.Name, out var inst))
        {
            var selectorName = string.Join("", msgSend.Segments.Select(s => s.Name + ":"));
            var label = $"_struct_{inst.StructType}_{selectorName}_{receiverIdent.Name}";

            if (!ctx.Symbols.IsStructMethodEmitted(label))
            {
                ctx.Symbols.MarkStructMethodEmitted(label);
            }

            ctx.Buffer.EmitJsrForward(label);
            return;
        }

        throw new InvalidOperationException("Unsupported message send");
    }

    public void EmitPoke(ExprNode addressExpr, ExprNode valueExpr)
    {
        if (addressExpr is BinaryExpr { Op: Language.Lexing.TokenKind.Plus } addExpr &&
            addExpr.Left is IntLiteralExpr baseAddr)
        {
            ctx.Expressions.EmitExprToA(addExpr.Right);
            ctx.Buffer.EmitTax();
            ctx.Expressions.EmitExprToA(valueExpr);
            ctx.Buffer.EmitStaAbsoluteX((ushort)baseAddr.Value);
        }
        else if (addressExpr is IntLiteralExpr constAddr)
        {
            ctx.Expressions.EmitExprToA(valueExpr);
            ctx.Buffer.EmitStaAbsolute((ushort)constAddr.Value);
        }
        else
        {
            throw new InvalidOperationException("poke address must be constant or constant + expression");
        }
    }

    public void EmitPokeScreen(ExprNode offsetExpr, ExprNode valueExpr)
    {
        if (offsetExpr is IdentifierExpr ident && ctx.Symbols.IsWordVar(ident.Name))
        {
            var zp = ctx.Symbols.GetLocal(ident.Name);
            ctx.Buffer.EmitLdaZeroPage(zp);
            ctx.Buffer.EmitClc();
            ctx.Buffer.EmitAdcImmediate(0x00);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
            ctx.Buffer.EmitLdaZeroPage((byte)(zp + 1));
            ctx.Buffer.EmitAdcImmediate(0x04);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);
            ctx.Expressions.EmitExprToA(valueExpr);
            ctx.Buffer.EmitLdyImmediate(0);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);
        }
        else
        {
            throw new InvalidOperationException("pokeScreen offset must be a word variable");
        }
    }

    public void EmitDebugHex(ExprNode addrExpr, ExprNode valueExpr)
    {
        ctx.Runtime.Add("debughex");
        var addr = (ushort)(((IntLiteralExpr)addrExpr).Value + 0x0400);

        if (valueExpr is IdentifierExpr ident && ctx.Symbols.IsWordVar(ident.Name))
        {
            var zp = ctx.Symbols.GetLocal(ident.Name);
            ctx.Buffer.EmitLdaZeroPage(zp);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Lo);
            ctx.Buffer.EmitLdaZeroPage((byte)(zp + 1));
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpFixedArg1Hi);
        }
        else
        {
            throw new InvalidOperationException("debugHex value must be a word/fixed/sfixed variable");
        }

        ctx.Buffer.EmitLdxImmediate((byte)(addr & 0xFF));
        ctx.Buffer.EmitLdyImmediate((byte)(addr >> 8));
        ctx.Buffer.EmitJsrForward("_rt_debughex");
    }

    public void EmitSpriteCopies(ProgramNode program, Dictionary<string, ushort> dataAddresses)
    {
        foreach (var decl in program.Declarations)
        {
            if (decl is ConstArrayDeclNode { Size: 63 } constArr &&
                dataAddresses.TryGetValue(constArr.Name, out var srcAddr))
            {
                var ptrName = constArr.Name.Replace("Data", "Ptr");
                byte? ptrValue = null;
                foreach (var gv in program.Declarations.OfType<GlobalVarDeclNode>())
                {
                    if (gv.Name == ptrName && gv.Initializer is IntLiteralExpr intLit)
                    {
                        ptrValue = (byte)intLit.Value;
                        break;
                    }
                }

                if (ptrValue == null) continue;

                var destAddr = (ushort)(ptrValue.Value * 64);

                ctx.Buffer.EmitLdxImmediate(62);
                ctx.Buffer.EmitLdaAbsoluteX(srcAddr);
                ctx.Buffer.EmitStaAbsoluteX(destAddr);
                ctx.Buffer.EmitDex();
                ctx.Buffer.EmitBpl(unchecked((sbyte)(-9)));
            }
        }
    }
}
