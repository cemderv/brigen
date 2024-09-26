using brigen.decl;

namespace brigen.gen;

/// <summary>
/// Handles calls from non-native Java methods to native Java methods.
/// </summary>
internal sealed class JavaToNativeFuncCaller
{
    private readonly FunctionDecl _function;
    private readonly Writer _writer;

    public JavaToNativeFuncCaller(Writer writer, FunctionDecl function)
    {
        _writer = writer;
        _function = function;
    }

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