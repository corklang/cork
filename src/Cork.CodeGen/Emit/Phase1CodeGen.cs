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
        // Collect const data
        var dataBytes = new List<byte>();
        var dataNames = new Dictionary<string, int>();
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

        // Collect all scenes
        var scenes = program.Declarations.OfType<SceneNode>().ToList();
        var entryScene = scenes.FirstOrDefault(s => s.IsEntry)
            ?? throw new InvalidOperationException("No entry scene found");

        // Collect global variables
        var globalVars = program.Declarations.OfType<GlobalVarDeclNode>().ToList();

        // First pass: measure code size
        var measuredSize = EmitAll(program, scenes, globalVars, dataNames, codeBase, 0xFFFF).Length;

        // Second pass: emit with correct data addresses
        var dataStart = (ushort)(codeBase + measuredSize);
        var dataAddresses = new Dictionary<string, ushort>();
        foreach (var (name, offset) in dataNames)
            dataAddresses[name] = (ushort)(dataStart + offset);

        var emitter = new Emitter(codeBase, dataAddresses);

        // Allocate globals first (persist across scenes)
        foreach (var gv in globalVars)
            emitter.AllocGlobal(gv.Name);

        // Emit SEI + global init
        emitter.Buffer.EmitSei();
        foreach (var gv in globalVars)
            emitter.EmitGlobalInit(gv);

        // Jump to entry scene
        emitter.Buffer.EmitJmpForward($"_scene_{entryScene.Name}");

        // Emit all scenes
        foreach (var scene in scenes)
            emitter.EmitScene(scene);

        emitter.Buffer.ResolveFixups();

        // Combine code + data
        var code = emitter.Buffer.ToArray();
        var result = new byte[code.Length + dataBytes.Count];
        code.CopyTo(result, 0);
        dataBytes.CopyTo(result.AsSpan()[code.Length..]);
        return (result, codeBase);
    }

    private static byte[] EmitAll(ProgramNode program, List<SceneNode> scenes,
        List<GlobalVarDeclNode> globalVars, Dictionary<string, int> dataNames,
        ushort codeBase, ushort dataStart)
    {
        var dataAddresses = new Dictionary<string, ushort>();
        foreach (var (name, offset) in dataNames)
            dataAddresses[name] = (ushort)(dataStart + offset);

        var emitter = new Emitter(codeBase, dataAddresses);
        foreach (var gv in globalVars)
            emitter.AllocGlobal(gv.Name);

        emitter.Buffer.EmitSei();
        foreach (var gv in globalVars)
            emitter.EmitGlobalInit(gv);
        emitter.Buffer.EmitJmpForward($"_scene_{scenes.First(s => s.IsEntry).Name}");

        foreach (var scene in scenes)
            emitter.EmitScene(scene);

        return emitter.Buffer.ToArray();
    }

    internal sealed class Emitter(ushort codeBase, Dictionary<string, ushort> dataAddresses)
    {
        public AssemblyBuffer Buffer { get; } = new(codeBase);
        private readonly Dictionary<string, byte> _locals = [];
        private byte _nextZp = 0x02;
        private int _labelCounter;

        private string NextLabel(string prefix) => $"{prefix}_{_labelCounter++}";

        private readonly Dictionary<string, List<MethodParameter>> _methodParams = [];
        private readonly Dictionary<string, byte> _globals = [];
        private byte _globalZpEnd = 0x02; // globals occupy $02-$xx

        public void AllocGlobal(string name)
        {
            if (!_globals.ContainsKey(name))
            {
                _globals[name] = _globalZpEnd++;
            }
        }

        public void EmitGlobalInit(GlobalVarDeclNode gv)
        {
            var zp = _globals[gv.Name];
            if (gv.Initializer is IntLiteralExpr intLit)
            {
                Buffer.EmitLdaImmediate((byte)intLit.Value);
                Buffer.EmitStaZeroPage(zp);
            }
            else
            {
                Buffer.EmitLdaImmediate(0);
                Buffer.EmitStaZeroPage(zp);
            }
        }

        public void EmitScene(SceneNode scene)
        {
            // Reset scene-local ZP allocation (after globals)
            _locals.Clear();
            foreach (var (name, zp) in _globals)
                _locals[name] = zp; // globals are accessible in all scenes
            _nextZp = _globalZpEnd;

            // Define scene label for `go` transitions
            Buffer.DefineLabel($"_scene_{scene.Name}");

            // Pre-register method parameters
            foreach (var member in scene.Members)
            {
                if (member is SceneMethodNode method)
                {
                    _methodParams[method.SelectorName] = method.Parameters;
                    foreach (var param in method.Parameters)
                    {
                        if (param.ParamName != "")
                            AllocZeroPage(param.ParamName);
                    }
                }
            }

            // Hardware setup
            foreach (var member in scene.Members)
                if (member is HardwareBlockNode hw)
                    EmitHardwareBlock(hw);

            // Scene variables
            foreach (var member in scene.Members)
                if (member is SceneVarDeclNode varDecl)
                    EmitSceneVar(varDecl);

            // Enter block
            foreach (var member in scene.Members)
                if (member is EnterBlockNode enter)
                    EmitBlock(enter.Body);

            // Frame loop with vsync
            Buffer.DefineLabel("_frame_loop");
            EmitVsyncWait();
            foreach (var member in scene.Members)
            {
                if (member is FrameBlockNode frame)
                    EmitBlock(frame.Body);
            }
            Buffer.EmitJmpAbsolute(Buffer.GetLabel("_frame_loop"));

            // Emit scene methods after the main loop
            foreach (var member in scene.Members)
            {
                if (member is SceneMethodNode method)
                    EmitSceneMethod(method);
            }
        }

        private void EmitVsyncWait()
        {
            // Wait for raster line 251 (vertical blank area)
            // LDA $D012 (3 bytes) + CMP #$FB (2 bytes) + BNE (2 bytes) = 7 bytes
            // BNE target = start of LDA = current_pos - 7 from end of BNE
            // BNE offset = -(3+2+2) = -7
            Buffer.EmitLdaAbsolute(0xD012);     // 3 bytes
            Buffer.EmitCmpImmediate(251);        // 2 bytes
            Buffer.EmitBne(unchecked((sbyte)-7)); // 2 bytes, branch back 7
        }

        private void EmitSceneMethod(SceneMethodNode method)
        {
            var label = $"_method_{method.SelectorName}";
            Buffer.DefineLabel(label);

            // Parameters already allocated in the pre-registration pass
            EmitBlock(method.Body);
            Buffer.EmitRts();
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
                case GoStmt goStmt: Buffer.EmitJmpForward($"_scene_{goStmt.SceneName}"); break;
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

            // Emit condition: branch-if-true skips over the JMP-to-end (3 bytes)
            EmitConditionBranchTrue(whileStmt.Condition, 3);
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
                // Simple if: branch-if-true skips over JMP-to-end (3 bytes)
                EmitConditionBranchTrue(ifStmt.Condition, 3);
                Buffer.EmitJmpForward(endLabel);
                EmitBlock(ifStmt.ThenBody);
            }
            else
            {
                var elseLabel = NextLabel("else");
                EmitConditionBranchTrue(ifStmt.Condition, 3);
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
        /// Emits condition check + branch-if-TRUE over the given number of bytes.
        /// The offset is the number of bytes to skip if condition is true (typically 3 for a JMP).
        /// </summary>
        private void EmitConditionBranchTrue(ExprNode condition, int skipBytes)
        {
            // Joystick check: joystick.port2.{direction}
            if (IsJoystickCheck(condition, out var bitMask))
            {
                // CIA1 $DC00 = joystick port 2. Bits are ACTIVE LOW (0 = pressed).
                // LDA $DC00; AND #mask; BEQ skip (zero = pressed = true)
                Buffer.EmitLdaAbsolute(0xDC00);      // 3 bytes
                Buffer.EmitByte(0x29);                // AND immediate opcode
                Buffer.EmitByte(bitMask);             // 2 bytes
                Buffer.EmitBeq((sbyte)skipBytes);     // 2 bytes: branch if zero (pressed)
                return;
            }

            // Binary comparison
            if (condition is BinaryExpr bin)
            {
                EmitExprToA(bin.Left);

                if (bin.Right is IntLiteralExpr intLit)
                    Buffer.EmitCmpImmediate((byte)intLit.Value);
                else if (bin.Right is IdentifierExpr ident)
                    Buffer.EmitCmpZeroPage(GetLocal(ident.Name));
                else
                {
                    Buffer.EmitPha();
                    EmitExprToA(bin.Right);
                    Buffer.EmitStaZeroPage(0x0F);
                    Buffer.EmitPla();
                    Buffer.EmitCmpZeroPage(0x0F);
                }

                // Emit branch-if-true
                switch (bin.Op)
                {
                    case TokenKind.Less:
                        Buffer.EmitBcc((sbyte)skipBytes);
                        break;
                    case TokenKind.GreaterEqual:
                        Buffer.EmitBcs((sbyte)skipBytes);
                        break;
                    case TokenKind.EqualEqual:
                        Buffer.EmitBeq((sbyte)skipBytes);
                        break;
                    case TokenKind.BangEqual:
                        Buffer.EmitBne((sbyte)skipBytes);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported comparison: {bin.Op}");
                }
                return;
            }

            throw new InvalidOperationException($"Unsupported condition: {condition.GetType().Name}");
        }

        private static bool IsJoystickCheck(ExprNode expr, out byte bitMask)
        {
            bitMask = 0;
            // Match: joystick.port2.{up|down|left|right|fire}
            if (expr is MemberAccessExpr { Receiver: MemberAccessExpr { Receiver: IdentifierExpr { Name: "joystick" }, MemberName: "port2" } } outer)
            {
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

        private void EmitMessageSend(MessageSendStmt msgSend)
        {
            // Built-in intrinsic: poke: addr value: val
            if (msgSend.Receiver == null && msgSend.Segments.Count == 2 &&
                msgSend.Segments[0].Name == "poke" && msgSend.Segments[1].Name == "value")
            {
                EmitPoke(msgSend.Segments[0].Argument!, msgSend.Segments[1].Argument!);
                return;
            }

            // Scene method call (no receiver)
            if (msgSend.Receiver == null && msgSend.Segments.Count > 0)
            {
                var selectorName = string.Join("", msgSend.Segments.Select(s => s.Name + ":"));
                var label = $"_method_{selectorName}";

                // Pass arguments to the method's parameter ZP slots
                if (_methodParams.TryGetValue(selectorName, out var methodParams))
                {
                    for (var i = 0; i < msgSend.Segments.Count; i++)
                    {
                        if (msgSend.Segments[i].Argument != null && i < methodParams.Count && methodParams[i].ParamName != "")
                        {
                            EmitExprToA(msgSend.Segments[i].Argument!);
                            Buffer.EmitStaZeroPage(GetLocal(methodParams[i].ParamName));
                        }
                    }
                }

                // JSR to method (forward reference — method emitted after main loop)
                Buffer.EmitJsrForward(label);
                return;
            }

            throw new InvalidOperationException($"Unsupported message send");
        }

        private void EmitPoke(ExprNode addressExpr, ExprNode valueExpr)
        {
            // poke: (constant + variable) value: expr  →  STA abs,X
            if (addressExpr is BinaryExpr { Op: TokenKind.Plus } addExpr &&
                addExpr.Left is IntLiteralExpr baseAddr &&
                addExpr.Right is IdentifierExpr indexVar)
            {
                Buffer.EmitLdxZeroPage(GetLocal(indexVar.Name));
                EmitExprToA(valueExpr);
                Buffer.EmitStaAbsoluteX((ushort)baseAddr.Value);
            }
            // poke: constant value: expr  →  STA abs
            else if (addressExpr is IntLiteralExpr constAddr)
            {
                EmitExprToA(valueExpr);
                Buffer.EmitStaAbsolute((ushort)constAddr.Value);
            }
            else
            {
                throw new InvalidOperationException("poke address must be constant or constant + variable");
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
