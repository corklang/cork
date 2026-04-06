namespace Cork.CodeGen.Emit;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Captures debug symbol information during code generation.
/// Serialized alongside the .prg as a .cork-debug file.
/// </summary>
public sealed class DebugInfo
{
    public List<StatementMap> Statements { get; } = [];
    public List<VariableInfo> Variables { get; } = [];
    public List<ScopeInfo> Scenes { get; } = [];
    public List<ScopeInfo> Methods { get; } = [];

    public void AddStatement(string file, int line, int column, ushort address)
    {
        Statements.Add(new StatementMap(file, line, column, address));
    }

    public void AddVariable(string name, string type, byte zpAddress, int size, string scope)
    {
        Variables.Add(new VariableInfo(name, type, zpAddress, size, scope));
    }

    public void OpenScope(List<ScopeInfo> list, string name, ushort startAddress)
    {
        list.Add(new ScopeInfo(name, startAddress, startAddress));
    }

    public void CloseScope(List<ScopeInfo> list, string name, ushort endAddress)
    {
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].Name == name)
            {
                list[i] = list[i] with { EndAddress = endAddress };
                return;
            }
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, DebugInfoJsonContext.Default.DebugInfo);
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(DebugInfo))]
internal partial class DebugInfoJsonContext : JsonSerializerContext;

public record struct StatementMap(
    string File,
    int Line,
    int Column,
    ushort Address
);

public record struct VariableInfo(
    string Name,
    string Type,
    byte ZpAddress,
    int Size,
    string Scope
);

public record struct ScopeInfo(
    string Name,
    ushort StartAddress,
    ushort EndAddress
);
