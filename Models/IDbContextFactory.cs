namespace KaraokePlayer.Models;

/// <summary>
/// Factory interface for creating short-lived DbContext instances.
/// Implements the factory pattern to avoid long-lived DbContext instances
/// which can cause memory leaks and stale data issues.
/// </summary>
public interface IDbContextFactory
{
    /// <summary>
    /// Creates a new KaraokeDbContext instance with proper configuration.
    /// The caller is responsible for disposing the context (use 'using' statement).
    /// </summary>
    /// <returns>A new configured DbContext instance</returns>
    KaraokeDbContext CreateDbContext();
}
