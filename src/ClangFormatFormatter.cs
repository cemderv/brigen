using System.Diagnostics;

namespace brigen;

public sealed class ClangFormatFormatter(string clangFormatLocation)
{
    private readonly string _clangFormatLocation = clangFormatLocation;

    public string Format(string filename)
    {
        Debugger.Break();

        if (string.IsNullOrEmpty(_clangFormatLocation))
            return string.Empty;

        string[] arguments = [$"-assume-filename={filename}", filename];

        return ShellExecutor.ExecuteAndGetOutput(_clangFormatLocation, arguments).Item1;
    }
}