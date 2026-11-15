using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KaraokePlayer.Models;
using KaraokePlayer.Services;

namespace KaraokePlayer.ViewModels;

/// <summary>
/// "Popular Songs" window: the songs most sung at karaoke (list shipped with the app),
/// split in those the library has (to queue) and those it lacks (to look for on YouTube).
/// </summary>
public partial class PopularSongsViewModel : ViewModelBase
{
    private static List<PopularSong>? _list;
    private readonly MainWindowViewModel? _main;
    private List<PopularSongMatch> _matches = new();

    public ObservableCollection<PopularSongRow> InLibrary { get; } = new();
    public ObservableCollection<PopularSongRow> Missing { get; } = new();

    /// <summary>0: all songs, 1: Italian, 2: foreign.</summary>
    [ObservableProperty]
    private int _languageFilter;

    /// <summary>Opera arias (from the Singa chart) hidden by default.</summary>
    [ObservableProperty]
    private bool _hideOpera = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _inLibraryHeader = "In your library";

    [ObservableProperty]
    private string _missingHeader = "Missing";

    // Design-time
    public PopularSongsViewModel() { }

    public PopularSongsViewModel(MainWindowViewModel main)
    {
        _main = main;
    }

    partial void OnLanguageFilterChanged(int value) => ApplyFilter();
    partial void OnHideOperaChanged(bool value) => ApplyFilter();

    /// <summary>The list shipped with the app (read once).</summary>
    private static List<PopularSong> LoadList()
    {
        if (_list != null) return _list;
        using var stream = Avalonia.Platform.AssetLoader.Open(new Uri("avares://KaraokePlayer/Assets/karaoke-popular-songs.csv"));
        using var reader = new StreamReader(stream);
        return _list = PopularSongs.Parse(reader);
    }

    /// <summary>Compares the list with the library (again after the library changed).</summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (_main == null || IsLoading) return;
        IsLoading = true;
        Summary = "Looking for the songs in your library...";
        try
        {
            var list = LoadList();
            var library = _main.MediaFiles.ToList(); // snapshot: the matching runs off the UI thread
            _matches = await Task.Run(() => PopularSongs.Match(list, library));
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Summary = $"Cannot read the list of popular songs: {ex.Message}";
            App.Current?.LoggingService?.LogError("Popular songs", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var shown = _matches.Where(m =>
            !(HideOpera && m.Song.IsOpera)
            && (LanguageFilter == 0 || (LanguageFilter == 1) == m.Song.IsItalian)).ToList();

        InLibrary.Clear();
        Missing.Clear();
        foreach (var m in shown)
            (m.InLibrary ? InLibrary : Missing).Add(new PopularSongRow(m));

        InLibraryHeader = $"In your library ({InLibrary.Count})";
        MissingHeader = $"Missing ({Missing.Count})";
        if (_matches.Count > 0)
            Summary = $"{InLibrary.Count} of the {shown.Count} most sung karaoke songs are in your library.";
    }

    [RelayCommand]
    private void PlayNext(PopularSongRow? row)
    {
        if (row?.File != null) _main?.AddToPlaylistNextCommand.Execute(row.File);
    }

    [RelayCommand]
    private void AddToEnd(PopularSongRow? row)
    {
        if (row?.File != null) _main?.AddToPlaylistEndCommand.Execute(row.File);
    }

    /// <summary>Opens the YouTube window searching "Artist Title" (it adds "karaoke").</summary>
    [RelayCommand]
    private void SearchYouTube(PopularSongRow? row)
    {
        if (row == null) return;
        var artist = row.Song.Artist.Equals("Traditional", StringComparison.OrdinalIgnoreCase) ? string.Empty : row.Song.Artist;
        ReactiveUI.MessageBus.Current.SendMessage(new OpenYouTubeMessage($"{artist} {row.Song.Title}".Trim()));
    }
}

/// <summary>One line of the Popular Songs window.</summary>
public sealed class PopularSongRow
{
    public PopularSongRow(PopularSongMatch match)
    {
        Song = match.Song;
        File = match.Files.FirstOrDefault();
        OtherVersions = Math.Max(0, match.Files.Count - 1);
    }

    public PopularSong Song { get; }
    public MediaFile? File { get; }
    public int OtherVersions { get; }

    public string Rank => $"{Song.Rank}.";
    public string Sources => Song.Sources.Replace("; ", "\n");

    /// <summary>The library file that will be queued, and how many other versions there are.</summary>
    public string FileInfo => File == null
        ? string.Empty
        : File.Filename + (OtherVersions > 0 ? $"  (+{OtherVersions} other version{(OtherVersions > 1 ? "s" : "")})" : string.Empty);
}
