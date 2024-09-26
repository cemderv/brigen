using ClangSharp.Interop;
using System.Text;

namespace brigen.diff;

internal abstract class ClangBasedFile(DiffDetector diffDetector) : IDisposable
{
    protected DiffDetector DiffDetector { get; } = diffDetector;

    public CXIndex Index { get; private set; }

    protected CXTranslationUnit TUnit { get; private set; }

    public CXFile File { get; private set; }

    public string? FileContents { get; private set; }

    public void Dispose()
    {
        Index.Dispose();
        TUnit.Dispose();
    }

    public virtual bool Load(string filename)
    {
        if (!System.IO.File.Exists(filename))
            return false;

        Index = CXIndex.Create();
        TUnit = CXTranslationUnit.Parse(Index, filename, null, null, CXTranslationUnit_Flags.CXTranslationUnit_None);

        if (TUnit == default)
            throw new Exception($"Failed to parse file \"{filename}\".");

        File = TUnit.GetFile(filename);

        {
            ReadOnlySpan<byte> bytes = TUnit.GetFileContents(File, out UIntPtr _);
            FileContents = Encoding.UTF8.GetString(bytes);
        }

        return true;
    }

    protected static bool IsForwardDeclaration(CXCursor cursor)
    {
        CXCursor definition = cursor.Definition;

        // If the definition is null, then there is no definition in this translation
        // unit, so this cursor must be a forward declaration.
        if (clang.equalCursors(definition, clang.getNullCursor()) != 0) return true;

        // If there is a definition, then the forward declaration and the definition
        // are in the same translation unit. This cursor is the forward declaration if
        // it is _not_ the definition.
        return clang.equalCursors(cursor, definition) == 0;
    }

    protected static ClangRange GetClangRange(CXTranslationUnit tu, CXSourceRange range)
    {
        var startLoc = range.Start;
        var endLoc = range.End;

        var ret = default(ClangRange);
        startLoc.GetExpansionLocation(out ret.File, out _, out _, out uint startOffset);
        endLoc.GetExpansionLocation(out _, out _, out _, out uint endOffset);

        ret.StartOffset = (int)startOffset;
        ret.Length = (int)endOffset - (int)startOffset;
        var fileContents = tu.GetFileContents(ret.File, out _);

        ret.Substring = fileContents.Slice(ret.StartOffset, ret.Length).AsString();

        return ret;
    }

    protected static string GetClangSubstring(CXTranslationUnit tu, CXSourceRange range)
      => GetClangRange(tu, range).Substring;

    protected static string ExtractClangFunctionSignature(string content, bool inHeader)
    {
        string signature;

        if (inHeader)
        {
            int i = content.IndexOfAny(['{', ';']);
            if (i < 0)
                i = content.Length;

            signature = content[..i];
            signature = signature.Replace("\n", string.Empty);
            signature = signature.Replace(" ", string.Empty);
        }
        else
        {
            int i = content.IndexOf('{');
            if (i < 0)
                return string.Empty;

            signature = content[..i];
            signature = signature.Replace("\n", string.Empty);
            signature = signature.Replace("\r", string.Empty);
            signature = signature.Replace(" ", string.Empty);
        }

        return signature;
    }

    protected static string TakeSemicolonIfPresent(string content) => content;

    protected static FunctionKind GetFunctionKind(CXCursorKind cursorKind)
      => cursorKind switch
      {
          CXCursorKind.CXCursor_Constructor => FunctionKind.Ctor,
          CXCursorKind.CXCursor_Destructor => FunctionKind.Dtor,
          CXCursorKind.CXCursor_CXXMethod => FunctionKind.Method,
          CXCursorKind.CXCursor_FunctionDecl => FunctionKind.FreeFunction,
          _ => throw new ArgumentException($"Invalid cursor kind {cursorKind}")
      };

    protected static void SortBasedOnVisibility<T>(List<T> elements) where T : IHasVisibility
      => elements = [.. elements.OrderBy(e => (int)e.Visibility)];

    protected struct ClangRange
    {
        public CXFile File;
        public uint Line;
        public uint EndLine;
        public uint Column;
        public uint EndColumn;
        public int StartOffset;
        public int Length;
        public string Substring;
    }
}