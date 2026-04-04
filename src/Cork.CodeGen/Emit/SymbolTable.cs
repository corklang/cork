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
    private readonly Dictionary<string, long> _constants = [];
    private readonly Dictionary<string, Dictionary<string, long>> _enumTypes = [];
    private readonly Dictionary<string, StructDeclNode> _structTypes = [];
    private readonly Dictionary<string, (string StructType, Dictionary<string, byte> Fields)> _structInstances = [];
    private readonly HashSet<string> _emittedStructMethods = [];
    private readonly Dictionary<string, List<MethodParameter>> _methodParams = [];

    private byte _nextZp = 0x02;
    private byte _globalZpEnd = 0x02;

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

    public void AllocGlobal(string name)
    {
        if (!_globals.ContainsKey(name))
        {
            var zp = _globalZpEnd++;
            _globals[name] = zp;
            _locals[name] = zp;
            _globalNames.Add(name);
        }
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
        _locals.Clear();
        _varTypes.Clear();
        _constants.Clear();
        _structInstances.Clear();
        foreach (var (name, zp) in _globals)
            _locals[name] = zp;
        _nextZp = _globalZpEnd;
    }

    public void ResetForScene()
    {
        _locals.Clear();
        _varTypes.Clear();
        _constants.Clear();
        _structInstances.Clear();
        _emittedStructMethods.Clear();
        foreach (var (name, zp) in _globals)
            _locals[name] = zp;
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

    // --- Global names ---

    public void AddGlobalName(string name) => _globalNames.Add(name);
    public bool IsGlobalName(string name) => _globalNames.Contains(name);

    // --- Constants ---

    public void AddConstant(string name, long value)
    {
        CheckShadowing(name);
        _constants[name] = value;
    }

    // --- Variable types ---

    public void SetVarType(string name, string type) => _varTypes[name] = type;

    // --- Locals direct access (for struct field remapping) ---

    public bool TryGetLocal(string name, out byte zp) => _locals.TryGetValue(name, out zp);
    public void SetLocal(string name, byte zp) => _locals[name] = zp;
    public void RemoveLocal(string name) => _locals.Remove(name);

    // --- Globals access ---

    public IEnumerable<KeyValuePair<string, byte>> Globals => _globals;
}
