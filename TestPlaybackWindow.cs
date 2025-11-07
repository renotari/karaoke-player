using System;
using System.Threading.Tasks;
using KaraokePlayer.Models;
using KaraokePlayer.Services;
using KaraokePlayer.ViewModels;
using KaraokePlayer.Views;

namespace KaraokePlayer;

/// <summary>
/// Test class to verify PlaybackWindow functionality
/// </summary>
public class TestPlaybackWindow
{
    public static async Task RunTests()
    {
        Console.WriteLine("=== PlaybackWindow Tests ===\n");

        await TestViewModelInitialization();
        await TestMediaInfoUpdate();
        await TestFullscreenToggle();
        await TestSubtitleToggle();
        await TestContentTypeDetection();

        Console.WriteLine("\n=== All PlaybackWindow Tests Completed ===");
    }

    private static async Task TestViewModelInitialization()
    {
        Console.WriteLine("Test: ViewModel Initialization");
        try
        {
            // Create a mock media player controller
            var mockController = new MockMediaPlayerController();
            var viewModel = new PlaybackWindowViewModel(mockController);

            if (viewModel == null)
            {
                Console.WriteLine("❌ FAILED: ViewModel is null");
                return;
            }

            if (viewModel.ToggleFullscreenCommand == null)
            {
                Console.WriteLine("❌ FAILED: ToggleFullscreenCommand is null");
                return;
            }

            if (viewModel.ToggleSubtitlesCommand == null)
            {
                Console.WriteLine("❌ FAILED: ToggleSubtitlesCommand is null");
                return;
            }

            Console.WriteLine("✅ PASSED: ViewModel initialized correctly");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FAILED: {ex.Message}");
        }
    }

    private static async Task TestMediaInfoUpdate()
    {
        Console.WriteLine("\nTest: Media Info Update");
        try
        {
            var mockController = new MockMediaPlayerController();
            var viewModel = new PlaybackWindowViewModel(mockController);

            // Create a test video file
            var videoFile = new MediaFile
            {
                Id = Guid.NewGuid().ToString(),
                FilePath = "C:\\test\\video.mp4",
                Filename = "video.mp4",
                Type = MediaType.Video,
                Format = MediaFormat.MP4,
                Metadata = new MediaMetadata
                {
                    Title = "Test Video",
                    Artist = "Test Artist",
                    Duration = 180
                }
            };

            viewModel.CurrentSong = videoFile;

            if (!viewModel.HasMedia)
            {
                Console.WriteLine("❌ FAILED: HasMedia should be true");
                return;
            }

            if (!viewModel.IsVideoContent)
            {
                Console.WriteLine("❌ FAILED: IsVideoContent should be true");
                return;
            }

            if (viewModel.IsAudioContent)
            {
                Console.WriteLine("❌ FAILED: IsAudioContent should be false for video");
                return;
            }

            if (viewModel.CurrentTitle != "Test Video")
            {
                Console.WriteLine($"❌ FAILED: CurrentTitle should be 'Test Video', got '{viewModel.CurrentTitle}'");
                return;
            }

            if (viewModel.CurrentArtist != "Test Artist")
            {
                Console.WriteLine($"❌ FAILED: CurrentArtist should be 'Test Artist', got '{viewModel.CurrentArtist}'");
                return;
            }

            Console.WriteLine("✅ PASSED: Media info updated correctly");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FAILED: {ex.Message}");
        }
    }

    private static async Task TestFullscreenToggle()
    {
        Console.WriteLine("\nTest: Fullscreen Toggle");
        try
        {
            var mockController = new MockMediaPlayerController();
            var viewModel = new PlaybackWindowViewModel(mockController);

            bool fullscreenRequested = false;
            bool requestedState = false;

            viewModel.FullscreenRequested += (sender, enterFullscreen) =>
            {
                fullscreenRequested = true;
                requestedState = enterFullscreen;
            };

            // Toggle fullscreen on
            viewModel.ToggleFullscreenCommand.Execute(System.Reactive.Unit.Default);

            if (!fullscreenRequested)
            {
                Console.WriteLine("❌ FAILED: FullscreenRequested event not raised");
                return;
            }

            if (!requestedState)
            {
                Console.WriteLine("❌ FAILED: Fullscreen should be requested as true");
                return;
            }

            if (!viewModel.IsFullscreen)
            {
                Console.WriteLine("❌ FAILED: IsFullscreen should be true");
                return;
            }

            // Toggle fullscreen off
            fullscreenRequested = false;
            viewModel.ToggleFullscreenCommand.Execute(System.Reactive.Unit.Default);

            if (!fullscreenRequested)
            {
                Console.WriteLine("❌ FAILED: FullscreenRequested event not raised on second toggle");
                return;
            }

            if (requestedState)
            {
                Console.WriteLine("❌ FAILED: Fullscreen should be requested as false");
                return;
            }

            if (viewModel.IsFullscreen)
            {
                Console.WriteLine("❌ FAILED: IsFullscreen should be false");
                return;
            }

            Console.WriteLine("✅ PASSED: Fullscreen toggle works correctly");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FAILED: {ex.Message}");
        }
    }

    private static async Task TestSubtitleToggle()
    {
        Console.WriteLine("\nTest: Subtitle Toggle");
        try
        {
            var mockController = new MockMediaPlayerController();
            var viewModel = new PlaybackWindowViewModel(mockController);

            // Set up video content
            var videoFile = new MediaFile
            {
                Id = Guid.NewGuid().ToString(),
                FilePath = "C:\\test\\video.mp4",
                Filename = "video.mp4",
                Type = MediaType.Video,
                Format = MediaFormat.MP4,
                Metadata = new MediaMetadata { Title = "Test", Duration = 180 }
            };

            viewModel.CurrentSong = videoFile;

            // Subtitles should be enabled by default
            if (!viewModel.SubtitlesEnabled)
            {
                Console.WriteLine("❌ FAILED: Subtitles should be enabled by default");
                return;
            }

            if (!viewModel.SubtitlesVisible)
            {
                Console.WriteLine("❌ FAILED: Subtitles should be visible for video content");
                return;
            }

            // Toggle subtitles off
            viewModel.ToggleSubtitlesCommand.Execute(System.Reactive.Unit.Default);

            if (viewModel.SubtitlesEnabled)
            {
                Console.WriteLine("❌ FAILED: Subtitles should be disabled after toggle");
                return;
            }

            if (viewModel.SubtitlesVisible)
            {
                Console.WriteLine("❌ FAILED: Subtitles should not be visible when disabled");
                return;
            }

            Console.WriteLine("✅ PASSED: Subtitle toggle works correctly");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FAILED: {ex.Message}");
        }
    }

    private static async Task TestContentTypeDetection()
    {
        Console.WriteLine("\nTest: Content Type Detection");
        try
        {
            var mockController = new MockMediaPlayerController();
            var viewModel = new PlaybackWindowViewModel(mockController);

            // Test audio file
            var audioFile = new MediaFile
            {
                Id = Guid.NewGuid().ToString(),
                FilePath = "C:\\test\\audio.mp3",
                Filename = "audio.mp3",
                Type = MediaType.Audio,
                Format = MediaFormat.MP3,
                Metadata = new MediaMetadata
                {
                    Title = "Test Audio",
                    Artist = "Test Artist",
                    Duration = 240
                }
            };

            viewModel.CurrentSong = audioFile;

            if (!viewModel.IsAudioContent)
            {
                Console.WriteLine("❌ FAILED: IsAudioContent should be true for MP3");
                return;
            }

            if (viewModel.IsVideoContent)
            {
                Console.WriteLine("❌ FAILED: IsVideoContent should be false for MP3");
                return;
            }

            if (viewModel.SubtitlesVisible)
            {
                Console.WriteLine("❌ FAILED: Subtitles should not be visible for audio content");
                return;
            }

            // Test video file
            var videoFile = new MediaFile
            {
                Id = Guid.NewGuid().ToString(),
                FilePath = "C:\\test\\video.mkv",
                Filename = "video.mkv",
                Type = MediaType.Video,
                Format = MediaFormat.MKV,
                Metadata = new MediaMetadata { Title = "Test Video", Duration = 180 }
            };

            viewModel.CurrentSong = videoFile;

            if (!viewModel.IsVideoContent)
            {
                Console.WriteLine("❌ FAILED: IsVideoContent should be true for MKV");
                return;
            }

            if (viewModel.IsAudioContent)
            {
                Console.WriteLine("❌ FAILED: IsAudioContent should be false for MKV");
                return;
            }

            Console.WriteLine("✅ PASSED: Content type detection works correctly");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FAILED: {ex.Message}");
        }
    }
}

// Mock implementation for testing
public class MockMediaPlayerController : IMediaPlayerController
{
    public PlaybackState State => PlaybackState.Stopped;
    public double CurrentTime => 0;
    public double Duration => 0;
    public float Volume => 0.5f;
    public bool SubtitlesEnabled { get; private set; }
    public MediaFile? CurrentMedia => null;
    public bool CrossfadeEnabled => false;
    public int CrossfadeDuration => 5;

    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
    public event EventHandler<TimeChangedEventArgs>? TimeChanged;
    public event EventHandler<MediaEndedEventArgs>? MediaEnded;
    public event EventHandler<PlaybackErrorEventArgs>? PlaybackError;

    public Task PlayAsync(MediaFile mediaFile) => Task.CompletedTask;
    public void Pause() { }
    public void Resume() { }
    public void Stop() { }
    public void Seek(double timeInSeconds) { }
    public void SetVolume(float level) { }
    public void SetAudioDevice(string deviceId) { }
    public System.Collections.Generic.List<AudioDevice> GetAudioDevices() => new();
    public void ToggleSubtitles(bool enabled) { SubtitlesEnabled = enabled; }
    public float[] GetAudioSpectrum() => Array.Empty<float>();
    public Task PreloadNextAsync(MediaFile mediaFile) => Task.CompletedTask;
    public void EnableCrossfade(bool enabled, int durationSeconds) { }
    public LibVLCSharp.Shared.MediaPlayer GetActiveMediaPlayer() => null!;
}
