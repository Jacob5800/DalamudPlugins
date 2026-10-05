using System.Net;
using System.Text.RegularExpressions;

namespace StrategyBoardLibrary;

internal static partial class ShareCodeTools
{
    [GeneratedRegex(@"\[stgy:[^\]\r\n]{20,}\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ShareCodeRegex();

    [GeneratedRegex("<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex TitleRegex();

    public static bool TryExtract(string? text, out string shareCode)
    {
        shareCode = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var match = ShareCodeRegex().Match(text.Trim());
        if (!match.Success)
            return false;

        shareCode = match.Value;
        return true;
    }

    public static string? ExtractPageTitle(string html)
    {
        var match = TitleRegex().Match(html);
        if (!match.Success)
            return null;

        var title = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
        return string.IsNullOrWhiteSpace(title) ? null : title;
    }
}
