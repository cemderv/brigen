using System.Text;

namespace brigen;

public static class Library
{
    public const string CopyrightNotice = "Copyright (C) 2021-2024 Cemalettin Dervis";

    public static Version Version => typeof(Library).Assembly.GetName().Version ?? new Version(0, 0);

    public static string AppDisplayName => "brigen - interface generator for C++";

    public static string GetDisplayName(bool withCopyrightNotice)
    {
        var version = Version;

        return withCopyrightNotice
            ? $"{AppDisplayName} {version.Major}.{version.Minor}\n{CopyrightNotice}"
            : $"{AppDisplayName} {version.Major}.{version.Minor}";
    }
}