using Cork.Language.Lexing;
using Cork.Language.Parsing;
using Cork.CodeGen.Emit;
using Cork.Output.Prg;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: cork <source.cork> [-o output.prg]");
    return 1;
}

var sourcePath = args[0];
if (!File.Exists(sourcePath))
{
    Console.Error.WriteLine($"Error: File not found: {sourcePath}");
    return 1;
}

var outputPath = Path.ChangeExtension(sourcePath, ".prg");
for (var i = 1; i < args.Length - 1; i++)
{
    if (args[i] == "-o") outputPath = args[i + 1];
}

try
{
    var source = File.ReadAllText(sourcePath);
    Console.WriteLine($"Cork compiler — {sourcePath}");

    // Lex
    var lexer = new Lexer(source, sourcePath);
    var tokens = lexer.Tokenize();
    Console.WriteLine($"  Lexed {tokens.Count} tokens");

    // Parse
    var parser = new Parser(tokens, Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? ".");
    var program = parser.ParseProgram();
    Console.WriteLine($"  Parsed {program.Declarations.Count} top-level declarations");

    // Generate 6510 code
    var codeStart = PrgWriter.CalculateCodeStart();
    var codeGen = new Phase1CodeGen(codeStart);
    var (machineCode, _) = codeGen.Generate(program);
    Console.WriteLine($"  Generated {machineCode.Length} bytes at ${codeStart:X4}");

    // Write PRG
    PrgWriter.WriteToFile(outputPath, machineCode);
    var fileSize = new FileInfo(outputPath).Length;
    Console.WriteLine($"  Output: {outputPath} ({fileSize} bytes)");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
