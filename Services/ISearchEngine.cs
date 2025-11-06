using System.Collections.Generic;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Interface for searching media files with history tracking
/// </summary>
public interface ISearchEngine
{
    /// <summary>
    /// Search for media files by query string
    /// </summary>
    /// <param name="query">Search query (searches artist, title, filename)</param>
    /// <returns>List of matching media files ranked by relevance</returns>
    Task<List<MediaFile>> SearchAsync(string query);

    /// <summary>
    /// Add a search term to history
    /// </summary>
    /// <param name="query">Search term to add</param>
    Task AddToHistoryAsync(string query);

    /// <summary>
    /// Get recent search history (last 10 searches)
    /// </summary>
    /// <returns>List of recent search terms</returns>
    Task<List<string>> GetHistoryAsync();

    /// <summary>
    /// Clear all search history
    /// </summary>
    Task ClearHistoryAsync();
}
