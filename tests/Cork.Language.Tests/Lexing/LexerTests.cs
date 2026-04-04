using Cork.Language.Lexing;

namespace Cork.Language.Tests.Lexing;

public class LexerTests
{
    [Test]
    public async Task Tokenizes_simple_variable_declaration()
    {
        var lexer = new Lexer("byte x = 5;");
        var tokens = lexer.Tokenize();

        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.ByteKw);
        await Assert.That(tokens[1].Kind).IsEqualTo(TokenKind.Identifier);
        await Assert.That(tokens[1].Text).IsEqualTo("x");
        await Assert.That(tokens[2].Kind).IsEqualTo(TokenKind.Equal);
        await Assert.That(tokens[3].Kind).IsEqualTo(TokenKind.IntegerLiteral);
        await Assert.That(tokens[3].LiteralValue).IsEqualTo(5L);
        await Assert.That(tokens[4].Kind).IsEqualTo(TokenKind.Semicolon);
        await Assert.That(tokens[5].Kind).IsEqualTo(TokenKind.Eof);
    }

    [Test]
    public async Task Tokenizes_message_send_with_colon()
    {
        var lexer = new Lexer("enemy update:;");
        var tokens = lexer.Tokenize();

        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.Identifier);
        await Assert.That(tokens[0].Text).IsEqualTo("enemy");
        await Assert.That(tokens[1].Kind).IsEqualTo(TokenKind.Identifier);
        await Assert.That(tokens[1].Text).IsEqualTo("update");
        await Assert.That(tokens[2].Kind).IsEqualTo(TokenKind.Colon);
        await Assert.That(tokens[3].Kind).IsEqualTo(TokenKind.Semicolon);
    }

    [Test]
    public async Task Tokenizes_hex_literal()
    {
        var lexer = new Lexer("0xFF_00");
        var tokens = lexer.Tokenize();

        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.IntegerLiteral);
        await Assert.That(tokens[0].LiteralValue).IsEqualTo(0xFF00L);
    }

    [Test]
    public async Task Tokenizes_fixed_point_literal()
    {
        var lexer = new Lexer("1.5");
        var tokens = lexer.Tokenize();

        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.FixedLiteral);
        await Assert.That(tokens[0].LiteralValue).IsEqualTo(1.5);
    }

    [Test]
    public async Task Tokenizes_string_literal_with_escapes()
    {
        var lexer = new Lexer("\"HELLO\\nWORLD\"");
        var tokens = lexer.Tokenize();

        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.StringLiteral);
        await Assert.That(tokens[0].LiteralValue).IsEqualTo("HELLO\nWORLD");
    }

    [Test]
    public async Task Tokenizes_scene_declaration()
    {
        var lexer = new Lexer("entry scene Hello {");
        var tokens = lexer.Tokenize();

        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.EntryKw);
        await Assert.That(tokens[1].Kind).IsEqualTo(TokenKind.SceneKw);
        await Assert.That(tokens[2].Kind).IsEqualTo(TokenKind.Identifier);
        await Assert.That(tokens[2].Text).IsEqualTo("Hello");
        await Assert.That(tokens[3].Kind).IsEqualTo(TokenKind.OpenBrace);
    }

    [Test]
    public async Task Skips_single_line_comment()
    {
        var lexer = new Lexer("byte x; // comment\nbyte y;");
        var tokens = lexer.Tokenize();

        var kinds = tokens.Select(t => t.Kind).ToList();
        await Assert.That(kinds).IsEquivalentTo([
            TokenKind.ByteKw, TokenKind.Identifier, TokenKind.Semicolon,
            TokenKind.ByteKw, TokenKind.Identifier, TokenKind.Semicolon,
            TokenKind.Eof
        ]);
    }

    [Test]
    public async Task Skips_block_comment()
    {
        var lexer = new Lexer("byte /* skip this */ x;");
        var tokens = lexer.Tokenize();

        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.ByteKw);
        await Assert.That(tokens[1].Kind).IsEqualTo(TokenKind.Identifier);
        await Assert.That(tokens[1].Text).IsEqualTo("x");
    }

    [Test]
    public async Task Tokenizes_multi_segment_message()
    {
        var lexer = new Lexer("enemy moveTo: 100 y: 50;");
        var tokens = lexer.Tokenize();

        var kinds = tokens.Select(t => t.Kind).ToList();
        await Assert.That(kinds).IsEquivalentTo([
            TokenKind.Identifier,      // enemy
            TokenKind.Identifier,      // moveTo
            TokenKind.Colon,           // :
            TokenKind.IntegerLiteral,  // 100
            TokenKind.Identifier,      // y
            TokenKind.Colon,           // :
            TokenKind.IntegerLiteral,  // 50
            TokenKind.Semicolon,       // ;
            TokenKind.Eof
        ]);
    }

    [Test]
    public async Task Tokenizes_all_comparison_operators()
    {
        var lexer = new Lexer("== != < > <= >=");
        var tokens = lexer.Tokenize();

        var kinds = tokens.Select(t => t.Kind).ToList();
        await Assert.That(kinds).IsEquivalentTo([
            TokenKind.EqualEqual, TokenKind.BangEqual,
            TokenKind.Less, TokenKind.Greater,
            TokenKind.LessEqual, TokenKind.GreaterEqual,
            TokenKind.Eof
        ]);
    }

    [Test]
    public async Task Tokenizes_underscore_separated_integer()
    {
        var lexer = new Lexer("10_000");
        var tokens = lexer.Tokenize();

        await Assert.That(tokens[0].Kind).IsEqualTo(TokenKind.IntegerLiteral);
        await Assert.That(tokens[0].LiteralValue).IsEqualTo(10_000L);
    }

    [Test]
    public async Task Tokenizes_hello_sample()
    {
        var source = File.ReadAllText("../../../../../samples/hello.cork");
        var lexer = new Lexer(source, "hello.cork");
        var tokens = lexer.Tokenize();

        // Should not throw and should end with Eof
        await Assert.That(tokens[^1].Kind).IsEqualTo(TokenKind.Eof);
        await Assert.That(tokens.Count).IsGreaterThan(20);
    }
}
