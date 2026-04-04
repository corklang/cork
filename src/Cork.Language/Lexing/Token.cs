namespace Cork.Language.Lexing;

public readonly record struct Token(
    TokenKind Kind,
    string Text,
    SourceLocation Location,
    object? LiteralValue = null
)
{
    public override string ToString() => $"{Kind} '{Text}' at {Location}";
}
