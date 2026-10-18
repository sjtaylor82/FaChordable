namespace FaChordable;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        if ((args.Length == 2 || args.Length == 3) && args[0] == "--download-index")
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("FaChordable/1.0");
            await File.WriteAllTextAsync(args[1], await client.GetStringAsync(args.Length == 3 ? args[2] : CatalogueUpdater.IndexUrl));
            return;
        }
        if (args.Length == 2 && args[0] == "--build-catalogue")
        {
            var progress = new Progress<string>(Console.WriteLine);
            var catalogue = await new CatalogueUpdater().DownloadAsync(progress);
            await using var output = File.Create(args[1]);
            await System.Text.Json.JsonSerializer.SerializeAsync(output, catalogue, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine($"Saved {catalogue.Keys.Count} keys to {args[1]}");
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
