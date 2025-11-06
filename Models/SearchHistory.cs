using System;
using System.ComponentModel.DataAnnotations;

namespace KaraokePlayer.Models;

/// <summary>
/// Stores search history for quick access to recent searches
/// </summary>
public class SearchHistory
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string SearchTerm { get; set; } = string.Empty;

    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
}
