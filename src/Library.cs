using System.Text;

namespace brigen;

public static class Library
{
    public static Version Version => typeof(Library).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

    public static string AppDisplayName => "brigen - interface generator for C++";

    public static string GetDisplayName(bool withCopyrightNotice)
    {
        var sb = new StringBuilder(128)
          .Append(AppDisplayName)
          .Append(" - Version ")
          .Append(Version);

        if (withCopyrightNotice)
            sb.Append("\nCopyright (C) 2021-2024 Cemalettin Dervis");

        return sb.ToString();
    }
}