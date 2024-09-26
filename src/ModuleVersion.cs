namespace brigen;

public sealed class ModuleVersion
{
    public ModuleVersion(int major, int minor, int revision, string tag)
    {
        Major = major;
        Minor = minor;
        Revision = revision;
        Tag = tag;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Revision { get; }
    public string Tag { get; }

    public static ModuleVersion? Parse(string str)
    {
        string[] split = str.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (split.Length > 0)
        {
            if (split.Length > 4)
                return null;

            if (!int.TryParse(split[0], out int major))
                return null;

            int minor = 0;
            if (split.Length > 1 && !int.TryParse(split[1], out minor))
                return null;

            int revision = 0;
            if (split.Length > 2 && !int.TryParse(split[2], out revision))
                return null;

            string tag = string.Empty;
            if (split.Length > 3)
                tag = split[3];

            return new ModuleVersion(major, minor, revision, tag);
        }

        return null;
    }

    public override string ToString() => Tag != string.Empty
      ? $"{Major}.{Minor}.{Revision}.{Tag}"
      : $"{Major}.{Minor}.{Revision}";
}