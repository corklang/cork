namespace Cork.CodeGen;

using Cork.Language.Lexing;

public sealed class CompileError(string message, SourceLocation? location = null)
    : InvalidOperationException(location.HasValue ? $"{location.Value}: {message}" : message);
