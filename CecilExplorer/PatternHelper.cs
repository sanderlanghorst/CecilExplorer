using System.Text.RegularExpressions;

namespace CecilExplorer;

public class PatternHelper
{
    public static Regex GetPattern(string patternString)
    {
        var pattern = string.Join("|", patternString.Split(',')
            .Select(s => s
                .Replace(".", "\\.")
                .Replace("*", ".{0,100}")
                .Replace("?", string.Empty)
                .Replace("+", string.Empty)
                .Replace("[", string.Empty)
                .Replace("]", string.Empty)
                .Trim())
        );

        return new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}