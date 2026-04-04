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
        foreach (var decl in program.Declarations)
            if (decl is EnumDeclNode ed)
                emitter.RegisterEnumType(ed);

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

        // Runtime library (multiply etc.) — only if needed
        emitter.EmitRuntimeLibrary();

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
        foreach (var decl in program.Declarations)
            if (decl is EnumDeclNode ed)
                emitter.RegisterEnumType(ed);
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

        emitter.EmitRuntimeLibrary();
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

        // Enum support: enum name -> (member name -> value)
        private readonly Dictionary<string, Dictionary<string, long>> _enumTypes = [];
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
        // ZP slots for runtime multiply
        private const byte ZpMulA = 0xF0;
        private const byte ZpMulB = 0xF1;
        private const byte ZpMulResultLo = 0xF2;
        private const byte ZpMulResultHi = 0xF3;
        // Fixed multiply scratch
        private const byte ZpFixedArg1Lo = 0xF4;
        private const byte ZpFixedArg1Hi = 0xF5;
        private const byte ZpFixedArg2Lo = 0xF6;
        private const byte ZpFixedArg2Hi = 0xF7;
        private const byte ZpFixedResB0 = 0xF8; // byte 0 (discarded fractional)
        private const byte ZpFixedResB1 = 0xF9; // byte 1 = result lo
        private const byte ZpFixedResB2 = 0xFA; // byte 2 = result hi
        private const byte ZpSignFlag = 0xEF;   // sign flag for signed multiply (separate from result bytes!)
        // Runtime features — only emitted if needed
        private readonly HashSet<string> _runtime = [];
        // Compile-time constants: name → value (no ZP allocation, inlined everywhere)
        private readonly Dictionary<string, long> _constants = [];
        // Loop context for break/continue
        private readonly Stack<(string BreakLabel, string ContinueLabel)> _loopStack = [];

        public void RegisterStructType(StructDeclNode structDecl)
        {
            _structTypes[structDecl.Name] = structDecl;
        }

        public void RegisterEnumType(EnumDeclNode enumDecl)
        {
            var members = new Dictionary<string, long>();
            foreach (var m in enumDecl.Members)
                members[m.Name] = m.Value;
            _enumTypes[enumDecl.Name] = members;
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
            // Const: store value in compile-time table, no ZP, no code
            if (decl.IsConst && decl.Initializer is IntLiteralExpr constLit)
            {
                _constants[decl.Name] = constLit.Value;
                return;
            }

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

            // Primitive variable (byte, sbyte, word, sword, fixed)
            EmitTypedVarInit(decl.TypeName, decl.Name, decl.Initializer);
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
                case ForStmt forStmt: EmitFor(forStmt); break;
                case SwitchStmt switchStmt: EmitSwitch(switchStmt); break;
                case MessageSendStmt msgSend: EmitMessageSend(msgSend); break;
                case GoStmt goStmt: Buffer.EmitJmpForward($"_scene_{goStmt.SceneName}"); break;
                case BreakStmt: EmitBreak(); break;
                case ContinueStmt: EmitContinue(); break;
                default: throw new InvalidOperationException($"Unsupported statement: {stmt.GetType().Name}");
            }
        }

        private void EmitVarDecl(VarDeclStmt decl)
        {
            if (decl.IsConst && decl.Initializer is IntLiteralExpr constLit)
            {
                _constants[decl.Name] = constLit.Value;
                return;
            }
            EmitTypedVarInit(decl.TypeName, decl.Name, decl.Initializer);
        }

        private void EmitTypedVarInit(string typeName, string name, ExprNode? initializer)
        {
            if (Is16BitType(typeName))
            {
                var zp = AllocWordZeroPage(name);
                _varTypes[name] = typeName;

                // Word/fixed variable initialized from another word/fixed variable
                if (initializer is IdentifierExpr srcIdent && IsWordVar(srcIdent.Name))
                {
                    var srcZp = GetLocal(srcIdent.Name);
                    Buffer.EmitLdaZeroPage(srcZp);
                    Buffer.EmitStaZeroPage(zp);
                    Buffer.EmitLdaZeroPage((byte)(srcZp + 1));
                    Buffer.EmitStaZeroPage((byte)(zp + 1));
                }
                else
                {
                    var val = Resolve16BitInitializer(typeName, initializer);
                    Buffer.EmitLdaImmediate((byte)(val & 0xFF));
                    Buffer.EmitStaZeroPage(zp);
                    Buffer.EmitLdaImmediate((byte)(val >> 8));
                    Buffer.EmitStaZeroPage((byte)(zp + 1));
                }
            }
            else
            {
                var zp = AllocZeroPage(name);
                _varTypes[name] = typeName;
                if (initializer != null)
                {
                    EmitExprToA(initializer);
                    Buffer.EmitStaZeroPage(zp);
                }
                else
                {
                    Buffer.EmitLdaImmediate(0);
                    Buffer.EmitStaZeroPage(zp);
                }
            }
        }

        private static bool Is16BitType(string t) => t is "word" or "sword" or "fixed" or "sfixed";
        private static bool IsSignedType(string t) => t is "sbyte" or "sword" or "sfixed";

        private static ushort Resolve16BitInitializer(string typeName, ExprNode? init)
        {
            if (init == null) return 0;
            if (init is IntLiteralExpr intLit) return (ushort)intLit.Value;
            if (init is FixedLiteralExpr fixLit)
            {
                // Convert double to 8.8 fixed-point
                var intPart = (int)fixLit.Value;
                var fracPart = (int)((fixLit.Value - intPart) * 256);
                if (fixLit.Value < 0)
                {
                    // Two's complement for negative fixed values
                    var raw = (int)(fixLit.Value * 256);
                    return (ushort)(raw & 0xFFFF);
                }
                return (ushort)((intPart << 8) | (fracPart & 0xFF));
            }
            if (init is UnaryExpr { Op: TokenKind.Minus } neg && neg.Operand is FixedLiteralExpr negFix)
            {
                // Negative fixed: -0.75 → two's complement of 0.75 in 8.8
                var raw = (int)(-negFix.Value * 256);
                return (ushort)(raw & 0xFFFF);
            }
            return 0;
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

            _loopStack.Push((endLabel, loopLabel));

            Buffer.DefineLabel(loopLabel);
            EmitConditionBranchTrue(whileStmt.Condition, 3);
            Buffer.EmitJmpForward(endLabel);
            EmitBlock(whileStmt.Body);
            Buffer.EmitJmpAbsolute(Buffer.GetLabel(loopLabel));
            Buffer.DefineLabel(endLabel);

            _loopStack.Pop();
        }

        private void EmitFor(ForStmt forStmt)
        {
            var loopLabel = NextLabel("floop");
            var stepLabel = NextLabel("fstep");
            var endLabel = NextLabel("fend");

            // Init
            EmitStatement(forStmt.Init);

            _loopStack.Push((endLabel, stepLabel));

            // Condition
            Buffer.DefineLabel(loopLabel);
            EmitConditionBranchTrue(forStmt.Condition, 3);
            Buffer.EmitJmpForward(endLabel);

            // Body
            EmitBlock(forStmt.Body);

            // Step
            Buffer.DefineLabel(stepLabel);
            EmitStatement(forStmt.Step);

            // Back to condition
            Buffer.EmitJmpAbsolute(Buffer.GetLabel(loopLabel));
            Buffer.DefineLabel(endLabel);

            _loopStack.Pop();
        }

        private void EmitSwitch(SwitchStmt stmt)
        {
            // Detect expression cases: if any case value is not a simple constant,
            // emit as an if-else chain instead of a CMP dispatch.
            var isExpressionSwitch = stmt.Cases.Any(c =>
                c.Value is not IntLiteralExpr and not MemberAccessExpr);

            if (isExpressionSwitch)
                EmitExpressionSwitch(stmt);
            else
                EmitConstantSwitch(stmt);
        }

        private void EmitConstantSwitch(SwitchStmt stmt)
        {
            var endLabel = NextLabel("swend");

            EmitExprToA(stmt.Subject);
            Buffer.EmitStaZeroPage(0x0F);

            for (var i = 0; i < stmt.Cases.Count; i++)
            {
                var caseValue = EvalConstExpr(stmt.Cases[i].Value);
                Buffer.EmitLdaZeroPage(0x0F);
                Buffer.EmitCmpImmediate(caseValue);
                Buffer.EmitBne(3);
                Buffer.EmitJmpForward($"swcase_{endLabel}_{i}");
            }

            if (stmt.DefaultBody != null)
                Buffer.EmitJmpForward($"swdef_{endLabel}");
            else
                Buffer.EmitJmpForward(endLabel);

            for (var i = 0; i < stmt.Cases.Count; i++)
            {
                Buffer.DefineLabel($"swcase_{endLabel}_{i}");
                foreach (var s in stmt.Cases[i].Body)
                    EmitStatement(s);
                if (!stmt.IsFallthrough)
                    Buffer.EmitJmpForward(endLabel);
            }

            if (stmt.DefaultBody != null)
            {
                Buffer.DefineLabel($"swdef_{endLabel}");
                EmitBlock(stmt.DefaultBody);
            }

            Buffer.DefineLabel(endLabel);
        }

        private void EmitExpressionSwitch(SwitchStmt stmt)
        {
            // Expression cases: emit as if-else chain.
            // switch (subject) { case expr1: body1; case expr2: body2; default: body3; }
            // becomes: evaluate each case expr, branch if true, else next case.
            var endLabel = NextLabel("swend");

            for (var i = 0; i < stmt.Cases.Count; i++)
            {
                var nextLabel = (i < stmt.Cases.Count - 1)
                    ? NextLabel($"swnext_{i}")
                    : (stmt.DefaultBody != null ? NextLabel("swdef") : endLabel);

                // Emit condition branch: if case expr is true, enter body
                EmitConditionBranchTrue(stmt.Cases[i].Value, 3);
                Buffer.EmitJmpForward(nextLabel);

                // Case body
                foreach (var s in stmt.Cases[i].Body)
                    EmitStatement(s);
                if (!stmt.IsFallthrough)
                    Buffer.EmitJmpForward(endLabel);

                if (i < stmt.Cases.Count - 1 || stmt.DefaultBody != null)
                    Buffer.DefineLabel(nextLabel);
            }

            if (stmt.DefaultBody != null)
            {
                EmitBlock(stmt.DefaultBody);
            }

            Buffer.DefineLabel(endLabel);
        }

        private void EmitBreak()
        {
            if (_loopStack.Count == 0)
                throw new InvalidOperationException("break outside of loop");
            Buffer.EmitJmpForward(_loopStack.Peek().BreakLabel);
        }

        private void EmitContinue()
        {
            if (_loopStack.Count == 0)
                throw new InvalidOperationException("continue outside of loop");
            Buffer.EmitJmpForward(_loopStack.Peek().ContinueLabel);
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

            // 16-bit comparison: word/fixed/sword var vs literal
            if (condition is BinaryExpr wordBin &&
                wordBin.Left is IdentifierExpr wordIdent &&
                IsWordVar(wordIdent.Name))
            {
                var litVal = Resolve16BitInitializer("", wordBin.Right);
                EmitWordComparisonBranchTrue(wordIdent.Name, wordBin.Op, litVal, skipBytes);
                return;
            }

            // Signed byte comparison: sbyte vs 0
            if (condition is BinaryExpr signedBin &&
                signedBin.Left is IdentifierExpr signedIdent &&
                _varTypes.TryGetValue(signedIdent.Name, out var stype) && stype == "sbyte" &&
                signedBin.Right is IntLiteralExpr { Value: 0 })
            {
                var zp = GetLocal(signedIdent.Name);
                Buffer.EmitLdaZeroPage(zp);
                switch (signedBin.Op)
                {
                    case TokenKind.Less:        // < 0 → negative → BMI
                        Buffer.EmitBmi((sbyte)skipBytes); break;
                    case TokenKind.GreaterEqual: // >= 0 → not negative → BPL
                        Buffer.EmitBpl((sbyte)skipBytes); break;
                    case TokenKind.Greater:      // > 0 → not negative AND not zero
                        Buffer.EmitBeq(2);
                        Buffer.EmitBpl((sbyte)skipBytes); break;
                    case TokenKind.LessEqual:    // <= 0 → negative OR zero
                        Buffer.EmitBmi((sbyte)skipBytes);
                        Buffer.EmitBeq((sbyte)(skipBytes - 2)); break;
                    default:
                        throw new InvalidOperationException($"Unsupported signed comparison: {signedBin.Op}");
                }
                return;
            }

            // 8-bit binary comparison
            if (condition is BinaryExpr bin)
            {
                EmitExprToA(bin.Left);

                if (bin.Right is IntLiteralExpr intLit)
                    Buffer.EmitCmpImmediate((byte)intLit.Value);
                else if (bin.Right is IdentifierExpr ident && _constants.TryGetValue(ident.Name, out var cv))
                    Buffer.EmitCmpImmediate((byte)cv);
                else if (bin.Right is IdentifierExpr ident2)
                    Buffer.EmitCmpZeroPage(GetLocal(ident2.Name));
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

                case UnaryExpr { Op: TokenKind.Minus } neg when neg.Operand is IntLiteralExpr negInt:
                    Buffer.EmitLdaImmediate((byte)(-negInt.Value & 0xFF));
                    break;

                case IdentifierExpr ident:
                    if (_constants.TryGetValue(ident.Name, out var constVal))
                        Buffer.EmitLdaImmediate((byte)constVal);
                    else if (IsWordVar(ident.Name))
                        Buffer.EmitLdaZeroPage((byte)(GetLocal(ident.Name) + 1));
                    else
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
                        Buffer.EmitLdaImmediate(ResolveMemberConstant(member));
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

        private byte EvalConstExpr(ExprNode expr) => expr switch
        {
            IntLiteralExpr intLit => (byte)intLit.Value,
            MemberAccessExpr member => ResolveMemberConstant(member),
            _ => throw new InvalidOperationException($"Cannot evaluate constant: {expr.GetType().Name}")
        };

        private byte ResolveMemberConstant(MemberAccessExpr member)
        {
            if (member.Receiver is IdentifierExpr ident)
            {
                // Enum lookup
                if (_enumTypes.TryGetValue(ident.Name, out var members) &&
                    members.TryGetValue(member.MemberName, out var value))
                    return (byte)value;

                // Built-in Color constants
                if (ident.Name == "Color")
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

        /// <summary>
        /// Emit runtime library routines (multiply etc.) at the end of the binary.
        /// </summary>
        public void EmitRuntimeLibrary()
        {
            if (_runtime.Count == 0) return;

            if (!_runtime.Contains("mul8x8")) return;

            // 8×8→16 unsigned multiply: ZpMulA × ZpMulB → ZpMulResultHi:ZpMulResultLo
            // Standard shift-and-add. ZpMulB is destroyed (becomes result low byte).
            Buffer.DefineLabel("_rt_mul8x8");
            Buffer.EmitLdaImmediate(0);              // A = result high byte
            Buffer.EmitLdxImmediate(8);              // 8 bits
            Buffer.EmitLsrZeroPage(ZpMulB);          // shift first multiplier bit → carry
            // .loop: (11-byte loop body)
            Buffer.EmitBcc(3);                       // +3: skip CLC+ADC if bit was 0
            Buffer.EmitClc();                        // 1
            Buffer.EmitAdcZeroPage(ZpMulA);          // 2: add multiplicand
            // .skip:
            Buffer.EmitByte(0x6A);                   // ROR A: shift result high right
            Buffer.EmitRorZeroPage(ZpMulB);          // ROR ZpMulB: shift result low + next multiplier bit → carry
            Buffer.EmitDex();                        // 1
            Buffer.EmitBne(unchecked((sbyte)(-11)));  // back to BCC (11 bytes back)
            Buffer.EmitStaZeroPage(ZpMulResultHi);   // store high byte
            Buffer.EmitLdaZeroPage(ZpMulB);          // ZpMulB now has result low
            Buffer.EmitStaZeroPage(ZpMulResultLo);
            Buffer.EmitRts();

            // Fixed 8.8 × 8.8 → 8.8 multiply
            // Input: ZpFixedArg1 (lo/hi), ZpFixedArg2 (lo/hi)
            // Output: ZpFixedResB1 (result lo), ZpFixedResB2 (result hi)
            // Algorithm: (A1h*A2h)<<16 + (A1h*A2l + A1l*A2h)<<8 + A1l*A2l
            // Result 8.8 = middle two bytes
            Buffer.DefineLabel("_rt_fixmul");

            // Clear result accumulators
            Buffer.EmitLdaImmediate(0);
            Buffer.EmitStaZeroPage(ZpFixedResB0);
            Buffer.EmitStaZeroPage(ZpFixedResB1);
            Buffer.EmitStaZeroPage(ZpFixedResB2);

            // 1. Al * Bl → add to bytes 0,1
            Buffer.EmitLdaZeroPage(ZpFixedArg1Lo);
            Buffer.EmitStaZeroPage(ZpMulA);
            Buffer.EmitLdaZeroPage(ZpFixedArg2Lo);
            Buffer.EmitStaZeroPage(ZpMulB);
            Buffer.EmitJsrAbsolute(Buffer.GetLabel("_rt_mul8x8"));
            Buffer.EmitLdaZeroPage(ZpMulResultLo);
            Buffer.EmitStaZeroPage(ZpFixedResB0);       // byte 0
            Buffer.EmitLdaZeroPage(ZpMulResultHi);
            Buffer.EmitStaZeroPage(ZpFixedResB1);       // byte 1

            // 2. Al * Bh → add to bytes 1,2
            Buffer.EmitLdaZeroPage(ZpFixedArg1Lo);
            Buffer.EmitStaZeroPage(ZpMulA);
            Buffer.EmitLdaZeroPage(ZpFixedArg2Hi);
            Buffer.EmitStaZeroPage(ZpMulB);
            Buffer.EmitJsrAbsolute(Buffer.GetLabel("_rt_mul8x8"));
            Buffer.EmitLdaZeroPage(ZpFixedResB1);
            Buffer.EmitClc();
            Buffer.EmitAdcZeroPage(ZpMulResultLo);
            Buffer.EmitStaZeroPage(ZpFixedResB1);
            Buffer.EmitLdaZeroPage(ZpFixedResB2);
            Buffer.EmitAdcZeroPage(ZpMulResultHi);
            Buffer.EmitStaZeroPage(ZpFixedResB2);

            // 3. Ah * Bl → add to bytes 1,2
            Buffer.EmitLdaZeroPage(ZpFixedArg1Hi);
            Buffer.EmitStaZeroPage(ZpMulA);
            Buffer.EmitLdaZeroPage(ZpFixedArg2Lo);
            Buffer.EmitStaZeroPage(ZpMulB);
            Buffer.EmitJsrAbsolute(Buffer.GetLabel("_rt_mul8x8"));
            Buffer.EmitLdaZeroPage(ZpFixedResB1);
            Buffer.EmitClc();
            Buffer.EmitAdcZeroPage(ZpMulResultLo);
            Buffer.EmitStaZeroPage(ZpFixedResB1);
            Buffer.EmitLdaZeroPage(ZpFixedResB2);
            Buffer.EmitAdcZeroPage(ZpMulResultHi);
            Buffer.EmitStaZeroPage(ZpFixedResB2);

            // 4. Ah * Bh → add to byte 2 (only low byte matters for 8.8)
            Buffer.EmitLdaZeroPage(ZpFixedArg1Hi);
            Buffer.EmitStaZeroPage(ZpMulA);
            Buffer.EmitLdaZeroPage(ZpFixedArg2Hi);
            Buffer.EmitStaZeroPage(ZpMulB);
            Buffer.EmitJsrAbsolute(Buffer.GetLabel("_rt_mul8x8"));
            Buffer.EmitLdaZeroPage(ZpFixedResB2);
            Buffer.EmitClc();
            Buffer.EmitAdcZeroPage(ZpMulResultLo);
            Buffer.EmitStaZeroPage(ZpFixedResB2);

            Buffer.EmitRts();

            // Debug hex display: writes 4 hex chars to screen
            // Input: ZpFixedArg1Lo/Hi = value, X = screen addr low, Y = screen addr high
            if (_runtime.Contains("debughex"))
            {
                Buffer.DefineLabel("_rt_debughex");
                Buffer.EmitStxZeroPage(ZpPointerLo);
                Buffer.EmitByte(0x84); Buffer.EmitByte(ZpPointerHi); // STY zp

                // Digit 0: high nybble of high byte
                Buffer.EmitLdaZeroPage(ZpFixedArg1Hi);
                Buffer.EmitByte(0x4A); Buffer.EmitByte(0x4A);
                Buffer.EmitByte(0x4A); Buffer.EmitByte(0x4A);
                Buffer.EmitJsrForward("_rt_hexchar");
                Buffer.EmitLdyImmediate(0);
                Buffer.EmitStaIndirectY(ZpPointerLo);

                // Digit 1: low nybble of high byte
                Buffer.EmitLdaZeroPage(ZpFixedArg1Hi);
                Buffer.EmitByte(0x29); Buffer.EmitByte(0x0F);
                Buffer.EmitJsrForward("_rt_hexchar");
                Buffer.EmitLdyImmediate(1);
                Buffer.EmitStaIndirectY(ZpPointerLo);

                // Digit 2: high nybble of low byte
                Buffer.EmitLdaZeroPage(ZpFixedArg1Lo);
                Buffer.EmitByte(0x4A); Buffer.EmitByte(0x4A);
                Buffer.EmitByte(0x4A); Buffer.EmitByte(0x4A);
                Buffer.EmitJsrForward("_rt_hexchar");
                Buffer.EmitLdyImmediate(2);
                Buffer.EmitStaIndirectY(ZpPointerLo);

                // Digit 3: low nybble of low byte
                Buffer.EmitLdaZeroPage(ZpFixedArg1Lo);
                Buffer.EmitByte(0x29); Buffer.EmitByte(0x0F);
                Buffer.EmitJsrForward("_rt_hexchar");
                Buffer.EmitLdyImmediate(3);
                Buffer.EmitStaIndirectY(ZpPointerLo);
                Buffer.EmitRts();

                // hex_char: A (0-15) → screen code
                Buffer.DefineLabel("_rt_hexchar");
                Buffer.EmitCmpImmediate(10);
                Buffer.EmitBcc(4);          // 0-9 → skip
                Buffer.EmitSec();
                Buffer.EmitSbcImmediate(9); // 10-15 → 1-6 (A-F screen codes)
                Buffer.EmitRts();
                Buffer.EmitClc();
                Buffer.EmitAdcImmediate(48); // 0-9 → 48-57 (0-9 screen codes)
                Buffer.EmitRts();
            }

            // Signed fixed multiply wrapper — handles sign, calls unsigned fixmul
            if (_runtime.Contains("sfixmul"))
            {
                // Uses ZpSignFlag (separate from result bytes cleared by fixmul!)
                Buffer.DefineLabel("_rt_sfixmul");

                // Clear sign flag (in separate ZP byte, NOT in result area!)
                Buffer.EmitLdaImmediate(0);
                Buffer.EmitStaZeroPage(ZpSignFlag);

                // Check arg1 sign (high byte bit 7)
                Buffer.EmitLdaZeroPage(ZpFixedArg1Hi);
                Buffer.EmitBpl(15); // skip negate (15 bytes)
                Buffer.EmitLdaImmediate(0);
                Buffer.EmitSec();
                Buffer.EmitSbcZeroPage(ZpFixedArg1Lo);
                Buffer.EmitStaZeroPage(ZpFixedArg1Lo);
                Buffer.EmitLdaImmediate(0);
                Buffer.EmitSbcZeroPage(ZpFixedArg1Hi);
                Buffer.EmitStaZeroPage(ZpFixedArg1Hi);
                Buffer.EmitIncZeroPage(ZpSignFlag);

                // Check arg2 sign
                Buffer.EmitLdaZeroPage(ZpFixedArg2Hi);
                Buffer.EmitBpl(15); // skip negate (15 bytes)
                Buffer.EmitLdaImmediate(0);
                Buffer.EmitSec();
                Buffer.EmitSbcZeroPage(ZpFixedArg2Lo);
                Buffer.EmitStaZeroPage(ZpFixedArg2Lo);
                Buffer.EmitLdaImmediate(0);
                Buffer.EmitSbcZeroPage(ZpFixedArg2Hi);
                Buffer.EmitStaZeroPage(ZpFixedArg2Hi);
                Buffer.EmitIncZeroPage(ZpSignFlag);

                // Do unsigned multiply
                Buffer.EmitJsrAbsolute(Buffer.GetLabel("_rt_fixmul"));

                // Check sign flag: if odd (1), negate result
                Buffer.EmitLdaZeroPage(ZpSignFlag);
                Buffer.EmitByte(0x29); Buffer.EmitByte(0x01); // AND #1
                Buffer.EmitBeq(13); // skip negate (13 bytes)
                Buffer.EmitLdaImmediate(0);
                Buffer.EmitSec();
                Buffer.EmitSbcZeroPage(ZpFixedResB1);
                Buffer.EmitStaZeroPage(ZpFixedResB1);
                Buffer.EmitLdaImmediate(0);
                Buffer.EmitSbcZeroPage(ZpFixedResB2);
                Buffer.EmitStaZeroPage(ZpFixedResB2);

                Buffer.EmitRts();
            }
        }

        /// <summary>
        /// debugHex: screenAddr value: expr
        /// Writes a 16-bit value as 4 hex digits at a screen address.
        /// </summary>
        private void EmitDebugHex(ExprNode addrExpr, ExprNode valueExpr)
        {
            _runtime.Add("debughex");
            var addr = (ushort)(((IntLiteralExpr)addrExpr).Value + 0x0400);

            // Load value into ZpFixedArg1 (reuse as scratch)
            if (valueExpr is IdentifierExpr ident && IsWordVar(ident.Name))
            {
                var zp = GetLocal(ident.Name);
                Buffer.EmitLdaZeroPage(zp);
                Buffer.EmitStaZeroPage(ZpFixedArg1Lo);
                Buffer.EmitLdaZeroPage((byte)(zp + 1));
                Buffer.EmitStaZeroPage(ZpFixedArg1Hi);
            }
            else
            {
                throw new InvalidOperationException("debugHex value must be a word/fixed/sfixed variable");
            }

            // Pass screen address in X (low) and Y (high)
            Buffer.EmitLdxImmediate((byte)(addr & 0xFF));
            Buffer.EmitLdyImmediate((byte)(addr >> 8));
            Buffer.EmitJsrForward("_rt_debughex");
        }

        private bool IsWordVar(string name) =>
            _varTypes.TryGetValue(name, out var t) && Is16BitType(t);

        private void EmitWordAssignment(byte zpLo, TokenKind op, ExprNode value)
        {
            var zpHi = (byte)(zpLo + 1);

            // Variable-to-variable 16-bit operations
            if (value is IdentifierExpr varExpr && IsWordVar(varExpr.Name))
            {
                var srcZp = GetLocal(varExpr.Name);
                var srcHi = (byte)(srcZp + 1);
                if (op == TokenKind.PlusEqual)
                {
                    Buffer.EmitLdaZeroPage(zpLo);
                    Buffer.EmitClc();
                    Buffer.EmitAdcZeroPage(srcZp);
                    Buffer.EmitStaZeroPage(zpLo);
                    Buffer.EmitLdaZeroPage(zpHi);
                    Buffer.EmitAdcZeroPage(srcHi);
                    Buffer.EmitStaZeroPage(zpHi);
                }
                else if (op == TokenKind.MinusEqual)
                {
                    Buffer.EmitLdaZeroPage(zpLo);
                    Buffer.EmitSec();
                    Buffer.EmitSbcZeroPage(srcZp);
                    Buffer.EmitStaZeroPage(zpLo);
                    Buffer.EmitLdaZeroPage(zpHi);
                    Buffer.EmitSbcZeroPage(srcHi);
                    Buffer.EmitStaZeroPage(zpHi);
                }
                else if (op == TokenKind.Equal)
                {
                    Buffer.EmitLdaZeroPage(srcZp);
                    Buffer.EmitStaZeroPage(zpLo);
                    Buffer.EmitLdaZeroPage(srcHi);
                    Buffer.EmitStaZeroPage(zpHi);
                }
                return;
            }

            // Literal-based 16-bit operations
            var val = Resolve16BitInitializer("", value);

            if (op == TokenKind.Equal)
            {
                Buffer.EmitLdaImmediate((byte)(val & 0xFF));
                Buffer.EmitStaZeroPage(zpLo);
                Buffer.EmitLdaImmediate((byte)(val >> 8));
                Buffer.EmitStaZeroPage(zpHi);
            }
            else if (op == TokenKind.PlusEqual)
            {
                Buffer.EmitLdaZeroPage(zpLo);
                Buffer.EmitClc();
                Buffer.EmitAdcImmediate((byte)(val & 0xFF));
                Buffer.EmitStaZeroPage(zpLo);
                Buffer.EmitLdaZeroPage(zpHi);
                Buffer.EmitAdcImmediate((byte)(val >> 8));
                Buffer.EmitStaZeroPage(zpHi);
            }
            else if (op == TokenKind.MinusEqual)
            {
                Buffer.EmitLdaZeroPage(zpLo);
                Buffer.EmitSec();
                Buffer.EmitSbcImmediate((byte)(val & 0xFF));
                Buffer.EmitStaZeroPage(zpLo);
                Buffer.EmitLdaZeroPage(zpHi);
                Buffer.EmitSbcImmediate((byte)(val >> 8));
                Buffer.EmitStaZeroPage(zpHi);
            }
            else if (op == TokenKind.StarEqual)
            {
                // Determine signed vs unsigned based on variable type
                var varType = _varTypes.FirstOrDefault(kv => _locals.TryGetValue(kv.Key, out var z) && z == zpLo).Value;
                var isSigned = varType == "sfixed" || varType == "sword";
                _runtime.Add("mul8x8");
                _runtime.Add(isSigned ? "sfixmul" : "fixmul");
                // Load operands into fixed multiply ZP slots
                Buffer.EmitLdaZeroPage(zpLo);
                Buffer.EmitStaZeroPage(ZpFixedArg1Lo);
                Buffer.EmitLdaZeroPage(zpHi);
                Buffer.EmitStaZeroPage(ZpFixedArg1Hi);

                // Resolve right operand
                if (value is IdentifierExpr mulVar && IsWordVar(mulVar.Name))
                {
                    var srcZp = GetLocal(mulVar.Name);
                    Buffer.EmitLdaZeroPage(srcZp);
                    Buffer.EmitStaZeroPage(ZpFixedArg2Lo);
                    Buffer.EmitLdaZeroPage((byte)(srcZp + 1));
                    Buffer.EmitStaZeroPage(ZpFixedArg2Hi);
                }
                else
                {
                    var mulVal = Resolve16BitInitializer("", value);
                    Buffer.EmitLdaImmediate((byte)(mulVal & 0xFF));
                    Buffer.EmitStaZeroPage(ZpFixedArg2Lo);
                    Buffer.EmitLdaImmediate((byte)(mulVal >> 8));
                    Buffer.EmitStaZeroPage(ZpFixedArg2Hi);
                }

                Buffer.EmitJsrForward(isSigned ? "_rt_sfixmul" : "_rt_fixmul");

                // Store result back
                Buffer.EmitLdaZeroPage(ZpFixedResB1);
                Buffer.EmitStaZeroPage(zpLo);
                Buffer.EmitLdaZeroPage(ZpFixedResB2);
                Buffer.EmitStaZeroPage(zpHi);
            }
            else if (op == TokenKind.ShiftRightEqual && value is IntLiteralExpr shrLit)
            {
                // 16-bit unsigned right shift: LSR hi; ROR lo (repeated N times)
                for (var i = 0; i < (int)shrLit.Value; i++)
                {
                    Buffer.EmitLsrZeroPage(zpHi);
                    Buffer.EmitRorZeroPage(zpLo);
                }
            }
            else if (op == TokenKind.ShiftLeftEqual && value is IntLiteralExpr shlLit)
            {
                // 16-bit left shift: ASL lo; ROL hi (repeated N times)
                for (var i = 0; i < (int)shlLit.Value; i++)
                {
                    Buffer.EmitAslZeroPage(zpLo);
                    Buffer.EmitRolZeroPage(zpHi);
                }
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
