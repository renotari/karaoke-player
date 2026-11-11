using System;
using System.Threading.Tasks;
using KaraokePlayer.ViewModels;

namespace KaraokePlayer.Services;

/// <summary>Mute and volume from the settings (MainWindowViewModel, no player).</summary>
public static class VolumeTest
{
    private static void Check(bool condition, string what)
    {
        Console.WriteLine($"  {(condition ? "✓" : "✗")} {what}");
        if (!condition)
            throw new Exception(what);
    }

    public static Task RunTests()
    {
        Console.WriteLine("=== Volume Tests ===\n");

        var vm = new MainWindowViewModel { Volume = 60 };
        vm.ToggleMute();
        Check(vm.IsMuted && vm.Volume == 0 && vm.VolumeToRemember == 60, "mute: slider at 0, level 60 remembered");

        vm.ApplyVolumeSetting(60);
        Check(vm.IsMuted && vm.Volume == 0, "same level from the settings: stays muted");

        vm.ApplyVolumeSetting(0);
        Check(!vm.IsMuted && vm.Volume == 0 && vm.VolumeToRemember == 0, "volume 0 from the settings while muted: mute ends, 0 remembered");

        vm.ToggleMute();
        vm.Volume = 30;
        Check(!vm.IsMuted && vm.VolumeToRemember == 30, "moving the slider while muted unmutes");

        vm.ApplyVolumeSetting(80);
        Check(vm.Volume == 80 && vm.IsSilent == false, "new level from the settings");

        vm.ToggleMute();
        vm.ToggleMute();
        Check(!vm.IsMuted && vm.Volume == 80, "unmute restores the level");

        Console.WriteLine();
        return Task.CompletedTask;
    }
}
