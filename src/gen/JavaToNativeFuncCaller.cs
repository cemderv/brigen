using brigen.decl;

namespace brigen.gen;

/// <summary>
/// Handles calls from non-native Java methods to native Java methods.
/// </summary>
internal sealed class JavaToNativeFuncCaller(Writer writer, FunctionDecl function)
{
    private readonly FunctionDecl _function = function;
    private readonly Writer _writer = writer;

    public void GenerateArguments()
    {
        foreach (FunctionParamDecl param in _function.Parameters)
        {
            _writer.Write(param.Name);

            if (param != _function.Parameters[^1])
                _writer.Write(", ");
        }
    }
}