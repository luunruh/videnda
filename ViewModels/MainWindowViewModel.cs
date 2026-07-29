using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Videnda.Data;
using Videnda.Models;

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

    // ==================== DATEN LADEN / FILTERN ====================

    private void LoadFromDb()
    {
        using var db = new VidendaContext();
        var titles = db.Titles
            .Include(t => t.Genres)
            .OrderByDescending(t => t.DateAdded)
            .ToList();

        _all.Clear();
        foreach (var t in