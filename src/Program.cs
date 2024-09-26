#if !DEBUG
#define WITH_TRY_CATCH
#endif

using System.Reflection;

namespace brigen;

internal static class Program
{
    private const string _helpOptionName = "help";

    public static void Main(string[] args)
    {
        //try
        //{
        Logger.Write += Console.Write;
        Logger.ColorRequested += color => Console.ForegroundColor = color;

        ProgramArguments programArgs = new(args);

        if (programArgs.Count == 0)
        {
            Logger.PushColor(ConsoleColor.Red);
            Logger.LogLine("No arguments specified.");
            Logger.PopColor();
            PrintGeneralHelpString();
            return;
        }

        if (programArgs.HasOption(_helpOptionName))
        {
            Option.PrintOptions([.. Option.AllOptions.Values]);
            return;
        }

        var settings = ParseSettings(programArgs);

        var compiler = new Compiler();
        compiler.Compile(settings);
        //}
        //catch (Exception ex)
        //{
        //    Logger.LogLine(ex.Message);
        //    Environment.ExitCode = 1;
        //}
    }

    private static void PrintGeneralHelpString()
    {
        Logger.LogLine($"{Library.GetDisplayName(true)}");
        Logger.LogLine($"Usage: brigen [options]");
        Logger.LogLine($"For help on a specific command, enter \"brigen -help\"");
    }

    public static Settings ParseSettings(ProgramArguments args)
    {
        var settings = new Settings
        {
            InputFilename = args.GetValue(Names.InFilename),
            OutputDirectory = args.TryGetValue(Names.OutDir, "out")
        };

        if (args.HasOption(Names.GenCSharp))
            settings.GenerateCSharpBindings = true;

        if (args.HasOption(Names.GenPython))
            settings.GeneratePythonBindings = true;

        if (args.HasOption(Names.GenJava))
            settings.GenerateJavaBindings = true;

        return settings;
    }
}