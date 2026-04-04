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

        // Register struct types
        foreach (var decl in program.Declarations)
            if (decl is StructDeclNode sd)
                emitter.RegisterStructType(sd);

        // Allocate globals first (persist across scenes)
        foreach (var gv in globalVars)
            emitter.AllocGlobal(gv.Name);
        foreach (var decl in program.Declarations)
            if (decl is GlobalMethodNode gm)
                emitter.RegisterGlobalMethod(gm);

        // Emit SEI + global init
        emitter.Buffer.EmitSei();
        foreach (var gv in globalVars)
            emitter.EmitGlobalInit(gv);

        // Copy sprite data (63-byte const arrays) to their target addresses
        // Sprite pointers reference address/64 within VIC bank 0
        // Look for matching spritePtr globals and spriteData arrays
        emitter.EmitSpriteCopies(program, dataAddresses);

        // Jump to entry scene
        emitter.Buffer.EmitJmpForward($"_scene_{entryScene.Name}");

        // Emit all scenes
        foreach (var scene in scenes)
            emitter.EmitScene(scene);

        // Emit global methods (shared across scenes)
        var globalMethods = program.Declarations.OfType<GlobalMethodNode>().ToList();
        foreach (var gm in globalMethods)
            emitter.EmitGlobalMethod(gm);

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
        foreach (var decl in program.Declarations)
            if (decl is StructDeclNode sd)
                emitter.RegisterStructType(sd);
        foreach (var gv in globalVars)
            emitter.AllocGlobal(gv.Name);
        foreach (var decl in program.Declarations)
            if (decl is GlobalMethodNode gm2)
                emitter.RegisterGlobalMethod(gm2);

        emitter.Buffer.EmitSei();
        foreach (var gv in globalVars)
            emitter.EmitGlobalInit(gv);
        emitter.EmitSpriteCopies(program, dataAddresses);
        emitter.Buffer.EmitJmpForward($"_scene_{scenes.First(s => s.IsEntry).Name}");

        foreach (var scene in scenes)
            emitter.EmitScene(scene);

        foreach (var decl in program.Declarations)
            if (decl is GlobalMethodNode gm)
                emitter.EmitGlobalMethod(gm);

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

        // Struct support
        private readonly Dictionary<string, StructDeclNode> _structTypes = [];
        // instance name -> (structType, fieldName -> zpAddr)
        private readonly Dictionary<string, (string StructType, Dictionary<string, byte> Fields)> _structInstances = [];
        // Pending struct method emissions
        private readonly HashSet<string> _emittedStructMethods = [];
        // Type tracking: variable name -> "byte" or "word"
        private readonly Dictionary<string, string> _varTypes = [];
        // ZP pointer for indirect indexed addressing
        private const byte ZpPointerLo = 0xFB;
        private const byte ZpPointerHi = 0xFC;

        public void RegisterStructType(StructDeclNode structDecl)
        {
            _structTypes[structDecl.Name] = structDecl;
        }

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

        public void RegisterGlobalMethod(GlobalMethodNode gm)
        {
            _methodParams[gm.SelectorName] = gm.Parameters;
            foreach (var param in gm.Parameters)
            {
                if (param.ParamName != "")
                    AllocZeroPage(param.ParamName);
            }
        }

        public void EmitSpriteCopies(ProgramNode program, Dictionary<string, ushort> dataAddresses)
        {
            // Find all 63-byte const arrays (sprite data) and copy them to the right VIC bank address
            // Convention: a const byte[63] named XxxData with a matching const byte XxxPtr
            // copies the data to address (ptrValue * 64)
            foreach (var decl in program.Declarations)
            {
                if (decl is ConstArrayDeclNode { Size: 63 } constArr &&
                    dataAddresses.TryGetValue(constArr.Name, out var srcAddr))
                {
                    // Find matching pointer: spritePtr, or same prefix + "Ptr"
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

                    // Emit copy loop: LDX #62; .loop: LDA src,X; STA dest,X; DEX; BPL .loop
                    Buffer.EmitLdxImmediate(62);
                    // LDA src,X (3 bytes)
                    Buffer.EmitLdaAbsoluteX(srcAddr);
                    // STA dest,X (3 bytes)
                    Buffer.EmitStaAbsoluteX(destAddr);
                    Buffer.EmitDex();
                    // LDA abs,X = 3 bytes + STA abs,X = 3 bytes + DEX = 1 byte + BPL = 2 bytes = 9
                    // BPL offset = -(3+3+1+2) = -9 from after BPL
                    Buffer.EmitBpl(unchecked((sbyte)(-9)));
                }
            }
        }

        public void EmitGlobalMethod(GlobalMethodNode gm)
        {
            var label = $"_method_{gm.SelectorName}";
            Buffer.DefineLabel(label);
            EmitBlock(gm.Body);
            Buffer.EmitRts();
        }

        public void EmitScene(SceneNode scene)
        {
            // Reset scene-local ZP allocation (after globals)
            _locals.Clear();
            _varTypes.Clear();
            _structInstances.Clear();
            _emittedStructMethods.Clear();
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

            // Set up raster interrupts if any
            var rasterBlocks = scene.Members.OfType<RasterBlockNode>()
                .OrderBy(r => r.Line).ToList();
            if (rasterBlocks.Count > 0)
            {
                AllocZeroPage($"_ridx_{scene.Name}"); // pre-allocate raster index
                EmitRasterIrqSetup(rasterBlocks, scene.Name);
            }

            // Frame loop with vsync
            var frameLabel = $"_frame_loop_{scene.Name}";
            Buffer.DefineLabel(frameLabel);
            EmitVsyncWait();
            foreach (var member in scene.Members)
            {
                if (member is FrameBlockNode frame)
                    EmitBlock(frame.Body);
            }
            Buffer.EmitJmpAbsolute(Buffer.GetLabel(frameLabel));

            // Emit scene methods after the main loop
            foreach (var member in scene.Members)
            {
                if (member is SceneMethodNode method)
                    EmitSceneMethod(method);
            }

            // Emit struct methods for instances in this scene
            EmitPendingStructMethods(scene);

            // Emit raster interrupt handler
            if (rasterBlocks.Count > 0)
                EmitRasterHandler(rasterBlocks, scene.Name);
        }

        private void EmitPendingStructMethods(SceneNode scene)
        {
            foreach (var (instanceName, inst) in _structInstances)
            {
                if (!_structTypes.TryGetValue(inst.StructType, out var structType)) continue;

                foreach (var method in structType.Methods)
                {
                    var label = $"_struct_{inst.StructType}_{method.SelectorName}_{instanceName}";
                    if (!_emittedStructMethods.Contains(label)) continue;

                    Buffer.DefineLabel(label);

                    // Temporarily map bare field names to this instance's ZP slots
                    var savedLocals = new Dictionary<string, byte>();
                    foreach (var (fieldName, zpAddr) in inst.Fields)
                    {
                        if (_locals.TryGetValue(fieldName, out var old))
                            savedLocals[fieldName] = old;
                        _locals[fieldName] = zpAddr;
                    }

                    EmitBlock(method.Body);
                    Buffer.EmitRts();

                    // Restore
                    foreach (var (fieldName, _) in inst.Fields)
                    {
                        if (savedLocals.TryGetValue(fieldName, out var old))
                            _locals[fieldName] = old;
                        else
                            _locals.Remove(fieldName);
                    }
                }
            }
        }

        private void EmitRasterIrqSetup(List<RasterBlockNode> rasters, string sceneName)
        {
            // JSR to setup routine (emitted after handler so addresses are known)
            Buffer.EmitJsrForward($"_irq_setup_{sceneName}");
        }

        private void EmitRasterHandler(List<RasterBlockNode> rasters, string sceneName)
        {
            var handlerLabel = $"_irq_handler_{sceneName}";
            var setupLabel = $"_irq_setup_{sceneName}";
            var doneLabel = $"_irq_done_{sceneName}";
            var firstLine = (byte)rasters[0].Line;

            // --- Setup routine ---
            Buffer.DefineLabel(setupLabel);
            Buffer.EmitLdaImmediate(0x7F);
            Buffer.EmitStaAbsolute(0xDC0D);         // disable CIA1 interrupts
            Buffer.EmitLdaAbsolute(0xDC0D);          // ack pending
            Buffer.EmitLdaImmediate(firstLine);
            Buffer.EmitStaAbsolute(0xD012);          // first raster line
            Buffer.EmitLdaAbsolute(0xD011);
            Buffer.EmitByte(0x29); Buffer.EmitByte(0x7F); // AND #$7F — clear bit 8
            Buffer.EmitStaAbsolute(0xD011);
            Buffer.EmitLdaImmediate(0x01);
            Buffer.EmitStaAbsolute(0xD01A);          // enable VIC raster IRQ
            Buffer.EmitStoreAddrForward(handlerLabel, 0x0314, 0x0315); // set vector
            Buffer.EmitLdaImmediate(0);
            Buffer.EmitStaZeroPage(GetLocal($"_ridx_{sceneName}")); // init index = 0
            Buffer.EmitCli();                        // enable interrupts
            Buffer.EmitRts();

            // --- IRQ handler ---
            // Uses a ZP index to track which raster block to run next.
            // This avoids comparing against $D012 (which has timing jitter).
            // KERNAL at $FF48 already saved A/X/Y. End with JMP $EA31.
            var rasterIdxZp = GetLocal($"_ridx_{sceneName}");
            Buffer.DefineLabel(handlerLabel);
            Buffer.EmitLdaImmediate(0xFF);
            Buffer.EmitStaAbsolute(0xD019);          // ack VIC interrupt

            // Dispatch by index: LDX idx; CPX #0; BEQ block0; CPX #1; BEQ block1; ...
            Buffer.EmitLdxZeroPage(rasterIdxZp);
            for (var i = 0; i < rasters.Count; i++)
            {
                Buffer.EmitCpxImmediate((byte)i);
                Buffer.EmitBne(3);
                Buffer.EmitJmpForward($"_rblk_{sceneName}_{i}");
            }
            Buffer.EmitJmpAbsolute(0xEA31);          // fallthrough (shouldn't happen)

            // Handler blocks
            for (var i = 0; i < rasters.Count; i++)
            {
                var nextIdx = (byte)((i + 1) % rasters.Count);
                var nextLine = (byte)rasters[(i + 1) % rasters.Count].Line;

                Buffer.DefineLabel($"_rblk_{sceneName}_{i}");
                EmitBlock(rasters[i].Body);
                Buffer.EmitLdaImmediate(nextLine);
                Buffer.EmitStaAbsolute(0xD012);      // set next raster line
                Buffer.EmitLdaImmediate(nextIdx);
                Buffer.EmitStaZeroPage(rasterIdxZp); // advance index
                Buffer.EmitJmpAbsolute(0xEA31);      // exit via KERNAL
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
            // Struct instance: allocate ZP for each field with defaults
            if (_structTypes.TryGetValue(decl.TypeName, out var structType))
            {
                var fieldMap = new Dictionary<string, byte>();
                foreach (var field in structType.Fields)
                {
                    var fieldZpName = $"{decl.Name}${field.Name}";
                    var zp = AllocZeroPage(fieldZpName);
                    fieldMap[field.Name] = zp;

                    // Initialize with default value
                    if (field.DefaultValue is IntLiteralExpr intLit)
                        Buffer.EmitLdaImmediate((byte)intLit.Value);
                    else
                        Buffer.EmitLdaImmediate(0);
                    Buffer.EmitStaZeroPage(zp);
                }
                _structInstances[decl.Name] = (decl.TypeName, fieldMap);
                return;
            }

            // Word variable
            if (decl.TypeName == "word")
            {
                var zpAddr = AllocWordZeroPage(decl.Name);
                if (decl.Initializer is IntLiteralExpr wordLit)
                {
                    var val = (ushort)wordLit.Value;
                    Buffer.EmitLdaImmediate((byte)(val & 0xFF));
                    Buffer.EmitStaZeroPage(zpAddr);
                    Buffer.EmitLdaImmediate((byte)(val >> 8));
                    Buffer.EmitStaZeroPage((byte)(zpAddr + 1));
                }
                else
                {
                    Buffer.EmitLdaImmediate(0);
                    Buffer.EmitStaZeroPage(zpAddr);
                    Buffer.EmitStaZeroPage((byte)(zpAddr + 1));
                }
                return;
            }

            // Byte variable
            var zpAddr2 = AllocZeroPage(decl.Name);
            _varTypes[decl.Name] = "byte";
            if (decl.Initializer != null)
            {
                EmitExprToA(decl.Initializer);
                Buffer.EmitStaZeroPage(zpAddr2);
            }
            else
            {
                Buffer.EmitLdaImmediate(0);
                Buffer.EmitStaZeroPage(zpAddr2);
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
            if (decl.TypeName == "word")
            {
                var zp = AllocWordZeroPage(decl.Name);
                if (decl.Initializer is IntLiteralExpr intLit)
                {
                    var val = (ushort)intLit.Value;
                    Buffer.EmitLdaImmediate((byte)(val & 0xFF));
                    Buffer.EmitStaZeroPage(zp);
                    Buffer.EmitLdaImmediate((byte)(val >> 8));
                    Buffer.EmitStaZeroPage((byte)(zp + 1));
                }
                else
                {
                    Buffer.EmitLdaImmediate(0);
                    Buffer.EmitStaZeroPage(zp);
                    Buffer.EmitStaZeroPage((byte)(zp + 1));
                }
            }
            else
            {
                var zp = AllocZeroPage(decl.Name);
                _varTypes[decl.Name] = "byte";
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
        }

        private byte AllocWordZeroPage(string name)
        {
            var addr = _nextZp;
            _nextZp += 2; // word takes 2 bytes
            if (_nextZp >= 0xFB) throw new InvalidOperationException("Out of zero page slots");
            _locals[name] = addr;
            _varTypes[name] = "word";
            return addr;
        }

        private void EmitAssignment(AssignmentStmt assign)
        {
            // Resolve target
            byte zp;
            string varName = "";
            if (assign.Target is IdentifierExpr ident)
            {
                zp = GetLocal(ident.Name);
                varName = ident.Name;
            }
            else if (assign.Target is MemberAccessExpr member && TryResolveStructField(member, out var fieldZp))
            {
                zp = fieldZp;
            }
            else
            {
                throw new InvalidOperationException("Unsupported assignment target");
            }

            // Word (16-bit) assignment
            if (varName != "" && IsWordVar(varName))
            {
                EmitWordAssignment(zp, assign.Op, assign.Value);
                return;
            }

            // Byte (8-bit) assignment
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

            // 16-bit comparison: word var vs literal
            if (condition is BinaryExpr wordBin &&
                wordBin.Left is IdentifierExpr wordIdent &&
                IsWordVar(wordIdent.Name) &&
                wordBin.Right is IntLiteralExpr wordLit)
            {
                EmitWordComparisonBranchTrue(wordIdent.Name, wordBin.Op, (ushort)wordLit.Value, skipBytes);
                return;
            }

            // 8-bit binary comparison
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
                    case TokenKind.Greater:
                        // A > operand: Carry=1,Zero=0. BEQ skips BCS (equal=not greater).
                        // BCS skips the JMP (greater=enter body). BCC falls to JMP (less).
                        Buffer.EmitBeq(2);                      // equal → skip BCS, fall to JMP
                        Buffer.EmitBcs((sbyte)skipBytes);       // greater → skip JMP, enter body
                        break;
                    case TokenKind.LessEqual:
                        // A <= operand: equal or less. BEQ skips BCC+JMP (enter body).
                        // BCC skips JMP (less=enter body). Fall to JMP (greater=skip).
                        Buffer.EmitBeq((sbyte)(2 + skipBytes)); // equal → skip BCC+JMP
                        Buffer.EmitBcc((sbyte)skipBytes);       // less → skip JMP
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

            // Built-in intrinsic: pokeScreen: wordOffset value: byteVal
            if (msgSend.Receiver == null && msgSend.Segments.Count == 2 &&
                msgSend.Segments[0].Name == "pokeScreen" && msgSend.Segments[1].Name == "value")
            {
                EmitPokeScreen(msgSend.Segments[0].Argument!, msgSend.Segments[1].Argument!);
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

            // Struct method call: receiver methodName:;
            if (msgSend.Receiver is IdentifierExpr receiverIdent &&
                _structInstances.TryGetValue(receiverIdent.Name, out var inst))
            {
                var selectorName = string.Join("", msgSend.Segments.Select(s => s.Name + ":"));
                var label = $"_struct_{inst.StructType}_{selectorName}_{receiverIdent.Name}";

                if (!_emittedStructMethods.Contains(label))
                {
                    _emittedStructMethods.Add(label);
                    // Queue for emission — will be emitted after scene code
                }

                Buffer.EmitJsrForward(label);
                return;
            }

            throw new InvalidOperationException($"Unsupported message send");
        }

        private void EmitPoke(ExprNode addressExpr, ExprNode valueExpr)
        {
            // poke: (constant + indexExpr) value: expr  →  LDX index; LDA value; STA base,X
            if (addressExpr is BinaryExpr { Op: TokenKind.Plus } addExpr &&
                addExpr.Left is IntLiteralExpr baseAddr)
            {
                // Evaluate index to X register
                EmitExprToA(addExpr.Right);
                Buffer.EmitTax();
                // Evaluate value to A
                EmitExprToA(valueExpr);
                Buffer.EmitStaAbsoluteX((ushort)baseAddr.Value);
            }
            // poke: constant value: expr  →  LDA value; STA abs
            else if (addressExpr is IntLiteralExpr constAddr)
            {
                EmitExprToA(valueExpr);
                Buffer.EmitStaAbsolute((ushort)constAddr.Value);
            }
            else
            {
                throw new InvalidOperationException("poke address must be constant or constant + expression");
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
                    if (TryResolveStructField(member, out var fieldZp))
                        Buffer.EmitLdaZeroPage(fieldZp);
                    else
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

        private bool TryResolveStructField(MemberAccessExpr member, out byte zpAddr)
        {
            zpAddr = 0;
            if (member.Receiver is IdentifierExpr ident &&
                _structInstances.TryGetValue(ident.Name, out var instance) &&
                instance.Fields.TryGetValue(member.MemberName, out zpAddr))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Emits 16-bit comparison + branch-if-true over skipBytes.
        /// Layout: comparison code (fixed size) + JMP-false (3 bytes).
        /// If true, execution skips the JMP and falls into skipBytes territory.
        /// </summary>
        private void EmitWordComparisonBranchTrue(string varName, TokenKind op, ushort literal, int skipBytes)
        {
            var zp = GetLocal(varName);
            var zpHi = (byte)(zp + 1);
            var immLo = (byte)(literal & 0xFF);
            var immHi = (byte)(literal >> 8);

            // For Less (<): true if var < literal
            // For Greater (>): true if var > literal
            // For LessEqual (<=): true if var <= literal
            // For GreaterEqual (>=): true if var >= literal
            // For EqualEqual (==): true if var == literal
            // For BangEqual (!=): true if var != literal

            // Strategy: emit comparison code ending with a conditional structure where
            // "true" falls through past a JMP, and "false" hits the JMP.
            var falseLabel = NextLabel("wcmp_f");

            // Layout: 14 bytes of comparison code, then caller emits JMP (3 bytes), then body.
            // Branch offsets calculated from PC after each branch instruction:
            //   Byte 0-1: LDA (2)    Byte 2-3: CMP (2)
            //   Byte 4-5: Bxx (2)    Byte 6-7: BNE (2)
            //   Byte 8-9: LDA (2)    Byte 10-11: CMP (2)
            //   Byte 12-13: Bxx (2)
            //   Byte 14-16: JMP end (3) ← emitted by caller
            //   Byte 17: body starts
            //
            // From byte 4→6:  to body (17) = 11 = 8 + skipBytes ← "early true"
            // From byte 6→8:  to JMP (14)  = 6                  ← "early false"
            // From byte 12→14: to body (17) = 3 = skipBytes     ← "late true"
            // Fall through at 14: hits JMP = false

            switch (op)
            {
                case TokenKind.Less:
                    Buffer.EmitLdaZeroPage(zpHi);
                    Buffer.EmitCmpImmediate(immHi);
                    Buffer.EmitBcc((sbyte)(8 + skipBytes)); // hi < litHi → TRUE
                    Buffer.EmitBne(6);                      // hi > litHi → FALSE
                    Buffer.EmitLdaZeroPage(zp);
                    Buffer.EmitCmpImmediate(immLo);
                    Buffer.EmitBcc((sbyte)skipBytes);        // lo < litLo → TRUE
                    break;

                case TokenKind.Greater:
                    Buffer.EmitLdaImmediate(immHi);
                    Buffer.EmitCmpZeroPage(zpHi);
                    Buffer.EmitBcc((sbyte)(8 + skipBytes)); // litHi < varHi → TRUE
                    Buffer.EmitBne(6);                      // litHi > varHi → FALSE
                    Buffer.EmitLdaImmediate(immLo);
                    Buffer.EmitCmpZeroPage(zp);
                    Buffer.EmitBcc((sbyte)skipBytes);        // litLo < varLo → TRUE
                    break;

                case TokenKind.LessEqual:
                    Buffer.EmitLdaZeroPage(zpHi);
                    Buffer.EmitCmpImmediate(immHi);
                    Buffer.EmitBcc((sbyte)(8 + skipBytes)); // hi < litHi → TRUE
                    Buffer.EmitBne(6);                      // hi > litHi → FALSE
                    Buffer.EmitLdaImmediate(immLo);
                    Buffer.EmitCmpZeroPage(zp);
                    Buffer.EmitBcs((sbyte)skipBytes);        // litLo >= varLo → TRUE
                    break;

                case TokenKind.GreaterEqual:
                    Buffer.EmitLdaImmediate(immHi);
                    Buffer.EmitCmpZeroPage(zpHi);
                    Buffer.EmitBcc((sbyte)(8 + skipBytes)); // litHi < varHi → TRUE
                    Buffer.EmitBne(6);                      // litHi > varHi → FALSE
                    Buffer.EmitLdaZeroPage(zp);
                    Buffer.EmitCmpImmediate(immLo);
                    Buffer.EmitBcs((sbyte)skipBytes);        // varLo >= litLo → TRUE
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported word comparison: {op}");
            }
            // FALSE path: JMP is emitted by the caller after this method returns
        }

        private bool IsWordVar(string name) =>
            _varTypes.TryGetValue(name, out var t) && t == "word";

        private void EmitWordAssignment(byte zpLo, TokenKind op, ExprNode value)
        {
            var zpHi = (byte)(zpLo + 1);
            if (op == TokenKind.Equal && value is IntLiteralExpr intLit)
            {
                var val = (ushort)intLit.Value;
                Buffer.EmitLdaImmediate((byte)(val & 0xFF));
                Buffer.EmitStaZeroPage(zpLo);
                Buffer.EmitLdaImmediate((byte)(val >> 8));
                Buffer.EmitStaZeroPage(zpHi);
            }
            else if (op == TokenKind.PlusEqual && value is IntLiteralExpr addLit)
            {
                var val = (ushort)addLit.Value;
                Buffer.EmitLdaZeroPage(zpLo);
                Buffer.EmitClc();
                Buffer.EmitAdcImmediate((byte)(val & 0xFF));
                Buffer.EmitStaZeroPage(zpLo);
                Buffer.EmitLdaZeroPage(zpHi);
                Buffer.EmitAdcImmediate((byte)(val >> 8));
                Buffer.EmitStaZeroPage(zpHi);
            }
            else if (op == TokenKind.MinusEqual && value is IntLiteralExpr subLit)
            {
                var val = (ushort)subLit.Value;
                Buffer.EmitLdaZeroPage(zpLo);
                Buffer.EmitSec();
                Buffer.EmitSbcImmediate((byte)(val & 0xFF));
                Buffer.EmitStaZeroPage(zpLo);
                Buffer.EmitLdaZeroPage(zpHi);
                Buffer.EmitSbcImmediate((byte)(val >> 8));
                Buffer.EmitStaZeroPage(zpHi);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported word assignment: {op}");
            }
        }

        /// <summary>
        /// pokeScreen: wordOffset value: byteValue
        /// Writes byteValue to $0400 + wordOffset using indirect indexed addressing.
        /// </summary>
        private void EmitPokeScreen(ExprNode offsetExpr, ExprNode valueExpr)
        {
            if (offsetExpr is IdentifierExpr ident && IsWordVar(ident.Name))
            {
                var zp = GetLocal(ident.Name);
                // Calculate $0400 + word into ZP pointer
                Buffer.EmitLdaZeroPage(zp);
                Buffer.EmitClc();
                Buffer.EmitAdcImmediate(0x00); // low byte of $0400
                Buffer.EmitStaZeroPage(ZpPointerLo);
                Buffer.EmitLdaZeroPage((byte)(zp + 1));
                Buffer.EmitAdcImmediate(0x04); // high byte of $0400
                Buffer.EmitStaZeroPage(ZpPointerHi);
                // Store value via indirect indexed
                EmitExprToA(valueExpr);
                Buffer.EmitLdyImmediate(0);
                Buffer.EmitStaIndirectY(ZpPointerLo);
            }
            else
            {
                throw new InvalidOperationException("pokeScreen offset must be a word variable");
            }
        }
    }
}
