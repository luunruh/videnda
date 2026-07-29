using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Videnda.Models;

namespace Videnda.Data;

public class VidendaContext : DbContext
{
    public DbSet<Title> Titles { get; set; }
    public DbSet<Genre> Genres { get; set; }

    private static string DbPath
    {
        get
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(folder, "Videnda");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "videnda.db");
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");
}
