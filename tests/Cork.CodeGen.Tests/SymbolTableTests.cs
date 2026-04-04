using Cork.CodeGen.Emit;
using Cork.Language.Ast;
using Cork.Language.Lexing;

namespace Cork.CodeGen.Tests;

public class SymbolTableTests
{
    [Test]
    public async Task AllocZeroPage_starts_at_0x02()
    {
        var symbols = new SymbolTable();
        var addr = symbols.AllocZeroPage("x");
        await Assert.That(addr).IsEqualTo((byte)0x02);
    }

    [Test]
    public async Task AllocZeroPage_increments_sequentially()
    {
        var symbols = new SymbolTable();
        var a1 = symbols.AllocZeroPage("a");
        var a2 = symbols.AllocZeroPage("b");
        var a3 = symbols.AllocZeroPage("c");
        await Assert.That(a1).IsEqualTo((byte)0x02);
        await Assert.That(a2).IsEqualTo((byte)0x03);
        await Assert.That(a3).IsEqualTo((byte)0x04);
    }

    [Test]
    public async Task AllocZeroPage_returns_existing_for_same_name()
    {
        var symbols = new SymbolTable();
        var a1 = symbols.AllocZeroPage("x");
        var a2 = symbols.AllocZeroPage("x");
        await Assert.That(a1).IsEqualTo(a2);
    }

    [Test]
    public async Task AllocWordZeroPage_takes_two_bytes()
    {
        var symbols = new SymbolTable();
        var w1 = symbols.AllocWordZeroPage("pos");
        var w2 = symbols.AllocZeroPage("next");
        // Word takes 2 bytes, so next allocation should be w1+2
        await Assert.That(w2).IsEqualTo((byte)(w1 + 2));
    }

    [Test]
    public async Task IsWordVar_true_for_word_allocated()
    {
        var symbols = new SymbolTable();
        symbols.AllocWordZeroPage("pos");
        await Assert.That(symbols.IsWordVar("pos")).IsTrue();
    }

    [Test]
    public async Task IsWordVar_false_for_byte_allocated()
    {
        var symbols = new SymbolTable();
        symbols.AllocZeroPage("x");
        await Assert.That(symbols.IsWordVar("x")).IsFalse();
    }

    [Test]
    public async Task CheckShadowing_throws_when_name_shadows_global()
    {
        var symbols = new SymbolTable();
        symbols.AllocGlobal("score");
        symbols.ResetForScene();

        var action = () => symbols.AddConstant("score", 42);
        await Assert.That(action).ThrowsException();
    }

    [Test]
    public async Task CheckShadowing_skips_internal_names()
    {
        var symbols = new SymbolTable();
        symbols.AllocGlobal("_internal");
        symbols.ResetForScene();

        // Should not throw since _internal starts with _
        var addr = symbols.AllocZeroPage("_internal");
        await Assert.That(addr).IsGreaterThanOrEqualTo((byte)0x02);
    }

    [Test]
    public async Task ResetForScene_preserves_globals()
    {
        var symbols = new SymbolTable();
        symbols.AllocGlobal("score");
        symbols.AllocZeroPage("localVar");
        symbols.ResetForScene();

        // Global should still be accessible
        var scoreZp = symbols.GetLocal("score");
        await Assert.That(scoreZp).IsEqualTo((byte)0x02);

        // Local should be gone
        var found = symbols.TryGetLocal("localVar", out _);
        await Assert.That(found).IsFalse();
    }

    [Test]
    public async Task ResetToGlobalScope_clears_locals_and_constants()
    {
        var symbols = new SymbolTable();
        symbols.AllocGlobal("g1");
        symbols.AllocZeroPage("local1");
        symbols.AddConstant("C1", 10);

        symbols.ResetToGlobalScope();

        var hasConst = symbols.TryGetConstant("C1", out _);
        await Assert.That(hasConst).IsFalse();

        var hasLocal = symbols.TryGetLocal("local1", out _);
        await Assert.That(hasLocal).IsFalse();

        var hasGlobal = symbols.TryGetLocal("g1", out _);
        await Assert.That(hasGlobal).IsTrue();
    }

    [Test]
    public async Task RegisterEnumType_and_lookup()
    {
        var symbols = new SymbolTable();
        var loc = new SourceLocation("<test>", 1, 1, 0, 0);
        var enumDecl = new EnumDeclNode("Color", "byte", false, [
            new EnumMemberNode("Red", 2, loc),
            new EnumMemberNode("Blue", 6, loc)
        ], loc);
        symbols.RegisterEnumType(enumDecl);

        var found = symbols.TryGetEnumMember("Color", "Red", out var value);
        await Assert.That(found).IsTrue();
        await Assert.That(value).IsEqualTo(2L);
    }

    [Test]
    public async Task Is16BitType_identifies_correctly()
    {
        await Assert.That(SymbolTable.Is16BitType("word")).IsTrue();
        await Assert.That(SymbolTable.Is16BitType("sword")).IsTrue();
        await Assert.That(SymbolTable.Is16BitType("fixed")).IsTrue();
        await Assert.That(SymbolTable.Is16BitType("sfixed")).IsTrue();
        await Assert.That(SymbolTable.Is16BitType("byte")).IsFalse();
        await Assert.That(SymbolTable.Is16BitType("sbyte")).IsFalse();
    }

    [Test]
    public async Task IsSignedType_identifies_correctly()
    {
        await Assert.That(SymbolTable.IsSignedType("sbyte")).IsTrue();
        await Assert.That(SymbolTable.IsSignedType("sword")).IsTrue();
        await Assert.That(SymbolTable.IsSignedType("sfixed")).IsTrue();
        await Assert.That(SymbolTable.IsSignedType("byte")).IsFalse();
        await Assert.That(SymbolTable.IsSignedType("word")).IsFalse();
    }
}
