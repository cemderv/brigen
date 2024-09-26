using brigen.gen;

namespace brigen;

internal sealed class Compiler
{
    public void Compile(Settings settings)
    {
        var fileContents = File.ReadAllText(settings.InputFilename);

        var tokenizer = new Tokenizer();
        tokenizer.Tokenize(fileContents, settings.InputFilename);

        IReadOnlyList<Token> tokens = tokenizer.Tokens;

        var parser = new Parser();
        var decls = parser.Parse(tokens);

        var module = new Module(settings, decls);

        Logger.LogLine($"Generating module {module.Name}");

        // C++
        new CppCodeGenerator(module).Generate();

        // C
        new CCodeGenerator(module).Generate();

        // C#
        if (module.Settings.GenerateCSharpBindings)
            new CSharpCodeGenerator(module).Generate();

        // Python
        if (module.Settings.GeneratePythonBindings)
            new PythonCodeGenerator(module).Generate();

        // Java
        if (module.Settings.GenerateJavaBindings)
            new JavaCodeGenerator(module).Generate();

        Logger.LogLine("--------------------");
        Logger.LogLine("Compilation finished");
    }
}