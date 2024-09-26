namespace brigen;

public sealed class TempVarNameGen
{
    private readonly List<string> _names;
    private readonly string _prefix;

    public TempVarNameGen(string prefix)
    {
        _prefix = prefix;
        _names = new List<string>(256);
    }

    public string this[int index] => _names[index];

    public string CreateNext()
    {
        string str = $"{_prefix}{_names.Count}";
        _names.Add(str);
        return str;
    }

    public void Clear() => _names.Clear();
}