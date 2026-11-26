using System;
using System.Windows.Forms;
using MiniNecesse.App.Audio;
using MiniNecesse.App.Forms;
using MiniNecesse.App.Settings;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Data;

namespace MiniNecesse.App;

/// <summary>
/// Application Composition Root wiring Core interfaces to Data and WinForms Presentation via Constructor DI.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        ILevelLoader levelLoader = new JsonLevelLoader();
        ISaveStorage saveStorage = new JsonSaveStorage();
        IHighScoreRepository highScoreRepository = new JsonHighScoreRepository();
        var settings = AppSettings.LoadOrDefault();
        using var audioManager = new AudioManager
        {
            IsMuted = settings.IsMuted,
            Volume = settings.Volume
        };

        using var mainMenu = new FrmMainMenu(
            levelLoader,
            saveStorage,
            highScoreRepository,
            audioManager,
            settings,
            gameplayLauncher: (mode, difficulty, islandId, worldSeed, loadSavedSlot) => new FrmGamePlay(
                levelLoader,
                saveStorage,
                highScoreRepository,
                audioManager,
                settings,
                islandId,
                loadSavedSlot,
                mode,
                difficulty,
                worldSeed));

        Application.Run(mainMenu);
    }
}
