using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Videnda.Models;

namespace Videnda.ViewModels;

// Umhüllt ein Title-Model und liefert alle Anzeige-Werte für die XAML.
public partial class TitleViewModel : ViewModelBase
{
    public Title Model { get; }

    public TitleViewModel(Title model)
    {
        Model = model;
        Status = model.Status;
    }

    // --- Direkte Durchreichungen ---
    public string Name => Model.Name;
    public string Year => Model.Year?.ToString() ?? "—";

    // Status ist observable, weil er sich zur Laufzeit ändert (Watched/Planned-Umschalten)
    [ObservableProperty]
    public partial WatchStatus Status { get; set; }


    // --- Echtes Cover (lazy geladen und gecacht), null wenn keins existiert ---
    private Bitmap? _cover;
    public Bitmap? Cover
    {
        get
        {
            if (_cover is null
                && !string.IsNullOrWhiteSpace(Model.CoverPath)
                && File.Exists(Model.CoverPath))
            {
                _cover = new Bitmap(Model.CoverPath);
            }
            return _cover;
        }
    }

    public bool HasCover => Cover is not null;


    // Nach einem Edit aufrufen: meldet alle aus dem Model berechneten Werte neu
    public void NotifyEdited()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Year));
        OnPropertyChanged(nameof(RatingText));
        OnPropertyChanged(nameof(RatingBarWidth));
        OnPropertyChanged(nameof(Genres));
        OnPropertyChanged(nameof(PosterBrush));
    }

    // --- Typ-Anzeige ---
    public string TypeLabel => Model.Type == TitleType.Tv ? "TV" : "FILM";
    public string TypeFull => Model.Type == TitleType.Tv ? "TV Series" : "Movie";

    // --- Rating-Anzeige ---
    public string RatingText => Model.Rating.HasValue ? $"{Model.Rating:0.0}" : "–";

    // Breite des Fortschrittsbalkens im Detail-Panel (max. 120px bei Rating 10)
    public double RatingBarWidth => Model.Rating.HasValue ? Model.Rating.Value / 10.0 * 120.0 : 0;

    // --- Genres als reine Strings (die XAML bindet an Text) ---
    public List<string> Genres => Model.Genres.Select(g => g.Name).ToList();

    // --- Status-Flags für die "active"-Markierung der Buttons ---
    public bool IsWatched => Status == WatchStatus.Watched;
    public bool IsPlanned => Status == WatchStatus.Planned;

    // Wenn sich der Status ändert, müssen die beiden Flags neu gemeldet werden,
    // damit die Buttons im Detail-Panel korrekt umschalten.
    partial void OnStatusChanged(WatchStatus value)
    {
        OnPropertyChanged(nameof(IsWatched));
        OnPropertyChanged(nameof(IsPlanned));
    }

    // --- Poster-Farbe: aus dem Namen generiert (deterministisch, gleiche Farbe pro Titel) ---
    public IBrush PosterBrush
    {
        get
        {
            // Hash des Namens → Farbwert. So bekommt jeder Titel eine stabile eigene Farbe.
            int hash = Model.Name.GetHashCode();
            var rng = new Random(hash);

            // Zwei Farben für einen Verlauf, in angenehmen (nicht zu grellen) Tönen
            byte H1 = (byte)rng.Next(0, 360);
            var c1 = FromHsl(H1, 0.55, 0.42);
            var c2 = FromHsl((H1 + 35) % 360, 0.60, 0.30);

            return new LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(c1, 0),
                    new GradientStop(c2, 1)
                }
            };
        }
    }

    // Hilfsfunktion: HSL → RGB-Farbe (damit die Poster harmonische Töne haben)
    private static Color FromHsl(double h, double s, double l)
    {
        h /= 360.0;
        double r, g, b;

        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3);
        }

        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}