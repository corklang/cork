namespace Cork.Language.Lexing;

public sealed class Lexer(string source, string filePath = "<stdin>")
{
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    private static readonly Dictionary<string, TokenKind> Keywords = new()
    {
        ["byte"] = TokenKind.ByteKw,
        ["sbyte"] = TokenKind.SbyteKw,
        ["word"] = TokenKind.WordKw,
        ["sword"] = TokenKind.SwordKw,
        ["bool"] = TokenKind.BoolKw,
        ["fixed"] = TokenKind.FixedKw,
        ["sfixed"] = TokenKind.SfixedKw,
        ["string"] = TokenKind.StringKw,
        ["var"] = TokenKind.VarKw,
        ["sprite"] = TokenKind.SpriteKw,
        ["charset"] = TokenKind.CharsetKw,
        ["music"] = TokenKind.MusicKw,
        ["sound"] = TokenKind.SoundKw,
        ["tilemap"] = TokenKind.TilemapKw,
        ["struct"] = TokenKind.StructKw,
        ["enum"] = TokenKind.EnumKw,
        ["flags"] = TokenKind.FlagsKw,
        ["scene"] = TokenKind.SceneKw,
        ["entry"] = TokenKind.EntryKw,
        ["const"] = TokenKind.ConstKw,
        ["import"] = TokenKind.ImportKw,
        ["if"] = TokenKind.IfKw,
        ["else"] = TokenKind.ElseKw,
        ["while"] = TokenKind.WhileKw,
        ["for"] = TokenKind.ForKw,
        ["in"] = TokenKind.InKw,
        ["switch"] = TokenKind.SwitchKw,
        ["case"] = TokenKind.CaseKw,
        ["default"] = TokenKind.DefaultKw,
        ["fallthrough"] = TokenKind.FallthroughKw,
        ["break"] = TokenKind.BreakKw,
        ["continue"] = TokenKind.ContinueKw,
        ["return"] = TokenKind.ReturnKw,
        ["go"] = TokenKind.GoKw,
        ["hardware"] = TokenKind.HardwareKw,
        ["enter"] = TokenKind.EnterKw,
        ["frame"] = TokenKind.FrameKw,
        ["relaxed"] = TokenKind.RelaxedKw,
        ["raster"] = TokenKind.RasterKw,
        ["exit"] = TokenKind.ExitKw,
        ["as"] = TokenKind.AsKw,
        ["true"] = TokenKind.TrueLiteral,
        ["false"] = TokenKind.FalseLiteral,
    };

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_pos < source.Length)
        {
            SkipWhitespaceAndComments();
            if (_pos >= source.Length) break;

            var token = ReadToken();
            tokens.Add(token);
        }

        tokens.Add(MakeToken(TokenKind.Eof, "", _pos));
        return tokens;
    }

    private void SkipWhitespaceAndComments()
    {
        while (_pos < source.Length)
        {
            if (char.IsWhiteSpace(Current))
            {
                Advance();
            }
            else if (Current == '/' && Peek(1) == '/')
            {
                while (_pos < source.Length && Current != '\n')
                    Advance();
            }
            else if (Current == '/' && Peek(1) == '*')
            {
                Advance(); Advance(); // skip /*
                var depth = 1;
                while (_pos < source.Length && depth > 0)
                {
                    if (Current == '/' && Peek(1) == '*') { depth++; Advance(); }
                    else if (Current == '*' && Peek(1) == '/') { depth--; Advance(); }
                    Advance();
                }
            }
            else
            {
                break;
            }
        }
    }

    private Token ReadToken()
    {
        var start = _pos;
        var c = Current;

        // String literals
        if (c == '"') return ReadStringLiteral();

        // Numbers
        if (char.IsAsciiDigit(c)) return ReadNumberLiteral();

        // Identifiers and keywords
        if (IsIdentStart(c)) return ReadIdentifierOrKeyword();

        // Punctuation and operators
        return ReadPunctuationOrOperator();
    }

    private Token ReadStringLiteral()
    {
        var start = _pos;
        Advance(); // skip opening "
        var sb = new System.Text.StringBuilder();

        while (_pos < source.Length && Current != '"')
        {
            if (Current == '\\')
            {
                Advance();
                sb.Append(Current switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '\\' => '\\',
                    '"' => '"',
                    _ => throw Error($"Unknown escape sequence '\\{Current}'"),
                });
            }
            else
            {
                sb.Append(Current);
            }
            Advance();
        }

        if (_pos >= source.Length) throw Error("Unterminated string literal");
        Advance(); // skip closing "

        var text = source[start.._pos];
        return MakeToken(TokenKind.StringLiteral, text, start, sb.ToString());
    }

    private Token ReadNumberLiteral()
    {
        var start = _pos;

        // Hex: 0x...
        if (Current == '0' && Peek(1) is 'x' or 'X')
        {
            Advance(); Advance(); // skip 0x
            while (_pos < source.Length && (IsHexDigit(Current) || Current == '_'))
                Advance();

            var text = source[start.._pos];
            var cleanHex = text[2..].Replace("_", "");
            var value = Convert.ToInt64(cleanHex, 16);
            return MakeToken(TokenKind.IntegerLiteral, text, start, value);
        }

        // Decimal (possibly fixed-point)
        while (_pos < source.Length && (char.IsAsciiDigit(Current) || Current == '_'))
            Advance();

        // Check for fixed-point: digits followed by . followed by digits
        if (_pos < source.Length && Current == '.' && _pos + 1 < source.Length && char.IsAsciiDigit(Peek(1)))
        {
            Advance(); // skip .
            while (_pos < source.Length && (char.IsAsciiDigit(Current) || Current == '_'))
                Advance();

            var text = source[start.._pos];
            var cleanFixed = text.Replace("_", "");
            var value = double.Parse(cleanFixed, System.Globalization.CultureInfo.InvariantCulture);
            return MakeToken(TokenKind.FixedLiteral, text, start, value);
        }

        {
            var text = source[start.._pos];
            var cleanInt = text.Replace("_", "");
            var value = long.Parse(cleanInt);
            return MakeToken(TokenKind.IntegerLiteral, text, start, value);
        }
    }

    private Token ReadIdentifierOrKeyword()
    {
        var start = _pos;
        while (_pos < source.Length && IsIdentPart(Current))
            Advance();

        var text = source[start.._pos];
        var kind = Keywords.GetValueOrDefault(text, TokenKind.Identifier);
        object? literal = kind switch
        {
            TokenKind.TrueLiteral => true,
            TokenKind.FalseLiteral => false,
            _ => null,
        };
        return MakeToken(kind, text, start, literal);
    }

    private Token ReadPunctuationOrOperator()
    {
        var start = _pos;
        var c = Current;
        Advance();

        switch (c)
        {
            case ';': return MakeToken(TokenKind.Semicolon, ";", start);
            case ',': return MakeToken(TokenKind.Comma, ",", start);
            case '.': return MakeToken(TokenKind.Dot, ".", start);
            case ':': return MakeToken(TokenKind.Colon, ":", start);
            case '(': return MakeToken(TokenKind.OpenParen, "(", start);
            case ')': return MakeToken(TokenKind.CloseParen, ")", start);
            case '{': return MakeToken(TokenKind.OpenBrace, "{", start);
            case '}': return MakeToken(TokenKind.CloseBrace, "}", start);
            case '[': return MakeToken(TokenKind.OpenBracket, "[", start);
            case ']': return MakeToken(TokenKind.CloseBracket, "]", start);
            case '~': return MakeToken(TokenKind.Tilde, "~", start);
            case '^':
                if (TryConsume('=')) return MakeToken(TokenKind.CaretEqual, "^=", start);
                return MakeToken(TokenKind.Caret, "^", start);
            case '%':
                if (TryConsume('=')) return MakeToken(TokenKind.PercentEqual, "%=", start);
                return MakeToken(TokenKind.Percent, "%", start);
            case '*':
                if (TryConsume('=')) return MakeToken(TokenKind.StarEqual, "*=", start);
                return MakeToken(TokenKind.Star, "*", start);
            case '/':
                if (TryConsume('=')) return MakeToken(TokenKind.SlashEqual, "/=", start);
                return MakeToken(TokenKind.Slash, "/", start);
            case '+':
                if (TryConsume('=')) return MakeToken(TokenKind.PlusEqual, "+=", start);
                return MakeToken(TokenKind.Plus, "+", start);
            case '-':
                if (TryConsume('=')) return MakeToken(TokenKind.MinusEqual, "-=", start);
                return MakeToken(TokenKind.Minus, "-", start);
            case '=':
                if (TryConsume('=')) return MakeToken(TokenKind.EqualEqual, "==", start);
                return MakeToken(TokenKind.Equal, "=", start);
            case '!':
                if (TryConsume('=')) return MakeToken(TokenKind.BangEqual, "!=", start);
                return MakeToken(TokenKind.Bang, "!", start);
            case '<':
                if (TryConsume('<'))
                {
                    if (TryConsume('=')) return MakeToken(TokenKind.ShiftLeftEqual, "<<=", start);
                    return MakeToken(TokenKind.ShiftLeft, "<<", start);
                }
                if (TryConsume('=')) return MakeToken(TokenKind.LessEqual, "<=", start);
                return MakeToken(TokenKind.Less, "<", start);
            case '>':
                if (TryConsume('>'))
                {
                    if (TryConsume('=')) return MakeToken(TokenKind.ShiftRightEqual, ">>=", start);
                    return MakeToken(TokenKind.ShiftRight, ">>", start);
                }
                if (TryConsume('=')) return MakeToken(TokenKind.GreaterEqual, ">=", start);
                return MakeToken(TokenKind.Greater, ">", start);
            case '&':
                if (TryConsume('&')) return MakeToken(TokenKind.AmpAmp, "&&", start);
                if (TryConsume('=')) return MakeToken(TokenKind.AmpEqual, "&=", start);
                return MakeToken(TokenKind.Ampersand, "&", start);
            case '|':
                if (TryConsume('|')) return MakeToken(TokenKind.PipePipe, "||", start);
                if (TryConsume('=')) return MakeToken(TokenKind.PipeEqual, "|=", start);
                return MakeToken(TokenKind.Pipe, "|", start);
            default:
                throw Error($"Unexpected character '{c}'");
        }
    }

    private char Current => _pos < source.Length ? source[_pos] : '\0';

    private char Peek(int offset)
    {
        var idx = _pos + offset;
        return idx < source.Length ? source[idx] : '\0';
    }

    private void Advance()
    {
        if (_pos < source.Length)
        {
            if (source[_pos] == '\n') { _line++; _col = 1; }
            else { _col++; }
            _pos++;
        }
    }

    private bool TryConsume(char expected)
    {
        if (_pos < source.Length && source[_pos] == expected)
        {
            Advance();
            return true;
        }
        return false;
    }

    private Token MakeToken(TokenKind kind, string text, int startOffset, object? literal = null)
    {
        var startLine = _line;
        var startCol = _col - text.Length;
        if (startCol < 1) startCol = 1;
        var loc = new SourceLocation(filePath, startLine, startCol, startOffset, text.Length);
        return new Token(kind, text, loc, literal);
    }

    private static bool IsIdentStart(char c) => char.IsAsciiLetter(c) || c == '_';
    private static bool IsIdentPart(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';
    private static bool IsHexDigit(char c) => char.IsAsciiHexDigit(c);

    private Exception Error(string message) =>
        new InvalidOperationException($"{filePath}({_line},{_col}): {message}");
}
