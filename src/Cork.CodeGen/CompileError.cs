namespace Cork.CodeGen;

using Cork.Language.Lexing;

public sealed class CompileError(string message, SourceLocation? location = null)
    : InvalidOperationException(location.HasValue ? $"{location.Value}: {message}" : message);

public sealed class AggregateCompileError(List<CompileError> errors)
    : InvalidOperationException(string.Join(Environment.NewLine, errors.Select(e => e.Message)))
{
    public List<CompileError> Errors => errors;
}
