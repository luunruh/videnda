using System.Collections.Generic;

namespace Videnda.Models;

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<Title> Titles { get; set; } = new();
}
