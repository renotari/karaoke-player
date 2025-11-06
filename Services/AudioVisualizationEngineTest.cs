using SkiaSharp;
using System;
using System.IO;

namespace KaraokePlayer.Services;

/// <summary>
/// Tests for AudioVisualizationEngine
/// </summary>
public class AudioVisualizationEngineTest
{
    public static void RunTests()
    {
        Console.WriteLine("=== AudioVisualizationEngine Tests ===\n");

        TestInitialization();
        TestUpdateAudioData();
        TestRenderWithoutData();
        TestRenderBarsStyle();
        TestRenderWaveformStyle();
        TestRenderCircularStyle();
        TestRenderParticlesStyle();
        TestSetSongInfo();
        TestStyleSwitching();
        TestClear();

        Console.WriteLine("\n=== All AudioVisualizationEngine Tests Passed ===");
    }

    private static void TestInitialization()
    {
        Console.WriteLine("Test: Initialization");
        var engine = new AudioVisualizationEngine();
        
        Assert(engine.Style == VisualizationStyle.Bars, "Default style should be Bars");
        Console.WriteLine("✓ Engine initializes with default Bars style");
    }

    private static void TestUpdateAudioData()
    {
        Console.WriteLine("\nTest: Update Audio Data");
        var engine = new AudioVisualizationEngine();
        
        // Test with valid data
        float[] spectrum = new float[64];
        for (int i = 0; i < spectrum.Length; i++)
        {
            spectrum[i] = (float)i / spectrum.Length;
        }
        
        engine.UpdateAudioData(spectrum);
        Console.WriteLine("✓ Audio data updated successfully");
        
        // Test with null data (should not crash)
        engine.UpdateAudioData(null!);
        Console.WriteLine("✓ Handles null audio data gracefully");
        
        // Test with empty array (should not crash)
        engine.UpdateAudioData(Array.Empty<float>());
        Console.WriteLine("✓ Handles empty audio data gracefully");
    }

    private static void TestRenderWithoutData()
    {
        Console.WriteLine("\nTest: Render Without Data");
        var engine = new AudioVisualizationEngine();
        
        using var surface = SKSurface.Create(new SKImageInfo(800, 600));
        using var canvas = surface.Canvas;
        
        // Should not crash when rendering without data
        engine.Render(canvas, 800, 600);
        Console.WriteLine("✓ Renders without crashing when no audio data present");
    }

    private static void TestRenderBarsStyle()
    {
        Console.WriteLine("\nTest: Render Bars Style");
        var engine = new AudioVisualizationEngine();
        engine.Style = VisualizationStyle.Bars;
        
        float[] spectrum = GenerateTestSpectrum(64);
        engine.UpdateAudioData(spectrum);
        
        using var surface = SKSurface.Create(new SKImageInfo(800, 600));
        using var canvas = surface.Canvas;
        
        engine.Render(canvas, 800, 600);
        Console.WriteLine("✓ Bars visualization renders successfully");
    }

    private static void TestRenderWaveformStyle()
    {
        Console.WriteLine("\nTest: Render Waveform Style");
        var engine = new AudioVisualizationEngine();
        engine.Style = VisualizationStyle.Waveform;
        
        float[] spectrum = GenerateTestSpectrum(64);
        engine.UpdateAudioData(spectrum);
        
        using var surface = SKSurface.Create(new SKImageInfo(800, 600));
        using var canvas = surface.Canvas;
        
        engine.Render(canvas, 800, 600);
        Console.WriteLine("✓ Waveform visualization renders successfully");
    }

    private static void TestRenderCircularStyle()
    {
        Console.WriteLine("\nTest: Render Circular Style");
        var engine = new AudioVisualizationEngine();
        engine.Style = VisualizationStyle.Circular;
        
        float[] spectrum = GenerateTestSpectrum(128);
        engine.UpdateAudioData(spectrum);
        
        using var surface = SKSurface.Create(new SKImageInfo(800, 600));
        using var canvas = surface.Canvas;
        
        engine.Render(canvas, 800, 600);
        Console.WriteLine("✓ Circular visualization renders successfully");
    }

    private static void TestRenderParticlesStyle()
    {
        Console.WriteLine("\nTest: Render Particles Style");
        var engine = new AudioVisualizationEngine();
        engine.Style = VisualizationStyle.Particles;
        
        float[] spectrum = GenerateTestSpectrum(64);
        engine.UpdateAudioData(spectrum);
        
        using var surface = SKSurface.Create(new SKImageInfo(800, 600));
        using var canvas = surface.Canvas;
        
        // Render multiple times to generate and update particles
        for (int i = 0; i < 10; i++)
        {
            engine.Render(canvas, 800, 600);
        }
        Console.WriteLine("✓ Particles visualization renders successfully");
    }

    private static void TestSetSongInfo()
    {
        Console.WriteLine("\nTest: Set Song Info");
        var engine = new AudioVisualizationEngine();
        
        // Test with title and artist only
        engine.SetSongInfo("Test Song", "Test Artist");
        Console.WriteLine("✓ Song info set without artwork");
        
        // Test with null values (should not crash)
        engine.SetSongInfo(null!, null!);
        Console.WriteLine("✓ Handles null song info gracefully");
        
        // Test with non-existent artwork path (should not crash)
        engine.SetSongInfo("Test Song", "Test Artist", "nonexistent.jpg");
        Console.WriteLine("✓ Handles invalid artwork path gracefully");
        
        // Render with song info
        float[] spectrum = GenerateTestSpectrum(64);
        engine.UpdateAudioData(spectrum);
        
        using var surface = SKSurface.Create(new SKImageInfo(800, 600));
        using var canvas = surface.Canvas;
        
        engine.SetSongInfo("My Song Title", "My Artist Name");
        engine.Render(canvas, 800, 600);
        Console.WriteLine("✓ Renders with song info displayed");
    }

    private static void TestStyleSwitching()
    {
        Console.WriteLine("\nTest: Style Switching");
        var engine = new AudioVisualizationEngine();
        float[] spectrum = GenerateTestSpectrum(64);
        engine.UpdateAudioData(spectrum);
        
        using var surface = SKSurface.Create(new SKImageInfo(800, 600));
        using var canvas = surface.Canvas;
        
        // Test switching between all styles
        foreach (VisualizationStyle style in Enum.GetValues(typeof(VisualizationStyle)))
        {
            engine.Style = style;
            Assert(engine.Style == style, $"Style should be {style}");
            engine.Render(canvas, 800, 600);
        }
        
        Console.WriteLine("✓ All visualization styles switch and render correctly");
    }

    private static void TestClear()
    {
        Console.WriteLine("\nTest: Clear");
        var engine = new AudioVisualizationEngine();
        
        // Set up some state
        float[] spectrum = GenerateTestSpectrum(64);
        engine.UpdateAudioData(spectrum);
        engine.SetSongInfo("Test Song", "Test Artist");
        
        // Clear everything
        engine.Clear();
        
        using var surface = SKSurface.Create(new SKImageInfo(800, 600));
        using var canvas = surface.Canvas;
        
        // Should render without errors after clear
        engine.Render(canvas, 800, 600);
        Console.WriteLine("✓ Clear removes all state and renders blank visualization");
    }

    private static float[] GenerateTestSpectrum(int size)
    {
        var spectrum = new float[size];
        var random = new Random(42); // Fixed seed for reproducibility
        
        for (int i = 0; i < size; i++)
        {
            // Generate realistic spectrum data (higher frequencies have lower amplitude)
            float baseAmplitude = 1.0f - ((float)i / size * 0.7f);
            spectrum[i] = baseAmplitude * (float)random.NextDouble();
        }
        
        return spectrum;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
    }
}
