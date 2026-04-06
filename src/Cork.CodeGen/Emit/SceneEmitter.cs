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
        ctx.IsBitmapMode = false;
        _currentSceneName = scene.Name;

        ctx.Buffer.DefineLabel($"_scene_{scene.Name}");
        var sceneStartMarker = ctx.Buffer.EmitDebugMarker();
        ctx.Debug?.OpenScope(ctx.Debug.Scenes, scene.Name, sceneStartMarker);

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

        // Capture scene-local variables for debug info
        if (ctx.Debug != null)
        {
            foreach (var (name, zp) in ctx.Symbols.Locals)
            {
                if (name.StartsWith('_')) continue; // skip internal vars
                var type = ctx.Symbols.GetVarType(name) ?? "byte";
                var size = SymbolTable.Is16BitType(type) ? 2 : 1;
                ctx.Debug.AddVariable(name, type, zp, size, scene.Name);
            }
            foreach (var (name, inst) in ctx.Symbols.StructInstances)
            {
                foreach (var (field, zp) in inst.Fields)
                    ctx.Debug.AddVariable($"{name}.{field}", inst.StructType, zp, 1, scene.Name);
            }
            foreach (var (name, info) in ctx.Symbols.StringVars)
            {
                if (name.StartsWith('_')) continue;
                ctx.Debug.AddVariable(name, "string", info.ZpBase, info.Length, scene.Name);
            }
        }

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

        var sceneEndMarker = ctx.Buffer.EmitDebugMarker();
        ctx.Debug?.CloseScope(ctx.Debug.Scenes, scene.Name, sceneEndMarker);
    }

    public void EmitHardwareBlock(HardwareBlockNode hw)
    {
        foreach (var setting in hw.Settings)
        {
            if (setting.Name == "mode")
            {
                EmitGraphicsMode(setting);
                continue;
            }

            var value = ctx.Expressions.EvalConstExpr(setting.Value);
            var addr = setting.Name switch
            {
                "border" => (ushort)0xD020,
                "background" => (ushort)0xD021,
                "background1" => (ushort)0xD022,
                "background2" => (ushort)0xD023,
                "background3" => (ushort)0xD024,
                "multicolor0" => (ushort)0xD025,
                "multicolor1" => (ushort)0xD026,
                _ => throw new InvalidOperationException($"Unknown hardware setting: {setting.Name}")
            };
            ctx.Buffer.EmitLdaImmediate(value);
            ctx.Buffer.EmitStaAbsolute(addr);
        }
    }

    /// <summary>
    /// Set VIC-II graphics mode via $D011 (bits 6,5) and $D016 (bit 4).
    /// Bitmap modes also set $D018 bit 3 for bitmap base at $2000.
    /// </summary>
    private void EmitGraphicsMode(HardwareSetting setting)
    {
        if (setting.Value is not IdentifierExpr modeIdent)
            throw new InvalidOperationException("mode: value must be an identifier");

        // $D011: base $1B (DEN=1, RSEL=1, YSCROLL=3), modify bits 5-6
        // $D016: base $08 (CSEL=1, XSCROLL=0), modify bit 4
        // $D018: $15 = screen at $0400, chars at $1000 (text default)
        //        $1D = screen at $0400, bitmap at $2000
        ctx.IsBitmapMode = modeIdent.Name is "bitmap" or "multicolorBitmap";

        var (d011, d016, d018) = modeIdent.Name switch
        {
            "text"             => ((byte)0x1B, (byte)0x08, (byte)0x15),
            "multicolorText"   => ((byte)0x1B, (byte)0x18, (byte)0x15),
            "bitmap"           => ((byte)0x3B, (byte)0x08, (byte)0x1D),
            "multicolorBitmap" => ((byte)0x3B, (byte)0x18, (byte)0x1D),
            "ecm"              => ((byte)0x5B, (byte)0x08, (byte)0x15),
            _ => throw new InvalidOperationException(
                $"Unknown graphics mode: {modeIdent.Name}. " +
                "Valid modes: text, multicolorText, bitmap, multicolorBitmap, ecm")
        };

        ctx.Buffer.EmitLdaImmediate(d011);
        ctx.Buffer.EmitStaAbsolute(0xD011);
        ctx.Buffer.EmitLdaImmediate(d016);
        ctx.Buffer.EmitStaAbsolute(0xD016);
        ctx.Buffer.EmitLdaImmediate(d018);
        ctx.Buffer.EmitStaAbsolute(0xD018);
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

        // Allocate ZP for mutable fields (X is word for 9-bit position 0-511)
        var xZp = ctx.Symbols.AllocWordZeroPage($"{sprite.Name}$x");
        var yZp = ctx.Symbols.AllocZeroPage($"{sprite.Name}$y");

        // Register as struct instance with auto-sync info
        var fieldMap = new Dictionary<string, byte> { ["x"] = xZp, ["y"] = yZp };
        ctx.Symbols.RegisterStructInstance(sprite.Name, "_sprite", fieldMap);

        // Register auto-sync: writing to x/y auto-writes VIC-II registers
        ctx.Symbols.RegisterSpriteSync(sprite.Name, idx, xZp, yZp);

        // Parse settings
        ushort initX = 0;
        byte initY = 0, color = 1;
        bool multicolor = false, expandX = false, expandY = false, priorityBack = false;
        string? dataRef = null;

        foreach (var setting in sprite.Settings)
        {
            switch (setting.Name)
            {
                case "x":
                    if (ctx.Expressions.TryFoldConstant(setting.Value, out var xVal))
                        initX = (ushort)xVal;
                    else
                        initX = ctx.Expressions.EvalConstExpr(setting.Value);
                    break;
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

        // Initialize ZP (X is word: lo at xZp, hi at xZp+1)
        ctx.Buffer.EmitLdaImmediate((byte)(initX & 0xFF));
        ctx.Buffer.EmitStaZeroPage(xZp);
        ctx.Buffer.EmitLdaImmediate((byte)(initX >> 8));
        ctx.Buffer.EmitStaZeroPage((byte)(xZp + 1));
        ctx.Buffer.EmitLdaImmediate(initY);
        ctx.Buffer.EmitStaZeroPage(yZp);

        // VIC-II register init
        ctx.Buffer.EmitLdaImmediate((byte)(initX & 0xFF));
        ctx.Buffer.EmitStaAbsolute((ushort)(0xD000 + idx * 2));
        ctx.Buffer.EmitLdaImmediate(initY);
        ctx.Buffer.EmitStaAbsolute((ushort)(0xD001 + idx * 2));
        ctx.Buffer.EmitLdaImmediate(color);
        ctx.Buffer.EmitStaAbsolute((ushort)(0xD027 + idx));

        // X MSB in $D010 (bit per sprite)
        if (initX > 255)
        {
            ctx.Buffer.EmitLdaAbsolute(0xD010);
            ctx.Buffer.EmitOraImmediate(bit);
            ctx.Buffer.EmitStaAbsolute(0xD010);
        }

        // Sprite pointer (from inline pattern — pointer computed during codegen)
        if (dataRef != null)
        {
            var ptrName = dataRef.Replace("Data", "Ptr");
            if (ctx.Symbols.TryGetConstant(ptrName, out var ptrVal))
            {
                ctx.Buffer.EmitLdaImmediate((byte)ptrVal);
                ctx.Buffer.EmitStaAbsolute((ushort)(0x07F8 + idx));
            }
        }

        // Enable sprite (track for cleanup on scene exit)
        ctx.Buffer.EmitLdaAbsolute(0xD015);
        ctx.Buffer.EmitOraImmediate(bit);
        ctx.Buffer.EmitStaAbsolute(0xD015);
        ctx.DirtySpriteRegs.Add(0xD015);

        // Multicolor
        if (multicolor)
        {
            ctx.Buffer.EmitLdaAbsolute(0xD01C);
            ctx.Buffer.EmitOraImmediate(bit);
            ctx.Buffer.EmitStaAbsolute(0xD01C);
            ctx.DirtySpriteRegs.Add(0xD01C);
        }

        // Expand X
        if (expandX)
        {
            ctx.Buffer.EmitLdaAbsolute(0xD01D);
            ctx.Buffer.EmitOraImmediate(bit);
            ctx.Buffer.EmitStaAbsolute(0xD01D);
            ctx.DirtySpriteRegs.Add(0xD01D);
        }

        // Expand Y
        if (expandY)
        {
            ctx.Buffer.EmitLdaAbsolute(0xD017);
            ctx.Buffer.EmitOraImmediate(bit);
            ctx.Buffer.EmitStaAbsolute(0xD017);
            ctx.DirtySpriteRegs.Add(0xD017);
        }

        // Priority (behind background)
        if (priorityBack)
        {
            ctx.Buffer.EmitLdaAbsolute(0xD01B);
            ctx.Buffer.EmitOraImmediate(bit);
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
        var methodStartMarker = ctx.Buffer.EmitDebugMarker();
        ctx.Debug?.OpenScope(ctx.Debug.Methods, method.SelectorName, methodStartMarker);
        var prevMethod = ctx.ActiveMethodSelector;
        ctx.ActiveMethodSelector = method.SelectorName;
        ctx.Statements.EmitBlock(method.Body);
        ctx.ActiveMethodSelector = prevMethod;
        ctx.Buffer.EmitRts();
        var methodEndMarker = ctx.Buffer.EmitDebugMarker();
        ctx.Debug?.CloseScope(ctx.Debug.Methods, method.SelectorName, methodEndMarker);
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
        ctx.Buffer.EmitAndImmediate(0x7F);
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
        foreach (var (instanceName, inst) in ctx.Symbols.StructInstances.ToList())
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

                // Register nested struct instances with bare names so pos.y resolves
                var nestedInstances = new List<string>();
                foreach (var field in structType.Fields)
                {
                    if (ctx.Symbols.TryGetStructType(field.TypeName, out _) &&
                        ctx.Symbols.TryGetStructInstance($"{instanceName}.{field.Name}", out var nestedInst))
                    {
                        ctx.Symbols.RegisterStructInstance(field.Name, nestedInst.StructType, nestedInst.Fields);
                        nestedInstances.Add(field.Name);
                    }
                }

                var prevMethod2 = ctx.ActiveMethodSelector;
                ctx.ActiveMethodSelector = method.SelectorName;
                ctx.Statements.EmitBlock(method.Body);
                ctx.ActiveMethodSelector = prevMethod2;
                ctx.Buffer.EmitRts();

                // Remove temporary nested instance registrations
                foreach (var name in nestedInstances)
                    ctx.Symbols.RemoveStructInstance(name);

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
