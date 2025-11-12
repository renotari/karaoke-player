# Playlist Manager Implementation

## Overview

The PlaylistManager service manages the playlist queue with support for adding, removing, reordering, and shuffling songs. It includes auto-save functionality, M3U/M3U8 playlist import/export, and duplicate detection.

## Implementation Details

### Core Features

1. **Add Song with Position**
   - Supports "next" (after current song) and "end" positions
   - Automatically detects duplicates
   - Updates positions for all items

2. **Remove Song**
   - Removes song by index
   - Updates positions for remaining items
   - Recalculates duplicate flags

3. **Reorder Song**
   - Moves song from one position to another
   - Updates all affected positions
   - Maintains playlist integrity

4. **Clear Playlist**
   - Removes all songs from playlist
   - Triggers PlaylistChanged event

5. **Shuffle Playlist**
   - Uses Fisher-Yates shuffle algorithm
   - Maintains all songs in playlist
   - Updates positions after shuffle

6. **Duplicate Detection**
   - Checks if a MediaFile is already in the playlist
   - Marks duplicate items with IsDuplicate flag
   - Updates flags when playlist changes

### Auto-Save Functionality

- **Debounced Auto-Save**: Saves playlist 1 second after last change
- **File Location**: `%APPDATA%/KaraokePlayer/current-playlist.json`
- **Format**: JSON with MediaFileId, Position, and AddedAt
- **Thread-Safe**: Uses SemaphoreSlim for concurrent access protection

### Session Restore

- **RestoreLastSession**: Loads playlist from auto-save file on startup
- **Error Handling**: Starts with empty playlist if restore fails
- **Database Integration**: Loads full MediaFile objects from database

### M3U/M3U8 Support

#### Save Playlist
- Exports to M3U/M3U8 format
- Includes #EXTM3U header
- Includes #EXTINF metadata (duration, artist, title)
- Saves file paths for each song

#### Load Playlist
- Imports from M3U/M3U8 format
- Parses file paths from playlist
- Loads MediaFile objects from database
- Marks missing files with error indicator
- Updates duplicate flags after load

### ReactiveUI Integration

- **ObservableCollection**: CurrentPlaylist is observable for UI binding
- **CollectionChanged Event**: Triggers auto-save on changes
- **PlaylistChanged Event**: Custom event for specific change types

### Position Management

- All playlist items maintain a Position property (0-based index)
- Positions are automatically updated after:
  - Adding songs
  - Removing songs
  - Reordering songs
  - Shuffling playlist

### Duplicate Flag Management

- Duplicate flags are recalculated after:
  - Removing songs
  - Loading playlists
  - Restoring sessions
- First occurrence of a MediaFileId is NOT marked as duplicate
- Subsequent occurrences ARE marked as duplicate

## Usage Example

```csharp
// Create PlaylistManager
var dbContext = new KaraokeDbContext(options);
var playlistManager = new PlaylistManager(dbContext);

// Restore last session
await playlistManager.RestoreLastSessionAsync();

// Add songs
await playlistManager.AddSongAsync(mediaFile1, "end");
await playlistManager.AddSongAsync(mediaFile2, "next");

// Check for duplicates
if (playlistManager.IsDuplicate(mediaFile1))
{
    Console.WriteLine("Song is already in playlist");
}

// Reorder songs
await playlistManager.ReorderSongAsync(0, 2);

// Shuffle playlist
await playlistManager.ShufflePlaylistAsync();

// Save to M3U
await playlistManager.SavePlaylistAsync("my-playlist.m3u");

// Load from M3U
await playlistManager.LoadPlaylistAsync("my-playlist.m3u");

// Clear playlist
await playlistManager.ClearPlaylistAsync();

// Subscribe to changes
playlistManager.PlaylistChanged += (sender, args) =>
{
    Console.WriteLine($"Playlist changed: {args.ChangeType}");
};
```

## Event Handling

The PlaylistManager raises `PlaylistChanged` events for:
- **Added**: Song added to playlist
- **Removed**: Song removed from playlist
- **Reordered**: Song moved to different position
- **Cleared**: All songs removed
- **Shuffled**: Playlist order randomized
- **Loaded**: Playlist loaded from file or session

## Thread Safety

- Auto-save uses SemaphoreSlim for thread-safe file access
- Timer-based debouncing prevents concurrent saves
- ObservableCollection is not thread-safe by default (use on UI thread)

## Performance Considerations

- Position updates are O(n) where n is playlist size
- Duplicate detection is O(n) for checking, O(n²) for updating all flags
- Shuffle uses Fisher-Yates algorithm: O(n)
- M3U save/load is O(n) with database lookups

## Requirements Satisfied

- **Requirement 3**: Playlist management (add, remove, reorder)
- **Requirement 9**: Save/load playlists (M3U/M3U8)
- **Requirement 14**: Shuffle playlist
- **Requirement 16**: Clear playlist

## Testing

Run tests with:
```bash
dotnet run --project . -- --test
```

Tests cover:
- Add song to end
- Add song next
- Remove song
- Reorder song
- Clear playlist
- Shuffle playlist
- Duplicate detection
- Save and load M3U playlist
- Position updates

## Future Enhancements

- Support for extended M3U metadata
- Playlist history/undo functionality
- Smart shuffle (avoid artist repetition)
- Playlist templates
- Collaborative playlist editing
