using System;
using Microsoft.EntityFrameworkCore;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Test factory that creates in-memory DbContext instances for unit testing.
/// Each factory instance creates contexts that share the same in-memory database.
/// </summary>
public class TestDbContextFactory : IDbContextFactory
{
    private readonly string _databaseName;

    /// <summary>
    /// Creates a new test factory with an optional database name.
    /// If not specified, a unique GUID-based name is generated.
    /// </summary>
    /// <param name="databaseName">Optional database name for the in-memory database</param>
    public TestDbContextFactory(string? databaseName = null)
    {
        _databaseName = databaseName ?? Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Creates a new DbContext instance configured to use in-memory database.
    /// All contexts created by the same factory instance share the same database.
    /// </summary>
    public KaraokeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<KaraokeDbContext>()
            .UseInMemoryDatabase(databaseName: _databaseName)
            .Options;

        var context = new KaraokeDbContext(options);

        // Ensure database schema is created
        context.Database.EnsureCreated();

        return context;
    }
}
