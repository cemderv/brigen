namespace brigen;

public sealed class TempVarNameGen(string prefix)
{
    private readonly List<string> _names = new(256);
    private readonly string _prefix = prefix;

    public string this[int index] => _names[index];

    public string CreateNext()
    {
        string str = $"{_prefix}{_names.Count}";
        _names.Add(str);
        return str;
    }

    public void Clear() => _names.Clear();
}