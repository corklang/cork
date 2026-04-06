namespace Cork.Language.Lexing;

public readonly record struct SourceLocation(
    string FilePath,
    int Line,
    int Column,
    int Offset,
    int Length
)
{
    public override string ToString() =>
        Length > 1
            ? $"{FilePath}({Line},{Column}-{Column + Length - 1})"
            : $"{FilePath}({Line},{Column})";
}
