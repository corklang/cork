namespace Cork.CodeGen.Emit;

using Cork.Language.Ast;

/// <summary>
/// Top-level code generator: direct AST-to-6510 emission.
/// Orchestrates EmitContext and sub-emitters to produce machine code.
/// Zero page $02-$0E reserved as compiler locals. $0F as temp.
/// </summary>
public sealed class CodeGenerator(ushort codeBase = 0x0810)
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

        var ctx = CreateContext(codeBase, dataAddresses);

        // Register struct types and const array sizes
        foreach (var decl in program.Declarations)
            if (decl is StructDeclNode sd)
                ctx.Symbols.RegisterStructType(sd);
        foreach (var decl in program.Declarations)
            if (decl is ConstArrayDeclNode ca)
                ctx.RegisterConstArraySize(ca.Name, ca.Size);
        foreach (var decl in program.Declarations)
            if (decl is EnumDeclNode ed)
                ctx.Symbols.RegisterEnumType(ed);

        // Allocate globals first (persist across scenes)
        foreach (var gv in globalVars)
            ctx.Symbols.AllocGlobal(gv.Name);
        foreach (var decl in program.Declarations)
            if (decl is GlobalMethodNode gm)
                RegisterGlobalMethod(ctx, gm);

        // Emit SEI + global init
        ctx.Buffer.EmitSei();
        foreach (var gv in globalVars)
            EmitGlobalInit(ctx, gv);

        // Copy sprite data
        ctx.Intrinsics.EmitSpriteCopies(program, dataAddresses);

        // Jump to entry scene
        ctx.Buffer.EmitJmpForward($"_scene_{entryScene.Name}");

        // Emit all scenes
        foreach (var scene in scenes)
            ctx.Scenes.EmitScene(scene);

        // Reset to global-only scope before emitting global methods
        ctx.Symbols.ResetToGlobalScope();
        var globalMethods = program.Declarations.OfType<GlobalMethodNode>().ToList();
        foreach (var gm in globalMethods)
            EmitGlobalMethod(ctx, gm);

        // Runtime library
        ctx.RuntimeLib.EmitRuntimeLibrary();

        ctx.Buffer.ResolveFixups();

        // Combine code + data
        var code = ctx.Buffer.ToArray();
        var result = new byte[code.Length + dataBytes.Count];
        code.CopyTo(result, 0);
        dataBytes.CopyTo(result.AsSpan()[code.Length..]);
        return (result, codeBase);
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

    private static byte[] EmitAll(ProgramNode program, List<SceneNode> scenes,
        List<GlobalVarDeclNode> globalVars, Dictionary<string, int> dataNames,
        ushort codeBase, ushort dataStart)
    {
        var dataAddresses = new Dictionary<string, ushort>();
        foreach (var (name, offset) in dataNames)
            dataAddresses[name] = (ushort)(dataStart + offset);

        var ctx = CreateContext(codeBase, dataAddresses);
        foreach (var decl in program.Declarations)
            if (decl is StructDeclNode sd)
                ctx.Symbols.RegisterStructType(sd);
        foreach (var decl in program.Declarations)
            if (decl is EnumDeclNode ed)
                ctx.Symbols.RegisterEnumType(ed);
        foreach (var decl in program.Declarations)
            if (decl is ConstArrayDeclNode ca)
                ctx.RegisterConstArraySize(ca.Name, ca.Size);
        foreach (var gv in globalVars)
            ctx.Symbols.AllocGlobal(gv.Name);
        foreach (var decl in program.Declarations)
            if (decl is GlobalMethodNode gm2)
                RegisterGlobalMethod(ctx, gm2);

        ctx.Buffer.EmitSei();
        foreach (var gv in globalVars)
            EmitGlobalInit(ctx, gv);
        ctx.Intrinsics.EmitSpriteCopies(program, dataAddresses);
        ctx.Buffer.EmitJmpForward($"_scene_{scenes.First(s => s.IsEntry).Name}");

        foreach (var scene in scenes)
            ctx.Scenes.EmitScene(scene);

        foreach (var decl in program.Declarations)
            if (decl is GlobalMethodNode gm)
                EmitGlobalMethod(ctx, gm);

        ctx.RuntimeLib.EmitRuntimeLibrary();
        return ctx.Buffer.ToArray();
    }

    private static void RegisterGlobalMethod(EmitContext ctx, GlobalMethodNode gm)
    {
        ctx.Symbols.AddGlobalName(gm.SelectorName);
        ctx.Symbols.RegisterMethodParams(gm.SelectorName, gm.Parameters);
        foreach (var param in gm.Parameters)
        {
            if (param.ParamName != "")
                ctx.Symbols.AllocGlobal(param.ParamName);
        }
    }

    private static void EmitGlobalInit(EmitContext ctx, GlobalVarDeclNode gv)
    {
        var zp = ctx.Symbols.GetLocal(gv.Name);
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
        ctx.Statements.EmitBlock(gm.Body);
        ctx.Buffer.EmitRts();
    }
}
