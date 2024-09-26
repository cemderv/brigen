using System.Diagnostics;

namespace brigen;

public readonly struct CodeRange : IEquatable<CodeRange>
{
    public CodeRange(string filename, int line = 0, int start = 0, int end = 0, int startColumn = 0, int endColumn = 0)
    {
        Filename = filename;
        Line = line;
        Start = start;
        End = end;
        StartColumn = startColumn;
        EndColumn = endColumn;
    }

    public static CodeRange Merge(CodeRange start, CodeRange end)
    {
        Debug.Assert(start.Filename == end.Filename);

        return new CodeRange(start.Filename, start.Line, start.Start, end.End, start.StartColumn, end.EndColumn);
    }

    public static bool AreDirectNeighbors(in CodeRange a, in CodeRange b)
      => a.Line == b.Line && a.EndColumn == b.StartColumn;

    public readonly string Filename;
    public readonly int Line;
    public readonly int Start;
    public readonly int End;
    public readonly int StartColumn;
    public readonly int EndColumn;

    public override string ToString()
      => $"Line {Line} Column {StartColumn}-{EndColumn} ({Path.GetFileName(Filename.AsSpan())})";

    public bool Equals(CodeRange other)
      => Filename == other.Filename
         && Line == other.Line
         && Start == other.Start
         && End == other.End
         && StartColumn == other.StartColumn
         && EndColumn == other.EndColumn;

    public override bool Equals(object? obj)
      => obj is CodeRange other && Equals(other);

    public override int GetHashCode()
      => HashCode.Combine(Filename, Line, Start, End, StartColumn, EndColumn);

    public static bool operator ==(CodeRange left, CodeRange right) => left.Equals(right);

    public static bool operator !=(CodeRange left, CodeRange right) => !(left == right);
}