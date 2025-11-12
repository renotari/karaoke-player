# Audio Visualization Engine Implementation

## Overview

The Audio Visualization Engine provides real-time audio-reactive visualizations for MP3 playback using SkiaSharp for hardware-accelerated rendering. It supports four distinct visualization styles and displays song metadata as background elements.

## Architecture

### Components

1. **IAudioVisualizationEngine** - Interface defining the visualization contract
2. **AudioVisualizationEngine** - Main implementation with rendering logic
3. **VisualizationStyle** - Enum defining available visualization types

### Key Features

- **Hardware-Accelerated Rendering**: Uses SkiaSharp for GPU-accelerated canvas rendering
- **Real-Time Audio Response**: Updates visualization based on audio spectrum data at 30+ FPS
- **Multiple Styles**: Supports Bars, Waveform, Circular, and Particles visualizations
- **Song Information Display**: Shows title, artist, and artwork as background elements
- **Smooth Animations**: Implements smoothing and interpolation for fluid visual effects

## Visualization Styles

### 1. Bars (Default)
- Classic frequency bars visualization
- Vertical bars representing frequency spectrum
- Color gradient based on amplitude (blue to red)
- Up to 64 bars for optimal performance

### 2. Waveform
- Oscilloscope-style waveform display
- Shows audio waveform in real-time
- Mirrored top/bottom for symmetry
- Smooth line rendering with anti-aliasing

### 3. Circular
- Radial frequency visualization
- Bars emanate from center in a circle
- Rotating animation for visual interest
- Pulsing center based on bass frequencies
- Up to 128 bars for detailed circular pattern

### 4. Particles
- Particle system reacting to audio
- Particles spawn from center based on audio intensity
- Color shifts based on amplitude (blue/green/red)
- Maximum 100 particles for performance
- Particles fade out over time

## Usage

### Basic Usage

```csharp
// Create engine
var engine = new AudioVisualizationEngine();

// Set visualization style
engine.Style = VisualizationStyle.Bars;

// Set song information
engine.SetSongInfo("Song Title", "Artist Name", "path/to/artwork.jpg");

// In audio callback, update spectrum data
float[] spectrumData = GetAudioSpectrum(); // From LibVLC
engine.UpdateAudioData(spectrumData);

// In render loop (30+ FPS)
using var surface = SKSurface.Create(new SKImageInfo(width, height));
using var canvas = surface.Canvas;
engine.Render(canvas, width, height);
```

### Integration with Media Player Controller

The Audio Visualization Engine is designed to work with the Media Player Controller:

```csharp
// Get spectrum data from LibVLC
float[] spectrum = mediaPlayerController.GetAudioSpectrum();

// Update visualization
visualizationEngine.UpdateAudioData(spectrum);

// Render in Avalonia control's render loop
public override void Render(DrawingContext context)
{
    using var surface = SKSurface.Create(new SKImageInfo(width, height));
    using var canvas = surface.Canvas;
    
    visualizationEngine.Render(canvas, width, height);
    
    // Convert SKSurface to Avalonia bitmap and draw
    // ... (platform-specific rendering code)
}
```

## Performance Characteristics

### Target Performance
- **Frame Rate**: 30+ FPS rendering
- **Spectrum Size**: 64-128 frequency bins
- **Smoothing**: Applied to reduce jitter
- **Memory**: Minimal allocations per frame

### Optimization Techniques

1. **Spectrum Smoothing**: Reduces visual jitter by blending current and previous frames
2. **Limited Particle Count**: Caps particles at 100 for consistent performance
3. **Efficient Drawing**: Uses SkiaSharp's hardware acceleration
4. **Object Reuse**: Minimizes allocations in render loop

## Configuration

### Style Selection

```csharp
// Change style at runtime
engine.Style = VisualizationStyle.Waveform;
```

### Song Information

```csharp
// Update song info (displayed as overlay)
engine.SetSongInfo(
    title: "My Song",
    artist: "My Artist",
    artworkPath: "path/to/artwork.jpg" // Optional
);
```

### Clear State

```csharp
// Clear all visualization state
engine.Clear();
```

## Background Elements

### Artwork Display
- Artwork is scaled to cover the entire canvas
- Rendered with low opacity (40/255) for background effect
- Dark overlay (180/255) applied for better text visibility

### Text Display
- Title: 48pt bold, white, centered at 15% height
- Artist: 32pt regular, light gray, centered below title
- Anti-aliased rendering for smooth text

## Thread Safety

The engine uses a lock around spectrum data updates to ensure thread-safe access:

```csharp
lock (_dataLock)
{
    // Update spectrum data
}
```

This allows audio callbacks (from LibVLC) to update data on one thread while the render loop runs on another.

## Error Handling

The engine handles various error conditions gracefully:

- **Null/Empty Spectrum Data**: Renders blank visualization
- **Invalid Artwork Path**: Continues without artwork
- **Invalid Canvas Dimensions**: Skips rendering
- **Missing Song Info**: Renders visualization only

## Testing

Comprehensive tests cover:

1. **Initialization**: Default state verification
2. **Audio Data Updates**: Valid, null, and empty data handling
3. **Rendering**: All four visualization styles
4. **Song Information**: Title, artist, and artwork display
5. **Style Switching**: Dynamic style changes
6. **Clear Operation**: State reset functionality

Run tests with:
```bash
dotnet run --project . TestRunner
```

## Future Enhancements

Potential improvements for future versions:

1. **Additional Styles**: Spectrum analyzer, 3D visualizations
2. **Customization**: User-configurable colors and parameters
3. **Beat Detection**: Sync animations to detected beats
4. **Shader Effects**: Advanced GPU effects using custom shaders
5. **Performance Modes**: Quality presets for different hardware

## Requirements Satisfied

This implementation satisfies the following requirements:

- **Requirement 4**: Audio visualization for MP3 playback with song info display
- **Requirement 17**: Multiple visualization styles with real-time audio response at 30+ FPS

## Dependencies

- **SkiaSharp**: Hardware-accelerated 2D graphics rendering
- **System.IO**: File operations for artwork loading
- **LibVLCSharp**: Audio spectrum data source (via Media Player Controller)

## Integration Points

1. **Media Player Controller**: Provides audio spectrum data via `GetAudioSpectrum()`
2. **Avalonia UI**: Hosts the visualization in a custom control with render loop
3. **Settings Manager**: Stores user's preferred visualization style
4. **Metadata Extractor**: Provides song title, artist, and artwork path
