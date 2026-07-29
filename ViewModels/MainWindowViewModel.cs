using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Videnda.Data;
using Videnda.Models;
using Videnda.Services;

namespace Videnda.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    // Alle Titel aus der DB (ungefiltert)
    private readonly List<TitleViewModel> _all = new();

    // Das, was die Kachel-ListBox anzeigt (gefiltert + sortiert)
    public ObservableCollection<TitleViewModel> FilteredTitles { get; } = new();

    // Genre-Optionen im Dropdown
    public ObservableCollection<string> GenreOptions { get; } = new();

    public MainWindowViewModel()
    {
        if (Avalonia.Controls.Design.IsDesignMode)
            return;

        LoadFromDb();
        Refresh();
    }

    // ==================== ZUSTAND (bindet an die XAML) ====================

    // --- aktiver Tab: "watched" oder "planned" ---
    [ObservableProperty]
    public partial string CurrentTab { get; set; } = "watched";

    public bool IsWatchedTab => CurrentTab == "watched";
    public bool IsPlannedTab => CurrentTab == "planned";

    partial void OnCurrentTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsWatchedTab));
        OnPropertyChanged(nameof(IsPlannedTab));
        Refresh();
    }

    // --- Suche ---
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    partial void OnSearchTextChanged(string value) => Refresh();

    // --- Sortierung: "rating" | "name" | "recent" ---
    [ObservableProperty]
    public partial string CurrentSort { get; set; } = "rating";

    public bool IsSortRating => CurrentSort == "rating";
    public bool IsSortName => CurrentSort == "name";
    public bool IsSortRecent => CurrentSort == "recent";

    partial void OnCurrentSortChanged(string value)
    {
        OnPropertyChanged(nameof(IsSortRating));
        OnPropertyChanged(nameof(IsSortName));
        OnPropertyChanged(nameof(IsSortRecent));
        Refresh();
    }

    // --- Genre-Filter ---
    [ObservableProperty]
    public partial string GenreFilter { get; set; } = "All genres";

    partial void OnGenreFilterChanged(string value) => Refresh();

    // --- Auswahl im Grid → speist das Detail-Panel unten ---
    [ObservableProperty]
    public partial TitleViewModel? GridSelection { get; set; }

    public TitleViewModel? SelectedTitle => GridSelection;
    public bool HasSelection => GridSelection is not null;

    partial void OnGridSelectionChanged(TitleViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedTitle));
        OnPropertyChanged(nameof(HasSelection));
    }

    // --- Theme ---
    [ObservableProperty]
    public partial bool IsLightTheme { get; set; }

    partial void OnIsLightThemeChanged(bool value)
    {
        if (Avalonia.Application.Current is { } app)
            app.RequestedThemeVariant = value
                ? Avalonia.Styling.ThemeVariant.Light
                : Avalonia.Styling.ThemeVariant.Dark;
    }

    // ==================== ZÄHLER & LABELS ====================

    public int WatchedCount => _all.Count(t => t.Status == WatchStatus.Watched);
    public int PlannedCount => _all.Count(t => t.Status == WatchStatus.Planned);

    public bool NoResults => FilteredTitles.Count == 0;
    public string ResultCountLabel => $"{FilteredTitles.Count} titles";

    // ==================== BEFEHLE (Buttons in der XAML) ====================

    [RelayCommand]
    private void SetTab(string tab) => CurrentTab = tab;

    [RelayCommand]
    private void SetSort(string sort) => CurrentSort = sort;

    [RelayCommand]
    private void ToggleTheme() => IsLightTheme = !IsLightTheme;

    // Status des ausgewählten Titels umschalten
    [RelayCommand]
    private void SetStatus(string status)
    {
        if (GridSelection is null) return;

        var newStatus = status == "watched" ? WatchStatus.Watched : WatchStatus.Planned;
        GridSelection.Status = newStatus;

        using (var db = new VidendaContext())
        {
            var dbTitle = db.Titles.Find(GridSelection.Model.Id);
            if (dbTitle is not null)
            {
                dbTitle.Status = newStatus;
                db.SaveChanges();
            }
        }

        GridSelection.Model.Status = newStatus;
        OnPropertyChanged(nameof(WatchedCount));
        OnPropertyChanged(nameof(PlannedCount));
        Refresh();
    }

    // ==================== ADD-MODAL ====================

    [ObservableProperty]
    public partial bool ShowAdd { get; set; }

    [ObservableProperty]
    public partial string DraftName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DraftRating { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DraftGenres { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DraftType { get; set; } = "movie";

    [ObservableProperty]
    public partial string DraftStatus { get; set; } = "planned";

    public bool IsDraftMovie => DraftType == "movie";
    public bool IsDraftTv => DraftType == "tv";
    public bool IsDraftWatched => DraftStatus == "watched";
    public bool IsDraftPlanned => DraftStatus == "planned";

    partial void OnDraftTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsDraftMovie));
        OnPropertyChanged(nameof(IsDraftTv));
    }

    partial void OnDraftStatusChanged(string value)
    {
        OnPropertyChanged(nameof(IsDraftWatched));
        OnPropertyChanged(nameof(IsDraftPlanned));
    }

    [RelayCommand]
    private void OpenAdd()
    {
        DraftName = string.Empty;
        DraftRating = string.Empty;
        DraftGenres = string.Empty;
        DraftType = "movie";
        DraftStatus = "planned";
        ShowAdd = true;
    }

    [RelayCommand]
    private void CloseAdd() => ShowAdd = false;

    [RelayCommand]
    private void SetDraftType(string t) => DraftType = t;

    [RelayCommand]
    private void SetDraftStatus(string s) => DraftStatus = s;

    [RelayCommand]
    private void SubmitAdd()
    {
        if (string.IsNullOrWhiteSpace(DraftName))
            return;

        // Rating parsen (optional)
        double? rating = null;
        if (double.TryParse(DraftRating, out var r))
            rating = r;

        // Genres aus Komma-Liste
        var genreNames = DraftGenres
            .Split(',')
            .Select(g => g.Trim())
            .Where(g => g.Length > 0)
            .ToList();

        var title = new Title
        {
            Name = DraftName.Trim(),
            Type = DraftType == "tv" ? TitleType.Tv : TitleType.Movie,
            Status = DraftStatus == "watched" ? WatchStatus.Watched : WatchStatus.Planned,
            Rating = rating
        };

        using (var db = new VidendaContext())
        {
            // Genres: existierende wiederverwenden, neue anlegen
            foreach (var name in genreNames)
            {
                var genre = db.Genres.FirstOrDefault(g => g.Name == name)
                            ?? new Genre { Name = name };
                title.Genres.Add(genre);
            }

            db.Titles.Add(title);
            db.SaveChanges();
        }

        var vm = new TitleViewModel(title);
        _all.Add(vm);

        ShowAdd = false;
        RebuildGenreOptions();
        OnPropertyChanged(nameof(WatchedCount));
        OnPropertyChanged(nameof(PlannedCount));

        // Zum passenden Tab wechseln und neuen Titel auswählen
        CurrentTab = title.Status == WatchStatus.Watched ? "watched" : "planned";
        Refresh();
        GridSelection = FilteredTitles.FirstOrDefault(t => t.Model.Id == title.Id);
    }


    // ==================== IMPORT-MODAL (OMDb) ====================

    [ObservableProperty]
    public partial bool ShowImport { get; set; }

    [ObservableProperty]
    public partial string ImportLink { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ImportStatus { get; set; } = "planned";

    [ObservableProperty]
    public partial bool IsImporting { get; set; }

    [ObservableProperty]
    public partial string? ImportError { get; set; }

    public bool IsImportWatched => ImportStatus == "watched";
    public bool IsImportPlanned => ImportStatus == "planned";
    public bool HasImportError => !string.IsNullOrEmpty(ImportError);

    partial void OnImportStatusChanged(string value)
    {
        OnPropertyChanged(nameof(IsImportWatched));
        OnPropertyChanged(nameof(IsImportPlanned));
    }

    partial void OnImportErrorChanged(string? value) => OnPropertyChanged(nameof(HasImportError));

    [RelayCommand]
    private void OpenImport()
    {
        ImportLink = string.Empty;
        ImportStatus = "planned";
        ImportError = null;
        ShowImport = true;
    }

    [RelayCommand]
    private void CloseImport() => ShowImport = false;

    [RelayCommand]
    private void SetImportStatus(string s) => ImportStatus = s;

    [RelayCommand]
    private async Task SubmitImportAsync()
    {
        ImportError = null;

        var imdbId = MetadataService.ExtractImdbId(ImportLink);
        if (imdbId is null)
        {
            ImportError = "No IMDb ID found in that link.";
            return;
        }

        IsImporting = true;
        try
        {
            var meta = await MetadataService.FetchAsync(imdbId);
            if (meta is null)
            {
                ImportError = "Title not found on OMDb.";
                return;
            }

            var coverPath = await MetadataService.DownloadPosterAsync(meta.Poster, imdbId);

            var genreNames = (meta.Genre ?? string.Empty)
                .Split(',')
                .Select(g => g.Trim())
                .Where(g => g.Length > 0)
                .ToList();

            var title = new Title
            {
                Name = meta.Title ?? imdbId,
                Year = MetadataService.ParseYear(meta.Year),
                Type = meta.Type == "series" ? TitleType.Tv : TitleType.Movie,
                Status = ImportStatus == "watched" ? WatchStatus.Watched : WatchStatus.Planned,
                CoverPath = coverPath
            };

            using (var db = new VidendaContext())
            {
                foreach (var name in genreNames)
                {
                    var genre = db.Genres.FirstOrDefault(g => g.Name == name)
                                ?? new Genre { Name = name };
                    title.Genres.Add(genre);
                }

                db.Titles.Add(title);
                db.SaveChanges();
            }

            _all.Add(new TitleViewModel(title));

            ShowImport = false;
            RebuildGenreOptions();
            OnPropertyChanged(nameof(WatchedCount));
            OnPropertyChanged(nameof(PlannedCount));

            CurrentTab = title.Status == WatchStatus.Watched ? "watched" : "planned";
            Refresh();
            GridSelection = FilteredTitles.FirstOrDefault(t => t.Model.Id == title.Id);
        }
        catch (Exception ex)
        {
            ImportError = $"Import failed: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }


    // ==================== EDIT-MODAL ====================

    [ObservableProperty]
    public partial bool ShowEdit { get; set; }

    [ObservableProperty]
    public partial string EditName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditYear { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditRating { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditGenres { get; set; } = string.Empty;

    [RelayCommand]
    private void OpenEdit()
    {
        if (GridSelection is null) return;

        var m = GridSelection.Model;
        EditName = m.Name;
        EditYear = m.Year?.ToString() ?? string.Empty;
        EditRating = m.Rating?.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        EditGenres = string.Join(", ", m.Genres.Select(g => g.Name));
        ShowEdit = true;
    }

    [RelayCommand]
    private void CloseEdit() => ShowEdit = false;

    [RelayCommand]
    private void SubmitEdit()
    {
        if (GridSelection is null) return;
        if (string.IsNullOrWhiteSpace(EditName)) return;

        // Jahr parsen (optional)
        int? year = int.TryParse(EditYear, out var y) ? y : null;

        // Rating parsen (optional), Komma wie Punkt akzeptieren, auf 0–10 begrenzen
        double? rating = null;
        var ratingText = EditRating.Replace(',', '.');
        if (double.TryParse(ratingText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var r))
            rating = Math.Clamp(r, 0, 10);

        var genreNames = EditGenres
            .Split(',')
            .Select(g => g.Trim())
            .Where(g => g.Length > 0)
            .ToList();

        var model = GridSelection.Model;

        using (var db = new VidendaContext())
        {
            var dbTitle = db.Titles
                .Include(t => t.Genres)
                .FirstOrDefault(t => t.Id == model.Id);
            if (dbTitle is null) return;

            dbTitle.Name = EditName.Trim();
            dbTitle.Year = year;
            dbTitle.Rating = rating;

            // Genres komplett ersetzen: vorhandene wiederverwenden, neue anlegen
            dbTitle.Genres.Clear();
            foreach (var name in genreNames)
            {
                var genre = db.Genres.FirstOrDefault(g => g.Name == name)
                            ?? new Genre { Name = name };
                dbTitle.Genres.Add(genre);
            }

            db.SaveChanges();

            // In-Memory-Model angleichen
            model.Name = dbTitle.Name;
            model.Year = dbTitle.Year;
            model.Rating = dbTitle.Rating;
            model.Genres = dbTitle.Genres.ToList();
        }

        GridSelection.NotifyEdited();

        ShowEdit = false;
        RebuildGenreOptions();
        Refresh();
    }

    // ==================== DATEN LADEN / FILTERN ====================

    private void LoadFromDb()
    {
        using var db = new VidendaContext();
        var titles = db.Titles
            .Include(t => t.Genres)
            .OrderByDescending(t => t.DateAdded)
            .ToList();

        _all.Clear();
        foreach (var t in titles)
            _all.Add(new TitleViewModel(t));

        RebuildGenreOptions();
    }

    private void RebuildGenreOptions()
    {
        var selected = GenreFilter;

        GenreOptions.Clear();
        GenreOptions.Add("All genres");
        foreach (var name in _all
                     .SelectMany(t => t.Genres)
                     .Distinct()
                     .OrderBy(n => n))
        {
            GenreOptions.Add(name);
        }

        // Auswahl behalten, falls das Genre noch existiert
        GenreFilter = GenreOptions.Contains(selected) ? selected : "All genres";
    }

    private void Refresh()
    {
        var wanted = CurrentTab == "watched" ? WatchStatus.Watched : WatchStatus.Planned;

        IEnumerable<TitleViewModel> result = _all.Where(t => t.Status == wanted);

        if (!string.IsNullOrWhiteSpace(SearchText))
            result = result.Where(t =>
                t.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        if (GenreFilter != "All genres")
            result = result.Where(t => t.Genres.Contains(GenreFilter));

        result = CurrentSort switch
        {
            "name"   => result.OrderBy(t => t.Name),
            "recent" => result.OrderByDescending(t => t.Model.DateAdded),
            _        => result.OrderByDescending(t => t.Model.Rating ?? -1)
        };

        FilteredTitles.Clear();
        foreach (var t in result)
            FilteredTitles.Add(t);

        OnPropertyChanged(nameof(NoResults));
        OnPropertyChanged(nameof(ResultCountLabel));

        // Auswahl aufheben, wenn sie aus der Liste gefiltert wurde
        if (GridSelection is not null && !FilteredTitles.Contains(GridSelection))
            GridSelection = null;
    }
}
