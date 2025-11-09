using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Manages keyboard shortcuts and their handlers
/// </summary>
public class KeyboardShortcutManager : IKeyboardShortcutManager
{
    private readonly Dictionary<string, Action> _handlers = new();
    private Dictionary<string, string> _shortcuts;

    public KeyboardShortcutManager()
    {
        _shortcuts = AppSettings.GetDefaultKeyboardShortcuts();
    }

    public void RegisterShortcut(string action, Action handler)
    {
        _handlers[action] = handler;
    }

    public void UnregisterShortcut(string action)
    {
        _handlers.Remove(action);
    }

    public bool HandleKeyEvent(KeyEventArgs e)
    {
        var keyGesture = GetKeyGesture(e);
        if (string.IsNullOrEmpty(keyGesture))
            return false;

        // Find matching action
        var action = _shortcuts.FirstOrDefault(kvp => 
            kvp.Value.Equals(keyGesture, StringComparison.OrdinalIgnoreCase)).Key;

        if (action != null && _handlers.TryGetValue(action, out var handler))
        {
            handler?.Invoke();
            e.Handled = true;
            return true;
        }

        return false;
    }

    public string? GetShortcutKey(string action)
    {
        return _shortcuts.TryGetValue(action, out var key) ? key : null;
    }

    public void SetShortcutKey(string action, string keyGesture)
    {
        _shortcuts[action] = keyGesture;
    }

    public Dictionary<string, string> GetAllShortcuts()
    {
        return new Dictionary<string, string>(_shortcuts);
    }

    public void ResetToDefaults()
    {
        _shortcuts = AppSettings.GetDefaultKeyboardShortcuts();
    }

    public bool HasConflict(string action, string keyGesture)
    {
        return _shortcuts.Any(kvp => 
            kvp.Key != action && 
            kvp.Value.Equals(keyGesture, StringComparison.OrdinalIgnoreCase));
    }

    public void LoadShortcuts(Dictionary<string, string> shortcuts)
    {
        _shortcuts = new Dictionary<string, string>(shortcuts);
    }

    /// <summary>
    /// Convert KeyEventArgs to a key gesture string
    /// </summary>
    private string GetKeyGesture(KeyEventArgs e)
    {
        var parts = new List<string>();

        // Add modifiers
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            parts.Add("Ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            parts.Add("Shift");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            parts.Add("Alt");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            parts.Add("Win");

        // Add key
        var key = GetKeyString(e.Key);
        if (!string.IsNullOrEmpty(key))
            parts.Add(key);

        return parts.Count > 0 ? string.Join("+", parts) : string.Empty;
    }

    /// <summary>
    /// Convert Key enum to string representation
    /// </summary>
    private string GetKeyString(Key key)
    {
        return key switch
        {
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Escape => "Escape",
            Key.Tab => "Tab",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.F1 => "F1",
            Key.F2 => "F2",
            Key.F3 => "F3",
            Key.F4 => "F4",
            Key.F5 => "F5",
            Key.F6 => "F6",
            Key.F7 => "F7",
            Key.F8 => "F8",
            Key.F9 => "F9",
            Key.F10 => "F10",
            Key.F11 => "F11",
            Key.F12 => "F12",
            Key.OemComma => "Comma",
            Key.OemPeriod => "Period",
            Key.OemPlus => "Plus",
            Key.OemMinus => "Minus",
            >= Key.A and <= Key.Z => key.ToString(),
            >= Key.D0 and <= Key.D9 => key.ToString().Replace("D", ""),
            >= Key.NumPad0 and <= Key.NumPad9 => "NumPad" + key.ToString().Replace("NumPad", ""),
            _ => string.Empty
        };
    }
}
