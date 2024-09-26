namespace brigen.gen;

/// <summary>
/// Represents the base class of all code generators.
/// </summary>
public abstract class CodeGenerator
{
    protected CodeGenerator(Module module)
    {
        Module = module;
        ClangFormatFormatter = new ClangFormatFormatter(module.ClangFormatLocation);
    }

    protected ClangFormatFormatter ClangFormatFormatter { get; }

    protected Module Module { get; }

    public abstract void Generate();
}