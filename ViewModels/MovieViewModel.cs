using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Videnda.Models;

namespace Videnda.ViewModels;

public partial class MovieViewModel : ViewModelBase
{
    public Movie Model { get; }

    public MovieViewModel(Movie model)
    {
        Model = model;
        Title = model.Title;
        Status = model.Status;
        Rating = model.Rating;
    }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial WatchStatus Status { get; set; }

    [ObservableProperty]
    public partial double? Rating { get; set; }

    public string StatusText => Status == WatchStatus.Watched ? "Gesehen" : "Geplant";

    public string RatingText => Rating.HasValue ? $"{Rating:0.0} / 10" : "Nicht bewertet";

    public string GenresText => Model.Genres.Count > 0
        ? string.Join(", ", Model.Genres.Select(g => g.Name))
        : "Keine Genres";

    public string NotesText => string.IsNullOrWhiteSpace(Model.Notes)
        ? "Keine Notizen"
        : Model.Notes;

    public Bitmap? Cover
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Model.CoverPath) && File.Exists(Model.CoverPath))
                return new Bitmap(Model.CoverPath);
            return null;
        }
    }

    partial void OnStatusChanged(WatchStatus value) => OnPropertyChanged(nameof(StatusText));
    partial void OnRatingChanged(double? value) => OnPropertyChanged(nameof(RatingText));
}
