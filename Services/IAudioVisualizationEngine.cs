using SkiaSharp;

namespace KaraokePlayer.Services;

/// <summary>
/// Defines the contract for audio visualization rendering
/// </summary>
public interface IAudioVisualizationEngine
{
    /// <summary>
    /// Gets or sets the current visualization style
    /// </summary>
    VisualizationStyle Style { get; set; }

    /// <summary>
    /// Updates the audio spectrum data for visualization
    /// </summary>
    /// <param name="spectrumData">Audio frequency spectrum data (0.0 to 1.0)</param>
    void UpdateAudioData(float[] spectrumData);

    /// <summary>
    /// Renders the visualization to the provided canvas
    /// </summary>
    /// <param name="canvas">SkiaSharp canvas to render to</param>
    /// <param name="width">Canvas width in pixels</param>
    /// <param name="height">Canvas height in pixels</param>
    void Render(SKCanvas canvas, int width, int height);

    /// <summary>
    /// Sets the song information to display as background elements
    /// </summary>
    /// <param name="title">Song title</param>
    /// <param name="artist">Artist name</param>
    /// <param name="artworkPath">Path to artwork image (optional)</param>
    void SetSongInfo(string title, string artist, string? artworkPath = null);

    /// <summary>
    /// Clears the current visualization state
    /// </summary>
    void Clear();
}

/// <summary>
/// Available visualization styles
/// </summary>
public enum VisualizationStyle
{
    Bars,
    Waveform,
    Circular,
    Particles
}
