using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MiniNecesse.App.Audio;
using MiniNecesse.App.Input;
using MiniNecesse.App.Rendering;
using MiniNecesse.App.Settings;
using MiniNecesse.App.Views;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Exceptions;
using MiniNecesse.Core.Farming;
using MiniNecesse.Core.Interfaces;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Patterns.Factory;
using MiniNecesse.Core.Rules;
using MiniNecesse.Data;

namespace MiniNecesse.App.Forms;

/// <summary>
/// Double-buffered WinForms 2D top-down gameplay view.
/// Runs a Stopwatch-driven Fixed Timestep loop (16.67ms), observes Core events via <see cref="HudViewModel"/>,
/// hosts Crafting, Pause, and Victory/GameOver overlays, and contains zero domain formulas.
/// </summary>
public sealed class FrmGamePlay : Form
{
    private const int DEFAULT_SAVE_SLOT = 1;

    private readonly ILevelLoader _levelLoader;
    private readonly ISaveStorage _saveStorage;
    private readonly IHighScoreRepository _highScoreRepository;
    private readonly AudioManager _audioManager;
    private readonly AppSettings _settings;

    private readonly WinFormsInputAdapter _inputAdapter;
    private readonly TopDownRenderer _renderer = new();
    private readonly HudViewModel _hud = new();
    private readonly Stopwatch _frameStopwatch = new();
    private readonly System.Windows.Forms.Timer _renderTimer;
    private readonly DoubleBufferedCanvas _canvas;
    private readonly CraftingOverlayPanel _craftingPanel;
    private readonly Panel _pausePanel;
    private readonly Panel _outcomePanel;
    private readonly Label _lblOutcomeTitle;
    private readonly Label _lblOutcomeSummary;
    private readonly TextBox _txtHighScoreName;
    private readonly Button _btnSaveHighScore;
    private readonly Button _btnNextIsland;
    private readonly Button _btnPlayAgain;
    private readonly Button _btnOutcomeMenu;
    private readonly Button _btnPauseResume;
    private readonly Button _btnPauseSave;
    private readonly Button _btnPauseLoad;
    private readonly Button _btnPauseSettings;
    private readonly Button _btnPauseMenu;
    private readonly List<Font> _ownedFonts = new();

    private readonly GameConfig _config;
    private readonly IReadOnlyList<CraftingRecipe> _recipes;
    private readonly IReadOnlyList<string> _availableIslands;
    private readonly CropRegistry _cropRegistry;
    private readonly ItemFactory _itemFactory;

    private EnemyFactory? _enemyFactory;
    private LoadedIslandPackage? _activePackage;
    private GameSession? _session;
    private GameLoopController? _loopController;
    private Vector2D _cameraOffset;
    private float _renderInterpolationAlpha = 1f;
    private string _currentIslandId;
    private bool _highScoreRecordedForRun;

    /// <summary>Initializes a new instance of the <see cref="FrmGamePlay"/> class via Constructor DI.</summary>
    public FrmGamePlay(
        ILevelLoader levelLoader,
        ISaveStorage saveStorage,
        IHighScoreRepository highScoreRepository,
        AudioManager audioManager,
        AppSettings settings,
        string startingIslandId,
        bool loadSavedSlot)
    {
        ArgumentNullException.ThrowIfNull(levelLoader);
        ArgumentNullException.ThrowIfNull(saveStorage);
        ArgumentNullException.ThrowIfNull(highScoreRepository);
        ArgumentNullException.ThrowIfNull(audioManager);
        ArgumentNullException.ThrowIfNull(settings);

        _levelLoader = levelLoader;
        _saveStorage = saveStorage;
        _highScoreRepository = highScoreRepository;
        _audioManager = audioManager;
        _settings = settings;
        _currentIslandId = startingIslandId;
        _inputAdapter = new WinFormsInputAdapter(settings.KeyBindings);

        _config = _levelLoader.LoadConfig();
        _recipes = _levelLoader.LoadRecipes();
        _availableIslands = _levelLoader.GetAvailableIslandIds();
        _itemFactory = ItemFactory.CreateDefault();
        _cropRegistry = CropRegistry.CreateDefault();
        if (_levelLoader is JsonLevelLoader jsonLoader)
        {
            IReadOnlyList<FoodDefinition> foods = jsonLoader.LoadFoods();
            if (foods.Count > 0)
            {
                _itemFactory.RegisterFoodDefinitions(foods);
            }

            IReadOnlyList<CropDefinition> crops = jsonLoader.LoadCrops();
            if (crops.Count > 0)
            {
                _cropRegistry.RegisterRange(crops);
                _itemFactory.RegisterCropDefinitions(crops);
            }
        }

        SuspendLayout();
        Text = "MiniNecesse — Survival Island";
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(14, 20, 32);

        _canvas = new DoubleBufferedCanvas { Dock = DockStyle.Fill };
        Controls.Add(_canvas);

        _craftingPanel = new CraftingOverlayPanel();
        Controls.Add(_craftingPanel);
        _craftingPanel.BringToFront();

        (_pausePanel, _btnPauseResume, _btnPauseSave, _btnPauseLoad, _btnPauseSettings, _btnPauseMenu) = BuildPauseOverlay();
        Controls.Add(_pausePanel);
        _pausePanel.BringToFront();

        (_outcomePanel, _lblOutcomeTitle, _lblOutcomeSummary, _txtHighScoreName, _btnSaveHighScore, _btnNextIsland, _btnPlayAgain, _btnOutcomeMenu) = BuildOutcomeOverlay();
        Controls.Add(_outcomePanel);
        _outcomePanel.BringToFront();

        ClientSize = new Size(1024, 680);
        ResumeLayout(false);

        _renderTimer = new System.Windows.Forms.Timer { Interval = 16 };
        SubscribeUiEvents();

        if (loadSavedSlot && _saveStorage.Exists(DEFAULT_SAVE_SLOT))
        {
            LoadFromSaveSlot(DEFAULT_SAVE_SLOT);
        }
        else
        {
            InitializeNewIslandSession(_currentIslandId);
        }

        CenterOverlays();
        _frameStopwatch.Start();
        _renderTimer.Start();
    }

    /// <inheritdoc />
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        CenterOverlays();
    }

    /// <inheritdoc />
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _renderTimer.Stop();
        _frameStopwatch.Stop();
        UnsubscribeUiEvents();
        DetachAndDisposeCurrentSession();
        _renderTimer.Dispose();
        _renderer.Dispose();
        base.OnFormClosing(e);
    }

    private void SubscribeUiEvents()
    {
        _renderTimer.Tick += HandleRenderTimerTick;
        _canvas.Paint += HandleCanvasPaint;
        _canvas.MouseDown += HandleCanvasMouseDown;
        _canvas.MouseUp += HandleCanvasMouseUp;
        _canvas.MouseMove += HandleCanvasMouseMove;
        _canvas.MouseWheel += HandleCanvasMouseWheel;

        KeyDown += HandleFormKeyDown;
        KeyUp += HandleFormKeyUp;
        Deactivate += HandleFormDeactivate;

        _hud.OnViewInventoryInvalidated += HandleHudInventoryInvalidated;
        _hud.OnViewGameStateChanged += HandleHudGameStateChanged;

        _craftingPanel.OnCloseRequested += HandleCloseCraftingRequested;
        _btnPauseResume.Click += HandlePauseResumeClick;
        _btnPauseSave.Click += HandlePauseSaveClick;
        _btnPauseLoad.Click += HandlePauseLoadClick;
        _btnPauseSettings.Click += HandlePauseSettingsClick;
        _btnPauseMenu.Click += HandleReturnMenuClick;

        _btnSaveHighScore.Click += HandleSaveHighScoreClick;
        _btnNextIsland.Click += HandleNextIslandClick;
        _btnPlayAgain.Click += HandlePlayAgainClick;
        _btnOutcomeMenu.Click += HandleReturnMenuClick;
    }

    private void UnsubscribeUiEvents()
    {
        _renderTimer.Tick -= HandleRenderTimerTick;
        _canvas.Paint -= HandleCanvasPaint;
        _canvas.MouseDown -= HandleCanvasMouseDown;
        _canvas.MouseUp -= HandleCanvasMouseUp;
        _canvas.MouseMove -= HandleCanvasMouseMove;
        _canvas.MouseWheel -= HandleCanvasMouseWheel;

        KeyDown -= HandleFormKeyDown;
        KeyUp -= HandleFormKeyUp;
        Deactivate -= HandleFormDeactivate;

        _hud.OnViewInventoryInvalidated -= HandleHudInventoryInvalidated;
        _hud.OnViewGameStateChanged -= HandleHudGameStateChanged;

        _craftingPanel.OnCloseRequested -= HandleCloseCraftingRequested;
        _btnPauseResume.Click -= HandlePauseResumeClick;
        _btnPauseSave.Click -= HandlePauseSaveClick;
        _btnPauseLoad.Click -= HandlePauseLoadClick;
        _btnPauseSettings.Click -= HandlePauseSettingsClick;
        _btnPauseMenu.Click -= HandleReturnMenuClick;

        _btnSaveHighScore.Click -= HandleSaveHighScoreClick;
        _btnNextIsland.Click -= HandleNextIslandClick;
        _btnPlayAgain.Click -= HandlePlayAgainClick;
        _btnOutcomeMenu.Click -= HandleReturnMenuClick;
    }

    private void InitializeNewIslandSession(string islandId)
    {
        try
        {
            LoadedIslandPackage package = _levelLoader.LoadIsland(islandId);
            BuildSessionFromPackage(package, seedStarterInventory: true);
        }
        catch (InvalidLevelDataException ex)
        {
            MessageBox.Show(this, $"Failed to load island '{islandId}': {ex.Message}", "Level Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private void LoadFromSaveSlot(int slotIndex)
    {
        try
        {
            GameStateDTO dto = _saveStorage.Load(slotIndex);
            LoadedIslandPackage package = _levelLoader.LoadIsland(dto.IslandId);
            BuildSessionFromPackage(package, seedStarterInventory: false);
            _session!.RestoreFromSnapshot(dto, _enemyFactory!);
            HideAllOverlays();
            _session.Start();
        }
        catch (Exception ex) when (ex is SaveCorruptedException or InvalidLevelDataException)
        {
            MessageBox.Show(this, $"Could not load save slot {slotIndex}: {ex.Message}", "Load Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (_session is null)
            {
                InitializeNewIslandSession(_currentIslandId);
            }
        }
    }

    private void BuildSessionFromPackage(LoadedIslandPackage package, bool seedStarterInventory)
    {
        DetachAndDisposeCurrentSession();

        _activePackage = package;
        _currentIslandId = package.Level.IslandId;
        _highScoreRecordedForRun = false;

        var inventory = new Inventory(_config.InventorySlots);
        var player = new Player(
            "player_1",
            package.Level.PlayerSpawnWorldPosition,
            _config.PlayerBaseHp,
            _config.PlayerBaseSpeed,
            _config.HotbarSlots,
            inventory,
            _config.PlayerMaxHunger,
            _config.HungerDrainPerSecond,
            _config.StarvationDamageIntervalSeconds,
            _config.StarvationDamageAmount);

        int viewDistance = Math.Clamp(
            _settings.ViewDistanceChunks,
            _config.MinViewDistanceChunks,
            _config.MaxViewDistanceChunks);

        var dayNight = new DayNightCycle(_config.DayDurationSeconds, _config.NightDurationSeconds);
        var session = new GameSession(
            package.Level,
            player,
            _itemFactory.ResolveItem,
            dayNight,
            difficulty: _settings.Difficulty,
            viewDistanceChunks: viewDistance,
            minViewDistanceChunks: _config.MinViewDistanceChunks,
            maxViewDistanceChunks: _config.MaxViewDistanceChunks,
            cropRegistry: _cropRegistry);

        EnemyFactory enemyFactory = EnemyFactory.CreateDefault(
            projectileSpawner: (origin, velocity, dmg, ownerId) =>
                session.SpawnPooledProjectile(origin, velocity, dmg, ownerId, isFromPlayer: false),
            structureLookup: session.FindStructureAtTile,
            pathCoordinator: session.PathfindingCoordinator);

        session.SetVillages(package.Villages);
        session.ConfigureChunkWorldSpawners(
            package.ChunkResourceGenerator,
            enemyFactory.CreateResourceNode,
            package.ChunkStructureGenerator,
            (type, col, row) => enemyFactory.CreateStructure(type, col, row, package.Level.TileSize),
            package.ChunkAnimalGenerator,
            enemyFactory.CreateAnimal);

        var spawner = new RaidWaveSpawner(
            package.Level,
            _settings.Difficulty,
            package.Waves,
            enemyFactory.CreateEnemy,
            seed: package.Seed);
        session.AttachWaveSpawner(spawner);

        if (seedStarterInventory)
        {
            SeedStarterResourcesAndGear(session, package, enemyFactory);
            session.InitializeChunksAroundPlayer();
        }

        _enemyFactory = enemyFactory;
        _session = session;
        _session.AttachHudObserver(_hud);
        _audioManager.AttachToSession(_session);
        _renderer.ResetForNewIsland();
        _craftingPanel.Bind(_session, _recipes, _config.WorkbenchInteractRadius, _renderer.Sprites);
        _loopController = new GameLoopController(RunFixedSimulationStep, _config.FixedDeltaTime);

        HideAllOverlays();
        _session.Start();
        Text = $"MiniNecesse — {package.Level.Name}";
    }

    private void SeedStarterResourcesAndGear(GameSession session, LoadedIslandPackage package, IEntityFactory factory)
    {
        session.Inventory.TryAddItem(_itemFactory.ResolveItem("wood_axe"), 1);
        session.Inventory.TryAddItem(_itemFactory.ResolveItem("wood_pickaxe"), 1);
        session.Inventory.TryAddItem(_itemFactory.ResolveItem("copper_sword"), 1);
        session.Inventory.TryAddItem(_itemFactory.ResolveItem("health_potion"), 2);
        session.Inventory.TryAddItem(_itemFactory.ResolveItem("wooden_wall"), 6);
        session.Inventory.TryAddItem(_itemFactory.ResolveItem("wood_hoe"), 1);
        session.Inventory.TryAddItem(_itemFactory.ResolveItem("wheat_seed"), 4);
        session.Player.SelectHotbarSlot(0);

        for (int i = 0; i < package.ResourceSpawns.Count; i++)
        {
            ResourceSpawnPoint pt = package.ResourceSpawns[i];
            Vector2D worldPos = new(pt.TileCol * package.Level.TileSize, pt.TileRow * package.Level.TileSize);
            session.AddResource(factory.CreateResourceNode(pt.ResourceType, worldPos));
        }
    }

    private void DetachAndDisposeCurrentSession()
    {
        if (_session is null) return;

        _audioManager.DetachFromSession();
        _session.DetachHudObserver(_hud);
        _session.Dispose();
        _session = null;
    }

    private void HandleRenderTimerTick(object? sender, EventArgs e)
    {
        if (_session is null || _loopController is null) return;

        float frameDt = (float)_frameStopwatch.Elapsed.TotalSeconds;
        _frameStopwatch.Restart();

        ProcessModalToggleInputs();
        _loopController.StepFrame(frameDt);
        _renderInterpolationAlpha = _session.CurrentState == GameState.Playing
            ? _loopController.InterpolationAlpha
            : 1f;
        _renderer.UpdateVisuals(_session, frameDt);
        _cameraOffset = _renderer.ComputeCameraOffset(_session, _canvas.ClientSize, _renderInterpolationAlpha);
        _inputAdapter.UpdateCameraOffset(_cameraOffset);
        _canvas.Invalidate();
    }

    private void ProcessModalToggleInputs()
    {
        if (_session is null) return;

        if (_inputAdapter.IsPausePressed())
        {
            if (_craftingPanel.Visible)
            {
                CloseCraftingPanel();
            }
            else if (_session.CurrentState == GameState.Playing)
            {
                _session.Pause();
                _inputAdapter.ClearAllInputs();
                _pausePanel.Visible = true;
            }
            else if (_session.CurrentState == GameState.Paused)
            {
                _pausePanel.Visible = false;
                _session.Resume();
            }
        }

        if (_inputAdapter.IsInventoryTogglePressed() && _session.CurrentState == GameState.Playing)
        {
            ToggleCraftingPanel();
        }
    }

    private void RunFixedSimulationStep(float fixedDt)
    {
        if (_session is null || _enemyFactory is null || _activePackage is null) return;

        if (_session.CurrentState == GameState.Playing)
        {
            _session.CapturePreviousPositions();
        }

        if (_session.CurrentState == GameState.Playing && !_craftingPanel.Visible)
        {
            int? hotbar = _inputAdapter.GetSelectedHotbarSlot();
            if (hotbar.HasValue)
            {
                _session.Player.SelectHotbarSlot(hotbar.Value);
            }

            _session.MovePlayer(_inputAdapter.GetMovementAxis(), fixedDt);

            Vector2D aimWorld = _inputAdapter.GetAimWorldPosition();
            if (_inputAdapter.IsPrimaryActionPressed())
            {
                _session.ExecutePrimaryAction(aimWorld);
            }

            if (_inputAdapter.IsSecondaryActionPressed())
            {
                _session.ExecuteSecondaryAction(aimWorld, _enemyFactory);
            }

            if (_inputAdapter.IsInteractPressed())
            {
                _session.TrySummonIslandBoss(_activePackage.Boss, _enemyFactory);
            }
        }

        _session.Update(fixedDt);
    }

    private void HandleCanvasPaint(object? sender, PaintEventArgs e)
    {
        if (_session is null) return;
        _renderer.Render(
            e.Graphics,
            _canvas.ClientSize,
            _session,
            _hud,
            _cameraOffset,
            _inputAdapter.GetAimWorldPosition(),
            _renderInterpolationAlpha);
    }

    private void HandleHudInventoryInvalidated()
    {
        if (_craftingPanel.Visible)
        {
            _craftingPanel.RefreshView();
        }
    }

    private void HandleHudGameStateChanged(GameState newState)
    {
        if (newState != GameState.Victory && newState != GameState.GameOver)
        {
            return;
        }

        _inputAdapter.ClearAllInputs();
        _craftingPanel.Visible = false;
        _pausePanel.Visible = false;

        bool victory = newState == GameState.Victory;
        _lblOutcomeTitle.Text = victory ? "ISLAND VICTORY!" : "GAME OVER";
        _lblOutcomeTitle.ForeColor = victory ? Color.FromArgb(118, 236, 146) : Color.FromArgb(244, 96, 102);
        _lblOutcomeSummary.Text = $"Island: {_currentIslandId}   |   Final Score: {_hud.Score}";
        _txtHighScoreName.Text = _settings.PlayerName;
        _btnSaveHighScore.Enabled = !_highScoreRecordedForRun;
        _btnNextIsland.Visible = victory && TryGetNextIslandId(out _);
        _outcomePanel.Visible = true;
    }

    private bool TryGetNextIslandId(out string nextIslandId)
    {
        nextIslandId = string.Empty;
        for (int i = 0; i < _availableIslands.Count - 1; i++)
        {
            if (string.Equals(_availableIslands[i], _currentIslandId, StringComparison.OrdinalIgnoreCase))
            {
                nextIslandId = _availableIslands[i + 1];
                return true;
            }
        }

        return false;
    }

    private void ToggleCraftingPanel()
    {
        if (_craftingPanel.Visible)
        {
            CloseCraftingPanel();
        }
        else
        {
            _inputAdapter.ClearAllInputs();
            _craftingPanel.RefreshView();
            _craftingPanel.Visible = true;
        }
    }

    private void CloseCraftingPanel()
    {
        _craftingPanel.Visible = false;
        _canvas.Focus();
    }

    private void HideAllOverlays()
    {
        _craftingPanel.Visible = false;
        _pausePanel.Visible = false;
        _outcomePanel.Visible = false;
    }

    private void CenterOverlays()
    {
        CenterControl(_craftingPanel);
        CenterControl(_pausePanel);
        CenterControl(_outcomePanel);
    }

    private void CenterControl(Control? control)
    {
        if (control is null)
        {
            return;
        }

        control.Location = new Point(
            Math.Max(0, (ClientSize.Width - control.Width) / 2),
            Math.Max(0, (ClientSize.Height - control.Height) / 2));
    }

    private void HandleFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (_outcomePanel.Visible) return;
        _inputAdapter.HandleKeyDown(e.KeyCode);
    }

    private void HandleFormKeyUp(object? sender, KeyEventArgs e) => _inputAdapter.HandleKeyUp(e.KeyCode);
    private void HandleFormDeactivate(object? sender, EventArgs e) => _inputAdapter.ClearAllInputs();
    private void HandleCanvasMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _renderer.TryHitTestHotbarSlot(e.Location, _canvas.ClientSize, out int hotbarSlot))
        {
            _inputAdapter.QueueHotbarSlot(hotbarSlot);
            return;
        }

        _inputAdapter.HandleMouseDown(e.Button, e.Location);
    }

    private void HandleCanvasMouseUp(object? sender, MouseEventArgs e) => _inputAdapter.HandleMouseUp(e.Button);
    private void HandleCanvasMouseMove(object? sender, MouseEventArgs e) => _inputAdapter.HandleMouseMove(e.Location);

    private void HandleCanvasMouseWheel(object? sender, MouseEventArgs e)
    {
        if (_session is null || _craftingPanel.Visible || _pausePanel.Visible || _outcomePanel.Visible)
        {
            return;
        }

        int slots = _session.Player.HotbarCapacity;
        int step = e.Delta > 0 ? -1 : 1;
        int nextSlot = (_session.Player.SelectedHotbarIndex + step + slots) % slots;
        _inputAdapter.QueueHotbarSlot(nextSlot);
    }
    private void HandleCloseCraftingRequested() => CloseCraftingPanel();

    private void HandlePauseResumeClick(object? sender, EventArgs e)
    {
        _pausePanel.Visible = false;
        _session?.Resume();
        _canvas.Focus();
    }

    private void HandlePauseSaveClick(object? sender, EventArgs e)
    {
        if (_session is null) return;
        try
        {
            _saveStorage.Save(DEFAULT_SAVE_SLOT, _session.CreateSnapshot());
            MessageBox.Show(this, "Game saved successfully to save_slot_1.json!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (SaveCorruptedException ex)
        {
            MessageBox.Show(this, $"Save failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void HandlePauseLoadClick(object? sender, EventArgs e) => LoadFromSaveSlot(DEFAULT_SAVE_SLOT);

    private void HandlePauseSettingsClick(object? sender, EventArgs e)
    {
        using var dialog = new FrmSettingsDialog(_settings, _audioManager);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _session?.SetViewDistanceChunks(_settings.ViewDistanceChunks);
        }
    }

    private void HandleSaveHighScoreClick(object? sender, EventArgs e)
    {
        if (_highScoreRecordedForRun) return;
        string playerName = string.IsNullOrWhiteSpace(_txtHighScoreName.Text) ? "Survivor" : _txtHighScoreName.Text.Trim();
        _settings.PlayerName = playerName;

        try
        {
            _highScoreRepository.RecordScore(new HighScoreEntry(
                playerName,
                _hud.Score,
                _currentIslandId,
                _settings.Difficulty.ToString(),
                DateTime.UtcNow));

            _highScoreRecordedForRun = true;
            _btnSaveHighScore.Enabled = false;
            MessageBox.Show(this, "Score recorded to highscore.json!", "High Score Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (SaveCorruptedException ex)
        {
            MessageBox.Show(this, $"Could not save high score: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void HandleNextIslandClick(object? sender, EventArgs e)
    {
        if (TryGetNextIslandId(out string nextId))
        {
            InitializeNewIslandSession(nextId);
        }
    }

    private void HandlePlayAgainClick(object? sender, EventArgs e) => InitializeNewIslandSession(_currentIslandId);
    private void HandleReturnMenuClick(object? sender, EventArgs e) => Close();

    private (Panel Panel, Button Resume, Button Save, Button Load, Button Settings, Button Menu) BuildPauseOverlay()
    {
        var panel = new Panel
        {
            Size = new Size(320, 340),
            BackColor = Color.FromArgb(20, 26, 38),
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false
        };

        panel.Controls.Add(new Label
        {
            Text = "GAME PAUSED",
            Location = new Point(0, 18),
            Size = new Size(318, 34),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = CreateOwnedFont("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = Color.FromArgb(246, 196, 82)
        });

        Button btnResume = MakeOverlayButton("Resume", 68, Color.FromArgb(46, 156, 98));
        Button btnSave = MakeOverlayButton("Save Game (Slot 1)", 118, Color.FromArgb(48, 118, 190));
        Button btnLoad = MakeOverlayButton("Load Game (Slot 1)", 168, Color.FromArgb(88, 108, 154));
        Button btnSettings = MakeOverlayButton("Settings", 218, Color.FromArgb(98, 92, 118));
        Button btnMenu = MakeOverlayButton("Return to Main Menu", 268, Color.FromArgb(164, 58, 58));

        panel.Controls.AddRange(new Control[] { btnResume, btnSave, btnLoad, btnSettings, btnMenu });
        return (panel, btnResume, btnSave, btnLoad, btnSettings, btnMenu);
    }

    private (Panel Panel, Label Title, Label Summary, TextBox NameBox, Button SaveScore, Button NextIsland, Button Replay, Button Menu) BuildOutcomeOverlay()
    {
        var panel = new Panel
        {
            Size = new Size(380, 360),
            BackColor = Color.FromArgb(20, 26, 38),
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false
        };

        var title = new Label
        {
            Text = "VICTORY",
            Location = new Point(0, 18),
            Size = new Size(378, 38),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = CreateOwnedFont("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.FromArgb(118, 236, 146)
        };

        var summary = new Label
        {
            Text = "Score: 0",
            Location = new Point(20, 62),
            Size = new Size(340, 26),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = CreateOwnedFont("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.White
        };

        var nameBox = new TextBox
        {
            Location = new Point(40, 100),
            Size = new Size(185, 28),
            BackColor = Color.FromArgb(34, 42, 60),
            ForeColor = Color.White,
            Text = "Survivor"
        };

        var btnSaveScore = new Button
        {
            Text = "Save Score",
            Location = new Point(234, 98),
            Size = new Size(106, 30),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(168, 122, 42),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };

        Button btnNext = MakeOverlayButton("Next Island", 148, Color.FromArgb(46, 156, 98), 380);
        Button btnReplay = MakeOverlayButton("Play Again", 200, Color.FromArgb(48, 118, 190), 380);
        Button btnMenu = MakeOverlayButton("Return to Main Menu", 252, Color.FromArgb(164, 58, 58), 380);

        panel.Controls.AddRange(new Control[] { title, summary, nameBox, btnSaveScore, btnNext, btnReplay, btnMenu });
        return (panel, title, summary, nameBox, btnSaveScore, btnNext, btnReplay, btnMenu);
    }

    private Button MakeOverlayButton(string text, int topY, Color backColor, int parentWidth = 320)
    {
        const int WIDTH = 240;
        return new Button
        {
            Text = text,
            Location = new Point((parentWidth - WIDTH) / 2, topY),
            Size = new Size(WIDTH, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Font = CreateOwnedFont("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            for (int i = 0; i < _ownedFonts.Count; i++)
            {
                _ownedFonts[i].Dispose();
            }

            _ownedFonts.Clear();
        }
    }

    private Font CreateOwnedFont(string family, float size, FontStyle style = FontStyle.Regular)
    {
        var font = new Font(family, size, style);
        _ownedFonts.Add(font);
        return font;
    }

    private sealed class DoubleBufferedCanvas : Control
    {
        public DoubleBufferedCanvas()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            UpdateStyles();
        }
    }
}
