namespace Cork.Language.Diagnostics;

using Cork.Language.Lexing;

public sealed class CompilerError(string message, SourceLocation? location = null) : Exception(
    location != null ? $"{location}: {message}" : message)
{
    public SourceLocation? Location { get; } = location;
}
