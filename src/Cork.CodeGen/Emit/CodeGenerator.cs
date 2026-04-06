namespace Cork.CodeGen.Emit;

using Cork.Language.Ast;

/// <summary>
/// Top-level code generator: direct AST-to-6510 emission.
/// Orchestrates EmitContext and sub-emitters to produce machine code.
/// Zero page $02-$0E reserved as compiler locals. $0F as temp.
/// </summary>
public sealed class CodeGenerator(ushort codeBase = 0x0810)
{
    public DebugInfo? LastDebugInfo { get; private set; }

    public (byte[] Code, ushort EntryPoint, int PeepholeRemovals) Generate(ProgramNode program)
    {
        // Dead code elimination: determine which globals are reachable from scenes
        var reachable = ReachabilityAnalysis.Analyze(program);
        var usesRandom = reachable.Methods.Contains("random:");

        // Collect inline sprite patterns (separate from const data — aligned later)
        var inlineSprites = CollectInlineSpritePatterns(program);

        // Collect const data (only reachable arrays)
        var dataBytes = new List<byte>();
        var dataNames = new Dictionary<string, int>();
        var constArraySizes = new Dictionary<string, int>();

        // Collect string literals from the entire AST and convert to screen code data
        CollectStringLiterals(program, dataBytes, dataNames, constArraySizes);

        foreach (var decl in program.Declarations)
        {
            if (decl is ConstArrayDeclNode constArr && reachable.ConstArrays.Contains(constArr.Name))
            {
                dataNames[constArr.Name] = dataBytes.Count;
                constArraySizes[constArr.Name] = constArr.Size;
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

        // Collect global variables (only reachable ones)
        var globalVars = program.Declarations.OfType<GlobalVarDeclNode>()
            .Where(gv => reachable.GlobalVars.Contains(gv.Name))
            .ToList();

        var constDataSize = dataBytes.Count;

        // First pass: measure code size (sprite pointers registered as placeholder 0)
        var measuredSize = EmitAll(program, scenes, globalVars, dataNames, constArraySizes,
            inlineSprites, reachable.Methods, usesRandom, codeBase, 0xFFFF).Length;

        // Second pass setup: compute data addresses
        var dataStart = (ushort)(codeBase + measuredSize);
        var dataAddresses = new Dictionary<string, ushort>();
        foreach (var (name, offset) in dataNames)
            dataAddresses[name] = (ushort)(dataStart + offset);

        // Measure inline string data size (deterministic, same between passes)
        var measureCtx = CreateContext(codeBase, dataAddresses);
        RegisterAllTypes(measureCtx, program, globalVars, constArraySizes, inlineSprites, reachable.Methods);
        EmitCode(measureCtx, program, scenes, globalVars, entryScene, reachable.Methods, usesRandom);
        var measureInlineData = measureCtx.FinalizeInlineData(
            (ushort)(codeBase + measureCtx.Buffer.Length + constDataSize));

        // Compute aligned sprite positions (after code + const data + inline strings)
        var spriteSegStart = (ushort)(codeBase + measuredSize + constDataSize + measureInlineData.Length);
        var (spriteData, spritePointers) = BuildAlignedSpriteData(inlineSprites, spriteSegStart);

        // Final pass: emit with real sprite pointer values and debug info
        var ctx = CreateContext(codeBase, dataAddresses);
        ctx.Debug = new DebugInfo();
        RegisterAllTypes(ctx, program, globalVars, constArraySizes, inlineSprites, reachable.Methods);
        foreach (var (dataName, _, _, _) in inlineSprites)
        {
            var ptrName = dataName.Replace("Data", "Ptr");
            ctx.Symbols.AddConstantNoShadowCheck(ptrName, spritePointers[dataName]);
        }
        EmitCode(ctx, program, scenes, globalVars, entryScene, reachable.Methods, usesRandom);

        if (ctx.Errors.Count > 0)
            throw new AggregateCompileError(ctx.Errors);

        // Finalize inline data (string literals)
        var codeSize = ctx.Buffer.Length;
        var inlineDataStart = (ushort)(codeBase + codeSize + constDataSize);
        var inlineData = ctx.FinalizeInlineData(inlineDataStart);
        ctx.Buffer.ResolveFixups();

        // Extract variable debug info from symbol table
        if (ctx.Debug != null)
        {
            foreach (var (name, zp) in ctx.Symbols.Globals)
            {
                var type = ctx.Symbols.GetVarType(name) ?? "byte";
                var size = SymbolTable.Is16BitType(type) ? 2 : 1;
                ctx.Debug.AddVariable(name, type, zp, size, "global");
            }
            foreach (var (name, inst) in ctx.Symbols.StructInstances)
            {
                foreach (var (field, zp) in inst.Fields)
                {
                    var fullName = $"{name}.{field}";
                    ctx.Debug.AddVariable(fullName, inst.StructType, zp, 1, "global");
                }
            }
            foreach (var (name, info) in ctx.Symbols.StringVars)
            {
                if (name.StartsWith('_')) continue;
                ctx.Debug.AddVariable(name, "string", info.ZpBase, info.Length, "global");
            }
            LastDebugInfo = ctx.Debug;
        }

        // Combine code + const data + inline data + aligned sprite data
        var code = ctx.Buffer.ToArray();
        var result = new byte[code.Length + constDataSize + inlineData.Length + spriteData.Length];
        code.CopyTo(result, 0);
        dataBytes.CopyTo(result.AsSpan()[code.Length..]);
        inlineData.CopyTo(result, code.Length + constDataSize);
        spriteData.CopyTo(result, code.Length + constDataSize + inlineData.Length);
        return (result, codeBase, ctx.Buffer.PeepholeRemovals);
    }

    private static EmitContext CreateContext(ushort codeBase, Dictionary<string, ushort> dataAddresses)
    {
        var ctx = new EmitContext(codeBase, dataAddresses);
        ctx.Expressions = new ExpressionEmitter(ctx);
        ctx.Statements = new StatementEmitter(ctx);
        ctx.ControlFlow = new ControlFlowEmitter(ctx);
        ctx.Scenes = new SceneEmitter(ctx);
        ctx.Intrinsics = new IntrinsicEmitter(ctx);
        ctx.RuntimeLib = new RuntimeLibrary(ctx);
        return ctx;
    }

    private static void RegisterAllTypes(EmitContext ctx, ProgramNode program,
        List<GlobalVarDeclNode> globalVars, Dictionary<string, int> constArraySizes,
        List<(string DataName, string SceneName, string SpriteName, byte[] Bytes)> inlineSprites,
        HashSet<string>? reachableMethods = null)
    {
        foreach (var decl in program.Declarations)
            if (decl is StructDeclNode sd)
                ctx.Symbols.RegisterStructType(sd);
        foreach (var decl in program.Declarations)
            if (decl is EnumDeclNode ed)
                ctx.Symbols.RegisterEnumType(ed);
        foreach (var (name, size) in constArraySizes)
            ctx.RegisterConstArraySize(name, size);
        foreach (var gv in globalVars)
        {
            // Const scalars are compile-time only — no ZP allocation needed
            if (gv.IsConst && gv.ArraySize == 0) continue;

            if (gv.ArraySize > 0)
            {
                var zpBase = ctx.Symbols.AllocGlobalArray(gv.Name, gv.ArraySize);
                ctx.RegisterConstArraySize(gv.Name, gv.ArraySize);
                if (gv.TypeName == "string")
                    ctx.Symbols.RegisterStringVar(gv.Name, zpBase, gv.ArraySize, isGlobal: true);
            }
            else
                ctx.Symbols.AllocGlobal(gv.Name);
        }
        foreach (var decl in program.Declarations)
            if (decl is GlobalMethodNode gm &&
                (reachableMethods == null || reachableMethods.Contains(gm.SelectorName)))
                RegisterGlobalMethod(ctx, gm);
        foreach (var gv in globalVars)
        {
            if (!gv.IsConst || gv.ArraySize > 0 || gv.Initializer == null) continue;
            if (gv.Initializer is IntLiteralExpr constInit)
                ctx.Symbols.AddConstantNoShadowCheck(gv.Name, constInit.Value);
            else if (ctx.Expressions.TryFoldConstant(gv.Initializer, out var folded))
                ctx.Symbols.AddConstantNoShadowCheck(gv.Name, folded);
        }

        // Register placeholder pointer constants for inline sprites (value 0 — code size unaffected)
        foreach (var (dataName, _, _, _) in inlineSprites)
        {
            var ptrName = dataName.Replace("Data", "Ptr");
            if (!ctx.Symbols.TryGetConstant(ptrName, out _))
                ctx.Symbols.AddConstantNoShadowCheck(ptrName, 0);
        }
    }

    private static void EmitCode(EmitContext ctx, ProgramNode program,
        List<SceneNode> scenes, List<GlobalVarDeclNode> globalVars, SceneNode entryScene,
        HashSet<string>? reachableMethods = null, bool usesRandom = false)
    {
        ctx.Buffer.EmitSei();
        foreach (var gv in globalVars)
            EmitGlobalInit(ctx, gv);
        // SID noise setup for random: — voice 3 max frequency + noise waveform
        if (usesRandom)
        {
            ctx.Buffer.EmitLdaImmediate(0xFF);
            ctx.Buffer.EmitStaAbsolute(0xD40E); // Voice 3 freq lo
            ctx.Buffer.EmitStaAbsolute(0xD40F); // Voice 3 freq hi
            ctx.Buffer.EmitLdaImmediate(0x80);
            ctx.Buffer.EmitStaAbsolute(0xD412); // Voice 3 control: noise waveform
        }

        ctx.Buffer.EmitJmpForward($"_scene_{entryScene.Name}");

        foreach (var scene in scenes)
            ctx.Scenes.EmitScene(scene);

        ctx.Symbols.ResetToGlobalScope();
        foreach (var decl in program.Declarations)
            if (decl is GlobalMethodNode gm &&
                (reachableMethods == null || reachableMethods.Contains(gm.SelectorName)))
                EmitGlobalMethod(ctx, gm);

        ctx.RuntimeLib.EmitRuntimeLibrary();
    }

    private static byte[] EmitAll(ProgramNode program, List<SceneNode> scenes,
        List<GlobalVarDeclNode> globalVars, Dictionary<string, int> dataNames,
        Dictionary<string, int> constArraySizes,
        List<(string DataName, string SceneName, string SpriteName, byte[] Bytes)> inlineSprites,
        HashSet<string> reachableMethods, bool usesRandom,
        ushort codeBase, ushort dataStart)
    {
        var dataAddresses = new Dictionary<string, ushort>();
        foreach (var (name, offset) in dataNames)
            dataAddresses[name] = (ushort)(dataStart + offset);

        var ctx = CreateContext(codeBase, dataAddresses);
        RegisterAllTypes(ctx, program, globalVars, constArraySizes, inlineSprites, reachableMethods);

        var entryScene = scenes.First(s => s.IsEntry);
        EmitCode(ctx, program, scenes, globalVars, entryScene, reachableMethods, usesRandom);
        return ctx.Buffer.ToArray();
    }

    private static void RegisterGlobalMethod(EmitContext ctx, GlobalMethodNode gm)
    {
        ctx.Symbols.AddGlobalName(gm.SelectorName);
        ctx.Symbols.RegisterMethodParams(gm.SelectorName, gm.Parameters);
        // Allocate params in shared zone (all methods overlap — safe, non-reentrant)
        ctx.Symbols.AllocMethodParams(gm.SelectorName, gm.Parameters);
    }

    private static void EmitGlobalInit(EmitContext ctx, GlobalVarDeclNode gv)
    {
        // Const scalars have no runtime storage
        if (gv.IsConst && gv.ArraySize == 0) return;

        var zp = ctx.Symbols.GetLocal(gv.Name);

        // Global string arrays: initialize with string content or space-fill
        if (gv.ArraySize > 0 && gv.TypeName == "string")
        {
            var strValue = gv.Initializer is StringLiteralExpr strLit ? strLit.Value : "";
            var screenCodes = ScreenCodes.FromString(strValue);
            for (var i = 0; i < gv.ArraySize; i++)
            {
                var val = i < screenCodes.Length ? screenCodes[i] : (byte)32;
                ctx.Buffer.EmitLdaImmediate(val);
                ctx.Buffer.EmitStaZeroPage((byte)(zp + i));
            }
            return;
        }

        // Zero-fill other global arrays
        if (gv.ArraySize > 0)
        {
            ctx.Buffer.EmitLdaImmediate(0);
            for (var i = 0; i < gv.ArraySize; i++)
                ctx.Buffer.EmitStaZeroPage((byte)(zp + i));
            return;
        }

        if (gv.Initializer is IntLiteralExpr intLit)
        {
            ctx.Buffer.EmitLdaImmediate((byte)intLit.Value);
            ctx.Buffer.EmitStaZeroPage(zp);
        }
        else
        {
            ctx.Buffer.EmitLdaImmediate(0);
            ctx.Buffer.EmitStaZeroPage(zp);
        }
    }

    private static void EmitGlobalMethod(EmitContext ctx, GlobalMethodNode gm)
    {
        var label = $"_method_{gm.SelectorName}";
        ctx.Buffer.DefineLabel(label);
        ctx.Debug?.OpenScope(ctx.Debug.Methods, gm.SelectorName, ctx.Buffer.CurrentAddress);
        // Each method gets its own non-overlapping local zone so nested calls work.
        ctx.Symbols.PrepareMethodLocals();
        ctx.Symbols.InstallMethodParamLocals(gm.SelectorName, gm.Parameters);
        ctx.Statements.EmitBlock(gm.Body);
        ctx.Symbols.RemoveMethodParamLocals(gm.Parameters);
        ctx.Symbols.FinalizeMethodLocals();
        ctx.Buffer.EmitRts();
        ctx.Debug?.CloseScope(ctx.Debug.Methods, gm.SelectorName, ctx.Buffer.CurrentAddress);
    }

    /// <summary>
    /// Collect inline sprite patterns from sprite blocks. Returns (dataName, bytes) pairs.
    /// Does NOT synthesize pointer globals — pointers are computed from aligned addresses.
    /// </summary>
    private static List<(string DataName, string SceneName, string SpriteName, byte[] Bytes)>
        CollectInlineSpritePatterns(ProgramNode program)
    {
        var results = new List<(string, string, string, byte[])>();
        foreach (var scene in program.Declarations.OfType<SceneNode>())
        {
            foreach (var sprite in scene.Members.OfType<SpriteBlockNode>())
            {
                var dataSetting = sprite.Settings
                    .FirstOrDefault(s => s.Name == "data" && s.Value is SpritePatternExpr);
                if (dataSetting == null) continue;

                var pattern = (SpritePatternExpr)dataSetting.Value;
                var isMulticolor = sprite.Settings
                    .Any(s => s.Name == "multicolor" && s.Value is BoolLiteralExpr { Value: true });

                var bytes = SpritePatternCompiler.Compile(pattern.Pattern, isMulticolor);
                var dataName = $"_sprite_{scene.Name}_{sprite.Name}Data";
                results.Add((dataName, scene.Name, sprite.Name, bytes));
            }
        }
        return results;
    }

    /// <summary>
    /// Build the sprite data segment with 64-byte alignment padding.
    /// Returns the raw bytes and a map of dataName → (offset, pointerValue).
    /// </summary>
    private static (byte[] Data, Dictionary<string, int> Pointers) BuildAlignedSpriteData(
        List<(string DataName, string SceneName, string SpriteName, byte[] Bytes)> sprites,
        ushort segmentStart)
    {
        if (sprites.Count == 0)
            return ([], []);

        var data = new List<byte>();
        var pointers = new Dictionary<string, int>();

        foreach (var (dataName, _, _, bytes) in sprites)
        {
            // Pad to next 64-byte boundary
            var currentAddr = segmentStart + data.Count;
            var aligned = (currentAddr + 63) & ~63;
            var padding = aligned - currentAddr;
            for (var i = 0; i < padding; i++)
                data.Add(0);

            pointers[dataName] = aligned / 64;
            data.AddRange(bytes);
            // Pad to full 64 bytes (sprite data is 63, VIC-II reads 64)
            data.Add(0);
        }

        return (data.ToArray(), pointers);
    }

    /// <summary>
    /// Scan the AST for all string literals and convert them to screen code data entries.
    /// </summary>
    private static void CollectStringLiterals(ProgramNode program,
        List<byte> dataBytes, Dictionary<string, int> dataNames, Dictionary<string, int> sizes)
    {
        var found = new HashSet<string>();
        CollectStringsFromNode(program, found);
        foreach (var str in found)
        {
            var name = $"_str_{str.GetHashCode():X8}";
            if (dataNames.ContainsKey(name)) continue;
            var screenCodes = ScreenCodes.FromString(str);
            dataNames[name] = dataBytes.Count;
            sizes[name] = screenCodes.Length;
            dataBytes.AddRange(screenCodes);
        }
    }

    private static void CollectStringsFromNode(AstNode node, HashSet<string> found)
    {
        switch (node)
        {
            case ProgramNode prog:
                foreach (var d in prog.Declarations) CollectStringsFromNode(d, found);
                break;
            case SceneNode scene:
                foreach (var m in scene.Members) CollectStringsFromNode(m, found);
                break;
            case EnterBlockNode enter: CollectStringsFromBlock(enter.Body, found); break;
            case FrameBlockNode frame: CollectStringsFromBlock(frame.Body, found); break;
            case ExitBlockNode exit: CollectStringsFromBlock(exit.Body, found); break;
            case SceneMethodNode method: CollectStringsFromBlock(method.Body, found); break;
            case GlobalMethodNode gm: CollectStringsFromBlock(gm.Body, found); break;
        }
    }

    private static void CollectStringsFromBlock(BlockNode block, HashSet<string> found)
    {
        foreach (var stmt in block.Statements)
        {
            switch (stmt)
            {
                case MessageSendStmt msg:
                    foreach (var seg in msg.Segments)
                        if (seg.Argument is StringLiteralExpr strLit)
                            found.Add(strLit.Value);
                    break;
                case IfStmt ifStmt:
                    CollectStringsFromBlock(ifStmt.ThenBody, found);
                    if (ifStmt.ElseBody != null) CollectStringsFromBlock(ifStmt.ElseBody, found);
                    foreach (var (_, body) in ifStmt.ElseIfs) CollectStringsFromBlock(body, found);
                    break;
                case WhileStmt w: CollectStringsFromBlock(w.Body, found); break;
                case ForStmt f: CollectStringsFromBlock(f.Body, found); break;
                case ForEachStmt fe: CollectStringsFromBlock(fe.Body, found); break;
                case SwitchStmt sw:
                    foreach (var c in sw.Cases)
                        CollectStringsFromBlock(new BlockNode(c.Body, c.Body[0].Location), found);
                    if (sw.DefaultBody != null) CollectStringsFromBlock(sw.DefaultBody, found);
                    break;
            }
        }
    }
}
