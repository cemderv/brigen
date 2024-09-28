namespace brigen;

internal sealed class CompileError : Exception
{
    public CompileError(string message, CodeRange? range = null)
    {
        Message = message;
        Range = range;

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

    public static CompileError UndefinedSymbolUsed(Module module, string searchedSymbol, CodeRange range)
    {
        Decl? similarSymbol = module.FindSimilarlyNamedDecl(searchedSymbol);

        string msg = similarSymbol != null
          ? $"Undefined symbol '{searchedSymbol}' used; did you mean '{similarSymbol.Name}'?"
          : $"Undefined symbol '{searchedSymbol}' used";

        return new CompileError(msg, range);
    }

    public static CompileError UnexpectedTopLevelToken(Token token)
      => new($"Unexpected top-level token '{token.Value}' encountered.", token.Range);

    public static CompileError UnexpectedToken(Token token)
    => new($"Unexpected token '{token.Value}' encountered.", token.Range);

    public static CompileError UnexpectedEof(Token token)
      => new("Unexpected end-of-file encountered", token.Range);

    public static CompileError Internal(string message, CodeRange? range = null)
      => new($"An internal compiler error occurred: {message}", range);

    public static CompileError UnknownClassModifier(Token token)
    => new($"Unknown class modifier '{token.Value}' specified.", token.Range);
}