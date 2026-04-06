namespace Cork.CodeGen.Emit;

using Cork.Language.Ast;
using Cork.Language.Lexing;

/// <summary>
/// Emits 6510 code for control flow: if, while, for, switch, break, continue.
/// </summary>
public sealed class ControlFlowEmitter(EmitContext ctx)
{
    public void EmitWhile(WhileStmt whileStmt)
    {
        var loopLabel = ctx.NextLabel("wloop");
        var endLabel = ctx.NextLabel("wend");

        ctx.LoopStack.Push((endLabel, loopLabel));

        ctx.Buffer.DefineLabel(loopLabel);
        EmitConditionBranchTrue(whileStmt.Condition, 3);
        ctx.Buffer.EmitJmpForward(endLabel);
        ctx.Statements.EmitBlock(whileStmt.Body);
        ctx.Buffer.EmitJmpAbsolute(ctx.Buffer.GetLabel(loopLabel));
        ctx.Buffer.DefineLabel(endLabel);

        ctx.LoopStack.Pop();
    }

    public void EmitFor(ForStmt forStmt)
    {
        var loopLabel = ctx.NextLabel("floop");
        var stepLabel = ctx.NextLabel("fstep");
        var endLabel = ctx.NextLabel("fend");

        ctx.Statements.EmitStatement(forStmt.Init);

        ctx.LoopStack.Push((endLabel, stepLabel));

        ctx.Buffer.DefineLabel(loopLabel);
        EmitConditionBranchTrue(forStmt.Condition, 3);
        ctx.Buffer.EmitJmpForward(endLabel);

        ctx.Statements.EmitBlock(forStmt.Body);

        ctx.Buffer.DefineLabel(stepLabel);
        ctx.Statements.EmitStatement(forStmt.Step);

        ctx.Buffer.EmitJmpAbsolute(ctx.Buffer.GetLabel(loopLabel));
        ctx.Buffer.DefineLabel(endLabel);

        ctx.LoopStack.Pop();
    }

    public void EmitForEach(ForEachStmt forEach)
    {
        // For const arrays: desugar to indexed loop
        // for (ch in message) → for (byte _idx = 0; _idx < message.length; _idx += 1) { ch = message[_idx]; ... }
        if (forEach.Collection is IdentifierExpr arrIdent &&
            ctx.DataAddresses.TryGetValue(arrIdent.Name, out var dataAddr))
        {
            // Look up array size from const arrays
            var arraySize = ctx.GetConstArraySize(arrIdent.Name);
            var idxZp = ctx.Symbols.AllocZeroPage($"_foreach_idx");
            var loopLabel = ctx.NextLabel("feloop");
            var stepLabel = ctx.NextLabel("festep");
            var endLabel = ctx.NextLabel("feend");

            // Init: idx = 0
            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitStaZeroPage(idxZp);

            ctx.LoopStack.Push((endLabel, stepLabel));

            // Condition: idx < arraySize
            ctx.Buffer.DefineLabel(loopLabel);
            ctx.Buffer.EmitLdaZeroPage(idxZp);
            ctx.Buffer.EmitCmpImmediate((byte)arraySize);
            ctx.Buffer.EmitBcc(3); // less → skip JMP, enter body
            ctx.Buffer.EmitJmpForward(endLabel);

            // Register loop variable as an alias for array[idx]
            // The variable name resolves in EmitExprToA via a special "foreach context"
            ctx.ForEachVar = (forEach.VarName, dataAddr, idxZp);

            ctx.Statements.EmitBlock(forEach.Body);

            ctx.ForEachVar = null;

            // Step: idx += 1
            ctx.Buffer.DefineLabel(stepLabel);
            ctx.Buffer.EmitIncZeroPage(idxZp);

            ctx.Buffer.EmitJmpAbsolute(ctx.Buffer.GetLabel(loopLabel));
            ctx.Buffer.DefineLabel(endLabel);

            ctx.LoopStack.Pop();
            return;
        }

        // Struct array: for (e in enemies)
        if (forEach.Collection is IdentifierExpr structArrIdent &&
            ctx.Symbols.TryGetStructArray(structArrIdent.Name, out var arrInfo))
        {
            var idxZp = ctx.Symbols.AllocZeroPage($"_foreach_idx");
            var loopLabel = ctx.NextLabel("feloop");
            var stepLabel = ctx.NextLabel("festep");
            var endLabel = ctx.NextLabel("feend");

            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitStaZeroPage(idxZp);

            ctx.LoopStack.Push((endLabel, stepLabel));

            ctx.Buffer.DefineLabel(loopLabel);
            ctx.Buffer.EmitLdaZeroPage(idxZp);
            ctx.Buffer.EmitCmpImmediate((byte)arrInfo.Size);
            ctx.Buffer.EmitBcc(3);
            ctx.Buffer.EmitJmpForward(endLabel);

            // Register for-each struct context
            ctx.ForEachStructVar = (forEach.VarName, arrInfo.StructType, arrInfo.FieldBases, idxZp);

            ctx.Statements.EmitBlock(forEach.Body);

            ctx.ForEachStructVar = null;

            ctx.Buffer.DefineLabel(stepLabel);
            ctx.Buffer.EmitIncZeroPage(idxZp);

            ctx.Buffer.EmitJmpAbsolute(ctx.Buffer.GetLabel(loopLabel));
            ctx.Buffer.DefineLabel(endLabel);

            ctx.LoopStack.Pop();
            return;
        }

        // Ref param (string or byte[]): for (ch in data) where data is a ref param
        if (forEach.Collection is IdentifierExpr refIdent &&
            ctx.Symbols.TryGetRefParam(refIdent.Name, out var refParam))
        {
            var idxZp = ctx.Symbols.AllocZeroPage($"_foreach_idx");
            var loopLabel = ctx.NextLabel("feloop");
            var stepLabel = ctx.NextLabel("festep");
            var endLabel = ctx.NextLabel("feend");

            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitStaZeroPage(idxZp);

            ctx.LoopStack.Push((endLabel, stepLabel));

            ctx.Buffer.DefineLabel(loopLabel);
            ctx.Buffer.EmitLdaZeroPage(idxZp);
            ctx.Buffer.EmitCmpZeroPage(refParam.LenZp);
            ctx.Buffer.EmitBcc(3);
            ctx.Buffer.EmitJmpForward(endLabel);

            // Register loop variable — resolves via ref param's pointer
            ctx.ForEachVar = (forEach.VarName, 0, idxZp); // dataAddr unused, we override in expression
            ctx.ForEachRefParam = refParam;

            ctx.Statements.EmitBlock(forEach.Body);

            ctx.ForEachVar = null;
            ctx.ForEachRefParam = null;

            ctx.Buffer.DefineLabel(stepLabel);
            ctx.Buffer.EmitIncZeroPage(idxZp);

            ctx.Buffer.EmitJmpAbsolute(ctx.Buffer.GetLabel(loopLabel));
            ctx.Buffer.DefineLabel(endLabel);

            ctx.LoopStack.Pop();
            return;
        }

        throw new InvalidOperationException("for-each: unsupported collection type");
    }

    public void EmitSwitch(SwitchStmt stmt)
    {
        var isExpressionSwitch = stmt.Cases.Any(c =>
            c.Value is not IntLiteralExpr and not MemberAccessExpr
            && !ctx.Expressions.TryFoldConstant(c.Value, out _));

        if (isExpressionSwitch)
            EmitExpressionSwitch(stmt);
        else
            EmitConstantSwitch(stmt);
    }

    private void EmitConstantSwitch(SwitchStmt stmt)
    {
        var endLabel = ctx.NextLabel("swend");

        ctx.Expressions.EmitExprToA(stmt.Subject);
        ctx.Buffer.EmitStaZeroPage(EmitContext.ZpTemp);

        for (var i = 0; i < stmt.Cases.Count; i++)
        {
            var caseValue = ctx.Expressions.EvalConstExpr(stmt.Cases[i].Value);
            ctx.Buffer.EmitLdaZeroPage(EmitContext.ZpTemp);
            ctx.Buffer.EmitCmpImmediate(caseValue);
            ctx.Buffer.EmitBne(3);
            ctx.Buffer.EmitJmpForward($"swcase_{endLabel}_{i}");
        }

        if (stmt.DefaultBody != null)
            ctx.Buffer.EmitJmpForward($"swdef_{endLabel}");
        else
            ctx.Buffer.EmitJmpForward(endLabel);

        for (var i = 0; i < stmt.Cases.Count; i++)
        {
            ctx.Buffer.DefineLabel($"swcase_{endLabel}_{i}");
            foreach (var s in stmt.Cases[i].Body)
                ctx.Statements.EmitStatement(s);
            if (!stmt.IsFallthrough)
                ctx.Buffer.EmitJmpForward(endLabel);
        }

        if (stmt.DefaultBody != null)
        {
            ctx.Buffer.DefineLabel($"swdef_{endLabel}");
            ctx.Statements.EmitBlock(stmt.DefaultBody);
        }

        ctx.Buffer.DefineLabel(endLabel);
    }

    private void EmitExpressionSwitch(SwitchStmt stmt)
    {
        var endLabel = ctx.NextLabel("swend");

        for (var i = 0; i < stmt.Cases.Count; i++)
        {
            var nextLabel = (i < stmt.Cases.Count - 1)
                ? ctx.NextLabel($"swnext_{i}")
                : (stmt.DefaultBody != null ? ctx.NextLabel("swdef") : endLabel);

            EmitConditionBranchTrue(stmt.Cases[i].Value, 3);
            ctx.Buffer.EmitJmpForward(nextLabel);

            foreach (var s in stmt.Cases[i].Body)
                ctx.Statements.EmitStatement(s);
            if (!stmt.IsFallthrough)
                ctx.Buffer.EmitJmpForward(endLabel);

            if (i < stmt.Cases.Count - 1 || stmt.DefaultBody != null)
                ctx.Buffer.DefineLabel(nextLabel);
        }

        if (stmt.DefaultBody != null)
        {
            ctx.Statements.EmitBlock(stmt.DefaultBody);
        }

        ctx.Buffer.DefineLabel(endLabel);
    }

    public void EmitBreak()
    {
        if (ctx.LoopStack.Count == 0)
            throw new InvalidOperationException("break outside of loop");
        ctx.Buffer.EmitJmpForward(ctx.LoopStack.Peek().BreakLabel);
    }

    public void EmitContinue()
    {
        if (ctx.LoopStack.Count == 0)
            throw new InvalidOperationException("continue outside of loop");
        ctx.Buffer.EmitJmpForward(ctx.LoopStack.Peek().ContinueLabel);
    }

    public void EmitIf(IfStmt ifStmt)
    {
        var endLabel = ctx.NextLabel("endif");

        if (ifStmt.ElseBody == null && ifStmt.ElseIfs.Count == 0)
        {
            EmitConditionBranchTrue(ifStmt.Condition, 3);
            ctx.Buffer.EmitJmpForward(endLabel);
            ctx.Statements.EmitBlock(ifStmt.ThenBody);
        }
        else
        {
            var elseLabel = ctx.NextLabel("else");
            EmitConditionBranchTrue(ifStmt.Condition, 3);
            ctx.Buffer.EmitJmpForward(elseLabel);
            ctx.Statements.EmitBlock(ifStmt.ThenBody);
            ctx.Buffer.EmitJmpForward(endLabel);
            ctx.Buffer.DefineLabel(elseLabel);
            if (ifStmt.ElseBody != null)
                ctx.Statements.EmitBlock(ifStmt.ElseBody);
        }

        ctx.Buffer.DefineLabel(endLabel);
    }

    public void EmitConditionBranchTrue(ExprNode condition, int skipBytes)
    {
        // Sprite-sprite collision: player collidedWith: enemy
        if (condition is MessageSendExpr { Segments: [{ Name: "collidedWith" }] } collMsg &&
            collMsg.Receiver is IdentifierExpr collReceiver &&
            collMsg.Segments[0].Argument is IdentifierExpr collTarget)
        {
            var recvIdx = GetSpriteIndex(collReceiver.Name);
            var targIdx = GetSpriteIndex(collTarget.Name);
            var mask = (byte)((1 << recvIdx) | (1 << targIdx));

            ctx.Buffer.EmitLdaAbsolute(0xD01E);
            ctx.Buffer.EmitByte(0x29); ctx.Buffer.EmitByte(mask); // AND #mask
            ctx.Buffer.EmitCmpImmediate(mask);
            ctx.Buffer.EmitBeq((sbyte)skipBytes);
            return;
        }

        // Sprite-background collision: player collidedWithBackground:
        if (condition is MessageSendExpr { Segments: [{ Name: "collidedWithBackground" }] } bgCollMsg &&
            bgCollMsg.Receiver is IdentifierExpr bgCollReceiver)
        {
            var sprIdx = GetSpriteIndex(bgCollReceiver.Name);
            var mask = (byte)(1 << sprIdx);

            ctx.Buffer.EmitLdaAbsolute(0xD01F);
            ctx.Buffer.EmitByte(0x29); ctx.Buffer.EmitByte(mask); // AND #mask
            ctx.Buffer.EmitBne((sbyte)skipBytes); // BNE = bit set = collision
            return;
        }

        if (IsJoystickCheck(condition, out var bitMask, out var ciaAddr))
        {
            ctx.Buffer.EmitLdaAbsolute(ciaAddr);
            ctx.Buffer.EmitByte(0x29);
            ctx.Buffer.EmitByte(bitMask);
            ctx.Buffer.EmitBeq((sbyte)skipBytes);
            return;
        }

        if (IsKeyboardCheck(condition, out var colSelect, out var rowMask))
        {
            // Drive keyboard column via CIA1 Port A, read row from Port B
            ctx.Buffer.EmitLdaImmediate(colSelect);
            ctx.Buffer.EmitStaAbsolute(0xDC00);
            ctx.Buffer.EmitLdaAbsolute(0xDC01);
            ctx.Buffer.EmitByte(0x29); ctx.Buffer.EmitByte(rowMask); // AND #rowMask
            ctx.Buffer.EmitBeq((sbyte)skipBytes); // BEQ = bit clear = key pressed
            return;
        }

        if (condition is BinaryExpr wordBin)
        {
            // Resolve left side to a word variable name (plain identifiers and struct/sprite fields)
            string? wordVarName = null;
            if (wordBin.Left is IdentifierExpr wordIdent && ctx.Symbols.IsWordVar(wordIdent.Name))
            {
                wordVarName = wordIdent.Name;
            }
            else if (wordBin.Left is MemberAccessExpr memberLeft &&
                     memberLeft.Receiver is IdentifierExpr recvIdent &&
                     ctx.Expressions.TryResolveStructField(memberLeft, out var memberZp) &&
                     ctx.Symbols.GetVarTypeForZp(memberZp) is { } ft && SymbolTable.Is16BitType(ft))
            {
                wordVarName = $"{recvIdent.Name}${memberLeft.MemberName}";
            }

            if (wordVarName != null)
            {
                // Word vs word variable comparison
                if (wordBin.Right is IdentifierExpr wordRight && ctx.Symbols.IsWordVar(wordRight.Name))
                {
                    EmitWordVarComparisonBranchTrue(wordVarName, wordBin.Op, wordRight.Name, skipBytes);
                    return;
                }
                var litVal = ExpressionEmitter.Resolve16BitInitializer("", wordBin.Right);
                EmitWordComparisonBranchTrue(wordVarName, wordBin.Op, litVal, skipBytes);
                return;
            }
        }

        if (condition is BinaryExpr signedBin &&
            signedBin.Left is IdentifierExpr signedIdent &&
            ctx.Symbols.TryGetVarType(signedIdent.Name, out var stype) && stype == "sbyte" &&
            signedBin.Right is IntLiteralExpr { Value: 0 })
        {
            var zp = ctx.Symbols.GetLocal(signedIdent.Name);
            ctx.Buffer.EmitLdaZeroPage(zp);
            switch (signedBin.Op)
            {
                case TokenKind.Less:
                    ctx.Buffer.EmitBmi((sbyte)skipBytes); break;
                case TokenKind.GreaterEqual:
                    ctx.Buffer.EmitBpl((sbyte)skipBytes); break;
                case TokenKind.Greater:
                    ctx.Buffer.EmitBeq(2);
                    ctx.Buffer.EmitBpl((sbyte)skipBytes); break;
                case TokenKind.LessEqual:
                    ctx.Buffer.EmitBmi((sbyte)skipBytes);
                    ctx.Buffer.EmitBeq((sbyte)(skipBytes - 2)); break;
                default:
                    throw new InvalidOperationException($"Unsupported signed comparison: {signedBin.Op}");
            }
            return;
        }

        if (condition is BinaryExpr bin)
        {
            ctx.Expressions.EmitExprToA(bin.Left);

            if (bin.Right is IntLiteralExpr intLit)
                ctx.Buffer.EmitCmpImmediate((byte)intLit.Value);
            else if (bin.Right is IdentifierExpr ident && ctx.Symbols.TryGetConstant(ident.Name, out var cv))
                ctx.Buffer.EmitCmpImmediate((byte)cv);
            else if (bin.Right is IdentifierExpr ident2)
                ctx.Buffer.EmitCmpZeroPage(ctx.Symbols.GetLocal(ident2.Name));
            else
            {
                ctx.Buffer.EmitPha();
                ctx.Expressions.EmitExprToA(bin.Right);
                ctx.Buffer.EmitStaZeroPage(EmitContext.ZpTemp);
                ctx.Buffer.EmitPla();
                ctx.Buffer.EmitCmpZeroPage(EmitContext.ZpTemp);
            }

            switch (bin.Op)
            {
                case TokenKind.Less:
                    ctx.Buffer.EmitBcc((sbyte)skipBytes);
                    break;
                case TokenKind.GreaterEqual:
                    ctx.Buffer.EmitBcs((sbyte)skipBytes);
                    break;
                case TokenKind.EqualEqual:
                    ctx.Buffer.EmitBeq((sbyte)skipBytes);
                    break;
                case TokenKind.BangEqual:
                    ctx.Buffer.EmitBne((sbyte)skipBytes);
                    break;
                case TokenKind.Greater:
                    ctx.Buffer.EmitBeq(2);
                    ctx.Buffer.EmitBcs((sbyte)skipBytes);
                    break;
                case TokenKind.LessEqual:
                    ctx.Buffer.EmitBeq((sbyte)(2 + skipBytes));
                    ctx.Buffer.EmitBcc((sbyte)skipBytes);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported comparison: {bin.Op}");
            }
            return;
        }

        // Bare identifier as condition: if (myVar) — treat non-zero as true
        if (condition is IdentifierExpr bareIdent)
        {
            ctx.Expressions.EmitExprToA(bareIdent);
            ctx.Buffer.EmitBeq((sbyte)skipBytes);
            return;
        }

        throw new InvalidOperationException($"Unsupported condition: {condition.GetType().Name}");
    }

    private int GetSpriteIndex(string spriteName)
    {
        var xZp = ctx.Symbols.GetLocal($"{spriteName}$x");
        ctx.Symbols.TryGetSpriteSync(xZp, out var reg);
        return (reg - 0xD000) / 2;
    }

    public static bool IsJoystickCheck(ExprNode expr, out byte bitMask, out ushort ciaAddr)
    {
        bitMask = 0;
        ciaAddr = 0;
        if (expr is MemberAccessExpr { Receiver: MemberAccessExpr { Receiver: IdentifierExpr { Name: "joystick" } } portAccess } outer)
        {
            ciaAddr = portAccess.MemberName switch
            {
                "port1" => 0xDC01,
                "port2" => 0xDC00,
                _ => 0
            };
            if (ciaAddr == 0) return false;

            bitMask = outer.MemberName switch
            {
                "up" => 0x01,
                "down" => 0x02,
                "left" => 0x04,
                "right" => 0x08,
                "fire" => 0x10,
                _ => 0
            };
            return bitMask != 0;
        }
        return false;
    }

    /// <summary>
    /// Detects keyboard.KEY_NAME conditions and returns the CIA1 PA drive
    /// byte ($DC00 write) and PB read mask ($DC01 read).
    /// C64 keyboard is an 8×8 matrix: PA0-7 ($DC00) drive columns,
    /// PB0-7 ($DC01) read rows. Both active low.
    /// </summary>
    public static bool IsKeyboardCheck(ExprNode expr, out byte colSelect, out byte rowMask)
    {
        colSelect = 0;
        rowMask = 0;
        if (expr is MemberAccessExpr { Receiver: IdentifierExpr { Name: "keyboard" } } keyAccess)
        {
            if (!KeyMatrix.TryGetValue(keyAccess.MemberName, out var entry))
                return false;
            colSelect = (byte)~(1 << entry.Pa); // drive PA bit low
            rowMask = (byte)(1 << entry.Pb);     // read PB bit
            return true;
        }
        return false;
    }

    // C64 keyboard matrix: key name → (PA bit for $DC00, PB bit for $DC01)
    // Visual layout — rows are PA lines, columns are PB lines:
    //       PB0   PB1   PB2   PB3   PB4   PB5   PB6   PB7
    // PA0:  DEL   RET   →     F7    F1    F3    F5    ↓
    // PA1:  3     W     A     4     Z     S     E     LSHFT
    // PA2:  5     R     D     6     C     F     T     X
    // PA3:  7     Y     G     8     B     H     U     V
    // PA4:  9     I     J     0     M     K     O     N
    // PA5:  +     P     L     -     .     :     @     ,
    // PA6:  £     *     ;     HOME  RSHFT =     ↑     /
    // PA7:  1     ←     CTRL  2     SPACE C=    Q     STOP
    private static readonly Dictionary<string, (int Pa, int Pb)> KeyMatrix = new()
    {
        // PA0: DEL RET → F7 F1 F3 F5 ↓
        ["del"] = (0, 0), ["return"] = (0, 1), ["cursorRight"] = (0, 2),
        ["f7"] = (0, 3), ["f1"] = (0, 4), ["f3"] = (0, 5), ["f5"] = (0, 6),
        ["cursorDown"] = (0, 7),
        // PA1: 3 W A 4 Z S E LSHFT
        ["n3"] = (1, 0), ["w"] = (1, 1), ["a"] = (1, 2), ["n4"] = (1, 3),
        ["z"] = (1, 4), ["s"] = (1, 5), ["e"] = (1, 6), ["leftShift"] = (1, 7),
        // PA2: 5 R D 6 C F T X
        ["n5"] = (2, 0), ["r"] = (2, 1), ["d"] = (2, 2), ["n6"] = (2, 3),
        ["c"] = (2, 4), ["f"] = (2, 5), ["t"] = (2, 6), ["x"] = (2, 7),
        // PA3: 7 Y G 8 B H U V
        ["n7"] = (3, 0), ["y"] = (3, 1), ["g"] = (3, 2), ["n8"] = (3, 3),
        ["b"] = (3, 4), ["h"] = (3, 5), ["u"] = (3, 6), ["v"] = (3, 7),
        // PA4: 9 I J 0 M K O N
        ["n9"] = (4, 0), ["i"] = (4, 1), ["j"] = (4, 2), ["n0"] = (4, 3),
        ["m"] = (4, 4), ["k"] = (4, 5), ["o"] = (4, 6), ["n"] = (4, 7),
        // PA5: + P L - . : @ ,
        ["plus"] = (5, 0), ["p"] = (5, 1), ["l"] = (5, 2), ["minus"] = (5, 3),
        ["period"] = (5, 4), ["colon"] = (5, 5), ["at"] = (5, 6), ["comma"] = (5, 7),
        // PA6: £ * ; HOME RSHFT = ↑ /
        ["pound"] = (6, 0), ["star"] = (6, 1), ["semicolon"] = (6, 2),
        ["home"] = (6, 3), ["rightShift"] = (6, 4), ["equals"] = (6, 5),
        ["upArrow"] = (6, 6), ["slash"] = (6, 7),
        // PA7: 1 ← CTRL 2 SPACE C= Q STOP
        ["n1"] = (7, 0), ["leftArrow"] = (7, 1), ["ctrl"] = (7, 2),
        ["n2"] = (7, 3), ["space"] = (7, 4), ["commodore"] = (7, 5),
        ["q"] = (7, 6), ["runStop"] = (7, 7),
    };

    public void EmitWordComparisonBranchTrue(string varName, TokenKind op, ushort literal, int skipBytes)
    {
        var zp = ctx.Symbols.GetLocal(varName);
        var zpHi = (byte)(zp + 1);
        var immLo = (byte)(literal & 0xFF);
        var immHi = (byte)(literal >> 8);

        switch (op)
        {
            case TokenKind.Less:
                ctx.Buffer.EmitLdaZeroPage(zpHi);
                ctx.Buffer.EmitCmpImmediate(immHi);
                ctx.Buffer.EmitBcc((sbyte)(8 + skipBytes));
                ctx.Buffer.EmitBne(6);
                ctx.Buffer.EmitLdaZeroPage(zp);
                ctx.Buffer.EmitCmpImmediate(immLo);
                ctx.Buffer.EmitBcc((sbyte)skipBytes);
                break;

            case TokenKind.Greater:
                ctx.Buffer.EmitLdaImmediate(immHi);
                ctx.Buffer.EmitCmpZeroPage(zpHi);
                ctx.Buffer.EmitBcc((sbyte)(8 + skipBytes));
                ctx.Buffer.EmitBne(6);
                ctx.Buffer.EmitLdaImmediate(immLo);
                ctx.Buffer.EmitCmpZeroPage(zp);
                ctx.Buffer.EmitBcc((sbyte)skipBytes);
                break;

            case TokenKind.LessEqual:
                ctx.Buffer.EmitLdaZeroPage(zpHi);
                ctx.Buffer.EmitCmpImmediate(immHi);
                ctx.Buffer.EmitBcc((sbyte)(8 + skipBytes));
                ctx.Buffer.EmitBne(6);
                ctx.Buffer.EmitLdaImmediate(immLo);
                ctx.Buffer.EmitCmpZeroPage(zp);
                ctx.Buffer.EmitBcs((sbyte)skipBytes);
                break;

            case TokenKind.GreaterEqual:
                ctx.Buffer.EmitLdaImmediate(immHi);
                ctx.Buffer.EmitCmpZeroPage(zpHi);
                ctx.Buffer.EmitBcc((sbyte)(8 + skipBytes));
                ctx.Buffer.EmitBne(6);
                ctx.Buffer.EmitLdaZeroPage(zp);
                ctx.Buffer.EmitCmpImmediate(immLo);
                ctx.Buffer.EmitBcs((sbyte)skipBytes);
                break;

            default:
                throw new InvalidOperationException($"Unsupported word comparison: {op}");
        }
    }

    /// <summary>
    /// Emit 16-bit comparison between two word variables.
    /// Same logic as literal version but uses CMP zp instead of CMP #imm.
    /// </summary>
    public void EmitWordVarComparisonBranchTrue(string leftName, TokenKind op, string rightName, int skipBytes)
    {
        var lZp = ctx.Symbols.GetLocal(leftName);
        var lHi = (byte)(lZp + 1);
        var rZp = ctx.Symbols.GetLocal(rightName);
        var rHi = (byte)(rZp + 1);

        switch (op)
        {
            case TokenKind.Less: // left < right
                ctx.Buffer.EmitLdaZeroPage(lHi);
                ctx.Buffer.EmitCmpZeroPage(rHi);
                ctx.Buffer.EmitBcc((sbyte)(8 + skipBytes));
                ctx.Buffer.EmitBne(6);
                ctx.Buffer.EmitLdaZeroPage(lZp);
                ctx.Buffer.EmitCmpZeroPage(rZp);
                ctx.Buffer.EmitBcc((sbyte)skipBytes);
                break;

            case TokenKind.Greater: // left > right → right < left
                ctx.Buffer.EmitLdaZeroPage(rHi);
                ctx.Buffer.EmitCmpZeroPage(lHi);
                ctx.Buffer.EmitBcc((sbyte)(8 + skipBytes));
                ctx.Buffer.EmitBne(6);
                ctx.Buffer.EmitLdaZeroPage(rZp);
                ctx.Buffer.EmitCmpZeroPage(lZp);
                ctx.Buffer.EmitBcc((sbyte)skipBytes);
                break;

            case TokenKind.LessEqual: // left <= right → !(right < left) → right >= left
                ctx.Buffer.EmitLdaZeroPage(lHi);
                ctx.Buffer.EmitCmpZeroPage(rHi);
                ctx.Buffer.EmitBcc((sbyte)(8 + skipBytes));
                ctx.Buffer.EmitBne(6);
                ctx.Buffer.EmitLdaZeroPage(rZp);
                ctx.Buffer.EmitCmpZeroPage(lZp);
                ctx.Buffer.EmitBcs((sbyte)skipBytes);
                break;

            case TokenKind.GreaterEqual: // left >= right
                ctx.Buffer.EmitLdaZeroPage(rHi);
                ctx.Buffer.EmitCmpZeroPage(lHi);
                ctx.Buffer.EmitBcc((sbyte)(8 + skipBytes));
                ctx.Buffer.EmitBne(6);
                ctx.Buffer.EmitLdaZeroPage(lZp);
                ctx.Buffer.EmitCmpZeroPage(rZp);
                ctx.Buffer.EmitBcs((sbyte)skipBytes);
                break;

            case TokenKind.EqualEqual:
                ctx.Buffer.EmitLdaZeroPage(lZp);
                ctx.Buffer.EmitCmpZeroPage(rZp);
                ctx.Buffer.EmitBne(4);
                ctx.Buffer.EmitLdaZeroPage(lHi);
                ctx.Buffer.EmitCmpZeroPage(rHi);
                ctx.Buffer.EmitBeq((sbyte)skipBytes);
                break;

            case TokenKind.BangEqual:
                ctx.Buffer.EmitLdaZeroPage(lZp);
                ctx.Buffer.EmitCmpZeroPage(rZp);
                ctx.Buffer.EmitBne((sbyte)(4 + skipBytes));
                ctx.Buffer.EmitLdaZeroPage(lHi);
                ctx.Buffer.EmitCmpZeroPage(rHi);
                ctx.Buffer.EmitBne((sbyte)skipBytes);
                break;

            default:
                throw new InvalidOperationException($"Unsupported word comparison: {op}");
        }
    }
}
