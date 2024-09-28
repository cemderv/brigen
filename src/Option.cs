namespace brigen;

internal record Option(string Name, string Description, bool IsSwitch)
{
    public const string SwitchPrefix = "--";

    public static void PrintOptions(IReadOnlyList<Option> options, int indent = 2)
    {
        string tabString = new(' ', indent);
        const int gapExtraLength = 10;
        const int maxDescLineWidth = 80;
        const string valueAssignmentStr = "=<...>";

        string GetOptionDisplayName(Option opt)
        {
            string str = opt.IsSwitch ? $"{SwitchPrefix}{opt.Name}" : $"{SwitchPrefix}{opt.Name}{valueAssignmentStr}";
            str = tabString + str;
            return str;
        }

        int gapLength = options.Max(o => GetOptionDisplayName(o).Length) + gapExtraLength;

        foreach (var option in options)
        {
            string displayName = GetOptionDisplayName(option);
            Logger.Log(displayName);

            Logger.Log(new string(' ', gapLength - displayName.Length));

            List<string> descLines = option.Description.WordWrap(maxDescLineWidth);

            foreach (string descLine in descLines)
            {
                if (descLine != descLines[0])
                {
                    Logger.LogLine();
                    Logger.Log(new string(' ', gapLength));
                }

                Logger.Log(descLine);
            }

            Logger.LogLine();
        }
    }

    public static readonly Dictionary<string, Option> AllOptions = new()
    {
        { Names.InFilename, new(Names.InFilename, "The input interface file", false) },
        { Names.OutDir, new(Names.OutDir, "The output directory", false) },
        { Names.GenCSharp, new Option(Names.GenCSharp, "Generate C# bindings", true) },
        { Names.GenPython, new Option(Names.GenPython, "Generate Python bindings", true) },
        { Names.GenJava, new Option(Names.GenJava, "Generate Java bindings", true) },
    };

    public IEnumerable<Option> Options => AllOptions.Values;
}