using System;
using System.Collections.Generic;

namespace Videnda.Models;

public class Title
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Year { get; set; }
    public TitleType Type { get; set; } = TitleType.Movie;
    public WatchStatus Status { get; set; } = WatchStatus.Planned;

    public double? Rating { get; set; }      // 0–10.0, optional
    public string? CoverPath { get; set; }   // für später
    public string? Notes { get; set; }

    public DateTime DateAdded { get; set; } = DateTime.Now;
    public DateTime? DateWatched { get; set; }

    public List<Genre> Genres { get; set; } = new();
}
