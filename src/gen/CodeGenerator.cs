namespace brigen.gen;

/// <summary>
/// Represents the base class of all code generators.
/// </summary>
public abstract class CodeGenerator(Module module)
{
    protected ClangFormatFormatter ClangFormatFormatter { get; } = new ClangFormatFormatter(module.ClangFormatLocation);

    protected Module Module { get; } = module;

    public abstract void Generate();
}