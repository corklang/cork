namespace Cork.CodeGen.Emit;

using Cork.Language.Ast;

/// <summary>
/// Emits 6510 code for scene declarations: hardware blocks, scene variables,
/// scene methods, vsync wait, raster IRQ setup, and pending struct methods.
/// </summary>
public sealed class SceneEmitter(EmitContext ctx)
{
    public void EmitScene(SceneNode scene)
    {
        ctx.Symbols.ResetForScene();

        ctx.Buffer.DefineLabel($"_scene_{scene.Name}");

        var sceneVarNames = new HashSet<string>();
        foreach (var member in scene.Members)
        {
            if (member is SceneVarDeclNode varDecl)
                sceneVarNames.Add(varDecl.Name);
        }

        foreach (var member in scene.Members)
        {
            if (member is SceneMethodNode method)
            {
                if (ctx.Symbols.IsGlobalName(method.SelectorName))
                    throw new InvalidOperationException($"Scene method '{method.SelectorName}' shadows a global method");
                ctx.Symbols.RegisterMethodParams(method.SelectorName, method.Parameters);
                foreach (var param in method.Parameters)
                {
                    if (param.ParamName != "")
                    {
                        if (sceneVarNames.Contains(param.ParamName))
                            throw new InvalidOperationException(
                                $"Parameter '{param.ParamName}' in method '{method.SelectorName}' shadows a scene declaration");
                        ctx.Symbols.AllocZeroPage(param.ParamName);
                    }
                }
            }
        }

        foreach (var member in scene.Members)
            if (member is HardwareBlockNode hw)
                EmitHardwareBlock(hw);

        foreach (var member in scene.Members)
            if (member is SceneVarDeclNode varDecl)
                EmitSceneVar(varDecl);

        foreach (var member in scene.Members)
            if (member is EnterBlockNode enter)
                ctx.Statements.EmitBlock(enter.Body);

        var rasterBlocks = scene.Members.OfType<RasterBlockNode>()
            .OrderBy(r => r.Line).ToList();
        if (rasterBlocks.Count > 0)
        {
            ctx.Symbols.AllocZeroPage($"_ridx_{scene.Name}");
            EmitRasterIrqSetup(rasterBlocks, scene.Name);
        }

        var frameLabel = $"_frame_loop_{scene.Name}";
        ctx.Buffer.DefineLabel(frameLabel);
        EmitVsyncWait();
        foreach (var member in scene.Members)
        {
            if (member is FrameBlockNode frame)
                ctx.Statements.EmitBlock(frame.Body);
        }
        ctx.Buffer.EmitJmpAbsolute(ctx.Buffer.GetLabel(frameLabel));

        foreach (var member in scene.Members)
        {
            if (member is SceneMethodNode method)
                EmitSceneMethod(method);
        }

        EmitPendingStructMethods(scene);

        if (rasterBlocks.Count > 0)
            EmitRasterHandler(rasterBlocks, scene.Name);
    }

    public void EmitHardwareBlock(HardwareBlockNode hw)
    {
        foreach (var setting in hw.Settings)
        {
            var value = ctx.Expressions.EvalConstExpr(setting.Value);
            var addr = setting.Name switch
            {
                "border" => (ushort)0xD020,
                "background" => (ushort)0xD021,
                _ => throw new InvalidOperationException($"Unknown hardware setting: {setting.Name}")
            };
            ctx.Buffer.EmitLdaImmediate(value);
            ctx.Buffer.EmitStaAbsolute(addr);
        }
    }

    public void EmitSceneVar(SceneVarDeclNode decl)
    {
        if (decl.IsConst && decl.Initializer is IntLiteralExpr constLit)
        {
            ctx.Symbols.AddConstant(decl.Name, constLit.Value);
            return;
        }

        if (ctx.Symbols.TryGetStructType(decl.TypeName, out var structType))
        {
            var fieldMap = new Dictionary<string, byte>();
            foreach (var field in structType.Fields)
            {
                var fieldZpName = $"{decl.Name}${field.Name}";
                var zp = ctx.Symbols.AllocZeroPage(fieldZpName);
                fieldMap[field.Name] = zp;

                if (field.DefaultValue is IntLiteralExpr intLit)
                    ctx.Buffer.EmitLdaImmediate((byte)intLit.Value);
                else
                    ctx.Buffer.EmitLdaImmediate(0);
                ctx.Buffer.EmitStaZeroPage(zp);
            }
            ctx.Symbols.RegisterStructInstance(decl.Name, decl.TypeName, fieldMap);
            return;
        }

        ctx.Statements.EmitTypedVarInit(decl.TypeName, decl.Name, decl.Initializer);
    }

    public void EmitSceneMethod(SceneMethodNode method)
    {
        var label = $"_method_{method.SelectorName}";
        ctx.Buffer.DefineLabel(label);
        ctx.Statements.EmitBlock(method.Body);
        ctx.Buffer.EmitRts();
    }

    public void EmitVsyncWait()
    {
        ctx.Buffer.EmitLdaAbsolute(0xD012);
        ctx.Buffer.EmitCmpImmediate(251);
        ctx.Buffer.EmitBne(unchecked((sbyte)-7));
    }

    public void EmitRasterIrqSetup(List<RasterBlockNode> rasters, string sceneName)
    {
        ctx.Buffer.EmitJsrForward($"_irq_setup_{sceneName}");
    }

    public void EmitRasterHandler(List<RasterBlockNode> rasters, string sceneName)
    {
        var handlerLabel = $"_irq_handler_{sceneName}";
        var setupLabel = $"_irq_setup_{sceneName}";
        var firstLine = (byte)rasters[0].Line;

        ctx.Buffer.DefineLabel(setupLabel);
        ctx.Buffer.EmitLdaImmediate(0x7F);
        ctx.Buffer.EmitStaAbsolute(0xDC0D);
        ctx.Buffer.EmitLdaAbsolute(0xDC0D);
        ctx.Buffer.EmitLdaImmediate(firstLine);
        ctx.Buffer.EmitStaAbsolute(0xD012);
        ctx.Buffer.EmitLdaAbsolute(0xD011);
        ctx.Buffer.EmitByte(0x29); ctx.Buffer.EmitByte(0x7F);
        ctx.Buffer.EmitStaAbsolute(0xD011);
        ctx.Buffer.EmitLdaImmediate(0x01);
        ctx.Buffer.EmitStaAbsolute(0xD01A);
        ctx.Buffer.EmitStoreAddrForward(handlerLabel, 0x0314, 0x0315);
        ctx.Buffer.EmitLdaImmediate(0);
        ctx.Buffer.EmitStaZeroPage(ctx.Symbols.GetLocal($"_ridx_{sceneName}"));
        ctx.Buffer.EmitCli();
        ctx.Buffer.EmitRts();

        var rasterIdxZp = ctx.Symbols.GetLocal($"_ridx_{sceneName}");
        ctx.Buffer.DefineLabel(handlerLabel);
        ctx.Buffer.EmitLdaImmediate(0xFF);
        ctx.Buffer.EmitStaAbsolute(0xD019);

        ctx.Buffer.EmitLdxZeroPage(rasterIdxZp);
        for (var i = 0; i < rasters.Count; i++)
        {
            ctx.Buffer.EmitCpxImmediate((byte)i);
            ctx.Buffer.EmitBne(3);
            ctx.Buffer.EmitJmpForward($"_rblk_{sceneName}_{i}");
        }
        ctx.Buffer.EmitJmpAbsolute(0xEA31);

        for (var i = 0; i < rasters.Count; i++)
        {
            var nextIdx = (byte)((i + 1) % rasters.Count);
            var nextLine = (byte)rasters[(i + 1) % rasters.Count].Line;

            ctx.Buffer.DefineLabel($"_rblk_{sceneName}_{i}");
            ctx.Statements.EmitBlock(rasters[i].Body);
            ctx.Buffer.EmitLdaImmediate(nextLine);
            ctx.Buffer.EmitStaAbsolute(0xD012);
            ctx.Buffer.EmitLdaImmediate(nextIdx);
            ctx.Buffer.EmitStaZeroPage(rasterIdxZp);
            ctx.Buffer.EmitJmpAbsolute(0xEA31);
        }
    }

    public void EmitPendingStructMethods(SceneNode scene)
    {
        foreach (var (instanceName, inst) in ctx.Symbols.StructInstances)
        {
            if (!ctx.Symbols.TryGetStructType(inst.StructType, out var structType)) continue;

            foreach (var method in structType.Methods)
            {
                var label = $"_struct_{inst.StructType}_{method.SelectorName}_{instanceName}";
                if (!ctx.Symbols.IsStructMethodEmitted(label)) continue;

                ctx.Buffer.DefineLabel(label);

                var savedLocals = new Dictionary<string, byte>();
                foreach (var (fieldName, zpAddr) in inst.Fields)
                {
                    if (ctx.Symbols.TryGetLocal(fieldName, out var old))
                        savedLocals[fieldName] = old;
                    ctx.Symbols.SetLocal(fieldName, zpAddr);
                }

                ctx.Statements.EmitBlock(method.Body);
                ctx.Buffer.EmitRts();

                foreach (var (fieldName, _) in inst.Fields)
                {
                    if (savedLocals.TryGetValue(fieldName, out var old))
                        ctx.Symbols.SetLocal(fieldName, old);
                    else
                        ctx.Symbols.RemoveLocal(fieldName);
                }
            }
        }
    }
}
