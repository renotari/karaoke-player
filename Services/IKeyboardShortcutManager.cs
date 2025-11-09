using System;
using System.Collections.Generic;
using Avalonia.Input;

namespace KaraokePlayer.Services;

/// <summary>
/// Interface for managing keyboard shortcuts
/// </summary>
public interface IKeyboardShortcutManager
{
    /// <summary>
    /// Register a keyboard shortcut handler
    /// </summary>
    void RegisterShortcut(string action, Action handler);

    /// <summary>
    /// Unregister a keyboard shortcut handler
    /// </summary>
    void UnregisterShortcut(string action);

    /// <summary>
    /// Handle a key event
    /// </summary>
    bool HandleKeyEvent(KeyEventArgs e);

    /// <summary>
    /// Get the shortcut key for an action
    /// </summary>
    string? GetShortcutKey(string action);

    /// <summary>
    /// Set a custom shortcut key for an action
    /// </summary>
    void SetShortcutKey(string action, string keyGesture);

    /// <summary>
    /// Get all registered shortcuts
    /// </summary>
    Dictionary<string, string> GetAllShortcuts();

    /// <summary>
    /// Reset shortcuts to defaults
    /// </summary>
    void ResetToDefaults();

    /// <summary>
    /// Check if a key gesture conflicts with existing shortcuts
    /// </summary>
    bool HasConflict(string action, string keyGesture);

    /// <summary>
    /// Load shortcuts from settings
    /// </summary>
    void LoadShortcuts(Dictionary<string, string> shortcuts);
}
