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

        // Built-in intrinsic: printAt: screenPos text: constArrayName
        if (msgSend.Receiver == null && msgSend.Segments.Count == 2 &&
            msgSend.Segments[0].Name == "printAt" && msgSend.Segments[1].Name == "text")
        {
            EmitPrintAt(msgSend.Segments[0].Argument!, msgSend.Segments[1].Argument!);
            return;
        }

        // Built-in intrinsic: peek: addr — read byte from memory address
        if (msgSend.Receiver == null && msgSend.Segments.Count == 1 &&
            msgSend.Segments[0].Name == "peek")
        {
            EmitPeek(msgSend.Segments[0].Argument!);
            return;
        }

        // Built-in intrinsic: random: — read SID noise register ($D41B)
        if (msgSend.Receiver == null && msgSend.Segments.Count == 1 &&
            msgSend.Segments[0].Name == "random" && msgSend.Segments[0].Argument == null)
        {
            ctx.Runtime.Add("random");
            ctx.Buffer.EmitLdaAbsolute(0xD41B);
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
                    if (msgSend.Segments[i].Argument == null || i >= methodParams.Count || methodParams[i].ParamName == "")
                        continue;

                    var arg = msgSend.Segments[i].Argument!;
                    var param = methodParams[i];

                    // String reference parameter: pass pointer + length
                    if ((param.TypeName == "string" || param.TypeName.EndsWith("[]")) &&
                        ctx.Symbols.TryGetRefParam(param.ParamName, out var refInfo))
                    {
                        if (arg is IdentifierExpr strIdent && ctx.Symbols.TryGetStringVar(strIdent.Name, out var strVar))
                        {
                            // String variable: pointer = ZP base ($00xx)
                            ctx.Buffer.EmitLdaImmediate(strVar.ZpBase);
                            ctx.Buffer.EmitStaZeroPage(refInfo.PtrLo);
                            ctx.Buffer.EmitLdaImmediate(0);
                            ctx.Buffer.EmitStaZeroPage(refInfo.PtrHi);
                            ctx.Buffer.EmitLdaImmediate((byte)strVar.Length);
                            ctx.Buffer.EmitStaZeroPage(refInfo.LenZp);
                        }
                        else if (arg is StringLiteralExpr strLit)
                        {
                            // String literal: pointer = data section address
                            var dataName = $"_str_{strLit.Value.GetHashCode():X8}";
                            if (ctx.DataAddresses.TryGetValue(dataName, out var dataAddr))
                            {
                                ctx.Buffer.EmitLdaImmediate((byte)(dataAddr & 0xFF));
                                ctx.Buffer.EmitStaZeroPage(refInfo.PtrLo);
                                ctx.Buffer.EmitLdaImmediate((byte)(dataAddr >> 8));
                                ctx.Buffer.EmitStaZeroPage(refInfo.PtrHi);
                                ctx.Buffer.EmitLdaImmediate((byte)strLit.Value.Length);
                                ctx.Buffer.EmitStaZeroPage(refInfo.LenZp);
                            }
                        }
                        else if (arg is IdentifierExpr arrIdent &&
                                 ctx.DataAddresses.TryGetValue(arrIdent.Name, out var arrAddr))
                        {
                            // Const array: pointer = data section address
                            ctx.Buffer.EmitLdaImmediate((byte)(arrAddr & 0xFF));
                            ctx.Buffer.EmitStaZeroPage(refInfo.PtrLo);
                            ctx.Buffer.EmitLdaImmediate((byte)(arrAddr >> 8));
                            ctx.Buffer.EmitStaZeroPage(refInfo.PtrHi);
                            ctx.Buffer.EmitLdaImmediate((byte)ctx.GetConstArraySize(arrIdent.Name));
                            ctx.Buffer.EmitStaZeroPage(refInfo.LenZp);
                        }
                    }
                    else
                    {
                        // Regular byte/word parameter
                        ctx.Expressions.EmitExprToA(arg);
                        ctx.Buffer.EmitStaZeroPage(ctx.Symbols.GetLocal(param.ParamName));
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

        // For-each struct method call: e draw:; where e is the loop variable
        if (msgSend.Receiver is IdentifierExpr feIdent &&
            ctx.ForEachStructVar is { } fesv && feIdent.Name == fesv.Name)
        {
            var selectorName = string.Join("", msgSend.Segments.Select(s => s.Name + ":"));
            // Find the method in the struct type
            if (ctx.Symbols.TryGetStructType(fesv.StructType, out var structDecl))
            {
                var method = structDecl.Methods.FirstOrDefault(m => m.SelectorName == selectorName);
                if (method != null)
                {
                    // Emit method body inline — field access resolves via ForEachStructVar context
                    ctx.Statements.EmitBlock(method.Body);
                    return;
                }
            }
        }

        throw new CompileError("Unsupported message send", msgSend.Location);
    }

    /// <summary>
    /// peek: addr — reads a byte from memory into A.
    /// Supports constant address, constant + expr, and word variable.
    /// </summary>
    public void EmitPeek(ExprNode addressExpr)
    {
        // Constant fold
        if (ctx.Expressions.TryFoldConstant(addressExpr, out var foldedAddr))
            addressExpr = new IntLiteralExpr(foldedAddr, addressExpr.Location);

        if (addressExpr is BinaryExpr { Op: Language.Lexing.TokenKind.Plus } preAdd &&
            preAdd.Left is not IntLiteralExpr &&
            ctx.Expressions.TryFoldConstant(preAdd.Left, out var foldedBase))
        {
            addressExpr = new BinaryExpr(
                new IntLiteralExpr(foldedBase, preAdd.Left.Location),
                Language.Lexing.TokenKind.Plus,
                preAdd.Right,
                preAdd.Location);
        }

        if (addressExpr is BinaryExpr { Op: Language.Lexing.TokenKind.Plus } addExpr &&
            addExpr.Left is IntLiteralExpr baseAddr)
        {
            // peek: (constant + expr) → LDA base,X
            ctx.Expressions.EmitExprToA(addExpr.Right);
            ctx.Buffer.EmitTax();
            ctx.Buffer.EmitLdaAbsoluteX((ushort)baseAddr.Value);
        }
        else if (addressExpr is IntLiteralExpr constAddr)
        {
            // peek: constant → LDA absolute
            ctx.Buffer.EmitLdaAbsolute((ushort)constAddr.Value);
        }
        else if (addressExpr is IdentifierExpr addrIdent && ctx.Symbols.IsWordVar(addrIdent.Name))
        {
            // peek: wordVar → LDA ($FB),Y
            var zp = ctx.Symbols.GetLocal(addrIdent.Name);
            ctx.Buffer.EmitLdaZeroPage(zp);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
            ctx.Buffer.EmitLdaZeroPage((byte)(zp + 1));
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);
            ctx.Buffer.EmitLdyImmediate(0);
            ctx.Buffer.EmitLdaIndirectY(EmitContext.ZpPointerLo);
        }
        else
        {
            throw new CompileError("peek address must be constant, constant + expr, or word variable", addressExpr.Location);
        }
    }

    public void EmitPoke(ExprNode addressExpr, ExprNode valueExpr)
    {
        // Try to fold the entire address to a constant first
        if (ctx.Expressions.TryFoldConstant(addressExpr, out var foldedAddr))
            addressExpr = new IntLiteralExpr(foldedAddr, addressExpr.Location);

        // For binary add, try to fold the left side to a constant base
        if (addressExpr is BinaryExpr { Op: Language.Lexing.TokenKind.Plus } preAdd &&
            preAdd.Left is not IntLiteralExpr &&
            ctx.Expressions.TryFoldConstant(preAdd.Left, out var foldedBase))
        {
            addressExpr = new BinaryExpr(
                new IntLiteralExpr(foldedBase, preAdd.Left.Location),
                Language.Lexing.TokenKind.Plus,
                preAdd.Right,
                preAdd.Location);
        }

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
        // poke: wordVar value: val — indirect indexed via ($FB),Y
        else if (addressExpr is IdentifierExpr addrIdent && ctx.Symbols.IsWordVar(addrIdent.Name))
        {
            var zp = ctx.Symbols.GetLocal(addrIdent.Name);
            ctx.Buffer.EmitLdaZeroPage(zp);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
            ctx.Buffer.EmitLdaZeroPage((byte)(zp + 1));
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);
            ctx.Expressions.EmitExprToA(valueExpr);
            ctx.Buffer.EmitLdyImmediate(0);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);
        }
        // poke: (wordVar + offset) value: val — compute address, use indirect
        else if (addressExpr is BinaryExpr { Op: Language.Lexing.TokenKind.Plus } wordAdd &&
                 wordAdd.Left is IdentifierExpr wordBase && ctx.Symbols.IsWordVar(wordBase.Name))
        {
            var zp = ctx.Symbols.GetLocal(wordBase.Name);
            // Load word address + offset into pointer
            ctx.Expressions.EmitExprToA(wordAdd.Right);
            ctx.Buffer.EmitClc();
            ctx.Buffer.EmitAdcZeroPage(zp);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
            ctx.Buffer.EmitLdaZeroPage((byte)(zp + 1));
            ctx.Buffer.EmitAdcImmediate(0);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);
            ctx.Expressions.EmitExprToA(valueExpr);
            ctx.Buffer.EmitLdyImmediate(0);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);
        }
        else
        {
            throw new CompileError("poke address must be constant, constant + expr, or word variable", addressExpr.Location);
        }
    }

    /// <summary>
    /// printAt: screenPos text: constArrayName
    /// Writes a const byte array to screen RAM at $0400 + screenPos.
    /// </summary>
    private void EmitPrintAt(ExprNode posExpr, ExprNode textExpr)
    {
        ushort dataAddr;
        int arraySize;

        if (textExpr is StringLiteralExpr strLit)
        {
            // String literal: look up pre-registered string data
            var dataName = $"_str_{strLit.Value.GetHashCode():X8}";
            if (!ctx.DataAddresses.TryGetValue(dataName, out var strAddr))
                throw new InvalidOperationException($"String not pre-registered: \"{strLit.Value}\"");
            dataAddr = strAddr;
            arraySize = strLit.Value.Length;
        }
        else if (textExpr is IdentifierExpr textIdent)
        {
            // Check string variable first
            if (ctx.Symbols.TryGetStringVar(textIdent.Name, out var strInfo))
            {
                // String variable: loop over ZP bytes
                long strPos = 0;
                if (posExpr is IntLiteralExpr pl) strPos = pl.Value;
                else if (ctx.Expressions.TryFoldConstant(posExpr, out var fp)) strPos = fp;

                var sa = (ushort)(0x0400 + strPos);
                ctx.Buffer.EmitLdxImmediate(0);
                // LDA zp,X = $B5 zp (2 bytes)
                ctx.Buffer.EmitByte(0xB5); ctx.Buffer.EmitByte(strInfo.ZpBase);
                ctx.Buffer.EmitStaAbsoluteX(sa);
                ctx.Buffer.EmitInx();
                ctx.Buffer.EmitCpxImmediate((byte)strInfo.Length);
                // Loop: LDA zp,X(2) + STA abs,X(3) + INX(1) + CPX(2) + BNE(2) = 10
                ctx.Buffer.EmitBne(unchecked((sbyte)(-10)));
                return;
            }
            // Const array
            if (ctx.DataAddresses.TryGetValue(textIdent.Name, out var addr2))
            {
                dataAddr = addr2;
                arraySize = ctx.GetConstArraySize(textIdent.Name);
            }
            else
            {
                throw new InvalidOperationException($"Unknown string or array: {textIdent.Name}");
            }
        }
        else
        {
            throw new InvalidOperationException("printAt text must be a string literal, variable, or const array");
        }

        // Calculate screen address: $0400 + pos
        long pos = 0;
        if (posExpr is IntLiteralExpr posLit)
            pos = posLit.Value;
        else if (ctx.Expressions.TryFoldConstant(posExpr, out var foldedPos))
            pos = foldedPos;

        var screenAddr = (ushort)(0x0400 + pos);

        // LDX #0; loop: LDA data,X; STA screen,X; INX; CPX #len; BNE loop
        ctx.Buffer.EmitLdxImmediate(0);
        ctx.Buffer.EmitLdaAbsoluteX(dataAddr);
        ctx.Buffer.EmitStaAbsoluteX(screenAddr);
        ctx.Buffer.EmitInx();
        ctx.Buffer.EmitCpxImmediate((byte)arraySize);
        ctx.Buffer.EmitBne(unchecked((sbyte)(-11)));
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
                // Try multiple naming conventions to find the sprite pointer
                string[] ptrNames = [
                    constArr.Name.Replace("Data", "Ptr"),
                    constArr.Name.EndsWith("Sprite") ? constArr.Name[..^6] + "Ptr" : "",
                    "spritePtr"
                ];
                byte? ptrValue = null;
                foreach (var tryName in ptrNames)
                {
                    if (tryName == "") continue;
                    foreach (var gv in program.Declarations.OfType<GlobalVarDeclNode>())
                    {
                        if (gv.Name == tryName && gv.Initializer is IntLiteralExpr intLit)
                        {
                            ptrValue = (byte)intLit.Value;
                            break;
                        }
                    }
                    if (ptrValue != null) break;
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
