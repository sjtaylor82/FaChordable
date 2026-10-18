using System.Text.Json.Serialization;

namespace FaChordable;

public sealed class ChordCatalogue
{
    public DateTimeOffset UpdatedAt { get; set; }
    public string Source { get; set; } = CatalogueUpdater.IndexUrl;
    public List<ChordKey> Keys { get; set; } = [];
}

public sealed class ChordKey
{
    public string Name { get; set; } = "";
    public List<ChordFamily> Families { get; set; } = [];
    public override string ToString() => Name;
}

public sealed class ChordFamily
{
    public string Name { get; set; } = "";
    public List<Chord> Chords { get; set; } = [];
    public override string ToString() => Name;
}

public sealed class Chord
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public List<ChordPosition> Positions { get; set; } = [];
    public override string ToString() => Name;
}

public sealed class ChordPosition
{
    public string Name { get; set; } = "";
    public List<string> FingerPositions { get; set; } = [];
    public List<string> Characteristics { get; set; } = [];
    public string? AudioUrl { get; set; }

    [JsonIgnore]
    public string AccessibleText
    {
        get
        {
            var lines = new List<string> { Name, "", "Finger positions:" };
            lines.AddRange(FingerPositions.Select(x => $"• {x}"));
            if (Characteristics.Count > 0)
            {
                lines.AddRange(["", "Position characteristics:"]);
                lines.AddRange(Characteristics.Select(x => $"• {x}"));
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
