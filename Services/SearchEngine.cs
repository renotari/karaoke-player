using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Search engine for media files with SQLite full-text search and history tracking
/// </summary>
public class SearchEngine : ISearchEngine
{
    /// <summary>Results returned at most (a huge list is of no use in the catalog).</summary>
    private const int MaxResults = 1000;

    private readonly IDbContextFactory _dbContextFactory;
    private readonly PerformanceMonitor? _performanceMonitor;
    private const int MaxHistoryItems = 10;

    public SearchEngine(IDbContextFactory dbContextFactory, PerformanceMonitor? performanceMonitor = null)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _performanceMonitor = performanceMonitor;
    }

    /// <summary>
    /// Search for media files by query string with partial matching
    /// Uses LIKE queries for compatibility and ranks results by relevance
    /// Optimized with AsNoTracking for read-only queries and limited result set
    /// </summary>
    public async Task<List<MediaFile>> SearchAsync(string query)
    {
        using var _ = _performanceMonitor?.MeasureOperation("SearchEngine.SearchAsync");

        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<MediaFile>();
        }

        var searchTerm = query.Trim();

        // % and _ typed by the user are literal characters, not LIKE wildcards
        var escaped = searchTerm.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var prefixPattern = $"{escaped}%";
        var containsPattern = $"%{escaped}%";

        using var context = _dbContextFactory.CreateDbContext();

        // The result list is limited, so the best matches are fetched first: names
        // that start with the text (exact matches included), then the other matches.
        // Otherwise, with thousands of matches, an exact title could be cut off.
        var prefixMatches = await context.MediaFiles
            .AsNoTracking()
            .Include(m => m.Metadata)
            .Where(m =>
                EF.Functions.Like(m.Filename, prefixPattern, "\\") ||
                (m.Metadata != null && EF.Functions.Like(m.Metadata.Artist, prefixPattern, "\\")) ||
                (m.Metadata != null && EF.Functions.Like(m.Metadata.Title, prefixPattern, "\\")))
            .Take(MaxResults)
            .ToListAsync();

        var results = prefixMatches;
        if (prefixMatches.Count < MaxResults)
        {
            var prefixIds = prefixMatches.Select(m => m.Id).ToHashSet();
            var otherMatches = await context.MediaFiles
                .AsNoTracking()
                .Include(m => m.Metadata)
                .Where(m =>
                    EF.Functions.Like(m.Filename, containsPattern, "\\") ||
                    (m.Metadata != null && EF.Functions.Like(m.Metadata.Artist, containsPattern, "\\")) ||
                    (m.Metadata != null && EF.Functions.Like(m.Metadata.Title, containsPattern, "\\")))
                .Take(MaxResults + prefixMatches.Count)
                .ToListAsync();
            results = prefixMatches
                .Concat(otherMatches.Where(m => !prefixIds.Contains(m.Id)))
                .Take(MaxResults)
                .ToList();
        }

        // Rank results by relevance
        var rankedResults = RankSearchResults(results, searchTerm);

        return rankedResults;
    }

    /// <summary>
    /// Add a search term to history (maintains last 10 searches)
    /// Optimized with indexed SearchTerm lookup
    /// </summary>
    public async Task AddToHistoryAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var searchTerm = query.Trim();

        using var context = _dbContextFactory.CreateDbContext();

        // Check if this term already exists in history using indexed column
        var existing = await context.SearchHistory
            .Where(h => h.SearchTerm == searchTerm)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            // Update timestamp to move it to the top
            existing.SearchedAt = DateTime.UtcNow;
        }
        else
        {
            // Add new search term
            var historyItem = new SearchHistory
            {
                SearchTerm = searchTerm,
                SearchedAt = DateTime.UtcNow
            };
            context.SearchHistory.Add(historyItem);
        }

        await context.SaveChangesAsync();

        // Maintain only the last 10 searches
        await TrimHistoryAsync();
    }

    /// <summary>
    /// Get recent search history (last 10 searches, most recent first)
    /// </summary>
    public async Task<List<string>> GetHistoryAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();

        var history = await context.SearchHistory
            .OrderByDescending(h => h.SearchedAt)
            .Take(MaxHistoryItems)
            .Select(h => h.SearchTerm)
            .ToListAsync();

        return history;
    }

    /// <summary>
    /// Clear all search history
    /// </summary>
    public async Task ClearHistoryAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();

        var allHistory = await context.SearchHistory.ToListAsync();
        context.SearchHistory.RemoveRange(allHistory);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Rank search results by relevance
    /// Priority: Exact match > Starts with > Contains
    /// Fields: Title > Artist > Filename
    /// </summary>
    private List<MediaFile> RankSearchResults(List<MediaFile> results, string searchTerm)
    {
        var lowerSearchTerm = searchTerm.ToLowerInvariant();

        var ranked = results.Select(file => new
        {
            File = file,
            Score = CalculateRelevanceScore(file, lowerSearchTerm)
        })
        .OrderByDescending(x => x.Score)
        .ThenBy(x => x.File.Metadata?.Title ?? x.File.Filename)
        .Select(x => x.File)
        .ToList();

        return ranked;
    }

    /// <summary>
    /// Calculate relevance score for a media file
    /// Higher score = more relevant
    /// </summary>
    private int CalculateRelevanceScore(MediaFile file, string lowerSearchTerm)
    {
        int score = 0;

        var title = file.Metadata?.Title?.ToLowerInvariant() ?? "";
        var artist = file.Metadata?.Artist?.ToLowerInvariant() ?? "";
        var filename = file.Filename.ToLowerInvariant();

        // Title matches (highest priority)
        if (title == lowerSearchTerm)
            score += 1000; // Exact match
        else if (title.StartsWith(lowerSearchTerm))
            score += 500; // Starts with
        else if (title.Contains(lowerSearchTerm))
            score += 250; // Contains

        // Artist matches (medium priority)
        if (artist == lowerSearchTerm)
            score += 800; // Exact match
        else if (artist.StartsWith(lowerSearchTerm))
            score += 400; // Starts with
        else if (artist.Contains(lowerSearchTerm))
            score += 200; // Contains

        // Filename matches (lower priority)
        if (filename == lowerSearchTerm)
            score += 600; // Exact match
        else if (filename.StartsWith(lowerSearchTerm))
            score += 300; // Starts with
        else if (filename.Contains(lowerSearchTerm))
            score += 150; // Contains

        return score;
    }

    /// <summary>
    /// Remove old search history items to maintain max limit
    /// </summary>
    private async Task TrimHistoryAsync()
    {
        using var context = _dbContextFactory.CreateDbContext();

        var historyCount = await context.SearchHistory.CountAsync();

        if (historyCount > MaxHistoryItems)
        {
            var itemsToRemove = await context.SearchHistory
                .OrderBy(h => h.SearchedAt)
                .Take(historyCount - MaxHistoryItems)
                .ToListAsync();

            context.SearchHistory.RemoveRange(itemsToRemove);
            await context.SaveChangesAsync();
        }
    }
}
