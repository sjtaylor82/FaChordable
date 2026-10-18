using System.Net;
using System.Text.RegularExpressions;

namespace FaChordable;

public sealed partial class CatalogueUpdater
{
    public const string IndexUrl = "https://www.fachords.com/guitar-chord-textual-description/";
    private readonly HttpClient client = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public CatalogueUpdater() => client.DefaultRequestHeaders.UserAgent.ParseAdd("FaChordable/1.0 (accessible offline chord reader)");

    public async Task<bool> IsUpdateAvailableAsync(ChordCatalogue existing, CancellationToken cancellationToken = default)
    {
        var current = ParseIndex(await client.GetStringAsync(IndexUrl, cancellationToken));
        var oldUrls = existing.Keys.SelectMany(k => k.Families).SelectMany(f => f.Chords).Select(c => c.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newUrls = current.Keys.SelectMany(k => k.Families).SelectMany(f => f.Chords).Select(c => c.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return !oldUrls.SetEquals(newUrls);
    }

    public async Task<ChordCatalogue> DownloadAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report("Downloading chord index…");
        var html = await client.GetStringAsync(IndexUrl, cancellationToken);
        var catalogue = ParseIndex(html);
        var chords = catalogue.Keys.SelectMany(k => k.Families).SelectMany(f => f.Chords).ToList();

        for (var i = 0; i < chords.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Downloading {i + 1} of {chords.Count}: {chords[i].Name}");
            var page = await client.GetStringAsync(chords[i].Url, cancellationToken);
            chords[i].Positions = ParsePositions(page, chords[i].Url);
            if (i < chords.Count - 1) await Task.Delay(25, cancellationToken);
        }

        if (catalogue.Keys.Count < 10 || chords.Count < 100 || chords.Count(c => c.Positions.Count == 0) > chords.Count / 2)
            throw new InvalidDataException($"The downloaded catalogue did not pass validation ({catalogue.Keys.Count} keys, {chords.Count} chords, {chords.Count(c => c.Positions.Count == 0)} empty chords); existing data was kept.");

        catalogue.UpdatedAt = DateTimeOffset.Now;
        return catalogue;
    }

    internal static ChordCatalogue ParseIndex(string html)
    {
        var result = new ChordCatalogue();
        ChordKey? key = null;
        ChordFamily? family = null;
        foreach (Match match in IndexItemRegex().Matches(html))
        {
            var level = match.Groups["level"].Value;
            var content = match.Groups["content"].Value;
            var text = HtmlTools.Text(content);
            if (level == "h2" && text.StartsWith("Chords in the key of ", StringComparison.OrdinalIgnoreCase))
            {
                key = new() { Name = text[21..].Trim() };
                result.Keys.Add(key);
                family = null;
            }
            else if (level == "h3" && key is not null)
            {
                var name = text;
                if (name.StartsWith(key.Name, StringComparison.OrdinalIgnoreCase)) name = name[key.Name.Length..].Trim();
                name = Regex.Replace(name, @"\s+chords\s*$", "", RegexOptions.IgnoreCase).Trim();
                family = new() { Name = name };
                key.Families.Add(family);
            }
            else if (level == "a" && family is not null)
            {
                var href = match.Groups["href"].Value;
                if (href.Contains("/textual", StringComparison.OrdinalIgnoreCase))
                    family.Chords.Add(new() { Name = text.Replace(" chords", "", StringComparison.OrdinalIgnoreCase).Trim(), Url = HtmlTools.AbsoluteUrl(href) });
            }
        }
        result.Keys.RemoveAll(k => k.Families.Count == 0);
        return result;
    }

    internal static List<ChordPosition> ParsePositions(string html, string pageUrl)
    {
        var positions = new List<ChordPosition>();
        foreach (Match match in PositionRegex().Matches(html))
        {
            var position = new ChordPosition { Name = HtmlTools.Text(match.Groups["name"].Value) };
            var body = match.Groups["body"].Value;
            var fingerSection = Section(body, "Fingers Positions", "Position Characteristics|Audio file");
            var characteristicSection = Section(body, "Position Characteristics", "Audio file");
            position.FingerPositions.AddRange(ListItems(fingerSection));
            position.Characteristics.AddRange(ListItems(characteristicSection));
            var audio = AudioRegex().Match(body);
            if (audio.Success) position.AudioUrl = new Uri(new Uri(pageUrl), WebUtility.HtmlDecode(audio.Groups["src"].Value)).ToString();
            positions.Add(position);
        }
        return positions;
    }

    private static string Section(string body, string start, string end)
    {
        var match = Regex.Match(body, $@"<h4[^>]*>.*?{Regex.Escape(start)}.*?</h4>(?<value>.*?)(?=<h4[^>]*>.*?(?:{end}).*?</h4>|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["value"].Value : "";
    }

    private static IEnumerable<string> ListItems(string html) => ListItemRegex().Matches(html).Select(m => HtmlTools.Text(m.Groups["text"].Value)).Where(x => x.Length > 0);

    [GeneratedRegex("<(?:(?<level>h[23])[^>]*>(?<content>.*?)</h[23]|(?<level>a)\\s+[^>]*href=[\"'](?<href>[^\"']+)[\"'][^>]*>(?<content>.*?)</a>)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex IndexItemRegex();

    [GeneratedRegex("<h3[^>]*>(?<name>.*?Position\\s+\\d+.*?)</h3>(?<body>.*?)(?=<h3[^>]*>.*?Position\\s+\\d+.*?</h3>|</main>|<footer|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PositionRegex();

    [GeneratedRegex("<li[^>]*>(?<text>.*?)</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex("<(?:audio|source)[^>]+src=[\"'](?<src>[^\"']+)[\"']", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AudioRegex();
}
