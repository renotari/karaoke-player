using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Interface for managing the playlist queue
/// </summary>
public interface IPlaylistManager
{
    /// <summary>
    /// Gets the current playlist as an observable collection
    /// </summary>
    ObservableCollection<PlaylistItem> CurrentPlaylist { get; }

    /// <summary>
    /// Adds a song to the playlist at the specified position
    /// </summary>
    /// <param name="mediaFile">The media file to add</param>
    /// <param name="position">"next" to add after current song, "end" to add at the end</param>
    Task AddSongAsync(MediaFile mediaFile, string position = "next");

    /// <summary>
    /// Removes a song from the playlist by index
    /// </summary>
    /// <param name="index">The zero-based index of the song to remove</param>
    Task RemoveSongAsync(int index);

    /// <summary>
    /// Reorders a song in the playlist
    /// </summary>
    /// <param name="fromIndex">The current index of the song</param>
    /// <param name="toIndex">The target index for the song</param>
    Task ReorderSongAsync(int fromIndex, int toIndex);

    /// <summary>
    /// Clears all songs from the playlist
    /// </summary>
    Task ClearPlaylistAsync();

    /// <summary>
    /// Shuffles the playlist order
    /// </summary>
    Task ShufflePlaylistAsync();

    /// <summary>
    /// Checks if a song is already in the playlist (duplicate detection)
    /// </summary>
    /// <param name="mediaFile">The media file to check</param>
    /// <returns>True if the song is already in the playlist</returns>
    bool IsDuplicate(MediaFile mediaFile);

    /// <summary>
    /// Saves the current playlist to an M3U/M3U8 file
    /// </summary>
    /// <param name="filePath">The path where to save the playlist file</param>
    Task SavePlaylistAsync(string filePath);

    /// <summary>
    /// Loads a playlist from an M3U/M3U8 file
    /// </summary>
    /// <param name="filePath">The path to the playlist file</param>
    Task LoadPlaylistAsync(string filePath);

    /// <summary>
    /// Restores the last session playlist on startup
    /// </summary>
    Task RestoreLastSessionAsync();

    /// <summary>
    /// Gets the current playlist as a list
    /// </summary>
    List<PlaylistItem> GetCurrentPlaylist();

    /// <summary>
    /// Event raised when the playlist changes
    /// </summary>
    event EventHandler<PlaylistChangedEventArgs>? PlaylistChanged;
}

/// <summary>
/// Event arguments for playlist changes
/// </summary>
public class PlaylistChangedEventArgs : EventArgs
{
    public PlaylistChangeType ChangeType { get; set; }
    public int? Index { get; set; }
    public PlaylistItem? Item { get; set; }
}

/// <summary>
/// Types of playlist changes
/// </summary>
public enum PlaylistChangeType
{
    Added,
    Removed,
    Reordered,
    Cleared,
    Shuffled,
    Loaded
}
