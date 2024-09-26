using System.Diagnostics;
using System.Text;

namespace brigen;

public sealed class Writer
{
    private readonly StringBuilder _contents = new(2048);
    private int _depth;
    private string _indentStr = string.Empty;

    public bool IndentationEnabled { get; set; } = true;

    public string Contents => _contents.ToString();

    public bool HasWrittenAnything { get; private set; }

    public void Clear()
    {
        _depth = 0;
        _contents.Clear();
        DetermineIndentString();
    }

    public Writer OpenBrace()
    {
        Write("{\n");
        Indent();
        return this;
    }

    public Writer CloseBrace(bool semicolon = false)
    {
        Unindent();
        Write(semicolon ? "};" : "}");
        WriteLine();
        return this;
    }

    public Writer Indent()
    {
        ++_depth;
        DetermineIndentString();
        return this;
    }

    public Writer Unindent()
    {
        Debug.Assert(_depth > 0);
        --_depth;
        DetermineIndentString();
        return this;
    }

    public Writer Write(char ch)
    {
        LazyAppendIndent();
        _contents.Append(ch);
        HasWrittenAnything = true;
        return this;
    }

    public Writer Write(string str)
    {
        LazyAppendIndent();
        _contents.Append(str);
        HasWrittenAnything = true;
        return this;
    }

    private void LazyAppendIndent()
    {
        if (IndentationEnabled && _contents.Length > 0 && _contents[^1] == '\n')
            _contents.Append(_indentStr);
    }

    public Writer WriteLine(string str) => Write(str).Write('\n');

    public Writer WriteLine(char ch) => Write(ch).Write('\n');

    public Writer WriteLine() => Write('\n');

    public void SaveContentsToDisk(string filename)
    {
        Debug.Assert(!string.IsNullOrEmpty(filename));
        _contents.Replace("\r\n", "\n");
        Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
        File.WriteAllText(filename, Contents);
    }

    public void WriteCommaSeparatorIfNotPresent()
    {
        if (_contents.Length == 0)
            return;

        bool haveComma = false;

        for (int i = _contents.Length - 1; i >= 0; --i)
        {
            char ch = _contents[i];
            if (ch == ' ')
                continue;
            haveComma = ch == ',';
            break;
        }

        if (!haveComma)
            Write(", ");
    }

    public void WriteNewlineIfWrittenAnythingAndResetMarker()
    {
        if (HasWrittenAnything)
            WriteLine();
        ClearWriteMarker();
    }

    public void WriteAutoGenerationNotice(string filename, IEnumerable<string>? lines, string commentStr,
      bool canBeEdited)
    {
        if (lines != null)
        {
            WriteLine($"{commentStr} Description:");
            Write(commentStr).Write(' ');
            Write(string.Join($"\n{commentStr} ", lines));
            WriteLine();
        }

        WriteLine(commentStr);
        WriteLine($"{commentStr} Auto-generated using {Library.GetDisplayName(false)}");

        if (!canBeEdited)
            WriteLine($"{commentStr} IMPORTANT: Changes made to this file will be discarded!");
        else
            WriteLine($"{commentStr} This file is editable, meaning that changes will remain after subsequent generations.");

        WriteLine();
    }

    public void ClearWriteMarker() => HasWrittenAnything = false;

    private void DetermineIndentString() => _indentStr = new string(' ', _depth * 2);
}