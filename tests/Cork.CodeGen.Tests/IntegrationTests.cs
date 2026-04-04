using Cork.CodeGen.Emit;
using Cork.Language.Lexing;
using Cork.Language.Parsing;
using Cork.Output.Prg;

namespace Cork.CodeGen.Tests;

public class IntegrationTests
{
    [Test]
    public async Task Minimal_program_produces_valid_prg()
    {
        var source = @"
            entry scene Main {
                hardware {
                    border: 0;
                }
                frame { }
            }
        ";

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.ParseProgram();

        var codeStart = PrgWriter.CalculateCodeStart();
        var codeGen = new Phase1CodeGen(codeStart);
        var (machineCode, entryPoint, _) = codeGen.Generate(program);
        var prg = PrgWriter.Create(machineCode);

        // PRG header: 2-byte load address (little-endian $0801)
        await Assert.That(prg[0]).IsEqualTo((byte)0x01);
        await Assert.That(prg[1]).IsEqualTo((byte)0x08);

        // Machine code should contain SEI (0x78) as the first generated instruction
        // The BASIC stub ends and machine code begins
        var codeOffset = codeStart - 0x0801 + 2; // +2 for load address header
        await Assert.That(prg[codeOffset]).IsEqualTo((byte)0x78); // SEI

        // Should be non-trivial
        await Assert.That(prg.Length).IsGreaterThan(20);
    }

    [Test]
    public async Task CodeGenerator_produces_same_output_as_Phase1CodeGen()
    {
        var source = @"
            entry scene Main {
                hardware {
                    border: 0;
                    background: 6;
                }
                byte x = 0;
                frame {
                    x += 1;
                    poke: 53280 value: x;
                }
            }
        ";

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.ParseProgram();

        var codeStart = PrgWriter.CalculateCodeStart();

        var phase1 = new Phase1CodeGen(codeStart);
        var (code1, entry1, _) = phase1.Generate(program);

        var codeGen = new CodeGenerator(codeStart);
        var (code2, entry2, _) = codeGen.Generate(program);

        await Assert.That(entry1).IsEqualTo(entry2);
        await Assert.That(code1.Length).IsEqualTo(code2.Length);

        for (var i = 0; i < code1.Length; i++)
        {
            await Assert.That(code2[i]).IsEqualTo(code1[i]);
        }
    }

    [Test]
    public async Task Compile_with_global_variable()
    {
        var source = @"
            byte score = 0;
            entry scene Main {
                frame {
                    score += 1;
                }
            }
        ";

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.ParseProgram();

        var codeStart = PrgWriter.CalculateCodeStart();
        var codeGen = new CodeGenerator(codeStart);
        var (machineCode, _, _) = codeGen.Generate(program);

        // Should produce valid machine code without errors
        await Assert.That(machineCode.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Compile_with_const_array_and_index()
    {
        var source = @"
            const byte[3] colors = { 0, 1, 2 };
            entry scene Main {
                byte i = 0;
                frame {
                    poke: 53280 value: colors[i];
                    i += 1;
                }
            }
        ";

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.ParseProgram();

        var codeStart = PrgWriter.CalculateCodeStart();
        var codeGen = new CodeGenerator(codeStart);
        var (machineCode, _, _) = codeGen.Generate(program);

        await Assert.That(machineCode.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Compile_with_if_else()
    {
        var source = @"
            entry scene Main {
                byte x = 0;
                frame {
                    if (x == 0) {
                        poke: 53280 value: 1;
                    } else {
                        poke: 53280 value: 2;
                    }
                    x += 1;
                }
            }
        ";

        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.ParseProgram();

        var codeStart = PrgWriter.CalculateCodeStart();
        var codeGen = new CodeGenerator(codeStart);
        var (machineCode, _, _) = codeGen.Generate(program);

        await Assert.That(machineCode.Length).IsGreaterThan(0);
    }
}
