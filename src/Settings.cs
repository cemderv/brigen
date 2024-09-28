namespace brigen;

public sealed class Settings
{
    private const string _helpOptionName = "help";

    public readonly bool IsHelp = false;
    public readonly string InputFilename = string.Empty;
    public readonly string OutputDirectory = string.Empty;
    public readonly bool GenerateCSharpBindings;
    public readonly bool GeneratePythonBindings;
    public readonly bool GenerateJavaBindings;
    public readonly bool GenerateCMake;

    private static string MakeCleanString(string str)
        => str.Trim(' ', '\"').Replace('\\', '/');

    public Settings(string[] args)
    {
        var map = ParseOptions(args);

        IsHelp = map.ContainsKey("help");

        if (IsHelp)
        {
            return;
        }

        bool HasOption(string name) => map.ContainsKey(name);

        string GetValue(string key) =>
          !map.TryGetValue(key, out string? result)
            ? throw new InvalidOptionError($"Missing required option '{Option.SwitchPrefix}{key}'.")
            : MakeCleanString(result);

        string TryGetValue(string key, string defaultValue)
          => map.TryGetValue(key, out string? result) ? MakeCleanString(result) : defaultValue;

        InputFilename = GetValue(Names.InFilename);
        OutputDirectory = TryGetValue(Names.OutDir, "out");

        GenerateCSharpBindings = HasOption(Names.GenCSharp);
        GeneratePythonBindings = HasOption(Names.GenPython);
        GenerateJavaBindings = HasOption(Names.GenJava);
        GenerateCMake = !HasOption(Names.NoCMake);
    }

    private Dictionary<string, string> ParseOptions(string[] args)
    {
        Dictionary<string, string> map = [];

        foreach (string? arg in args)
        {
            int idxOfEqualSign = arg.IndexOf('=');
            if (idxOfEqualSign >= 0)
            {
                // It's a setting.
                string key = arg[..idxOfEqualSign].Trim();

                if (!key.StartsWith(Option.SwitchPrefix))
                {
                    throw new InvalidOptionError($"An option must start with '{Option.SwitchPrefix}'");
                }

                if (key == string.Empty)
                {
                    throw new InvalidOptionError("No name specified for option.");
                }

                string value = arg[(idxOfEqualSign + 1)..].Trim();
                if (value == string.Empty)
                {
                    throw new InvalidOptionError($"No value specified for option '{key}'.");
                }

                map.Add(key[Option.SwitchPrefix.Length..], value);
            }
            else
            {
                // It's a switch.
                map.Add(arg[Option.SwitchPrefix.Length..], string.Empty);
            }
        }

        return map;
    }
}