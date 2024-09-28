using System.Text.RegularExpressions;

namespace brigen.decl;

public sealed partial class CommentDecl : Decl
{
    private readonly string[] _contentLines;
    private readonly List<(string, string)> _parameterDescriptions = [];

    public CommentDecl(CodeRange range, string content)
      : base("<comment>", range)
    {
        Regex regex = CommentRegex();

        Match match = regex.Match(content);

        while (match.Success)
        {
            string paramName = match.Groups[1].Value;
            string paramDesc = match.Groups[2].Value;
            _parameterDescriptions.Add((paramName, paramDesc));
            content = content.Remove(match.Index, match.Length);
            match = regex.Match(content);
        }

        content = content.Trim(' ', '\r', '\n');

        _contentLines = content.Contains('\n') ? content.Split(content, '\n') : [content];
    }

    public IReadOnlyList<string> ContentLines => _contentLines;

    public IReadOnlyList<(string, string)> ParameterDescriptions => _parameterDescriptions;

    protected override void OnVerify(Module module)
    {
    }

    [GeneratedRegex(@"@param ([a-zA-Z_0-9]+) (.+)$")]
    private static partial Regex CommentRegex();
}