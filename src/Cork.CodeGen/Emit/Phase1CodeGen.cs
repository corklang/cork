namespace Cork.CodeGen.Emit;

using Cork.Language.Ast;

/// <summary>
/// Backward-compatible wrapper around CodeGenerator.
/// Existing callers (e.g., Program.cs) can continue using Phase1CodeGen unchanged.
/// </summary>
public sealed class Phase1CodeGen(ushort codeBase = 0x0810)
{
    private readonly CodeGenerator _generator = new(codeBase);

    public (byte[] Code, ushort EntryPoint) Generate(ProgramNode program) =>
        _generator.Generate(program);
}
