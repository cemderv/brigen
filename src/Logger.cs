using System.Diagnostics;

namespace brigen;

public static class Logger
{
    private static readonly Stack<ConsoleColor> _colorStack = new();

    public static event Action<string>? Write;
    public static event Action<ConsoleColor>? ColorRequested;

    public static void Log(string message) => Write?.Invoke(message);

    public static void LogLine(string message) => Write?.Invoke(message + '\n');

    public static void LogLine() => Write?.Invoke("\n");

    public static void LogCategoryGenerationStatus(string name)
    {
        PushColor(ConsoleColor.Yellow);
        LogLine($"=== {name} ===");
        PopColor();
    }

    public static void LogFileGenerationStatus(string name, string filename)
    {
        Debug.Assert(!string.IsNullOrEmpty(name));
        Debug.Assert(!string.IsNullOrEmpty(filename));

        PushColor(ConsoleColor.White);
        Log("-- ");
        PopColor();

        Log(name);

        Log(" -> ");
        PopColor();
        PushColor(ConsoleColor.White);
        Log(Path.GetDirectoryName(filename)!.CleanPath());
        Log("/");
        PopColor();
        PushColor(ConsoleColor.Yellow);
        Log(Path.GetFileName(filename).CleanPath());
        PopColor();

        LogLine();
    }

    public static void PushColor(ConsoleColor color)
    {
        _colorStack.Push(color);
        ColorRequested?.Invoke(color);
    }

    public static void PopColor()
    {
        if (!_colorStack.Any())
            return;

        _colorStack.Pop();
        ColorRequested?.Invoke(_colorStack.Any() ? _colorStack.Peek() : ConsoleColor.White);
    }
}