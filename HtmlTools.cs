using System.Net;
using System.Text.RegularExpressions;

namespace FaChordable;

internal static partial class HtmlTools
{
    public static string Text(string html) => WebUtility.HtmlDecode(TagRegex().Replace(html, " "))
        .Replace("\u00a0", " ").Trim();

    public static string AbsoluteUrl(string href) => new Uri(new Uri(CatalogueUpdater.IndexUrl), WebUtility.HtmlDecode(href)).ToString();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();
}
