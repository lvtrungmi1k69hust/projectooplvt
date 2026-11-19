using MiniNecesse.App.Input;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.App.Settings;

/// <summary>
/// Application-level user preferences shared across menus, settings dialogs, and gameplay.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Gets or sets the selected gameplay difficulty.</summary>
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;

    /// <summary>Gets or sets whether audio output is muted.</summary>
    public bool IsMuted { get; set; }

    /// <summary>Gets or sets audio volume in [0, 100].</summary>
    public int Volume { get; set; } = 80;

    /// <summary>Gets or sets the default player name for high-score submissions.</summary>
    public string PlayerName { get; set; } = "Survivor";

    /// <summary>Gets the active keyboard binding configuration.</summary>
    public KeyBindingConfig KeyBindings { get; } = KeyBindingConfig.LoadOrDefault();
}
