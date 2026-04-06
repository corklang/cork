namespace Cork.CodeGen.Emit;

using Cork.Language.Ast;

/// <summary>
/// Manages all symbol state for code generation: locals, globals, constants,
/// struct types, enum types, struct instances, method parameters, and variable types.
/// </summary>
public sealed class SymbolTable
{
    private readonly Dictionary<string, byte> _locals = [];
    private readonly Dictionary<string, byte> _globals = [];
    private readonly HashSet<string> _globalNames = [];
    private readonly Dictionary<string, string> _varTypes = [];
    private readonly Dictionary<string, string> _globalVarTypes = [];
    private readonly Dictionary<string, long> _constants = [];
    private readonly Dictionary<string, Dictionary<string, long>> _enumTypes = [];
    private readonly Dictionary<string, StructDeclNode> _structTypes = [];
    private readonly Dictionary<string, (string StructType, Dictionary<string, byte> Fields)> _structInstances = [];
    // Struct arrays: name → (structType, fieldBases, arraySize)
    private readonly Dictionary<string, (string StructType, Dictionary<string, byte> FieldBases, int Size)> _structArrays = [];
    private readonly HashSet<string> _emittedStructMethods = [];
    private readonly Dictionary<string, List<MethodParameter>> _methodParams = [];
    // Method param ZP addresses: selectorName → [paramName → zpAddr]
    private readonly Dictionary<string, Dictionary<string, byte>> _methodParamZp = [];
    // String variables: name → (zpBase, length)
    private readonly Dictionary<string, (byte ZpBase, int Length)> _stringVars = [];
    // Global string variables survive scene resets
    private readonly Dictionary<string, (byte ZpBase, int Length)> _globalStringVars = [];
    // Reference parameters (string/array): name → (ptrZpLo, ptrZpHi, lenZp)
    private readonly Dictionary<string, (byte PtrLo, byte PtrHi, byte LenZp)> _refParams = [];
    // Sprite auto-sync: zpAddr → VIC-II register address
    private readonly Dictionary<byte, ushort> _spriteSyncRegs = [];

    private byte _nextZp = 0x02;
    private byte _globalZpEnd = 0x02;
    // Shared param zone: all methods reuse the same ZP region for params
    private const byte ParamZoneBase = 0x80;
    private byte _paramZoneEnd = ParamZoneBase;
    // Method locals zone: starts after param zone, reused across methods
    public byte MethodLocalsBase => _paramZoneEnd;

    // --- Zero page allocation ---

    public byte AllocZeroPage(string name)
    {
        CheckShadowing(name);
        if (_locals.ContainsKey(name)) return _locals[name];
        var addr = _nextZp++;
        if (_nextZp >= 0xEF) throw new InvalidOperationException("Out of zero page slots");
        _locals[name] = addr;
        return addr;
    }

    public byte AllocWordZeroPage(string name)
    {
        CheckShadowing(name);
        var addr = _nextZp;
        _nextZp += 2;
        if (_nextZp >= 0xEF) throw new InvalidOperationException("Out of zero page slots");
        _locals[name] = addr;
        _varTypes[name] = "word";
        return addr;
    }

    public byte AllocArrayZeroPage(string name, int count)
    {
        var addr = _nextZp;
        _nextZp += (byte)count;
        if (_nextZp >= 0xEF) throw new InvalidOperationException("Out of zero page slots");
        _locals[name] = addr;
        return addr;
    }

    public void RegisterStructArray(string name, string structType, Dictionary<string, byte> fieldBases, int size)
    {
        _structArrays[name] = (structType, fieldBases, size);
    }

    public bool TryGetStructArray(string name, out (string StructType, Dictionary<string, byte> FieldBases, int Size) info)
    {
        return _structArrays.TryGetValue(name, out info);
    }

    public byte AllocGlobal(string name)
    {
        if (!_globals.ContainsKey(name))
        {
            var zp = _globalZpEnd++;
            _globals[name] = zp;
            _locals[name] = zp;
            _globalNames.Add(name);
        }
        return _globals[name];
    }

    public byte AllocGlobalArray(string name, int count)
    {
        var addr = _globalZpEnd;
        _globalZpEnd += (byte)count;
        if (_globalZpEnd >= 0x80) throw new InvalidOperationException("Out of global zero page slots");
        _globals[name] = addr;
        _locals[name] = addr;
        _globalNames.Add(name);
        return addr;
    }

    // --- Lookup ---

    public byte GetLocal(string name) =>
        _locals.TryGetValue(name, out var zp) ? zp
            : throw new InvalidOperationException($"Undefined local: {name}");

    public bool IsWordVar(string name) =>
        _varTypes.TryGetValue(name, out var t) && Is16BitType(t);

    public string? GetVarType(string name) =>
        _varTypes.TryGetValue(name, out var t) ? t : null;

    public static bool Is16BitType(string t) => t is "word" or "sword" or "fixed" or "sfixed";
    public static bool IsSignedType(string t) => t is "sbyte" or "sword" or "sfixed";

    public bool TryGetConstant(string name, out long value) =>
        _constants.TryGetValue(name, out value);

    public bool TryGetVarType(string name, out string type) =>
        _varTypes.TryGetValue(name, out type!);

    public string? GetVarTypeForZp(byte zpLo) =>
        _varTypes.FirstOrDefault(kv => _locals.TryGetValue(kv.Key, out var z) && z == zpLo).Value;

    // --- Shadowing ---

    public void CheckShadowing(string name)
    {
        if (name.StartsWith('_') || name.Contains('$')) return;
        if (_globalNames.Contains(name))
            throw new InvalidOperationException($"'{name}' shadows a global declaration");
    }

    // --- Scope management ---

    public void ResetToGlobalScope()
    {
        ResetCoreState();
    }

    public void RegisterSpriteSync(string name, int spriteIndex, byte xZp, byte yZp)
    {
        _spriteSyncRegs[xZp] = (ushort)(0xD000 + spriteIndex * 2);     // X register
        _spriteSyncRegs[yZp] = (ushort)(0xD001 + spriteIndex * 2);     // Y register
    }

    public bool TryGetSpriteSync(byte zpAddr, out ushort vicRegister)
    {
        return _spriteSyncRegs.TryGetValue(zpAddr, out vicRegister);
    }

    public void RegisterRefParam(string name, byte ptrLo, byte ptrHi, byte lenZp)
    {
        _refParams[name] = (ptrLo, ptrHi, lenZp);
    }

    public bool TryGetRefParam(string name, out (byte PtrLo, byte PtrHi, byte LenZp) info)
    {
        return _refParams.TryGetValue(name, out info);
    }

    public void RegisterStringVar(string name, byte zpBase, int length, bool isGlobal = false)
    {
        _stringVars[name] = (zpBase, length);
        if (isGlobal)
            _globalStringVars[name] = (zpBase, length);
    }

    public bool TryGetStringVar(string name, out (byte ZpBase, int Length) info)
    {
        return _stringVars.TryGetValue(name, out info);
    }

    public void ResetForScene()
    {
        ResetCoreState();
        _structArrays.Clear();
        _stringVars.Clear();
        _spriteSyncRegs.Clear();
        // Note: _refParams NOT cleared — global method ref params persist
        _emittedStructMethods.Clear();
        // Restore global string vars so they're visible in every scene
        foreach (var (name, info) in _globalStringVars)
            _stringVars[name] = info;
    }

    private void ResetCoreState()
    {
        _locals.Clear();
        _varTypes.Clear();
        _constants.Clear();
        _structInstances.Clear();
        foreach (var (name, zp) in _globals)
            _locals[name] = zp;
        foreach (var (name, type) in _globalVarTypes)
            _varTypes[name] = type;
        foreach (var (name, val) in _globalConstants)
            _constants[name] = val;
        _nextZp = _globalZpEnd;
    }

    // --- Struct types ---

    public void RegisterStructType(StructDeclNode structDecl) =>
        _structTypes[structDecl.Name] = structDecl;

    public bool TryGetStructType(string name, out StructDeclNode structType) =>
        _structTypes.TryGetValue(name, out structType!);

    // --- Enum types ---

    public void RegisterEnumType(EnumDeclNode enumDecl)
    {
        var members = new Dictionary<string, long>();
        foreach (var m in enumDecl.Members)
            members[m.Name] = m.Value;
        _enumTypes[enumDecl.Name] = members;
    }

    public bool TryGetEnumMember(string enumName, string memberName, out long value)
    {
        value = 0;
        return _enumTypes.TryGetValue(enumName, out var members) &&
               members.TryGetValue(memberName, out value);
    }

    // --- Struct instances ---

    public void RegisterStructInstance(string name, string structType, Dictionary<string, byte> fields) =>
        _structInstances[name] = (structType, fields);

    public bool TryGetStructInstance(string name, out (string StructType, Dictionary<string, byte> Fields) instance) =>
        _structInstances.TryGetValue(name, out instance);

    public IEnumerable<KeyValuePair<string, (string StructType, Dictionary<string, byte> Fields)>> StructInstances =>
        _structInstances;

    // --- Emitted struct methods ---

    public bool IsStructMethodEmitted(string label) => _emittedStructMethods.Contains(label);
    public void MarkStructMethodEmitted(string label) => _emittedStructMethods.Add(label);

    // --- Method parameters ---

    public void RegisterMethodParams(string selectorName, List<MethodParameter> parameters) =>
        _methodParams[selectorName] = parameters;

    public bool TryGetMethodParams(string selectorName, out List<MethodParameter> parameters) =>
        _methodParams.TryGetValue(selectorName, out parameters!);

    // High-water mark for method locals — ensures each method gets its own
    // non-overlapping ZP region so nested calls don't clobber each other.
    private byte _methodLocalsHighWater;

    /// <summary>
    /// Prepare ZP for method body locals. Each method gets a unique region
    /// that doesn't overlap with other methods (important for nested calls).
    /// </summary>
    public void PrepareMethodLocals()
    {
        if (_methodLocalsHighWater < _paramZoneEnd)
            _methodLocalsHighWater = _paramZoneEnd;
        _nextZp = _methodLocalsHighWater;
    }

    /// <summary>
    /// Called after method body emission to record how much ZP this method used.
    /// The next method's locals will start after this method's.
    /// </summary>
    public void FinalizeMethodLocals()
    {
        if (_nextZp > _methodLocalsHighWater)
            _methodLocalsHighWater = _nextZp;
    }

    /// <summary>
    /// Allocate shared param zone ZP slots for a method's parameters.
    /// All methods reuse the same ZP region starting at ParamZoneBase.
    /// Each method gets sequential slots from the base — they overlap between methods
    /// which is safe since calls are non-reentrant.
    /// </summary>
    public Dictionary<string, byte> AllocMethodParams(string selectorName, List<MethodParameter> parameters)
    {
        var zpMap = new Dictionary<string, byte>();
        var zp = ParamZoneBase;
        foreach (var param in parameters)
        {
            if (param.ParamName == "") continue;
            if (param.TypeName == "string" || param.TypeName.EndsWith("[]"))
            {
                // Ref params: 3 bytes (ptr_lo, ptr_hi, len)
                zpMap[$"{param.ParamName}$ptr_lo"] = zp++;
                zpMap[$"{param.ParamName}$ptr_hi"] = zp++;
                zpMap[$"{param.ParamName}$len"] = zp++;
            }
            else
            {
                zpMap[param.ParamName] = zp++;
                if (Is16BitType(param.TypeName))
                    zp++; // reserve hi byte (adjacent)
            }
        }
        if (zp > _paramZoneEnd) _paramZoneEnd = zp;
        _methodParamZp[selectorName] = zpMap;
        return zpMap;
    }

    public bool TryGetMethodParamZp(string selectorName, out Dictionary<string, byte> zpMap) =>
        _methodParamZp.TryGetValue(selectorName, out zpMap!);

    /// <summary>
    /// Temporarily install method params into locals for method body emission.
    /// Call RemoveMethodParamLocals after emission.
    /// </summary>
    public void InstallMethodParamLocals(string selectorName, List<MethodParameter> parameters)
    {
        if (!_methodParamZp.TryGetValue(selectorName, out var zpMap)) return;
        foreach (var param in parameters)
        {
            if (param.ParamName == "") continue;
            if (zpMap.TryGetValue(param.ParamName, out var zp))
            {
                _locals[param.ParamName] = zp;
                if (Is16BitType(param.TypeName))
                    _varTypes[param.ParamName] = param.TypeName;
            }
            // Ref params
            if (zpMap.TryGetValue($"{param.ParamName}$ptr_lo", out var ptrLo))
            {
                var ptrHi = zpMap[$"{param.ParamName}$ptr_hi"];
                var lenZp = zpMap[$"{param.ParamName}$len"];
                _refParams[param.ParamName] = (ptrLo, ptrHi, lenZp);
            }
        }
    }

    public void RemoveMethodParamLocals(List<MethodParameter> parameters)
    {
        foreach (var param in parameters)
        {
            if (param.ParamName == "") continue;
            _locals.Remove(param.ParamName);
            _varTypes.Remove(param.ParamName);
            _refParams.Remove(param.ParamName);
        }
    }

    // --- Global names ---

    public void AddGlobalName(string name) => _globalNames.Add(name);
    public bool IsGlobalName(string name) => _globalNames.Contains(name);

    // --- Constants ---

    public void AddConstant(string name, long value)
    {
        CheckShadowing(name);
        _constants[name] = value;
    }

    private readonly Dictionary<string, long> _globalConstants = [];

    public void AddConstantNoShadowCheck(string name, long value)
    {
        _constants[name] = value;
        _globalConstants[name] = value;
    }

    // --- Variable types ---

    public void SetVarType(string name, string type)
    {
        _varTypes[name] = type;
        // If set before any scene (during global init), persist it
        if (_globals.ContainsKey(name))
            _globalVarTypes[name] = type;
    }

    // --- Locals direct access (for struct field remapping) ---

    public bool TryGetLocal(string name, out byte zp) => _locals.TryGetValue(name, out zp);
    public void SetLocal(string name, byte zp) => _locals[name] = zp;
    public void RemoveLocal(string name) => _locals.Remove(name);

    // --- Globals access ---

    public IEnumerable<KeyValuePair<string, byte>> Globals => _globals;
}
