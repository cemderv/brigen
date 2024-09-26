namespace brigen;

internal sealed class InvalidOptionError : Exception
{
    public InvalidOptionError(string message)
      : base(message)
    {
    }
}