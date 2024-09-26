using System.Text.RegularExpressions;

namespace brigen.decl;

public sealed class CommentDecl : Decl
{
    private readonly string[] _contentLines;
    private readonly List<(string, string)> _parameterDescriptions = new();

    public CommentDecl(CodeRange range, string content)
      : base("<comment>", range)
    {
        Regex regex = new(@"@param ([a-zA-Z_0-9]+) (.+)$");

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

        _contentLines = content.Contains('\n') ? content.Split(content, '\n') : new[] { content };
    }

    public IReadOnlyList<string> ContentLines => _contentLines;

    public IReadOnlyList<(string, string)> ParameterDescriptions => _parameterDescriptions;

    protected override void OnVerify(Module module)
    {
    }
}