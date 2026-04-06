using System.Reflection;
using Cork.Language.Lexing;
using Cork.Language.Parsing;
using Cork.CodeGen;
using Cork.CodeGen.Emit;
using Cork.Output.Prg;

var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine($"Cork {version} — a programming language for the Commodore 64");
    Console.WriteLine();
    Console.WriteLine("Usage: cork <source.cork> [-o output.prg]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  -o <file>    Output file path (default: <source>.prg)");
    Console.WriteLine("  --no-debug   Don't write .cork-debug symbol file");
    Console.WriteLine("  --version    Print version");
    Console.WriteLine("  --help, -h   Print this help");
    return 0;
}

if (args[0] is "--version")
{
    Console.WriteLine(version);
    return 0;
}

var sourcePath = args[0];
if (!File.Exists(sourcePath))
{
    Console.Error.WriteLine($"Error: File not found: {sourcePath}");
    return 1;
}

var outputPath = Path.ChangeExtension(sourcePath, ".prg");
var emitDebug = true;
for (var i = 1; i < args.Length; i++)
{
    if (args[i] == "-o" && i + 1 < args.Length) outputPath = args[++i];
    if (args[i] == "--no-debug") emitDebug = false;
}

// Resolve stdlib search root: imports like "stdlib/screen.cork" resolve against this.
// Check CORK_STDLIB env var, then Homebrew layout (../share/corklang), then CWD.
string? stdlibPath = Environment.GetEnvironmentVariable("CORK_STDLIB");
if (stdlibPath == null)
{
    var binDir = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
    if (binDir != null)
    {
        var candidate = Path.Combine(binDir, "share", "corklang");
        if (Directory.Exists(Path.Combine(candidate, "stdlib")))
            stdlibPath = candidate;
    }
}
stdlibPath ??= Directory.GetCurrentDirectory();

try
{
    var source = File.ReadAllText(sourcePath);
    Console.WriteLine($"Cork compiler — {sourcePath}");

    // Lex
    var lexer = new Lexer(source, sourcePath);
    var tokens = lexer.Tokenize();
    Console.WriteLine($"  Lexed {tokens.Count} tokens");

    // Parse
    var parser = new Parser(tokens, Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? ".", stdlibPath);
    var program = parser.ParseProgram();
    Console.WriteLine($"  Parsed {program.Declarations.Count} top-level declarations");

    // Generate 6510 code
    var codeStart = PrgWriter.CalculateCodeStart();
    var codeGen = new CodeGenerator(codeStart);
    var (machineCode, _, peepholeRemovals) = codeGen.Generate(program);

    // Write PRG
    PrgWriter.WriteToFile(outputPath, machineCode);
    var fileSize = new FileInfo(outputPath).Length;

    // Write debug symbols
    if (emitDebug && codeGen.LastDebugInfo != null)
    {
        var debugPath = Path.ChangeExtension(outputPath, ".cork-debug");
        File.WriteAllText(debugPath, codeGen.LastDebugInfo.ToJson());
    }

    // Memory usage report
    var codeEnd = (ushort)(codeStart + machineCode.Length);
    var availableRam = 0x9FFF - codeStart; // $080E to $9FFF (BASIC ROM banked out)
    var usedPercent = machineCode.Length * 100 / availableRam;
    Console.WriteLine($"  Code:   ${codeStart:X4}-${codeEnd:X4} ({machineCode.Length} bytes)");
    Console.WriteLine($"  Output: {outputPath} ({fileSize} bytes)");
    Console.WriteLine($"  RAM:    {machineCode.Length}/{availableRam} bytes used ({usedPercent}%)");
    if (peepholeRemovals > 0)
        Console.WriteLine($"  Peephole: {peepholeRemovals} bytes removed");

    return 0;
}
catch (AggregateCompileError ex)
{
    foreach (var error in ex.Errors)
        Console.Error.WriteLine($"Error: {error.Message}");
    Console.Error.WriteLine($"\n{ex.Errors.Count} error(s)");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
