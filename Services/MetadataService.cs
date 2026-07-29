using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Videnda.Services;

// Holt Film-/Serien-Metadaten von der OMDb-API anhand einer IMDb-ID.
// API-Key liegt in ~/.config/Videnda/omdb.key (eine Zeile, nur der Key).
public static class MetadataService
{
    private static readonly HttpClient Http = new();

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    // Gleicher Ordner wie die Datenbank (~/.config/Videnda unter Linux)
    private static string ConfigDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Videnda");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string? LoadApiKey()
    {
        var path = Path.Combine(ConfigDir, "omdb.key");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    // Zieht die "tt1234567" aus einem IMDb-Link (oder erkennt eine direkt gepastete ID).
    public static string? ExtractImdbId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var match = Regex.Match(input, @"tt\d{7,8}");
        return match.Success ? match.Value : null;
    }

    // "2022" → 2022, "2019–2023" → 2019, Unfug → null
    public static int? ParseYear(string? year)
    {
        if (string.IsNullOrWhiteSpace(year) || year.Length < 4)
            return null;

        return int.TryParse(year[..4], out var y) ? y : null;
    }

    // Fragt OMDb ab. Gibt null zurück, wenn nichts gefunden wurde.
    // Wirft InvalidOperationException, wenn kein API-Key hinterlegt ist.
    public static async Task<OmdbResponse?> FetchAsync(string imdbId)
    {
        var key = LoadApiKey()
            ?? throw new InvalidOperationException(
                "Kein OMDb-API-Key gefunden. Lege ihn in ~/.config/Videnda/omdb.key ab.");

        var url = $"https://www.omdbapi.com/?i={Uri.EscapeDataString(imdbId)}&apikey={key}";
        var json = await Http.GetStringAsync(url);

        var result = JsonSerializer.Deserialize<OmdbResponse>(json, JsonOptions);

        // OMDb meldet Fehler mit HTTP 200 + Response:"False" — daher hier prüfen
        return result?.Response == "True" ? result : null;
    }

    // Lädt das Poster nach ~/.config/Videnda/covers/{imdbId}.jpg
    // und gibt den lokalen Pfad zurück (null, wenn kein Poster existiert).
    public static async Task<string?> DownloadPosterAsync(string? posterUrl, string imdbId)
    {
        if (string.IsNullOrWhiteSpace(posterUrl) || posterUrl == "N/A")
            return null;

        var dir = Path.Combine(ConfigDir, "covers");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{imdbId}.jpg");

        var bytes = await Http.GetByteArrayAsync(posterUrl);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }
}
