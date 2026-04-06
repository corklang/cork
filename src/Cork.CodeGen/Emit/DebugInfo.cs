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

    // Deferred address records — marker IDs resolved after ResolveFixups
    private readonly List<(string File, int Line, int Column, int MarkerId)> _deferredStatements = [];
    private readonly List<(List<ScopeInfo> List, string Name, int MarkerId, bool IsClose)> _deferredScopes = [];

    public void AddStatement(string file, int line, int column, int markerId)
    {
        _deferredStatements.Add((file, line, column, markerId));
    }

    public void AddVariable(string name, string type, byte zpAddress, int size, string scope)
    {
        Variables.Add(new VariableInfo(name, type, zpAddress, size, scope));
    }

    public void OpenScope(List<ScopeInfo> list, string name, int markerId)
    {
        _deferredScopes.Add((list, name, markerId, false));
    }

    public void CloseScope(List<ScopeInfo> list, string name, int markerId)
    {
        _deferredScopes.Add((list, name, markerId, true));
    }

    /// <summary>
    /// Resolve all deferred marker IDs to final byte addresses.
    /// Must be called after InstructionBuffer.ResolveFixups().
    /// </summary>
    public void ResolveAddresses(InstructionBuffer buffer)
    {
        foreach (var (file, line, column, markerId) in _deferredStatements)
            Statements.Add(new StatementMap(file, line, column, buffer.ResolveMarkerAddress(markerId)));

        foreach (var (list, name, markerId, isClose) in _deferredScopes)
        {
            var addr = buffer.ResolveMarkerAddress(markerId);
            if (isClose)
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].Name == name)
                    {
                        list[i] = list[i] with { EndAddress = addr };
                        break;
                    }
                }
            }
            else
            {
                list.Add(new ScopeInfo(name, addr, addr));
            }
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, DebugInfoJsonContext.Default.DebugInfo);

    /// <summary>
    /// Generate a VICE monitor commands file (.mon) with labels for
    /// scenes, methods, variables, and source line addresses.
    /// Load with: x64sc -moncommands file.mon -autostart file.prg
    /// </summary>
    public string ToViceMonCommands()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("; Cork debug symbols");
        sb.AppendLine();

        foreach (var s in Scenes)
            sb.AppendLine($"al C:{s.StartAddress:x4} .scene_{s.Name}");
        foreach (var m in Methods)
            sb.AppendLine($"al C:{m.StartAddress:x4} .{Sanitize(m.Name)}");

        sb.AppendLine();

        var seen = new HashSet<byte>();
        foreach (var v in Variables)
        {
            if (v.Name.StartsWith('_')) continue;
            if (!seen.Add(v.ZpAddress)) continue;
            sb.AppendLine($"al C:{v.ZpAddress:x4} .{Sanitize(v.Name)}");
            if (v.Size == 2)
                sb.AppendLine($"al C:{v.ZpAddress + 1:x4} .{Sanitize(v.Name)}_hi");
        }

        sb.AppendLine();

        var seenLines = new HashSet<(string, int)>();
        foreach (var s in Statements)
        {
            if (!seenLines.Add((s.File, s.Line))) continue;
            var file = Path.GetFileNameWithoutExtension(s.File);
            sb.AppendLine($"al C:{s.Address:x4} .{Sanitize(file)}_L{s.Line}");
        }

        return sb.ToString();
    }

    private static string Sanitize(string name) =>
        name.Replace(':', '_').Replace('.', '_').Replace('$', '_').Replace(' ', '_');
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
