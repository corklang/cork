namespace Cork.CodeGen.Emit;

using Cork.Language.Ast;
using Cork.Language.Lexing;

/// <summary>
/// Phase 1 code generator: direct AST-to-6510 emission.
/// Handles only the minimal subset needed for hello.cork.
/// Zero page $02-$0E reserved as compiler locals. $0F as temp.
/// </summary>
public sealed class Phase1CodeGen(ushort codeBase = 0x0810)
{
    public (byte[] Code, ushort EntryPoint) Generate(ProgramNode program)
    {
        // Calculate data section: const arrays placed after code
        // We need two passes: first to measure code size, then emit with known addresses.
        // For Phase 1, just do it in one pass with forward JMP fixups.

        // Collect const data
        var dataBytes = new List<byte>();
        var dataNames = new Dictionary<string, int>(); // name -> offset within data section
        foreach (var decl in program.Declarations)
        {
            if (decl is ConstArrayDeclNode constArr)
            {
                dataNames[constArr.Name] = dataBytes.Count;
                foreach (var val in constArr.Values)
                {
                    if (val is IntLiteralExpr intLit)
                        dataBytes.Add((byte)intLit.Value);
                    else
                        throw new InvalidOperationException("Const array values must be integer literals");
                }
            }
        }

        // First pass: emit code to measure size
        var measuredSize = EmitPass(program, dataNames, codeBase, 0xFFFF, measure: true);

        // Now we know where data goes
        var dataStart = (ushort)(codeBase + measuredSize);
        var dataAddresses = new Dictionary<string, ushort>();
        foreach (var (name, offset) in dataNames)
            dataAddresses[name] = (ushort)(dataStart + offset);

        // Second pass: emit code with correct data addresses
        var emitter = new Emitter(codeBase, dataAddresses);
        foreach (var decl in program.Declarations)
        {
            if (decl is SceneNode { IsEntry: true } scene)
                emitter.EmitScene(scene);
        }
        emitter.Buffer.ResolveFixups();

        // Combine code + data
        var code = emitter.Buffer.ToArray();
        var result = new byte[code.Length + dataBytes.Count];
        code.CopyTo(result, 0);
        dataBytes.CopyTo(result.AsSpan()[code.Length..]);
        return (result, codeBase);
    }

    private static int EmitPass(ProgramNode program, Dictionary<string, int> dataNames, ushort codeBase, ushort dataStart, bool measure)
    {
        var dataAddresses = new Dictionary<string, ushort>();
        foreach (var (name, offset) in dataNames)
            dataAddresses[name] = (ushort)(dataStart + offset);

        var emitter = new Emitter(codeBase, dataAddresses);
        foreach (var decl in program.Declarations)
        {
            if (decl is SceneNode { IsEntry: true } scene)
                emitter.EmitScene(scene);
        }
        return emitter.Buffer.Length;
    }

    internal sealed class Emitter(ushort codeBase, Dictionary<string, ushort> dataAddresses)
    {
        public AssemblyBuffer Buffer { get; } = new(codeBase);
        private readonly Dictionary<string, byte> _locals = [];
        private byte _nextZp = 0x02;
        private int _labelCounter;

        private string NextLabel(string prefix) => $"{prefix}_{_labelCounter++}";

        public void EmitScene(SceneNode scene)
        {
            // Disable KERNAL interrupts — stops cursor blink and keyboard scan
            Buffer.EmitSei();

            foreach (var member in scene.Members)
                if (member is HardwareBlockNode hw)
                    EmitHardwareBlock(hw);

            foreach (var member in scene.Members)
                if (member is SceneVarDeclNode varDecl)
                    EmitSceneVar(varDecl);

            foreach (var member in scene.Members)
                if (member is EnterBlockNode enter)
                    EmitBlock(enter.Body);

            Buffer.DefineLabel("_frame_loop");
            foreach (var member in scene.Members)
            {
                if (member is FrameBlockNode frame)
                    EmitBlock(frame.Body);
            }
            Buffer.EmitJmpAbsolute(Buffer.GetLabel("_frame_loop"));
        }

        private void EmitHardwareBlock(HardwareBlockNode hw)
        {
            foreach (var setting in hw.Settings)
            {
                var value = EvalConstExpr(setting.Value);
                var addr = setting.Name switch
                {
                    "border" => (ushort)0xD020,
                    "background" => (ushort)0xD021,
                    _ => throw new InvalidOperationException($"Unknown hardware setting: {setting.Name}")
                };
                Buffer.EmitLdaImmediate(value);
                Buffer.EmitStaAbsolute(addr);
            }
        }

        private void EmitSceneVar(SceneVarDeclNode decl)
        {
            var zp = AllocZeroPage(decl.Name);
            if (decl.Initializer != null)
            {
                EmitExprToA(decl.Initializer);
                Buffer.EmitStaZeroPage(zp);
            }
            else
            {
                Buffer.EmitLdaImmediate(0);
                Buffer.EmitStaZeroPage(zp);
            }
        }

        private void EmitBlock(BlockNode block)
        {
            foreach (var stmt in block.Statements)
                EmitStatement(stmt);
        }

        private void EmitStatement(StmtNode stmt)
        {
            switch (stmt)
            {
                case VarDeclStmt varDecl: EmitVarDecl(varDecl); break;
                case AssignmentStmt assign: EmitAssignment(assign); break;
                case WhileStmt whileStmt: EmitWhile(whileStmt); break;
                case IfStmt ifStmt: EmitIf(ifStmt); break;
                case MessageSendStmt msgSend: EmitMessageSend(msgSend); break;
                default: throw new InvalidOperationException($"Unsupported statement: {stmt.GetType().Name}");
            }
        }

        private void EmitVarDecl(VarDeclStmt decl)
        {
            var zp = AllocZeroPage(decl.Name);
            if (decl.Initializer != null)
            {
                EmitExprToA(decl.Initializer);
                Buffer.EmitStaZeroPage(zp);
            }
            else
            {
                Buffer.EmitLdaImmediate(0);
                Buffer.EmitStaZeroPage(zp);
            }
        }

        private void EmitAssignment(AssignmentStmt assign)
        {
            if (assign.Target is not IdentifierExpr ident)
                throw new InvalidOperationException("Phase 1: only simple variable assignments supported");

            var zp = GetLocal(ident.Name);
            switch (assign.Op)
            {
                case TokenKind.Equal:
                    EmitExprToA(assign.Value);
                    Buffer.EmitStaZeroPage(zp);
                    break;
                case TokenKind.PlusEqual:
                    Buffer.EmitLdaZeroPage(zp);
                    Buffer.EmitClc();
                    EmitAdcValue(assign.Value);
                    Buffer.EmitStaZeroPage(zp);
                    break;
                case TokenKind.MinusEqual:
                    Buffer.EmitLdaZeroPage(zp);
                    Buffer.EmitSec();
                    EmitSbcValue(assign.Value);
                    Buffer.EmitStaZeroPage(zp);
                    break;
                default:
                    throw new InvalidOperationException($"Phase 1: unsupported assignment op {assign.Op}");
            }
        }

        private void EmitWhile(WhileStmt whileStmt)
        {
            var loopLabel = NextLabel("wloop");
            var endLabel = NextLabel("wend");

            Buffer.DefineLabel(loopLabel);

            // Emit condition: compare, then branch-over-JMP if false
            EmitComparisonToFlags(whileStmt.Condition);
            EmitBranchIfTrue(2 + 1); // skip over JMP (3 bytes): branch offset = 3
            Buffer.EmitJmpForward(endLabel);

            // Body
            EmitBlock(whileStmt.Body);

            // Back to top
            Buffer.EmitJmpAbsolute(Buffer.GetLabel(loopLabel));

            Buffer.DefineLabel(endLabel);
        }

        private void EmitIf(IfStmt ifStmt)
        {
            var endLabel = NextLabel("endif");

            if (ifStmt.ElseBody == null && ifStmt.ElseIfs.Count == 0)
            {
                // Simple if
                EmitComparisonToFlags(ifStmt.Condition);
                EmitBranchIfTrue(2 + 1); // skip JMP
                Buffer.EmitJmpForward(endLabel);
                EmitBlock(ifStmt.ThenBody);
            }
            else
            {
                var elseLabel = NextLabel("else");
                EmitComparisonToFlags(ifStmt.Condition);
                EmitBranchIfTrue(2 + 1);
                Buffer.EmitJmpForward(elseLabel);
                EmitBlock(ifStmt.ThenBody);
                Buffer.EmitJmpForward(endLabel);
                Buffer.DefineLabel(elseLabel);
                if (ifStmt.ElseBody != null)
                    EmitBlock(ifStmt.ElseBody);
            }

            Buffer.DefineLabel(endLabel);
        }

        /// <summary>
        /// Emits comparison, sets CPU flags. Caller should emit the right branch.
        /// </summary>
        private TokenKind _lastComparisonOp;

        private void EmitComparisonToFlags(ExprNode condition)
        {
            if (condition is not BinaryExpr bin)
                throw new InvalidOperationException("Phase 1: condition must be a comparison");

            _lastComparisonOp = bin.Op;

            // Load left into A, compare with right
            EmitExprToA(bin.Left);

            if (bin.Right is IntLiteralExpr intLit)
            {
                Buffer.EmitCmpImmediate((byte)intLit.Value);
            }
            else if (bin.Right is IdentifierExpr ident)
            {
                Buffer.EmitCmpZeroPage(GetLocal(ident.Name));
            }
            else
            {
                // Complex right side: evaluate to temp
                Buffer.EmitPha();
                EmitExprToA(bin.Right);
                Buffer.EmitStaZeroPage(0x0F);
                Buffer.EmitPla();
                Buffer.EmitCmpZeroPage(0x0F);
            }
        }

        /// <summary>
        /// Emit a branch-if-condition-is-true with the given offset.
        /// Uses the last comparison op to determine which branch to emit.
        /// The offset is relative to the byte after the branch instruction.
        /// </summary>
        private void EmitBranchIfTrue(int offset)
        {
            switch (_lastComparisonOp)
            {
                case TokenKind.Less:
                    Buffer.EmitBcc((sbyte)offset); // carry clear = A < operand
                    break;
                case TokenKind.GreaterEqual:
                    Buffer.EmitBcs((sbyte)offset); // carry set = A >= operand
                    break;
                case TokenKind.EqualEqual:
                    Buffer.EmitBeq((sbyte)offset);
                    break;
                case TokenKind.BangEqual:
                    Buffer.EmitBne((sbyte)offset);
                    break;
                case TokenKind.Greater:
                    // A > operand: carry set AND zero clear
                    // BEQ skip; BCS target; skip:
                    Buffer.EmitBeq(2); // skip BCS if equal
                    Buffer.EmitBcs((sbyte)(offset - 2)); // adjust offset
                    break;
                case TokenKind.LessEqual:
                    // A <= operand: carry clear OR zero set
                    Buffer.EmitBeq((sbyte)offset);
                    Buffer.EmitBcc((sbyte)(offset - 2));
                    break;
                default:
                    throw new InvalidOperationException($"Phase 1: unsupported comparison {_lastComparisonOp}");
            }
        }

        private void EmitMessageSend(MessageSendStmt msgSend)
        {
            if (msgSend.Receiver == null && msgSend.Segments.Count == 2 &&
                msgSend.Segments[0].Name == "poke" && msgSend.Segments[1].Name == "value")
            {
                EmitPoke(msgSend.Segments[0].Argument!, msgSend.Segments[1].Argument!);
                return;
            }

            throw new InvalidOperationException("Phase 1: unsupported message send");
        }

        private void EmitPoke(ExprNode addressExpr, ExprNode valueExpr)
        {
            if (addressExpr is BinaryExpr { Op: TokenKind.Plus } addExpr &&
                addExpr.Left is IntLiteralExpr baseAddr &&
                addExpr.Right is IdentifierExpr indexVar)
            {
                // LDX index; LDA value; STA base,X
                Buffer.EmitLdxZeroPage(GetLocal(indexVar.Name));
                EmitExprToA(valueExpr);
                Buffer.EmitStaAbsoluteX((ushort)baseAddr.Value);
            }
            else
            {
                throw new InvalidOperationException("Phase 1: poke address must be constant + variable");
            }
        }

        private void EmitExprToA(ExprNode expr)
        {
            switch (expr)
            {
                case IntLiteralExpr intLit:
                    Buffer.EmitLdaImmediate((byte)intLit.Value);
                    break;

                case IdentifierExpr ident:
                    Buffer.EmitLdaZeroPage(GetLocal(ident.Name));
                    break;

                case BinaryExpr { Op: TokenKind.Plus } bin:
                    EmitExprToA(bin.Left);
                    Buffer.EmitClc();
                    EmitAdcValue(bin.Right);
                    break;

                case BinaryExpr { Op: TokenKind.Minus } bin:
                    EmitExprToA(bin.Left);
                    Buffer.EmitSec();
                    EmitSbcValue(bin.Right);
                    break;

                case IndexExpr { Receiver: IdentifierExpr arrName } indexExpr
                    when dataAddresses.TryGetValue(arrName.Name, out var dataAddr):
                    EmitExprToA(indexExpr.Index);
                    Buffer.EmitTax();
                    Buffer.EmitLdaAbsoluteX(dataAddr);
                    break;

                case MemberAccessExpr member:
                    Buffer.EmitLdaImmediate(ResolveColorConstant(member));
                    break;

                default:
                    throw new InvalidOperationException($"Phase 1: unsupported expression: {expr.GetType().Name}");
            }
        }

        private void EmitAdcValue(ExprNode expr)
        {
            switch (expr)
            {
                case IntLiteralExpr intLit: Buffer.EmitAdcImmediate((byte)intLit.Value); break;
                case IdentifierExpr ident: Buffer.EmitAdcZeroPage(GetLocal(ident.Name)); break;
                default: throw new InvalidOperationException("Phase 1: ADC operand must be simple");
            }
        }

        private void EmitSbcValue(ExprNode expr)
        {
            switch (expr)
            {
                case IntLiteralExpr intLit: Buffer.EmitSbcImmediate((byte)intLit.Value); break;
                case IdentifierExpr ident: Buffer.EmitSbcZeroPage(GetLocal(ident.Name)); break;
                default: throw new InvalidOperationException("Phase 1: SBC operand must be simple");
            }
        }

        private static byte EvalConstExpr(ExprNode expr) => expr switch
        {
            IntLiteralExpr intLit => (byte)intLit.Value,
            MemberAccessExpr member => ResolveColorConstant(member),
            _ => throw new InvalidOperationException($"Cannot evaluate constant: {expr.GetType().Name}")
        };

        private static byte ResolveColorConstant(MemberAccessExpr member)
        {
            if (member.Receiver is IdentifierExpr { Name: "Color" })
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
            throw new InvalidOperationException($"Unknown constant: {member.Receiver}");
        }

        private byte AllocZeroPage(string name)
        {
            if (_locals.ContainsKey(name)) return _locals[name];
            var addr = _nextZp++;
            if (_nextZp >= 0x0F) throw new InvalidOperationException("Phase 1: out of zero page slots");
            _locals[name] = addr;
            return addr;
        }

        private byte GetLocal(string name) =>
            _locals.TryGetValue(name, out var zp) ? zp
                : throw new InvalidOperationException($"Undefined local: {name}");
    }
}
