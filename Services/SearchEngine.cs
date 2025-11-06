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
    private readonly KaraokeDbContext _dbContext;
    private const int MaxHistoryItems = 10;

    public SearchEngine(KaraokeDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Search for media files by query string with partial matching
    /// Uses LIKE queries for compatibility and ranks results by relevance
    /// </summary>
    public async Task<List<MediaFile>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<MediaFile>();
        }

        var searchTerm = query.Trim();
        var searchPattern = $"%{searchTerm}%";

        // Query with LIKE for partial matching on artist, title, and filename
        // Include metadata for searching
        var results = await _dbContext.MediaFiles
            .Include(m => m.Metadata)
            .Where(m =>
                EF.Functions.Like(m.Filename, searchPattern) ||
                (m.Metadata != null && EF.Functions.Like(m.Metadata.Artist, searchPattern)) ||
                (m.Metadata != null && EF.Functions.Like(m.Metadata.Title, searchPattern))
            )
            .ToListAsync();

        // Rank results by relevance
        var rankedResults = RankSearchResults(results, searchTerm);

        return rankedResults;
    }

    /// <summary>
    /// Add a search term to history (maintains last 10 searches)
    /// </summary>
    public async Task AddToHistoryAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        var searchTerm = query.Trim();

        // Check if this term already exists in history
        var existing = await _dbContext.SearchHistory
            .FirstOrDefaultAsync(h => h.SearchTerm == searchTerm);

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
            _dbContext.SearchHistory.Add(historyItem);
        }

        await _dbContext.SaveChangesAsync();

        // Maintain only the last 10 searches
        await TrimHistoryAsync();
    }

    /// <summary>
    /// Get recent search history (last 10 searches, most recent first)
    /// </summary>
    public async Task<List<string>> GetHistoryAsync()
    {
        var history = await _dbContext.SearchHistory
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
        var allHistory = await _dbContext.SearchHistory.ToListAsync();
        _dbContext.SearchHistory.RemoveRange(allHistory);
        await _dbContext.SaveChangesAsync();
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
        var historyCount = await _dbContext.SearchHistory.CountAsync();

        if (historyCount > MaxHistoryItems)
        {
            var itemsToRemove = await _dbContext.SearchHistory
                .OrderBy(h => h.SearchedAt)
                .Take(historyCount - MaxHistoryItems)
                .ToListAsync();

            _dbContext.SearchHistory.RemoveRange(itemsToRemove);
            await _dbContext.SaveChangesAsync();
        }
    }
}
