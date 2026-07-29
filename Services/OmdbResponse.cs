namespace Videnda.Services;

// Spiegelt die für uns relevanten Felder der OMDb-JSON-Antwort.
// System.Text.Json matcht die Namen case-insensitive (siehe MetadataService).
public class OmdbResponse
{
    public string? Title { get; set; }
    public string? Year { get; set; }      // "2022" oder bei Serien "2019–2023"
    public string? Genre { get; set; }     // "Comedy, Music, Romance"
    public string? Poster { get; set; }    // Bild-URL oder "N/A"
    public string? Type { get; set; }      // "movie" | "series"
    public string? ImdbID { get; set; }

    // Statusfelder: OMDb liefert auch Fehler mit HTTP 200!
    public string? Response { get; set; }  // "True" | "False"
    public string? Error { get; set; }
}
