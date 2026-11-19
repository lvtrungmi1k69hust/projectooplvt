using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace MiniNecesse.App.Input;

/// <summary>
/// Configurable keyboard binding profile loaded from and saved to relative JSON storage.
/// </summary>
public sealed class KeyBindingConfig
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Gets or sets the key for moving up.</summary>
    public Keys MoveUp { get; set; } = Keys.W;

    /// <summary>Gets or sets the key for moving down.</summary>
    public Keys MoveDown { get; set; } = Keys.S;

    /// <summary>Gets or sets the key for moving left.</summary>
    public Keys MoveLeft { get; set; } = Keys.A;

    /// <summary>Gets or sets the key for moving right.</summary>
    public Keys MoveRight { get; set; } = Keys.D;

    /// <summary>Gets or sets the key toggling the crafting panel.</summary>
    public Keys ToggleCrafting { get; set; } = Keys.C;

    /// <summary>Gets or sets the key triggering interaction / boss summoning.</summary>
    public Keys Interact { get; set; } = Keys.E;

    /// <summary>Gets or sets the key toggling the pause overlay.</summary>
    public Keys Pause { get; set; } = Keys.Escape;

    /// <summary>Gets or sets the hotbar slot 1-5 keys.</summary>
    public Keys[] HotbarKeys { get; set; } = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5 };

    /// <summary>Loads key bindings from a relative JSON file, falling back to defaults safely.</summary>
    public static KeyBindingConfig LoadOrDefault(string? filePath = null)
    {
        string resolvedPath = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(AppContext.BaseDirectory, "Saves", "keybindings.json")
            : filePath;

        if (!File.Exists(resolvedPath))
        {
            return new KeyBindingConfig();
        }

        try
        {
            string json = File.ReadAllText(resolvedPath);
            KeyBindingConfig? loaded = JsonSerializer.Deserialize<KeyBindingConfig>(json, JSON_OPTIONS);
            return loaded ?? new KeyBindingConfig();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            System.Diagnostics.Debug.WriteLine($"KeyBindingConfig fallback to defaults: {ex.Message}");
            return new KeyBindingConfig();
        }
    }

    /// <summary>Saves the current key binding profile to a relative JSON path.</summary>
    public void Save(string? filePath = null)
    {
        string resolvedPath = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(AppContext.BaseDirectory, "Saves", "keybindings.json")
            : filePath;

        string? directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(resolvedPath, JsonSerializer.Serialize(this, JSON_OPTIONS));
    }
}
