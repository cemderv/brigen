namespace brigen;

public sealed class ProgramArguments
{
    private readonly Dictionary<string, string> _args = [];

    public ProgramArguments(string[] args)
    {
        foreach (string? arg in args)
        {
            int idxOfEqualSign = arg.IndexOf('=');
            if (idxOfEqualSign >= 0)
            {
                // It's a setting.
                string key = arg[..idxOfEqualSign].Trim();
                if (key == string.Empty)
                    throw new InvalidOptionError("No name specified for option.");

                string value = arg[(idxOfEqualSign + 1)..].Trim();
                if (value == string.Empty)
                    throw new InvalidOptionError($"No value specified for option '{key}'.");

                _args.Add(key[1..], value);
            }
            else
            {
                // It's a switch.
                _args.Add(arg[1..], string.Empty);
            }
        }
    }

    public int Count => _args.Count;

    public bool HasOption(string name) => _args.ContainsKey(name);

    public string GetValue(string key) =>
      !_args.TryGetValue(key, out string? result)
        ? throw new InvalidOptionError($"Missing required option '-{key}'.")
        : MakeCleanString(result);

    public string TryGetValue(string key, string defaultValue)
      => _args.TryGetValue(key, out string? result) ? MakeCleanString(result) : defaultValue;

    private static string MakeCleanString(string str)
      => str.Trim(' ', '\"').Replace('\\', '/');
}