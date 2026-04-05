using Cork.Language.Lexing;
using Cork.Language.Parsing;
using Cork.CodeGen.Emit;
using Cork.Output.Prg;

namespace Cork.Integration.Tests;

public class SampleSnapshotTests
{
    [Test]
    [Arguments("hello")]
    [Arguments("joystick")]
    [Arguments("scenes")]
    [Arguments("structs")]
    [Arguments("combined")]
    [Arguments("raster")]
    [Arguments("sprite")]
    [Arguments("word")]
    [Arguments("forloop")]
    [Arguments("enumswitch")]
    [Arguments("signed")]
    [Arguments("fixedpoint")]
    [Arguments("gravity")]
    public async Task Sample_produces_expected_output(string name)
    {
        var sourcePath = Path.Combine(FindSamplesDir(), $"{name}.cork");
        var source = File.ReadAllText(sourcePath);

        // Compile using the pipeline
        var lexer = new Lexer(source, sourcePath);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.ParseProgram();
        var codeStart = PrgWriter.CalculateCodeStart();
        var codeGen = new CodeGenerator(codeStart);
        var (machineCode, _, _) = codeGen.Generate(program);
        var prg = PrgWriter.Create(machineCode);

        // Compare against baseline
        var baselinePath = Path.Combine(FindBaselinesDir(), $"{name}.prg");
        if (!File.Exists(baselinePath))
        {
            File.WriteAllBytes(baselinePath, prg);
            Assert.Fail($"Baseline created for '{name}'. Run tests again to verify.");
        }

        var baseline = File.ReadAllBytes(baselinePath);

        await Assert.That(prg.Length).IsEqualTo(baseline.Length)
            .Because($"Sample '{name}' output size changed: expected {baseline.Length} bytes but got {prg.Length} bytes");

        await Assert.That(prg).IsEquivalentTo(baseline)
            .Because($"Sample '{name}' output bytes differ from baseline");
    }

    private static string FindSamplesDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "samples");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException(
            "Could not find 'samples' directory by walking up from " + AppContext.BaseDirectory);
    }

    private static string FindBaselinesDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "tests", "Cork.Integration.Tests", "Baselines");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException(
            "Could not find 'Baselines' directory by walking up from " + AppContext.BaseDirectory);
    }
}
