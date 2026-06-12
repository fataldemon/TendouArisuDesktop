using System.Text.RegularExpressions;

public static class EmotionParser
{
    private static readonly Regex ExpressionPattern = new Regex(@"【\{'expression':\s*'([^']*)'\}\】", RegexOptions.Compiled);

    public static string Extract(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        Match match = ExpressionPattern.Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    public static string RemoveEmotionTag(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return ExpressionPattern.Replace(text, "").Trim();
    }

    public static string RemoveActionTag(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Regex.Replace(text, @"【\{[^}]*\}\】", "").Trim();
    }
}
