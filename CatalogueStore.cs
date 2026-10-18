using System.Reflection;
using System.Text.Json;

namespace FaChordable;

public static class CatalogueStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FaChordable");
    private static readonly string LegacyFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FaChording");
    private static readonly string CachePath = Path.Combine(Folder, "chords.json");
    private static readonly string CheckPath = Path.Combine(Folder, "last-update-check.txt");

    public static DateTimeOffset? LastUpdateCheck => File.Exists(CheckPath) && DateTimeOffset.TryParse(File.ReadAllText(CheckPath), out var value) ? value : null;

    public static void RecordUpdateCheck()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(CheckPath, DateTimeOffset.Now.ToString("O"));
    }

    public static async Task<ChordCatalogue> LoadAsync()
    {
        var availableCache = File.Exists(CachePath) ? CachePath : Path.Combine(LegacyFolder, "chords.json");
        if (File.Exists(availableCache))
        {
            await using var cached = File.OpenRead(availableCache);
            return await JsonSerializer.DeserializeAsync<ChordCatalogue>(cached, JsonOptions) ?? new();
        }

        await using var bundled = Assembly.GetExecutingAssembly().GetManifestResourceStream("FaChordable.Data.chords.json")
            ?? throw new InvalidOperationException("The bundled chord catalogue is missing.");
        return await JsonSerializer.DeserializeAsync<ChordCatalogue>(bundled, JsonOptions) ?? new();
    }

    public static async Task SaveAsync(ChordCatalogue catalogue)
    {
        Directory.CreateDirectory(Folder);
        var temporary = CachePath + ".new";
        await using (var output = File.Create(temporary))
            await JsonSerializer.SerializeAsync(output, catalogue, JsonOptions);
        File.Move(temporary, CachePath, true);
    }
}
