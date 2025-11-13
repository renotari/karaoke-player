using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace KaraokePlayer.Models;

/// <summary>
/// Runtime factory for creating DbContext instances with proper configuration.
/// Ensures database is initialized once and all contexts share the same configuration.
/// </summary>
public class DbContextFactory : IDbContextFactory
{
    private readonly string _connectionString;
    private static bool _databaseInitialized = false;
    private static readonly object _initLock = new object();

    /// <summary>
    /// Creates a new DbContextFactory with the specified connection string
    /// </summary>
    /// <param name="connectionString">SQLite connection string</param>
    public DbContextFactory(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Creates a new KaraokeDbContext instance.
    /// First call initializes the database with performance optimizations.
    /// </summary>
    public KaraokeDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<KaraokeDbContext>();
        optionsBuilder.UseSqlite(_connectionString, options =>
        {
            options.CommandTimeout(30);
        });

        var context = new KaraokeDbContext(optionsBuilder.Options);

        // Ensure database is initialized only once (thread-safe)
        if (!_databaseInitialized)
        {
            lock (_initLock)
            {
                if (!_databaseInitialized)
                {
                    context.Database.EnsureCreated();
                    InitializeDatabaseSettings(context);
                    _databaseInitialized = true;
                }
            }
        }

        return context;
    }

    /// <summary>
    /// Initializes database performance settings (WAL mode, cache size, etc.)
    /// Called once during first context creation
    /// </summary>
    private static void InitializeDatabaseSettings(KaraokeDbContext context)
    {
        // Enable Write-Ahead Logging (WAL) mode for better concurrency
        using (var connection = context.Database.GetDbConnection())
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                // Enable WAL mode
                command.CommandText = "PRAGMA journal_mode=WAL;";
                command.ExecuteNonQuery();

                // Set synchronous mode to NORMAL for better performance
                command.CommandText = "PRAGMA synchronous=NORMAL;";
                command.ExecuteNonQuery();

                // Set cache size to 64MB
                command.CommandText = "PRAGMA cache_size=-64000;";
                command.ExecuteNonQuery();

                // Use memory for temporary storage
                command.CommandText = "PRAGMA temp_store=MEMORY;";
                command.ExecuteNonQuery();
            }
        }
    }
}
