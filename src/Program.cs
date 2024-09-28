#if !DEBUG
#define WITH_TRY_CATCH
#endif

using brigen.gen;

namespace brigen;

internal static class Program
{
    public static void Main(string[] args)
    {
#if WITH_TRY_CATCH
        try
        {
#endif
        Logger.Write += Console.Write;
        Logger.ColorRequested += color => Console.ForegroundColor = color;

        if (args.Length == 0)
        {
            Logger.LogLine("No arguments specified.", ConsoleColor.Red);
            PrintGeneralHelpString();
            return;
        }

        var settings = new Settings(args);

        if (settings.IsHelp)
        {
            Option.PrintOptions([.. Option.AllOptions.Values]);
            return;
        }


        Compile(settings);
#if WITH_TRY_CATCH
        }
        catch (Exception ex)
        {
            Logger.LogLine(ex.Message);
            Environment.ExitCode = 1;
        }
#endif
    }

    private static void PrintGeneralHelpString()
    {
        Logger.LogLine($@"{Library.GetDisplayName(true)}

Usage: brigen [options]
For help on a specific command, enter 'brigen {Option.SwitchPrefix}help'");
    }

    private static void Compile(Settings settings)
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

        if (module.Settings.GenerateCMake)
            new CMakeGenerator(module).Generate();

        Logger.LogLine("--------------------");
        Logger.LogLine("Compilation finished");
    }
}