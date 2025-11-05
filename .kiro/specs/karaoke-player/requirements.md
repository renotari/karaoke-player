# Requirements Document

## Introduction

A media player application that plays video files (MP4, MKV, WEBM) and audio files (MP3) from a configurable directory, supporting continuous playback and song search functionality. The system must accommodate both single-screen (one window) and dual-screen (two windows) setups for optimal karaoke and music playback experiences.

## Glossary

- **Karaoke_Player**: The main application system that manages media playback and user interactions
- **Media_Directory**: A user-configurable folder containing media files (MP4, MKV, WEBM, MP3)
- **Playlist**: An ordered queue of songs to be played sequentially
- **Search_Interface**: The user interface component for finding and selecting songs
- **Playback_Window**: The window displaying the currently playing karaoke video
- **Control_Window**: The window containing playlist management and search functionality
- **Single_Screen_Mode**: Operating mode where all functionality is contained in one window
- **Dual_Screen_Mode**: Operating mode where playback and controls are in separate windows
- **Video_Mode**: Full-screen video display mode within single window with minimal interface
- **Control_Handle**: A collapsible interface element that provides access to search, playlist, and settings
- **Quick_Search**: A search interface accessible from the Control_Handle for immediate song requests
- **Settings_Interface**: The configuration panel where users can customize application behavior
- **File_Metadata**: Information extracted from media files including duration, resolution, file size, artist, and title
- **Thumbnail**: A small preview image generated from video files or extracted from audio file metadata for visual identification
- **Playlist_File**: A saved playlist that can be loaded to restore a specific song queue
- **Queue_Position**: The insertion point for new songs, either "next" or "end" of playlist
- **Audio_Visualization**: Animated visual effects that respond to audio playback for MP3 files
- **ID3_Tags**: Metadata embedded in MP3 files containing artist, title, album, and artwork information
- **Audio_Only_Mode**: Playback mode for MP3 files that displays visualizations instead of video content
- **Global_Volume_Control**: System-wide audio level adjustment that applies to all playback
- **Fullscreen_Mode**: Display mode that hides all window decorations and UI elements, showing only video content
- **Auto_Refresh**: Automatic detection and indexing of new files added to the Media_Directory
- **Shuffle_Mode**: Randomized playback order for playlist songs
- **Search_History**: Previously entered search terms stored for quick access
- **Playlist_Clear**: Function to remove all songs from the current playlist
- **Error_Indicator**: Visual marker showing that a file has issues (corrupted, missing, or inaccessible)
- **Playback_Buffer**: Temporary storage of media data to enable smooth playback
- **Subtitle_Track**: Embedded text overlay in video files that can be toggled on or off
- **Log_File**: A file recording application errors, warnings, and diagnostic information
- **Focus_Indicator**: Visual highlight showing which UI element currently has keyboard focus
- **Audio_Output_Device**: The hardware device (speakers, HDMI, USB DAC, etc.) used for audio playback
- **Mode_Toggle**: UI control for switching between Single_Screen_Mode and Dual_Screen_Mode
- **Playlist_Composer**: A dedicated window for building playlists with catalog browsing and drag-and-drop functionality
- **Catalog_View**: The area in Playlist_Composer showing the full song library with filtering capabilities
- **Composition_View**: The area in Playlist_Composer showing the playlist being built
- **Placeholder_Metadata**: Temporary default information displayed for a media file while its actual metadata is being extracted in the background
- **Placeholder_Thumbnail**: A temporary default image displayed for a media file while its actual thumbnail is being generated in the background

## Requirements

### Requirement 1

**User Story:** As a karaoke host, I want to configure the directory where my media files are stored, so that the application can find and play my song collection.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL provide a settings interface for specifying the Media_Directory path
2. ON first launch without a configured Media_Directory, THE Karaoke_Player SHALL default to the Windows user media directory
3. WHEN the Media_Directory is changed, THE Karaoke_Player SHALL perform a quick scan to index all supported media files (MP4, MKV, WEBM, MP3) filenames and paths in the directory and all subdirectories
4. WHEN scanning the Media_Directory, THE Karaoke_Player SHALL display a progress indicator showing scan status
5. AFTER the quick scan completes, THE Karaoke_Player SHALL process File_Metadata extraction and Thumbnail generation in the background
6. THE Karaoke_Player SHALL allow users to interact with the application while background processing of metadata and thumbnails is in progress
7. THE Karaoke_Player SHALL cache scan results and perform incremental updates on subsequent launches
8. THE Karaoke_Player SHALL persist the Media_Directory configuration between application sessions
9. IF the specified Media_Directory does not exist, THEN THE Karaoke_Player SHALL display an error message and prompt for a valid directory
10. WHEN the current playlist is empty, THE Karaoke_Player SHALL disable commands that require songs (clear, shuffle, play) and visually indicate their disabled state

### Requirement 1A

**User Story:** As a karaoke host, I want the application to extract metadata from my media files, so that I can search and organize songs by artist and title.

#### Acceptance Criteria

1. AFTER the initial Media_Directory scan, THE Karaoke_Player SHALL extract File_Metadata from video files in the background including duration, resolution, and file size
2. THE Karaoke_Player SHALL attempt to extract artist and title metadata from video file containers when available
3. WHERE video files do not contain artist and title metadata, THE Karaoke_Player SHALL parse the filename to extract artist and title information using multiple common patterns including "Artist - Title", "Artist-Title", "Title (Artist)", and "Artist_Title"
4. THE Karaoke_Player SHALL extract ID3_Tags from MP3 files including artist, title, album, and embedded artwork
5. WHERE ID3_Tags are not available in MP3 files, THE Karaoke_Player SHALL parse the filename to extract artist and title information using multiple common patterns including "Artist - Title", "Artist-Title", "Title (Artist)", and "Artist_Title"
6. WHILE metadata extraction is in progress, THE Karaoke_Player SHALL display default or placeholder metadata for songs not yet processed
7. THE Karaoke_Player SHALL allow playback of media files even when metadata extraction is still in progress

### Requirement 1B

**User Story:** As a karaoke host, I want visual thumbnails for my media files, so that I can quickly identify songs visually in search results and playlists.

#### Acceptance Criteria

1. AFTER the initial Media_Directory scan, THE Karaoke_Player SHALL generate Thumbnail images in the background at appropriate size for display in search results and playlist for video files
2. THE Karaoke_Player SHALL extract or generate Thumbnail images at appropriate size for display in search results and playlist for MP3 files from embedded artwork or create default thumbnails
3. WHILE thumbnail generation is in progress, THE Karaoke_Player SHALL display placeholder thumbnails for songs not yet processed
4. THE Karaoke_Player SHALL allow playback of media files even when thumbnail generation is still in progress

### Requirement 2

**User Story:** As a karaoke participant, I want to search for songs by title or artist, so that I can quickly find the song I want to sing.

#### Acceptance Criteria

1. THE Search_Interface SHALL provide a text input field for entering search terms
2. WHEN a user enters search terms, THE Karaoke_Player SHALL filter the song list to show matching artist, titles, and filenames
3. THE Search_Interface SHALL display search results in real-time as the user types
4. THE Karaoke_Player SHALL support partial matching for song searches
5. WHEN no search results are found, THE Search_Interface SHALL display a "no results found" message
6. THE Search_Interface SHALL display File_Metadata and Thumbnail for each search result
7. THE Search_Interface SHALL match against artist, title, and filename metadata only without grouping or hiding results deemed to be duplicates

### Requirement 3

**User Story:** As a karaoke host, I want to manage a playlist of songs, so that I can queue up multiple songs for continuous playback.

**Note:** For bulk playlist creation, see Requirement 23 Playlist_Composer.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL maintain a playlist of selected songs in order
2. WHEN a song is selected from search results, THE Karaoke_Player SHALL add the song to the Queue_Position with "next" as default
3. THE Karaoke_Player SHALL provide options to add songs either "next" or at "end" of playlist
4. THE Karaoke_Player SHALL allow users to reorder songs in the playlist
5. THE Karaoke_Player SHALL allow users to remove songs from the playlist
6. WHEN the current song ends, THE Karaoke_Player SHALL automatically start the next song in the playlist
7. THE Karaoke_Player SHALL persist the current playlist when the application is closed
8. THE Karaoke_Player SHALL restore the last playlist when the application is restarted

### Requirement 4

**User Story:** As a karaoke host, I want the application to play video and audio files with proper synchronization, so that participants can sing along effectively.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL play video files (MP4, MKV, WEBM) with synchronized audio and video
2. THE Karaoke_Player SHALL play audio files (MP3) with proper audio quality
3. THE Playback_Window SHALL display video content at appropriate resolution and aspect ratio for video files
4. WHEN playing MP3 files, THE Playback_Window SHALL display Audio_Visualization with music-reactive animations
5. WHILE in Audio_Only_Mode, THE Karaoke_Player SHALL display song title, artist, and artwork as background elements behind the Audio_Visualization
6. THE Karaoke_Player SHALL provide playback controls including play, pause, stop, and seek functionality for all media types
7. THE Karaoke_Player SHALL display the current playback time and total duration for all media types
8. THE Karaoke_Player SHALL support high-resolution video files including 4K and high bitrate content
9. THE Karaoke_Player SHALL provide configurable keyboard shortcuts for common playback actions
10. THE Karaoke_Player SHALL allow users to toggle embedded subtitles on or off for video files that contain subtitle tracks

### Requirement 5

**User Story:** As a karaoke host with a single screen setup, I want all functionality in one window, so that I can manage everything from one interface.

#### Acceptance Criteria

1. WHERE Single_Screen_Mode is selected, THE Karaoke_Player SHALL provide a normal mode that displays the video player, playlist, and search interface in a single window
2. THE Karaoke_Player SHALL provide a layout that accommodates both video playback and control interfaces without overlap
3. THE Karaoke_Player SHALL allow users to resize interface sections within the single window
4. THE Karaoke_Player SHALL remember the single window layout preferences between sessions
5. THE Karaoke_Player SHALL allow users to toggle between normal mode and Video_Mode

### Requirement 5A

**User Story:** As a karaoke host in single window mode, I want a full-screen video mode with quick access to essential controls, so that I can maximize the video display while still handling song requests from guests.

#### Acceptance Criteria

1. WHERE Single_Screen_Mode is selected, THE Karaoke_Player SHALL provide a Video_Mode that displays video content in the full window
2. WHILE in Video_Mode, THE Karaoke_Player SHALL display a Control_Handle that remains accessible without obscuring the video
3. WHEN the Control_Handle is activated, THE Karaoke_Player SHALL reveal a Quick_Search interface for immediate song selection
4. WHEN a song is selected through Quick_Search, THE Karaoke_Player SHALL add the song to the Queue_Position following the default "next" setting
5. THE Control_Handle SHALL provide access to playlist view
6. THE Control_Handle SHALL provide access to application settings without leaving Video_Mode
7. THE Karaoke_Player SHALL allow the Control_Handle to be collapsed or expanded based on user interaction

### Requirement 6

**User Story:** As a karaoke host with a dual screen setup, I want separate windows for playback and controls, so that I can display the karaoke video on one screen while managing the playlist on another.

#### Acceptance Criteria

1. WHERE Dual_Screen_Mode is selected, THE Karaoke_Player SHALL create separate windows for video playback and controls
2. THE Playback_Window SHALL contain only the video player and basic playback controls
3. THE Control_Window SHALL contain the playlist, search interface, and advanced controls
4. THE Karaoke_Player SHALL allow independent positioning and sizing of both windows
5. THE Karaoke_Player SHALL remember the dual window positions and sizes between sessions

### Requirement 7

**User Story:** As a karaoke host, I want the application to handle continuous playback automatically, so that there are no awkward pauses between songs during the karaoke session.

#### Acceptance Criteria

1. WHEN a song finishes playing, THE Karaoke_Player SHALL automatically start the next song in the playlist within 1 second
2. WHEN the playlist is empty, THE Karaoke_Player SHALL display a "waiting for next song" message
3. THE Karaoke_Player SHALL provide an option to enable or disable automatic continuous playback
4. WHILE continuous playback is enabled, THE Karaoke_Player SHALL transition smoothly between songs without user intervention

### Requirement 8

**User Story:** As a karaoke host, I want smooth video transitions between songs with crossfade effects, so that the experience feels professional and seamless.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL provide an option to enable crossfade transitions between songs for both video and audio files
2. WHERE crossfade is enabled for video files, THE Karaoke_Player SHALL fade the current video to black while fading out its audio
3. WHERE crossfade is enabled for MP3 files, THE Karaoke_Player SHALL fade the current audio visualization to black while fading out its audio
4. WHILE the current media fades to black, THE Karaoke_Player SHALL simultaneously fade in the next media from black while fading in its audio
5. THE Karaoke_Player SHALL allow configuration of crossfade duration between 1 and 20 seconds
6. THE Karaoke_Player SHALL preload the next media file in the playlist to enable seamless crossfade transitions
7. THE Karaoke_Player SHALL initiate crossfade transitions precisely at the configured time before song end without delay
8. THE crossfade transition SHALL use a dip-to-black approach rather than blending two video streams simultaneously

### Requirement 9

**User Story:** As a karaoke host, I want to save and load playlists, so that I can prepare different song collections for various events and reuse them.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL provide functionality to save the current playing playlist as a Playlist_File in M3U or M3U8 format
2. THE Karaoke_Player SHALL allow users to specify the save location using a standard "Save As" dialog
3. THE Karaoke_Player SHALL allow users to load previously saved Playlist_File in M3U or M3U8 format to restore song queues for playback
4. THE Karaoke_Player SHALL prompt users to name playlists when saving
5. THE Karaoke_Player SHALL display a list of available saved playlists for loading
6. WHEN loading a playlist from the main interface, THE Karaoke_Player SHALL replace the current playing playlist with the loaded one

### Requirement 10

**User Story:** As a karaoke host, I want to customize various application settings to match my setup and preferences, so that the karaoke player works optimally in my environment.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL provide a Settings_Interface accessible from both single and dual screen modes
2. THE Settings_Interface SHALL provide audio and video playback settings including volume levels and video scaling options
3. THE Settings_Interface SHALL allow customization of crossfade settings including enable/disable, duration, and transition type
4. THE Settings_Interface SHALL provide display and interface options including theme selection, font sizes, and window layout preferences
5. THE Settings_Interface SHALL allow configuration of playlist behavior including auto-play and shuffle mode
6. THE Settings_Interface SHALL provide search configuration options including search algorithm type and result display preferences
7. THE Settings_Interface SHALL allow performance tuning including video preloading buffer size and cache settings
8. THE Settings_Interface SHALL allow users to select the operating mode between Single_Screen_Mode and Dual_Screen_Mode
9. THE Karaoke_Player SHALL provide a quick toggle button in the main interface to switch between Single_Screen_Mode and Dual_Screen_Mode
10. THE Karaoke_Player SHALL save all configuration changes immediately and apply them without requiring application restart

### Requirement 11

**User Story:** As a karaoke host, I want global volume control with audio enhancement options, so that I can ensure consistent audio levels across different karaoke files.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL provide Global_Volume_Control that applies to all audio playback
2. THE Karaoke_Player SHALL offer a "boost quiet songs" mode that applies real-time audio normalization
3. THE Global_Volume_Control SHALL be accessible via keyboard shortcuts and interface controls
4. THE Karaoke_Player SHALL remember the volume settings between application sessions
5. THE Karaoke_Player SHALL provide visual feedback for current volume level

### Requirement 12

**User Story:** As a karaoke host, I want a fullscreen mode for any window, so that I can provide an immersive experience without any interface distractions.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL provide Fullscreen_Mode that hides all window decorations and UI elements
2. THE Karaoke_Player SHALL allow toggling Fullscreen_Mode using the F11 key from any window or mode
3. WHILE in Fullscreen_Mode, THE Karaoke_Player SHALL display only the video content without any interface elements
4. THE Karaoke_Player SHALL provide a way to exit Fullscreen_Mode using the F11 key, ESC key, or double-click
5. THE Fullscreen_Mode SHALL work in Single_Screen_Mode (both normal and Video_Mode) and Dual_Screen_Mode
6. WHEN Fullscreen_Mode is activated in Video_Mode, THE Control_Handle SHALL be hidden

### Requirement 13

**User Story:** As a karaoke host, I want the application to automatically detect new songs added to my media directory, so that I don't need to restart the app when adding new media files.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL implement Auto_Refresh functionality to monitor the Media_Directory for changes
2. WHEN new media files (MP4, MKV, WEBM, MP3) are added to the Media_Directory, THE Karaoke_Player SHALL automatically index them and update the song list
3. WHEN files are removed from the Media_Directory, THE Karaoke_Player SHALL remove them from the song list and playlists
4. THE Auto_Refresh SHALL generate File_Metadata and Thumbnail for newly detected files
5. THE Karaoke_Player SHALL provide visual notification showing the number of newly detected songs

### Requirement 14

**User Story:** As a karaoke host, I want to shuffle the playlist order, so that I can randomize the song sequence for variety during events.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL provide Shuffle_Mode functionality for the current playlist
2. WHEN Shuffle_Mode is activated, THE Karaoke_Player SHALL randomize the order of songs in the playlist
3. THE Karaoke_Player SHALL maintain the shuffled order until manually changed or shuffle is disabled
4. THE Karaoke_Player SHALL provide a visual indicator when Shuffle_Mode is active
5. THE Karaoke_Player SHALL allow toggling Shuffle_Mode on and off without affecting the current playing song

### Requirement 15

**User Story:** As a karaoke host, I want the search interface to remember recent search terms, so that I can quickly access previously searched songs without retyping.

#### Acceptance Criteria

1. THE Search_Interface SHALL maintain Search_History of recently entered search terms
2. THE Search_Interface SHALL display Search_History as suggestions when the search field is focused
3. THE Karaoke_Player SHALL limit Search_History to the most recent 10 search terms
4. THE Search_Interface SHALL allow users to select from Search_History suggestions to quickly repeat searches
5. THE Karaoke_Player SHALL persist Search_History between application sessions

### Requirement 16

**User Story:** As a karaoke host, I want to quickly clear the entire playlist, so that I can start fresh for new events without removing songs individually.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL provide Playlist_Clear functionality to remove all songs from the current playlist
2. WHEN Playlist_Clear is initiated, THE Karaoke_Player SHALL display a confirmation dialog asking "Are you sure you want to clear the playlist?"
3. THE Karaoke_Player SHALL only clear the playlist after user confirmation in the dialog
4. THE Playlist_Clear SHALL not affect the currently playing song until it finishes
5. THE Karaoke_Player SHALL provide keyboard shortcut access to Playlist_Clear functionality

### Requirement 17

**User Story:** As a user, I want customizable audio visualizations for MP3 playback, so that I have an engaging visual experience even without video content.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL provide multiple Audio_Visualization styles for MP3 playback
2. THE Audio_Visualization SHALL react in real-time to the audio frequency spectrum and amplitude
3. WHILE displaying Audio_Visualization, THE Karaoke_Player SHALL show song title and artist information as overlay or background elements
4. WHERE embedded artwork exists in ID3_Tags, THE Karaoke_Player SHALL display it as a background element behind the Audio_Visualization
5. THE Karaoke_Player SHALL allow users to select different visualization styles through the Settings_Interface
6. THE Audio_Visualization SHALL maintain smooth animation at 30 frames per second or higher

### Requirement 18

**User Story:** As a karaoke host, I want the application to handle errors and edge cases gracefully, so that technical issues don't disrupt the karaoke experience.

#### Acceptance Criteria

1. IF a media file is corrupted or cannot be played, THEN THE Karaoke_Player SHALL skip to the next song in the playlist and mark the problematic file with an Error_Indicator
2. THE Error_Indicator SHALL visually distinguish problematic files in the playlist and indicate the error type
3. WHEN a file is deleted from the Media_Directory while in the playlist, THE Karaoke_Player SHALL continue playback from the Playback_Buffer until depleted, then skip to the next song
4. THE Karaoke_Player SHALL mark deleted files in the playlist with an Error_Indicator indicating the file is missing
5. IF the Media_Directory becomes unavailable during playback, THEN THE Karaoke_Player SHALL continue playing from the Playback_Buffer until depleted, then display an error message
6. WHEN files have permission issues preventing access, THE Karaoke_Player SHALL display them in the song list with an Error_Indicator indicating access permission issues
7. WHEN loading a saved playlist with missing songs, THE Karaoke_Player SHALL load the playlist and mark missing songs with an Error_Indicator
8. WHEN a user attempts to add a duplicate song to the playlist, THE Karaoke_Player SHALL allow it but display a visual indicator in both search results and playlist showing the song is already queued
9. IF the next song fails to load during crossfade, THEN THE Karaoke_Player SHALL cancel the crossfade and skip directly to the following valid song
10. WHERE filename parsing fails to extract artist and title, THE Karaoke_Player SHALL use the entire filename as the title and leave the artist field empty
11. WHEN artist or title text exceeds available display space, THE Karaoke_Player SHALL truncate with ellipsis
12. IF thumbnail generation fails for a media file, THEN THE Karaoke_Player SHALL display the filename text in place of the thumbnail
13. THE Karaoke_Player SHALL clear all Error_Indicator states when the application is restarted

### Requirement 19

**User Story:** As a karaoke host, I want full keyboard navigation support, so that I can operate the application efficiently without a mouse during performances.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL support Tab key navigation to move focus between interface elements
2. THE Karaoke_Player SHALL support Enter key to activate focused buttons and select items
3. THE Karaoke_Player SHALL support arrow keys for navigating lists including search results and playlist
4. THE Karaoke_Player SHALL provide visual focus indicators showing which element is currently selected
5. THE Karaoke_Player SHALL support Escape key to close dialogs and cancel operations
6. THE Karaoke_Player SHALL allow all primary functions to be accessible via keyboard without requiring mouse interaction

### Requirement 20

**User Story:** As a karaoke host, I want the application to log errors and issues, so that I can troubleshoot problems when they occur.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL maintain a log file recording errors, file load failures, and system issues
2. THE Karaoke_Player SHALL log entries with timestamp, severity level, and descriptive message
3. THE Karaoke_Player SHALL store log files in a standard application data directory
4. THE Karaoke_Player SHALL limit log file size to prevent excessive disk usage
5. THE Karaoke_Player SHALL rotate log files when size limits are reached

### Requirement 21

**User Story:** As a developer, I want the application to meet specific performance targets, so that users have a responsive and smooth experience.

#### Acceptance Criteria

1. THE Search_Interface SHALL update search results within 300 milliseconds of user input
2. THE Karaoke_Player SHALL support media libraries containing up to 30,000 files without performance degradation
3. THE Karaoke_Player SHALL maintain smooth video playback at the source frame rate without dropped frames
4. THE Karaoke_Player SHALL load and display the application interface within 3 seconds of launch
5. THE Karaoke_Player SHALL respond to user interactions within 100 milliseconds for immediate feedback

### Requirement 22

**User Story:** As a karaoke host, I want to explicitly select which audio output device the application uses, so that I can route sound to my PA system or external speakers.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL detect and list all available Audio_Output_Device options on the system
2. THE Settings_Interface SHALL display available audio output devices with their device names
3. THE Karaoke_Player SHALL allow users to select their preferred Audio_Output_Device from the available options
4. WHEN an Audio_Output_Device is selected, THE Karaoke_Player SHALL route all audio playback to that device
5. THE Karaoke_Player SHALL persist the selected Audio_Output_Device between application sessions
6. IF the selected Audio_Output_Device becomes unavailable, THEN THE Karaoke_Player SHALL fall back to the system default device and display a notification
7. THE Karaoke_Player SHALL provide a test audio button to verify the selected Audio_Output_Device is working correctly

### Requirement 23

**User Story:** As a karaoke host, I want a dedicated playlist composer tool, so that I can efficiently build playlists by browsing my entire catalog and selecting multiple songs at once.

#### Acceptance Criteria

1. THE Karaoke_Player SHALL provide a Playlist_Composer accessible from the main interface
2. THE Playlist_Composer SHALL open in a separate window
3. THE Catalog_View SHALL display the complete song library with File_Metadata and Thumbnail for each song
4. THE Catalog_View SHALL provide filtering capabilities by artist, title, and other metadata attributes
5. THE Composition_View SHALL display the playlist being composed
6. THE Playlist_Composer SHALL support multi-select of songs in the Catalog_View
7. THE Playlist_Composer SHALL allow users to add selected songs to the Composition_View using a button
8. THE Playlist_Composer SHALL support drag-and-drop of songs from Catalog_View to Composition_View
9. THE Playlist_Composer SHALL allow users to reorder songs in the Composition_View
10. THE Playlist_Composer SHALL allow users to remove songs from the Composition_View
11. THE Playlist_Composer SHALL provide functionality to save the composed playlist as a Playlist_File to disk
12. THE Playlist_Composer SHALL provide functionality to save the composed playlist and immediately load it as the current playing playlist
13. THE Playlist_Composer SHALL allow users to load an existing Playlist_File into the Composition_View for editing purposes and can be loaded for playback using Requirement 9