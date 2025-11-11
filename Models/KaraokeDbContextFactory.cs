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
        
        // Configure SQLite with performance optimizations
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
            Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared, // Enable shared cache for connection pooling
            Pooling = true // Enable connection pooling
        }.ToString();
        
        optionsBuilder.UseSqlite(connectionString, options =>
        {
            options.CommandTimeout(30);
        });

        return new KaraokeDbContext(optionsBuilder.Options);
    }
}
