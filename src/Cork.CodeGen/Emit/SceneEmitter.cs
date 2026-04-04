namespace Cork.CodeGen.Emit;

using Cork.Language.Ast;

/// <summary>
/// Emits 6510 code for scene declarations: hardware blocks, scene variables,
/// scene methods, vsync wait, raster IRQ setup, and pending struct methods.
/// </summary>
public sealed class SceneEmitter(EmitContext ctx)
{
    private string _currentSceneName = "";

    public void EmitScene(SceneNode scene)
    {
        ctx.Symbols.ResetForScene();
        ctx.DirtySpriteRegs.Clear();
        _currentSceneName = scene.Name;

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
            if (member is SpriteBlockNode sprite)
                EmitSpriteBlock(sprite);

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

        // String variable: string name = "HELLO"; or string[20] name = "HI";
        if (decl.TypeName == "string")
        {
            var strValue = decl.Initializer is StringLiteralExpr strLit ? strLit.Value : "";
            var screenCodes = ScreenCodes.FromString(strValue);
            var length = decl.ArraySize > 0 ? decl.ArraySize : screenCodes.Length;

            var zpBase = ctx.Symbols.AllocArrayZeroPage(decl.Name, length);
            ctx.Symbols.RegisterStringVar(decl.Name, zpBase, length);

            // Initialize: copy screen codes, pad with spaces if needed
            for (var i = 0; i < length; i++)
            {
                var val = i < screenCodes.Length ? screenCodes[i] : (byte)32; // space padding
                ctx.Buffer.EmitLdaImmediate(val);
                ctx.Buffer.EmitStaZeroPage((byte)(zpBase + i));
            }
            return;
        }

        if (ctx.Symbols.TryGetStructType(decl.TypeName, out var structType))
        {
            if (decl.ArraySize > 0)
            {
                // Struct array: allocate N bytes per field (struct-of-arrays)
                var fieldMap = new Dictionary<string, byte>();
                foreach (var field in structType.Fields)
                {
                    var fieldZpName = $"{decl.Name}${field.Name}";
                    var baseZp = ctx.Symbols.AllocArrayZeroPage(fieldZpName, decl.ArraySize);
                    fieldMap[field.Name] = baseZp;

                    // Initialize all elements with default
                    var defVal = field.DefaultValue is IntLiteralExpr il ? (byte)il.Value : (byte)0;
                    for (var i = 0; i < decl.ArraySize; i++)
                    {
                        ctx.Buffer.EmitLdaImmediate(defVal);
                        ctx.Buffer.EmitStaZeroPage((byte)(baseZp + i));
                    }
                }
                ctx.Symbols.RegisterStructArray(decl.Name, decl.TypeName, fieldMap, decl.ArraySize);
                return;
            }

            // Single struct instance (supports nested structs)
            var singleFieldMap = new Dictionary<string, byte>();
            AllocStructFields(decl.Name, structType, singleFieldMap);
            ctx.Symbols.RegisterStructInstance(decl.Name, decl.TypeName, singleFieldMap);
            return;
        }

        // var with struct initializer: var name = TypeName { field = value, ... }
        if (decl.Initializer is StructInitExpr structInit)
        {
            if (ctx.Symbols.TryGetStructType(structInit.TypeName, out var initStructType))
            {
                // Allocate all fields (including nested structs) with defaults
                var fieldMap = new Dictionary<string, byte>();
                AllocStructFields(decl.Name, initStructType, fieldMap);

                // Apply overrides from the initializer
                foreach (var (fieldName, value) in structInit.FieldInits)
                {
                    if (value is StructInitExpr nestedInit)
                    {
                        // Nested struct init: pos = Position { x = 20, y = 12 }
                        foreach (var (nf, nv) in nestedInit.FieldInits)
                        {
                            var key = $"{fieldName}.{nf}";
                            if (fieldMap.TryGetValue(key, out var nestedZp))
                            {
                                ctx.Expressions.EmitExprToA(nv);
                                ctx.Buffer.EmitStaZeroPage(nestedZp);
                            }
                        }
                    }
                    else if (fieldMap.TryGetValue(fieldName, out var zp))
                    {
                        ctx.Expressions.EmitExprToA(value);
                        ctx.Buffer.EmitStaZeroPage(zp);
                    }
                }

                ctx.Symbols.RegisterStructInstance(decl.Name, structInit.TypeName, fieldMap);
                return;
            }
        }

        ctx.Statements.EmitTypedVarInit(decl.TypeName, decl.Name, decl.Initializer);
    }

    private void EmitSpriteBlock(SpriteBlockNode sprite)
    {
        var idx = sprite.SpriteIndex;
        var bit = (byte)(1 << idx);

        // Allocate ZP for mutable fields
        var xZp = ctx.Symbols.AllocZeroPage($"{sprite.Name}$x");
        var yZp = ctx.Symbols.AllocZeroPage($"{sprite.Name}$y");

        // Register as struct instance with auto-sync info
        var fieldMap = new Dictionary<string, byte> { ["x"] = xZp, ["y"] = yZp };
        ctx.Symbols.RegisterStructInstance(sprite.Name, "_sprite", fieldMap);

        // Register auto-sync: writing to x/y auto-writes VIC-II registers
        ctx.Symbols.RegisterSpriteSync(sprite.Name, idx, xZp, yZp);

        // Parse settings
        byte initX = 0, initY = 0, color = 1;
        bool multicolor = false, expandX = false, expandY = false, priorityBack = false;
        string? dataRef = null;

        foreach (var setting in sprite.Settings)
        {
            switch (setting.Name)
            {
                case "x": initX = ctx.Expressions.EvalConstExpr(setting.Value); break;
                case "y": initY = ctx.Expressions.EvalConstExpr(setting.Value); break;
                case "color": color = ctx.Expressions.EvalConstExpr(setting.Value); break;
                case "multicolor": multicolor = setting.Value is BoolLiteralExpr { Value: true }; break;
                case "expandX": expandX = setting.Value is BoolLiteralExpr { Value: true }; break;
                case "expandY": expandY = setting.Value is BoolLiteralExpr { Value: true }; break;
                case "priority":
                    if (setting.Value is IdentifierExpr { Name: "back" }) priorityBack = true;
                    break;
                case "data":
                    if (setting.Value is IdentifierExpr dataIdent)
                        dataRef = dataIdent.Name;
                    else if (setting.Value is SpritePatternExpr)
                        dataRef = $"_sprite_{_currentSceneName}_{sprite.Name}Data";
                    break;
            }
        }

        // Initialize ZP
        ctx.Buffer.EmitLdaImmediate(initX);
        ctx.Buffer.EmitStaZeroPage(xZp);
        ctx.Buffer.EmitLdaImmediate(initY);
        ctx.Buffer.EmitStaZeroPage(yZp);

        // VIC-II register init
        ctx.Buffer.EmitLdaImmediate(initX);
        ctx.Buffer.EmitStaAbsolute((ushort)(0xD000 + idx * 2));
        ctx.Buffer.EmitLdaImmediate(initY);
        ctx.Buffer.EmitStaAbsolute((ushort)(0xD001 + idx * 2));
        ctx.Buffer.EmitLdaImmediate(color);
        ctx.Buffer.EmitStaAbsolute((ushort)(0xD027 + idx));

        // Sprite pointer
        if (dataRef != null)
        {
            // Try derived name first, then common names
            string[] ptrNames = [
                dataRef.EndsWith("Sprite") ? dataRef[..^6] + "Ptr" : dataRef + "Ptr",
                "spritePtr",
                dataRef.EndsWith("Data") ? dataRef[..^4] + "Ptr" : ""
            ];
            foreach (var ptrName in ptrNames)
            {
                if (ptrName != "" && ctx.Symbols.TryGetConstant(ptrName, out var ptrVal))
                {
                    ctx.Buffer.EmitLdaImmediate((byte)ptrVal);
                    ctx.Buffer.EmitStaAbsolute((ushort)(0x07F8 + idx));
                    break;
                }
            }
        }

        // Enable sprite (track for cleanup on scene exit)
        ctx.Buffer.EmitLdaAbsolute(0xD015);
        ctx.Buffer.EmitByte(0x09); ctx.Buffer.EmitByte(bit); // ORA #bit
        ctx.Buffer.EmitStaAbsolute(0xD015);
        ctx.DirtySpriteRegs.Add(0xD015);

        // Multicolor
        if (multicolor)
        {
            ctx.Buffer.EmitLdaAbsolute(0xD01C);
            ctx.Buffer.EmitByte(0x09); ctx.Buffer.EmitByte(bit);
            ctx.Buffer.EmitStaAbsolute(0xD01C);
            ctx.DirtySpriteRegs.Add(0xD01C);
        }

        // Expand X
        if (expandX)
        {
            ctx.Buffer.EmitLdaAbsolute(0xD01D);
            ctx.Buffer.EmitByte(0x09); ctx.Buffer.EmitByte(bit);
            ctx.Buffer.EmitStaAbsolute(0xD01D);
            ctx.DirtySpriteRegs.Add(0xD01D);
        }

        // Expand Y
        if (expandY)
        {
            ctx.Buffer.EmitLdaAbsolute(0xD017);
            ctx.Buffer.EmitByte(0x09); ctx.Buffer.EmitByte(bit);
            ctx.Buffer.EmitStaAbsolute(0xD017);
            ctx.DirtySpriteRegs.Add(0xD017);
        }

        // Priority (behind background)
        if (priorityBack)
        {
            ctx.Buffer.EmitLdaAbsolute(0xD01B);
            ctx.Buffer.EmitByte(0x09); ctx.Buffer.EmitByte(bit);
            ctx.Buffer.EmitStaAbsolute(0xD01B);
            ctx.DirtySpriteRegs.Add(0xD01B);
        }
    }

    private void AllocStructFields(string prefix, StructDeclNode structType, Dictionary<string, byte> fieldMap)
    {
        foreach (var field in structType.Fields)
        {
            // Check if field type is itself a struct (composition)
            if (ctx.Symbols.TryGetStructType(field.TypeName, out var nestedStruct))
            {
                // Recursively allocate nested struct fields with dotted prefix
                var nestedMap = new Dictionary<string, byte>();
                AllocStructFields($"{prefix}${field.Name}", nestedStruct, nestedMap);
                // Register the nested struct as a sub-instance so hero.pos.x resolves
                ctx.Symbols.RegisterStructInstance($"{prefix}.{field.Name}", field.TypeName, nestedMap);
                // Also put in parent map with dotted key for direct field resolution
                foreach (var (k, v) in nestedMap)
                    fieldMap[$"{field.Name}.{k}"] = v;
            }
            else
            {
                var fieldZpName = $"{prefix}${field.Name}";
                var zp = ctx.Symbols.AllocZeroPage(fieldZpName);
                fieldMap[field.Name] = zp;

                if (field.DefaultValue is IntLiteralExpr intLit)
                    ctx.Buffer.EmitLdaImmediate((byte)intLit.Value);
                else
                    ctx.Buffer.EmitLdaImmediate(0);
                ctx.Buffer.EmitStaZeroPage(zp);
            }
        }
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
