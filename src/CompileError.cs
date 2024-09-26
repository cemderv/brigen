using brigen.Properties;

namespace brigen;

internal enum ErrorCategory
{
    General,
    Syntax,
    Internal,
    FileContents
}

internal enum CompileErrorId
{
}

internal sealed class CompileError : Exception
{
    public CompileError(
      string message, CodeRange? range = null,
      ErrorCategory category = ErrorCategory.General,
      CompileErrorId? id = null)
    {
        Message = message;
        Range = range;
        Category = category;
        Id = id;

        if (range != null)
        {
            var rangeValue = range.Value;
            var rangeString = $"{rangeValue.Filename}({rangeValue.Line},{rangeValue.StartColumn}-{rangeValue.EndColumn}): ";
            rangeString += "error: ";
            Message = Message.Insert(0, rangeString);
        }
    }

    public override string Message { get; }

    public CodeRange? Range { get; }

    public ErrorCategory Category { get; }

    public CompileErrorId? Id { get; }

    public static CompileError UndefinedSymbolUsed(Module module, string searchedSymbol, CodeRange range)
    {
        Decl? similarSymbol = module.FindSimilarlyNamedDecl(searchedSymbol);

        string msg = similarSymbol != null
          ? string.Format(Messages.UndefinedSymUsed_WithSuggestion, searchedSymbol, similarSymbol.Name)
          : string.Format(Messages.UndefinedSymUsed, searchedSymbol);

        return new CompileError(msg, range);
    }

    public static CompileError UnexpectedTopLevelToken(Token token)
      => new(string.Format(Messages.UnexpectedTopLevelTk, token.Value), token.Range, ErrorCategory.Syntax);

    public static CompileError UnexpectedEof(Token token)
      => new(Messages.UnexpectedEof, token.Range, ErrorCategory.Syntax);

    public static CompileError Internal(string message, CodeRange? range = null)
      => new(string.Format(Messages.InternalCompileError, message), range, ErrorCategory.Internal);
}