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

        // Built-in intrinsic: printAt: screenPos char: screenCode
        if (msgSend.Receiver == null && msgSend.Segments.Count == 2 &&
            msgSend.Segments[0].Name == "printAt" && msgSend.Segments[1].Name == "char")
        {
            EmitPrintAtChar(msgSend.Segments[0].Argument!, msgSend.Segments[1].Argument!);
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

        // Built-in intrinsic: plotPixel: x y: y — set pixel in bitmap mode
        if (msgSend.Receiver == null && msgSend.Segments.Count == 2 &&
            msgSend.Segments[0].Name == "plotPixel" && msgSend.Segments[1].Name == "y")
        {
            if (!ctx.IsBitmapMode)
                throw new CompileError("plotPixel requires bitmap mode (hardware { mode: bitmap; })",
                    msgSend.Location);
            ctx.Runtime.Add("plotPixel");
            EmitPlotPixelCall(msgSend.Segments[0].Argument!, msgSend.Segments[1].Argument!, setPixel: true);
            return;
        }

        // Built-in intrinsic: clearPixel: x y: y — clear pixel in bitmap mode
        if (msgSend.Receiver == null && msgSend.Segments.Count == 2 &&
            msgSend.Segments[0].Name == "clearPixel" && msgSend.Segments[1].Name == "y")
        {
            if (!ctx.IsBitmapMode)
                throw new CompileError("clearPixel requires bitmap mode (hardware { mode: bitmap; })",
                    msgSend.Location);
            ctx.Runtime.Add("plotPixel");
            EmitPlotPixelCall(msgSend.Segments[0].Argument!, msgSend.Segments[1].Argument!, setPixel: false);
            return;
        }

        // Scene method call (no receiver)
        if (msgSend.Receiver == null && msgSend.Segments.Count > 0)
        {
            var selectorName = string.Join("", msgSend.Segments.Select(s => s.Name + ":"));
            var label = $"_method_{selectorName}";

            if (ctx.Symbols.TryGetMethodParams(selectorName, out var methodParams) &&
                ctx.Symbols.TryGetMethodParamZp(selectorName, out var paramZpMap))
            {
                for (var i = 0; i < msgSend.Segments.Count; i++)
                {
                    if (msgSend.Segments[i].Argument == null || i >= methodParams.Count || methodParams[i].ParamName == "")
                        continue;

                    var arg = msgSend.Segments[i].Argument!;
                    var param = methodParams[i];

                    // String/array reference parameter: pass pointer + length
                    if ((param.TypeName == "string" || param.TypeName.EndsWith("[]")) &&
                        paramZpMap.TryGetValue($"{param.ParamName}$ptr_lo", out var ptrLo))
                    {
                        var ptrHi = paramZpMap[$"{param.ParamName}$ptr_hi"];
                        var lenZp = paramZpMap[$"{param.ParamName}$len"];
                        if (arg is IdentifierExpr strIdent && ctx.Symbols.TryGetStringVar(strIdent.Name, out var strVar))
                        {
                            ctx.Buffer.EmitLdaImmediate(strVar.ZpBase);
                            ctx.Buffer.EmitStaZeroPage(ptrLo);
                            ctx.Buffer.EmitLdaImmediate(0);
                            ctx.Buffer.EmitStaZeroPage(ptrHi);
                            ctx.Buffer.EmitLdaImmediate((byte)strVar.Length);
                            ctx.Buffer.EmitStaZeroPage(lenZp);
                        }
                        else if (arg is StringLiteralExpr strLit)
                        {
                            var dataName = $"_str_{strLit.Value.GetHashCode():X8}";
                            if (ctx.DataAddresses.TryGetValue(dataName, out var dataAddr))
                            {
                                ctx.Buffer.EmitLdaImmediate((byte)(dataAddr & 0xFF));
                                ctx.Buffer.EmitStaZeroPage(ptrLo);
                                ctx.Buffer.EmitLdaImmediate((byte)(dataAddr >> 8));
                                ctx.Buffer.EmitStaZeroPage(ptrHi);
                                ctx.Buffer.EmitLdaImmediate((byte)strLit.Value.Length);
                                ctx.Buffer.EmitStaZeroPage(lenZp);
                            }
                        }
                        else if (arg is IdentifierExpr arrIdent &&
                                 ctx.DataAddresses.TryGetValue(arrIdent.Name, out var arrAddr))
                        {
                            ctx.Buffer.EmitLdaImmediate((byte)(arrAddr & 0xFF));
                            ctx.Buffer.EmitStaZeroPage(ptrLo);
                            ctx.Buffer.EmitLdaImmediate((byte)(arrAddr >> 8));
                            ctx.Buffer.EmitStaZeroPage(ptrHi);
                            ctx.Buffer.EmitLdaImmediate((byte)ctx.GetConstArraySize(arrIdent.Name));
                            ctx.Buffer.EmitStaZeroPage(lenZp);
                        }
                    }
                    else if (SymbolTable.Is16BitType(param.TypeName) &&
                             paramZpMap.TryGetValue(param.ParamName, out var wordZp))
                    {
                        if (arg is IdentifierExpr wordArg && ctx.Symbols.IsWordVar(wordArg.Name))
                        {
                            var srcZp = ctx.Symbols.GetLocal(wordArg.Name);
                            ctx.Buffer.EmitLdaZeroPage(srcZp);
                            ctx.Buffer.EmitStaZeroPage(wordZp);
                            ctx.Buffer.EmitLdaZeroPage((byte)(srcZp + 1));
                            ctx.Buffer.EmitStaZeroPage((byte)(wordZp + 1));
                        }
                        else
                        {
                            var val = ExpressionEmitter.Resolve16BitInitializer(param.TypeName, arg);
                            ctx.Buffer.EmitLdaImmediate((byte)(val & 0xFF));
                            ctx.Buffer.EmitStaZeroPage(wordZp);
                            ctx.Buffer.EmitLdaImmediate((byte)(val >> 8));
                            ctx.Buffer.EmitStaZeroPage((byte)(wordZp + 1));
                        }
                    }
                    else if (paramZpMap.TryGetValue(param.ParamName, out var byteZp))
                    {
                        ctx.Expressions.EmitExprToA(arg);
                        ctx.Buffer.EmitStaZeroPage(byteZp);
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
            if (addExpr.Right is IdentifierExpr peekOffsetId && ctx.Symbols.IsWordVar(peekOffsetId.Name))
            {
                // peek: (constant + wordVar) → compute 16-bit address into $FB/$FC
                var zp = ctx.Symbols.GetLocal(peekOffsetId.Name);
                ctx.Buffer.EmitLdaZeroPage(zp);
                ctx.Buffer.EmitClc();
                ctx.Buffer.EmitAdcImmediate((byte)(baseAddr.Value & 0xFF));
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
                ctx.Buffer.EmitLdaZeroPage((byte)(zp + 1));
                ctx.Buffer.EmitAdcImmediate((byte)((baseAddr.Value >> 8) & 0xFF));
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);
                ctx.Buffer.EmitLdyImmediate(0);
                ctx.Buffer.EmitLdaIndirectY(EmitContext.ZpPointerLo);
            }
            else
            {
                // peek: (constant + byteExpr) → LDA base,X
                ctx.Expressions.EmitExprToA(addExpr.Right);
                ctx.Buffer.EmitTax();
                ctx.Buffer.EmitLdaAbsoluteX((ushort)baseAddr.Value);
            }
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
            if (addExpr.Right is IdentifierExpr pokeOffsetId && ctx.Symbols.IsWordVar(pokeOffsetId.Name))
            {
                // poke: (constant + wordVar) → compute 16-bit address into $FB/$FC
                var zp = ctx.Symbols.GetLocal(pokeOffsetId.Name);
                ctx.Expressions.EmitExprToA(valueExpr);
                ctx.Buffer.EmitPha();
                ctx.Buffer.EmitLdaZeroPage(zp);
                ctx.Buffer.EmitClc();
                ctx.Buffer.EmitAdcImmediate((byte)(baseAddr.Value & 0xFF));
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
                ctx.Buffer.EmitLdaZeroPage((byte)(zp + 1));
                ctx.Buffer.EmitAdcImmediate((byte)((baseAddr.Value >> 8) & 0xFF));
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);
                ctx.Buffer.EmitPla();
                ctx.Buffer.EmitLdyImmediate(0);
                ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);
            }
            else
            {
                // poke: (constant + byteExpr) → STA base,X
                ctx.Expressions.EmitExprToA(addExpr.Right);
                ctx.Buffer.EmitTax();
                ctx.Expressions.EmitExprToA(valueExpr);
                ctx.Buffer.EmitStaAbsoluteX((ushort)baseAddr.Value);
            }
        }
        else if (addressExpr is IntLiteralExpr constAddr)
        {
            ctx.Expressions.EmitExprToA(valueExpr);
            ctx.Buffer.EmitStaAbsolute((ushort)constAddr.Value);
        }
        // poke: wordVar value: val — indirect indexed via ($FB),Y
        // Evaluate value FIRST to avoid $FB/$FC clobber (e.g. nested peek of word var)
        else if (addressExpr is IdentifierExpr addrIdent && ctx.Symbols.IsWordVar(addrIdent.Name))
        {
            var zp = ctx.Symbols.GetLocal(addrIdent.Name);
            ctx.Expressions.EmitExprToA(valueExpr);
            ctx.Buffer.EmitPha();
            ctx.Buffer.EmitLdaZeroPage(zp);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
            ctx.Buffer.EmitLdaZeroPage((byte)(zp + 1));
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);
            ctx.Buffer.EmitPla();
            ctx.Buffer.EmitLdyImmediate(0);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);
        }
        // poke: (wordVar + offset) value: val — compute address, use indirect
        // Evaluate value FIRST to avoid $FB/$FC clobber (e.g. nested peek of word var)
        else if (addressExpr is BinaryExpr { Op: Language.Lexing.TokenKind.Plus } wordAdd &&
                 wordAdd.Left is IdentifierExpr wordBase && ctx.Symbols.IsWordVar(wordBase.Name))
        {
            var zp = ctx.Symbols.GetLocal(wordBase.Name);
            ctx.Expressions.EmitExprToA(valueExpr);
            ctx.Buffer.EmitPha();
            // Load word address + offset into pointer
            ctx.Expressions.EmitExprToA(wordAdd.Right);
            ctx.Buffer.EmitClc();
            ctx.Buffer.EmitAdcZeroPage(zp);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
            ctx.Buffer.EmitLdaZeroPage((byte)(zp + 1));
            ctx.Buffer.EmitAdcImmediate(0);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);
            ctx.Buffer.EmitPla();
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
                var strPosIsConstant = false;
                if (posExpr is IntLiteralExpr pl) { strPos = pl.Value; strPosIsConstant = true; }
                else if (ctx.Expressions.TryFoldConstant(posExpr, out var fp)) { strPos = fp; strPosIsConstant = true; }

                if (strPosIsConstant)
                {
                    var sa = (ushort)(0x0400 + strPos);
                    ctx.Buffer.EmitLdxImmediate(0);
                    // LDA zp,X = $B5 zp (2 bytes)
                    ctx.Buffer.EmitByte(0xB5); ctx.Buffer.EmitByte(strInfo.ZpBase);
                    ctx.Buffer.EmitStaAbsoluteX(sa);
                    ctx.Buffer.EmitInx();
                    ctx.Buffer.EmitCpxImmediate((byte)strInfo.Length);
                    // Loop: LDA zp,X(2) + STA abs,X(3) + INX(1) + CPX(2) + BNE(2) = 10
                    ctx.Buffer.EmitBne(unchecked((sbyte)(-10)));
                }
                else
                {
                    // Runtime position: compute $0400 + posExpr → $FB/$FC
                    ctx.Expressions.EmitExprToA(posExpr);
                    ctx.Buffer.EmitClc();
                    ctx.Buffer.EmitAdcImmediate(0x00);
                    ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
                    ctx.Buffer.EmitLdaImmediate(0x04);
                    ctx.Buffer.EmitAdcImmediate(0x00);
                    ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);
                    // Loop: LDX=index into ZP string, Y=index into screen via ($FB),Y
                    ctx.Buffer.EmitLdxImmediate(0);
                    ctx.Buffer.EmitLdyImmediate(0);
                    // LDA zp,X = $B5 zp (2 bytes)
                    ctx.Buffer.EmitByte(0xB5); ctx.Buffer.EmitByte(strInfo.ZpBase);
                    ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);
                    ctx.Buffer.EmitInx();
                    ctx.Buffer.EmitIny();
                    ctx.Buffer.EmitCpxImmediate((byte)strInfo.Length);
                    // Loop: LDA zp,X(2) + STA ($FB),Y(2) + INX(1) + INY(1) + CPX(2) + BNE(2) = 10
                    ctx.Buffer.EmitBne(unchecked((sbyte)(-10)));
                }
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
                throw new CompileError(
                    $"printAt text: '{textIdent.Name}' is not a string variable or const array. " +
                    "Use a string literal, string variable, or const byte array. " +
                    "To write a single screen code, use printAt: char: instead.",
                    textExpr.Location);
            }
        }
        else
        {
            throw new CompileError(
                "printAt text: expects a string literal, string variable, or const array. " +
                "To write a single screen code, use printAt: char: instead.",
                textExpr.Location);
        }

        // Calculate screen address: $0400 + pos
        long pos = 0;
        var posIsConstant = false;
        if (posExpr is IntLiteralExpr posLit)
        {
            pos = posLit.Value;
            posIsConstant = true;
        }
        else if (ctx.Expressions.TryFoldConstant(posExpr, out var foldedPos))
        {
            pos = foldedPos;
            posIsConstant = true;
        }

        if (posIsConstant)
        {
            var screenAddr = (ushort)(0x0400 + pos);
            // LDX #0; loop: LDA data,X; STA screen,X; INX; CPX #len; BNE loop
            ctx.Buffer.EmitLdxImmediate(0);
            ctx.Buffer.EmitLdaAbsoluteX(dataAddr);
            ctx.Buffer.EmitStaAbsoluteX(screenAddr);
            ctx.Buffer.EmitInx();
            ctx.Buffer.EmitCpxImmediate((byte)arraySize);
            ctx.Buffer.EmitBne(unchecked((sbyte)(-11)));
        }
        else
        {
            // Runtime position: compute $0400 + posExpr → $FB/$FC
            ctx.Expressions.EmitExprToA(posExpr);
            ctx.Buffer.EmitClc();
            ctx.Buffer.EmitAdcImmediate(0x00);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
            ctx.Buffer.EmitLdaImmediate(0x04);
            ctx.Buffer.EmitAdcImmediate(0x00);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);
            // Loop: LDY as index into both data and screen
            ctx.Buffer.EmitLdyImmediate(0);
            ctx.Buffer.EmitLdaAbsoluteY(dataAddr);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);
            ctx.Buffer.EmitIny();
            ctx.Buffer.EmitCpyImmediate((byte)arraySize);
            // Loop: LDA abs,Y(3) + STA ($FB),Y(2) + INY(1) + CPY(2) + BNE(2) = 10
            ctx.Buffer.EmitBne(unchecked((sbyte)(-10)));
        }
    }

    /// <summary>
    /// printAt: screenPos char: screenCode
    /// Writes a single screen code byte to $0400 + screenPos.
    /// </summary>
    private void EmitPrintAtChar(ExprNode posExpr, ExprNode charExpr)
    {
        long pos = 0;
        var posIsConstant = false;
        if (posExpr is IntLiteralExpr posLit)
        {
            pos = posLit.Value;
            posIsConstant = true;
        }
        else if (ctx.Expressions.TryFoldConstant(posExpr, out var foldedPos))
        {
            pos = foldedPos;
            posIsConstant = true;
        }

        if (posIsConstant)
        {
            var screenAddr = (ushort)(0x0400 + pos);
            ctx.Expressions.EmitExprToA(charExpr);
            ctx.Buffer.EmitStaAbsolute(screenAddr);
        }
        else
        {
            // Runtime position: compute $0400 + posExpr → $FB/$FC
            ctx.Expressions.EmitExprToA(posExpr);
            ctx.Buffer.EmitClc();
            ctx.Buffer.EmitAdcImmediate(0x00);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerLo);
            ctx.Buffer.EmitLdaImmediate(0x04);
            ctx.Buffer.EmitAdcImmediate(0x00);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpPointerHi);
            ctx.Expressions.EmitExprToA(charExpr);
            ctx.Buffer.EmitLdyImmediate(0);
            ctx.Buffer.EmitStaIndirectY(EmitContext.ZpPointerLo);
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

    /// <summary>
    /// Setup args and JSR to plotPixel/clearPixel runtime routine.
    /// Args: $F0=x_lo, $F1=x_hi, $0F=y. ~100 cycles total.
    /// </summary>
    private void EmitPlotPixelCall(ExprNode xExpr, ExprNode yExpr, bool setPixel)
    {
        // Evaluate y (byte) → $0F
        ctx.Expressions.EmitExprToA(yExpr);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpTemp);

        // Evaluate x → $F0/$F1
        if (xExpr is IdentifierExpr xIdent && ctx.Symbols.IsWordVar(xIdent.Name))
        {
            var xZp = ctx.Symbols.GetLocal(xIdent.Name);
            ctx.Buffer.EmitLdaZeroPage(xZp);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
            ctx.Buffer.EmitLdaZeroPage((byte)(xZp + 1));
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
        }
        else if (ctx.Expressions.TryFoldConstant(xExpr, out var xConst))
        {
            ctx.Buffer.EmitLdaImmediate((byte)(xConst & 0xFF));
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
            ctx.Buffer.EmitLdaImmediate((byte)(xConst >> 8));
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
        }
        else
        {
            ctx.Expressions.EmitExprToA(xExpr);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulA);
            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitStaZeroPage(EmitContext.ZpMulB);
        }

        ctx.Buffer.EmitJsrForward(setPixel ? "_rt_plotPixel" : "_rt_clearPixel");
    }
}
