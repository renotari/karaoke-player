using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KaraokePlayer.Models;

/// <summary>
/// Factory for creating DbContext instances at design time (for migrations)
/// </summary>
public class KaraokeDbContextFactory : IDesignTimeDbContextFactory<KaraokeDbContext>
{
    public KaraokeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<KaraokeDbContext>();
        
        // Use a temporary database path for migrations
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KaraokePlayer",
            "karaoke.db"
        );
        
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new KaraokeDbContext(optionsBuilder.Options);
    }
}
