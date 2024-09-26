namespace brigen;

[Flags]
public enum TargetLanguage
{
    None = 0,
    CSharp = 1,
    Python = 2,
    Java = 4,
    C = 8,
    Cpp = 16,
    All = CSharp | Python | Java | C | Cpp
}

internal static class TargetLanguageHelper
{
    private static readonly LanguageInfo[] _list =
    {
    new(TargetLanguage.CSharp, "csharp"), new(TargetLanguage.Python, "python"), new(TargetLanguage.Java, "java"),
    new(TargetLanguage.C, "c"), new(TargetLanguage.Cpp, "cpp"), new(TargetLanguage.All, "all")
  };

    public static TargetLanguage ParseTargetLanguageFlags(string str)
      => str.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(split => _list.FirstOrDefault(i => i.FlagString == split))
        .Where(info => info != null)
        .Aggregate(TargetLanguage.None, (current, info) => current | info!.Id);

    private record LanguageInfo(TargetLanguage Id, string FlagString);
}