namespace brigen;

internal sealed class InvalidOptionError(string message) : Exception(message)
{
}