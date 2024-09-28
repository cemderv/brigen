using System.Diagnostics;

namespace brigen;

internal static class ShellExecutor
{
    public static (string, int) ExecuteAndGetOutput(string filename, string[]? arguments)
    {
        using var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = filename,
                Arguments = arguments != null ? string.Join(' ', arguments) : string.Empty,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        proc.Start();

        string outputStr = proc.StandardOutput.ReadToEnd();

        return (outputStr, proc.ExitCode);
    }

    public static (string, int) ExecuteAndGetOutput(string filename) => ExecuteAndGetOutput(filename, []);
}