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
            c.Value is not IntLiteralExpr and not MemberAccessExpr);

        if (isExpressionSwitch)
            EmitExpressionSwitch(stmt);
        else
            EmitConstantSwitch(stmt);
    }

    private void EmitConstantSwitch(SwitchStmt stmt)
    {
        var endLabel = ctx.NextLabel("swend");

        ctx.Expressions.EmitExprToA(stmt.Subject);
        ctx.Buffer.EmitStaZeroPage(0x0F);

        for (var i = 0; i < stmt.Cases.Count; i++)
        {
            var caseValue = ctx.Expressions.EvalConstExpr(stmt.Cases[i].Value);
            ctx.Buffer.EmitLdaZeroPage(0x0F);
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
            // Select keyboard matrix column via CIA1 Port A
            ctx.Buffer.EmitLdaImmediate(colSelect);
            ctx.Buffer.EmitStaAbsolute(0xDC00);
            // Settling time: CIA needs ~8μs for keyboard matrix lines to stabilize
            ctx.Buffer.EmitNop();
            ctx.Buffer.EmitNop();
            ctx.Buffer.EmitNop();
            ctx.Buffer.EmitNop();
            // Read row bits from CIA1 Port B (active low: 0 = pressed)
            ctx.Buffer.EmitLdaAbsolute(0xDC01);
            ctx.Buffer.EmitByte(0x29); ctx.Buffer.EmitByte(rowMask); // AND #rowMask
            // Restore $DC00 to $FF (deselect all columns) so joystick reads work
            ctx.Buffer.EmitPha();
            ctx.Buffer.EmitLdaImmediate(0xFF);
            ctx.Buffer.EmitStaAbsolute(0xDC00);
            ctx.Buffer.EmitPla();
            ctx.Buffer.EmitBeq((sbyte)skipBytes); // BEQ = bit clear = key pressed
            return;
        }

        if (condition is BinaryExpr wordBin &&
            wordBin.Left is IdentifierExpr wordIdent &&
            ctx.Symbols.IsWordVar(wordIdent.Name))
        {
            var litVal = ExpressionEmitter.Resolve16BitInitializer("", wordBin.Right);
            EmitWordComparisonBranchTrue(wordIdent.Name, wordBin.Op, litVal, skipBytes);
            return;
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
                ctx.Buffer.EmitStaZeroPage(0x0F);
                ctx.Buffer.EmitPla();
                ctx.Buffer.EmitCmpZeroPage(0x0F);
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
    /// Detects keyboard.KEY_NAME conditions and returns the CIA1 column select
    /// byte ($DC00 write) and row bit mask ($DC01 read).
    /// C64 keyboard is an 8×8 matrix scanned via CIA1: active low on both sides.
    /// </summary>
    public static bool IsKeyboardCheck(ExprNode expr, out byte colSelect, out byte rowMask)
    {
        colSelect = 0;
        rowMask = 0;
        if (expr is MemberAccessExpr { Receiver: IdentifierExpr { Name: "keyboard" } } keyAccess)
        {
            if (!KeyMatrix.TryGetValue(keyAccess.MemberName, out var entry))
                return false;
            colSelect = (byte)~(1 << entry.Col); // active low: clear the target column bit
            rowMask = (byte)(1 << entry.Row);
            return true;
        }
        return false;
    }

    // C64 keyboard matrix: key name → (column 0-7, row 0-7)
    // Column selects $DC00 (write, active low), row reads $DC01 (read, active low)
    private static readonly Dictionary<string, (int Col, int Row)> KeyMatrix = new()
    {
        // Row 0
        ["del"] = (0, 0), ["return"] = (1, 0), ["cursorRight"] = (2, 0),
        ["f7"] = (3, 0), ["f1"] = (4, 0), ["f3"] = (5, 0), ["f5"] = (6, 0),
        ["cursorDown"] = (7, 0),
        // Row 1
        ["n3"] = (0, 1), ["w"] = (1, 1), ["a"] = (2, 1), ["n4"] = (3, 1),
        ["z"] = (4, 1), ["s"] = (5, 1), ["e"] = (6, 1), ["leftShift"] = (7, 1),
        // Row 2
        ["n5"] = (0, 2), ["r"] = (1, 2), ["d"] = (2, 2), ["n6"] = (3, 2),
        ["c"] = (4, 2), ["f"] = (5, 2), ["t"] = (6, 2), ["x"] = (7, 2),
        // Row 3
        ["n7"] = (0, 3), ["y"] = (1, 3), ["g"] = (2, 3), ["n8"] = (3, 3),
        ["b"] = (4, 3), ["h"] = (5, 3), ["u"] = (6, 3), ["v"] = (7, 3),
        // Row 4
        ["n9"] = (0, 4), ["i"] = (1, 4), ["j"] = (2, 4), ["n0"] = (3, 4),
        ["m"] = (4, 4), ["k"] = (5, 4), ["o"] = (6, 4), ["n"] = (7, 4),
        // Row 5
        ["plus"] = (0, 5), ["p"] = (1, 5), ["l"] = (2, 5), ["minus"] = (3, 5),
        ["period"] = (4, 5), ["colon"] = (5, 5), ["at"] = (6, 5), ["comma"] = (7, 5),
        // Row 6
        ["pound"] = (0, 6), ["star"] = (1, 6), ["semicolon"] = (2, 6),
        ["home"] = (3, 6), ["rightShift"] = (4, 6), ["equals"] = (5, 6),
        ["upArrow"] = (6, 6), ["slash"] = (7, 6),
        // Row 7
        ["n1"] = (0, 7), ["leftArrow"] = (1, 7), ["ctrl"] = (2, 7),
        ["n2"] = (3, 7), ["space"] = (4, 7), ["commodore"] = (5, 7),
        ["q"] = (6, 7), ["runStop"] = (7, 7),
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
}
