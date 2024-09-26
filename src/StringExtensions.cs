namespace brigen;

public static class StringExtensions
{
    public static string Cased(this string str, CaseStyle style)
      => style switch
      {
          CaseStyle.PascalCase => PascalCased(str),
          CaseStyle.CamelCase => CamelCased(str),
          _ => str
      };

    public static string PascalCased(this string str)
      => string.IsNullOrEmpty(str) ? str : char.ToUpperInvariant(str[0]) + str[1..];

    public static string CamelCased(this string str)
      => string.IsNullOrEmpty(str) ? str : char.ToLowerInvariant(str[0]) + str[1..];

    public static string AllUpperCasedIdentifier(this string s)
      => s.ToUpperInvariant();

    public static string CleanPath(this string str)
      => str.Replace('\\', '/');

    public static List<string> WordWrap(this string text, int maxCharactersPerLine)
    {
        text = text.Trim();
        var result = new List<string>();
        string[] words = text.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

        if (text.Length <= maxCharactersPerLine || words.Length == 1)
            result.Add(text);
        else
            foreach (string? word in words)
            {
                string? addition = $" {word.Trim()}";
                int lineIndex = result.Count - 1;
                int lineCharacters = lineIndex > -1 ? result[lineIndex].Length + addition.Length : 0;

                if (result.Count < 1 || lineCharacters > maxCharactersPerLine)
                {
                    // Start new line
                    addition = addition.Trim();
                    result.Add(addition);
                }
                else
                {
                    // Append existing line
                    result[lineIndex] += addition;
                }
            }

        return result;
    }

    public static int LevenshteinDistance(string s1, string s2)
    {
        int m = s1.Length;
        int n = s2.Length;

        if (m == 0)
            return n;
        if (n == 0)
            return m;

        // allocation below is not ISO-compliant,
        // it won't work with -pedantic-errors.
        int[] costs = new int[n + 1];

        for (int k = 0; k <= n; k++)
            costs[k] = k;

        int i = 0;
        foreach (char c1 in s1)
        {
            costs[0] = i + 1;
            int corner = i, j = 0;
            foreach (char c2 in s2)
            {
                int upper = costs[j + 1];
                if (c1 == c2)
                {
                    costs[j + 1] = corner;
                }
                else
                {
                    int t = upper < corner ? upper : corner;
                    costs[j + 1] = (costs[j] < t ? costs[j] : t) + 1;
                }

                corner = upper;
                j++;
            }

            i++;
        }

        return costs[n];
    }

    public static float LevenshteinDistanceNormalized(string lhs, string rhs)
    {
        int distance = LevenshteinDistance(lhs, rhs);
        int maxLength = Math.Max(lhs.Length, rhs.Length);
        return (float)((double)distance / maxLength);
    }
}