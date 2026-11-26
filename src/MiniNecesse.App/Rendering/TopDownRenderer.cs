using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using MiniNecesse.App.Views;
using MiniNecesse.Core.AI;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Farming;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Rules;
using MiniNecesse.Core.World;

namespace MiniNecesse.App.Rendering;

/// <summary>
/// Necesse-inspired 2D top-down pixel-art renderer drawing textured tilemaps, shorelines,
/// multi-layered sprites, held weapons/tools, placement previews, floating damage VFX,
/// day/night lantern lighting, minimap, HUD, boss bar, and icon hotbar using pre-cached resources.
/// Contains zero gameplay math or domain state mutations.
/// </summary>
public sealed class TopDownRenderer : IDisposable
{
    private const int HOTBAR_DISPLAY_SLOTS = 9;
    private const int HOTBAR_SLOT_SIZE = 58;
    private const int HOTBAR_GAP = 6;
    private const int HOTBAR_BOTTOM_MARGIN = 14;
    private const int MINIMAP_SIZE_PX = 192;

    private static readonly string[] SLOT_LABELS = { "1", "2", "3", "4", "5", "6", "7", "8", "9" };
    private static readonly string[] QUANTITY_LABELS = BuildQuantityLabels(100);

    private readonly RenderResourceCache _cache = new();
    private readonly SpriteAtlasCache _sprites = new();
    private readonly ChunkTerrainCache _chunkTerrainCache = new();
    private readonly NightLightmapRenderer _nightLightmap = new();
    private readonly VisualEffectsPool _vfx = new();
    private readonly HashSet<ChunkCoord> _paintedMinimapChunks = new();
    private readonly Dictionary<int, string> _extraQuantityLabels = new();

    private Bitmap? _minimapTerrainCache;
    private string _cachedMinimapIslandId = string.Empty;
    private int _lastMinimapStructureVersion = -1;

    private string _lastTitleIslandName = string.Empty;
    private Difficulty _lastTitleDifficulty = (Difficulty)(-1);
    private string _cachedTitleText = string.Empty;

    private int _lastHp = -1;
    private int _lastMaxHp = -1;
    private string _cachedHpText = "HP 0/1";

    private int _lastHunger = -1;
    private int _lastMaxHunger = -1;
    private string _cachedHungerText = "HUN 100/100";

    private string _lastBuffSummary = string.Empty;
    private string _cachedBuffLine = "Buffs: None";

    private int _lastScore = -1;
    private int _lastDay = -1;
    private DayPhase _lastDayPhase = (DayPhase)(-1);
    private string _cachedScoreDayText = string.Empty;

    private int _lastWave = -1;
    private int _lastEnemyCount = -1;
    private int _lastDefense = -1;
    private string _cachedWaveEnemiesText = string.Empty;

    private int _lastChunkX = int.MinValue;
    private int _lastChunkY = int.MinValue;
    private int _lastLoadedChunks = -1;
    private int _lastTotalChunks = -1;
    private int _lastViewDistance = -1;
    private string? _lastVillageName;
    private string _cachedChunkText = string.Empty;

    private int _lastMinimapViewDist = -1;
    private int _lastMinimapLoaded = -1;
    private int _lastMinimapTotal = -1;
    private string _cachedMinimapText = string.Empty;

    private string _lastBossName = string.Empty;
    private int _lastBossHp = -1;
    private int _lastBossMaxHp = -1;
    private string _cachedBossText = string.Empty;

    private string? _lastEquippedId;
    private string? _lastEquippedName;
    private int _lastEquippedQty = -1;
    private string _cachedEquippedText = "Equipped: Bare Hands (Empty Slot)";

    private float _animTime;
    private bool _disposed;

    /// <summary>Gets the shared pixel-art sprite atlas (also used by Crafting UI for item icons).</summary>
    public SpriteAtlasCache Sprites => _sprites;

    /// <summary>Resets cached chunk terrain, lightmap, minimap, and particle VFX when switching islands.</summary>
    public void ResetForNewIsland()
    {
        _vfx.Reset();
        _chunkTerrainCache.Clear();
        _nightLightmap.Invalidate();
        _paintedMinimapChunks.Clear();
        _minimapTerrainCache?.Dispose();
        _minimapTerrainCache = null;
        _cachedMinimapIslandId = string.Empty;
        _lastMinimapStructureVersion = -1;
        _lastTitleIslandName = string.Empty;
    }

    /// <summary>Advances presentation-only animation clocks and visual hit particles.</summary>
    public void UpdateVisuals(GameSession session, float frameDeltaTime)
    {
        _animTime += Math.Clamp(frameDeltaTime, 0f, 0.1f);
        _vfx.Update(session, frameDeltaTime);
    }

    /// <summary>Computes the camera top-left world offset centered on the interpolated player and clamped to island bounds.</summary>
    public Vector2D ComputeCameraOffset(GameSession session, Size viewportSize, float interpolationAlpha = 1f)
    {
        IslandLevel level = session.Level;
        Vector2D playerCenter = session.Player.GetInterpolatedCenter(interpolationAlpha);
        float camX = playerCenter.X - (viewportSize.Width * 0.5f);
        float camY = playerCenter.Y - (viewportSize.Height * 0.5f);

        camX = level.WorldWidth <= viewportSize.Width
            ? -((viewportSize.Width - level.WorldWidth) * 0.5f)
            : Math.Clamp(camX, 0f, level.WorldWidth - viewportSize.Width);

        camY = level.WorldHeight <= viewportSize.Height
            ? -((viewportSize.Height - level.WorldHeight) * 0.5f)
            : Math.Clamp(camY, 0f, level.WorldHeight - viewportSize.Height);

        return new Vector2D(camX, camY);
    }

    /// <summary>Renders the complete Necesse-style pixel-art scene and HUD with zero brush/pen/string allocations.</summary>
    public void Render(
        Graphics g,
        Size viewportSize,
        GameSession session,
        HudViewModel hud,
        Vector2D cameraOffset,
        Vector2D aimWorldPos,
        float interpolationAlpha = 1f)
    {
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighSpeed;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.None;
        g.Clear(Color.FromArgb(14, 24, 38));

        DrawVisibleChunks(g, viewportSize, session.Level, cameraOffset);
        DrawFarmPlots(g, viewportSize, session.Farming.Plots, session.Level.TileSize, cameraOffset);
        DrawBuildPlacementPreview(g, session, cameraOffset, aimWorldPos);
        DrawResources(g, viewportSize, session.Resources, cameraOffset);
        DrawStructures(g, viewportSize, session.Structures, cameraOffset);
        DrawDroppedItems(g, viewportSize, session.DroppedItems, cameraOffset);
        DrawAnimals(g, viewportSize, session.Animals, cameraOffset, interpolationAlpha);
        DrawSettlers(g, viewportSize, session.Settlement.Settlers, cameraOffset, interpolationAlpha);
        DrawEnemies(g, viewportSize, session.Enemies, cameraOffset, interpolationAlpha);
        DrawPlayerWithHeldItem(g, session, cameraOffset, aimWorldPos, interpolationAlpha);
        DrawProjectiles(g, viewportSize, session.Projectiles, cameraOffset, interpolationAlpha);
        _vfx.Render(g, cameraOffset, _cache);

        DrawDayNightOverlay(g, viewportSize, session, hud, cameraOffset, interpolationAlpha);
        DrawTopHud(g, session, hud);
        DrawMinimap(g, viewportSize, session, cameraOffset);
        DrawBossBar(g, viewportSize, hud);
        DrawHotbar(g, viewportSize, session);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _chunkTerrainCache.Dispose();
        _nightLightmap.Dispose();
        _minimapTerrainCache?.Dispose();
        _sprites.Dispose();
        _cache.Dispose();
        _disposed = true;
    }

    private void DrawVisibleChunks(Graphics g, Size viewport, IslandLevel level, Vector2D cam)
    {
        _chunkTerrainCache.PruneUnloaded(level);

        float tileSize = level.TileSize;
        int chunkSize = level.ChunkSize;
        int waterFrame = ((int)(_animTime * 2.2f)) & 1;

        int startCol = Math.Max(0, (int)MathF.Floor(cam.X / tileSize));
        int endCol = Math.Min(level.Width - 1, (int)MathF.Ceiling((cam.X + viewport.Width) / tileSize));
        int startRow = Math.Max(0, (int)MathF.Floor(cam.Y / tileSize));
        int endRow = Math.Min(level.Height - 1, (int)MathF.Ceiling((cam.Y + viewport.Height) / tileSize));

        if (endCol < startCol || endRow < startRow)
        {
            return;
        }

        int startChunkX = Math.Max(0, startCol / chunkSize);
        int endChunkX = Math.Min(level.ChunkColumns - 1, endCol / chunkSize);
        int startChunkY = Math.Max(0, startRow / chunkSize);
        int endChunkY = Math.Min(level.ChunkRows - 1, endRow / chunkSize);

        CompositingMode prevCompositing = g.CompositingMode;
        PixelOffsetMode prevOffset = g.PixelOffsetMode;
        g.CompositingMode = CompositingMode.SourceCopy;
        g.PixelOffsetMode = PixelOffsetMode.None;

        try
        {
            for (int cy = startChunkY; cy <= endChunkY; cy++)
            {
                int chunkTileRow = cy * chunkSize;
                int chunkTileH = Math.Min(chunkSize, level.Height - chunkTileRow);
                int iy = (int)MathF.Round((chunkTileRow * tileSize) - cam.Y);
                int chunkWorldH = (int)MathF.Round(chunkTileH * tileSize);

                for (int cx = startChunkX; cx <= endChunkX; cx++)
                {
                    int chunkTileCol = cx * chunkSize;
                    int chunkTileW = Math.Min(chunkSize, level.Width - chunkTileCol);
                    int ix = (int)MathF.Round((chunkTileCol * tileSize) - cam.X);
                    int chunkWorldW = (int)MathF.Round(chunkTileW * tileSize);

                    var coord = new ChunkCoord(cx, cy);
                    if (!level.TryGetLoadedChunk(coord, out WorldChunk? chunk) || chunk is null)
                    {
                        g.FillRectangle(_cache.UnloadedFogBrush, ix, iy, chunkWorldW, chunkWorldH);
                        g.DrawRectangle(_cache.HudBorderPen, ix, iy, chunkWorldW, chunkWorldH);
                        continue;
                    }

                    Bitmap chunkBitmap = _chunkTerrainCache.GetOrBuild(level, chunk, waterFrame, _sprites, _cache);
                    g.DrawImage(chunkBitmap, ix, iy, chunkBitmap.Width, chunkBitmap.Height);
                }
            }
        }
        finally
        {
            g.CompositingMode = prevCompositing;
            g.PixelOffsetMode = prevOffset;
        }
    }

    private void DrawFarmPlots(Graphics g, Size viewport, IReadOnlyList<FarmPlot> plots, float tileSize, Vector2D cam)
    {
        Bitmap? tilledSprite = _sprites.Get("farm_tilled");
        for (int i = 0; i < plots.Count; i++)
        {
            FarmPlot plot = plots[i];
            float sx = (plot.TileCol * tileSize) - cam.X;
            float sy = (plot.TileRow * tileSize) - cam.Y;
            if (!IsInViewport(sx, sy, tileSize, tileSize, viewport))
            {
                continue;
            }

            if (tilledSprite is not null)
            {
                g.DrawImage(tilledSprite, sx, sy, tileSize, tileSize);
            }

            if (!plot.IsPlanted)
            {
                continue;
            }

            string cropSpriteKey = ResolveCropSpriteKey(plot);
            Bitmap? cropSprite = _sprites.Get(cropSpriteKey);
            if (cropSprite is not null)
            {
                g.DrawImage(cropSprite, sx + 2, sy - 2, tileSize - 4, tileSize - 2);
            }
        }
    }

    private static string ResolveCropSpriteKey(FarmPlot plot)
    {
        if (!plot.IsMature)
        {
            if (plot.CurrentStage == 0) return "crop_stage_0";
            if (plot.CurrentStage == 1) return "crop_stage_1";
            return "crop_stage_2";
        }

        if (string.Equals(plot.CropId, "carrot_crop", StringComparison.OrdinalIgnoreCase))
        {
            return "crop_carrot_mature";
        }

        if (string.Equals(plot.CropId, "pumpkin_crop", StringComparison.OrdinalIgnoreCase))
        {
            return "crop_pumpkin_mature";
        }

        return "crop_wheat_mature";
    }

    private void DrawBuildPlacementPreview(Graphics g, GameSession session, Vector2D cam, Vector2D aimWorldPos)
    {
        ItemStack? selected = session.Player.SelectedItemStack;
        if (selected is null)
        {
            return;
        }

        bool isStructure = selected.Item.TryGetPlaceableStructure(out _);
        bool isSeed = selected.Item.TryGetPlantableCropId(out _);
        bool isHoe = session.Player.EquippedToolType == ToolType.Hoe;
        if (!isStructure && !isSeed && !isHoe)
        {
            return;
        }

        (int col, int row) = session.Level.WorldToTile(aimWorldPos);
        if (!session.Level.IsInBounds(col, row)) return;

        float tileSize = session.Level.TileSize;
        float maxReach = isHoe ? 96f : 112f;
        bool inReach = Vector2D.Distance(session.Player.Center, aimWorldPos) <= maxReach;
        bool valid;
        if (isStructure)
        {
            valid = inReach && session.Level.IsTileWalkable(col, row) && session.FindStructureAtTile(new GridPoint(col, row)) is null;
        }
        else if (isSeed)
        {
            FarmPlot? plot = session.Farming.GetPlotAt(col, row);
            valid = inReach && plot is not null && !plot.IsPlanted;
        }
        else
        {
            valid = inReach && session.Level.TryGetTile(col, row, out Tile? tile) && tile is not null &&
                    tile.IsWalkable && (tile.TileType == TileType.Grass || tile.TileType == TileType.Sand) &&
                    session.FindStructureAtTile(new GridPoint(col, row)) is null &&
                    session.Farming.GetPlotAt(col, row) is null;
        }

        float sx = (col * tileSize) - cam.X;
        float sy = (row * tileSize) - cam.Y;
        g.FillRectangle(valid ? _cache.BuildValidBrush : _cache.BuildInvalidBrush, sx, sy, tileSize, tileSize);
        g.DrawRectangle(valid ? _cache.BuildValidPen : _cache.BuildInvalidPen, sx, sy, tileSize, tileSize);
    }

    private static bool IsInViewport(float sx, float sy, float w, float h, Size viewport, float margin = 48f)
    {
        return sx + w >= -margin && sx <= viewport.Width + margin &&
               sy + h >= -margin && sy <= viewport.Height + margin;
    }

    private void DrawResources(Graphics g, Size viewport, IReadOnlyList<ResourceNode> resources, Vector2D cam)
    {
        for (int i = 0; i < resources.Count; i++)
        {
            ResourceNode node = resources[i];
            if (!node.IsAlive) continue;

            float sx = node.Position.X - cam.X;
            float sy = node.Position.Y - cam.Y;
            float w = node.Size.X;
            float h = node.Size.Y;
            if (!IsInViewport(sx, sy, w, h, viewport)) continue;

            g.FillEllipse(_cache.ShadowBrush, sx + 1, sy + h - 7, w - 2, 9);

            string spriteKey = node.ResourceType switch
            {
                ResourceType.Tree => "res_tree",
                ResourceType.CopperDeposit => "res_copper",
                ResourceType.IronDeposit => "res_iron",
                ResourceType.GoldDeposit => "res_gold",
                ResourceType.BerryBush => "res_berry_bush",
                ResourceType.WheatCrop => "res_wheat",
                ResourceType.CoalDeposit => "res_coal",
                ResourceType.CrystalCluster => "res_crystal",
                _ => "res_stone"
            };

            Bitmap? sprite = _sprites.Get(spriteKey);
            if (sprite is not null)
            {
                float drawH = node.ResourceType == ResourceType.Tree ? h * 1.32f : h;
                float drawY = node.ResourceType == ResourceType.Tree ? sy - (h * 0.30f) : sy;
                g.DrawImage(sprite, sx - 2, drawY, w + 4, drawH);
            }

            if (node.CurrentHp < node.MaxHp)
            {
                DrawMiniHealthBar(g, sx, sy - 10, w, node.CurrentHp, node.MaxHp, _cache.ResourceHpFillBrush);
            }
        }
    }

    private void DrawStructures(Graphics g, Size viewport, IReadOnlyList<Structure> structures, Vector2D cam)
    {
        int torchFrame = ((int)(_animTime * 6f)) & 1;

        for (int i = 0; i < structures.Count; i++)
        {
            Structure s = structures[i];
            if (!s.IsAlive) continue;

            float sx = s.Position.X - cam.X;
            float sy = s.Position.Y - cam.Y;
            if (!IsInViewport(sx, sy, s.Size.X, s.Size.Y, viewport)) continue;
            string key = s.StructureType switch
            {
                StructureType.StoneWall => "str_stone_wall",
                StructureType.Workbench => "str_workbench",
                StructureType.Torch => torchFrame == 0 ? "str_torch_0" : "str_torch_1",
                StructureType.WoodenFence => "str_wood_fence",
                StructureType.Chest => "str_chest",
                StructureType.WoodenDoor => "str_door",
                _ => "str_wood_wall"
            };

            Bitmap? sprite = _sprites.Get(key);
            if (sprite is not null)
            {
                g.DrawImage(sprite, sx, sy, s.Size.X, s.Size.Y);
            }

            if (s.CurrentHp < s.MaxHp)
            {
                DrawMiniHealthBar(g, sx, sy - 6, s.Size.X, s.CurrentHp, s.MaxHp, _cache.ResourceHpFillBrush);
            }
        }
    }

    private void DrawDroppedItems(Graphics g, Size viewport, IReadOnlyList<DroppedItem> drops, Vector2D cam)
    {
        float bob = MathF.Sin(_animTime * 5f) * 2.5f;

        for (int i = 0; i < drops.Count; i++)
        {
            DroppedItem d = drops[i];
            if (!d.IsActive) continue;

            float sx = d.Position.X - cam.X;
            float sy = d.Position.Y - cam.Y + bob;
            if (!IsInViewport(sx, sy, d.Size.X, d.Size.Y, viewport)) continue;
            g.FillEllipse(_cache.ShadowBrush, sx + 1, d.Position.Y - cam.Y + d.Size.Y - 3, d.Size.X - 2, 5);

            Bitmap? sprite = _sprites.Get(d.ItemId);
            if (sprite is not null)
            {
                g.DrawImage(sprite, sx - 2, sy - 2, d.Size.X + 4, d.Size.Y + 4);
            }
            else
            {
                g.FillEllipse(_cache.DropBrush, sx, sy, d.Size.X, d.Size.Y);
            }
        }
    }

    private void DrawAnimals(Graphics g, Size viewport, IReadOnlyList<Animal> animals, Vector2D cam, float alpha)
    {
        int walkFrame = ((int)(_animTime * 5f)) & 1;

        for (int i = 0; i < animals.Count; i++)
        {
            Animal a = animals[i];
            if (!a.IsAlive) continue;

            Vector2D pos = a.GetInterpolatedPosition(alpha);
            float sx = pos.X - cam.X;
            float sy = pos.Y - cam.Y;
            float w = a.Size.X;
            float h = a.Size.Y;
            if (!IsInViewport(sx, sy, w, h, viewport)) continue;

            g.FillEllipse(_cache.ShadowBrush, sx + 1, sy + h - 4, w - 2, 6);
            int frame = a.Velocity.LengthSquared > 1f ? walkFrame : 0;
            string spriteKey = a.AnimalType switch
            {
                AnimalType.Chicken => frame == 0 ? "animal_chicken_0" : "animal_chicken_1",
                AnimalType.Sheep => frame == 0 ? "animal_sheep_0" : "animal_sheep_1",
                AnimalType.Cow => frame == 0 ? "animal_cow_0" : "animal_cow_1",
                _ => frame == 0 ? "animal_pig_0" : "animal_pig_1"
            };

            Bitmap? sprite = _sprites.Get(spriteKey);
            if (sprite is not null)
            {
                g.DrawImage(sprite, sx - 2, sy - 3, w + 4, h + 4);
            }

            if (a.CurrentHp < a.MaxHp)
            {
                DrawMiniHealthBar(g, sx, sy - 7, w, a.CurrentHp, a.MaxHp, _cache.AnimalHpFillBrush);
            }
        }
    }

    private void DrawSettlers(Graphics g, Size viewport, IReadOnlyList<Settler> settlers, Vector2D cam, float alpha)
    {
        for (int i = 0; i < settlers.Count; i++)
        {
            Settler s = settlers[i];
            if (!s.IsAlive) continue;

            Vector2D pos = s.GetInterpolatedPosition(alpha);
            float sx = pos.X - cam.X;
            float sy = pos.Y - cam.Y;
            float w = s.Size.X;
            float h = s.Size.Y;
            if (!IsInViewport(sx, sy, w, h, viewport)) continue;

            g.FillEllipse(_cache.ShadowBrush, sx + 1, sy + h - 4, w - 2, 6);
            string key = s.Profession == SettlerProfession.Hunter ? "settler_hunter" : "settler_miner";
            Bitmap? sprite = _sprites.Get(key);
            if (sprite is not null)
            {
                g.DrawImage(sprite, sx - 3, sy - 5, w + 6, h + 6);
            }

            g.DrawString(s.Name, _cache.SmallFont, _cache.BuffTextBrush, sx - 4, sy - 16);
            if (s.CurrentHp < s.MaxHp)
            {
                DrawMiniHealthBar(g, sx, sy - 6, w, s.CurrentHp, s.MaxHp, _cache.AnimalHpFillBrush);
            }
        }
    }

    private void DrawEnemies(Graphics g, Size viewport, IReadOnlyList<Enemy> enemies, Vector2D cam, float alpha)
    {
        int bounceFrame = ((int)(_animTime * 4f)) & 1;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (!e.IsAlive) continue;

            Vector2D pos = e.GetInterpolatedPosition(alpha);
            float sx = pos.X - cam.X;
            float sy = pos.Y - cam.Y;
            float w = e.Size.X;
            float h = e.Size.Y;
            if (!IsInViewport(sx, sy, w, h, viewport)) continue;

            g.FillEllipse(_cache.ShadowBrush, sx + 2, sy + h - 5, w - 4, 8);
            Bitmap? sprite = ResolveEnemySprite(e, bounceFrame);
            if (sprite is not null)
            {
                bool isTanky = !e.IsBoss && (e.Archetype.Contains("Golem", StringComparison.OrdinalIgnoreCase) || e.Archetype.Contains("Orc", StringComparison.OrdinalIgnoreCase));
                float extra = isTanky ? 8f : 4f;
                g.DrawImage(sprite, sx - (extra * 0.5f), sy - extra, w + extra, h + extra);
            }

            if (e.IsBoss && e.State == EnemyState.Enraged)
            {
                g.DrawEllipse(_cache.BossAuraPen, sx - 5, sy - 5, w + 10, h + 10);
            }

            DrawMiniHealthBar(g, sx, sy - 9, w, e.CurrentHp, e.MaxHp, e.IsBoss ? _cache.BossHpFillBrush : _cache.EnemyHpFillBrush);
        }
    }

    private Bitmap? ResolveEnemySprite(Enemy enemy, int bounceFrame)
    {
        if (enemy.IsBoss)
        {
            return _sprites.Get(enemy.State == EnemyState.Enraged ? "boss_enraged" : "boss_normal");
        }

        if (enemy.Archetype.Contains("Slime", StringComparison.OrdinalIgnoreCase))
        {
            return _sprites.Get(bounceFrame == 0 ? "enemy_slime_0" : "enemy_slime_1");
        }

        if (enemy.Archetype.Contains("Archer", StringComparison.OrdinalIgnoreCase))
        {
            return _sprites.Get("enemy_archer");
        }

        if (enemy.Archetype.Contains("Frost", StringComparison.OrdinalIgnoreCase))
        {
            return _sprites.Get("enemy_frost");
        }

        if (enemy.Archetype.Contains("Golem", StringComparison.OrdinalIgnoreCase))
        {
            return _sprites.Get("enemy_golem");
        }

        if (enemy.Archetype.Contains("Orc", StringComparison.OrdinalIgnoreCase))
        {
            return _sprites.Get("enemy_orc");
        }

        if (enemy.Archetype.Contains("Mage", StringComparison.OrdinalIgnoreCase))
        {
            return _sprites.Get("enemy_mage");
        }

        return _sprites.Get("enemy_zombie");
    }

    private void DrawPlayerWithHeldItem(Graphics g, GameSession session, Vector2D cam, Vector2D aimWorldPos, float alpha)
    {
        Player player = session.Player;
        Vector2D pos = player.GetInterpolatedPosition(alpha);
        Vector2D center = pos + (player.Size * 0.5f);
        float sx = pos.X - cam.X;
        float sy = pos.Y - cam.Y;
        float cx = center.X - cam.X;
        float cy = center.Y - cam.Y;

        bool isMoving = player.Velocity.LengthSquared > 1f;
        string spriteKey = !isMoving
            ? "player_idle"
            : (((int)(_animTime * 8f)) & 1) == 0 ? "player_walk_0" : "player_walk_1";

        g.FillEllipse(_cache.ShadowBrush, sx + 2, sy + player.Size.Y - 4, player.Size.X - 4, 8);
        Bitmap? heroSprite = _sprites.Get(spriteKey);
        if (heroSprite is not null)
        {
            g.DrawImage(heroSprite, sx - 4, sy - 6, player.Size.X + 8, player.Size.Y + 8);
        }

        Vector2D lookDir = (aimWorldPos - center).Normalized();
        if (lookDir == Vector2D.Zero) lookDir = session.LastAimDirection;
        DrawHeldItemSprite(g, session, cx, cy, lookDir);

        if (session.SwingEffectRemaining > 0f)
        {
            float angleDeg = MathF.Atan2(lookDir.Y, lookDir.X) * (180f / MathF.PI);
            g.DrawArc(_cache.SwingArcPen, cx - 26f, cy - 26f, 52f, 52f, angleDeg - 44f, 88f);
        }
    }

    private void DrawHeldItemSprite(Graphics g, GameSession session, float cx, float cy, Vector2D lookDir)
    {
        ItemStack? selected = session.Player.SelectedItemStack;
        if (selected is null) return;

        Bitmap? icon = _sprites.Get(selected.Item.Id);
        if (icon is null) return;

        float swingOffset = session.SwingEffectRemaining > 0f ? 6f : 0f;
        float hx = cx + (lookDir.X * (13f + swingOffset)) - 9f;
        float hy = cy + (lookDir.Y * (13f + swingOffset)) - 9f;
        g.DrawImage(icon, hx, hy, 18f, 18f);
    }

    private void DrawProjectiles(Graphics g, Size viewport, IReadOnlyList<Projectile> projectiles, Vector2D cam, float alpha)
    {
        for (int i = 0; i < projectiles.Count; i++)
        {
            Projectile p = projectiles[i];
            if (!p.IsActive) continue;

            Vector2D pos = p.GetInterpolatedPosition(alpha);
            float sx = pos.X - cam.X;
            float sy = pos.Y - cam.Y;
            if (!IsInViewport(sx, sy, p.Size.X, p.Size.Y, viewport)) continue;
            Bitmap? sprite = _sprites.Get(p.IsFromPlayer ? "proj_player" : "proj_enemy");
            if (sprite is not null)
            {
                g.DrawImage(sprite, sx - 4, sy - 4, p.Size.X + 8, p.Size.Y + 8);
            }
            else
            {
                g.FillEllipse(p.IsFromPlayer ? _cache.PlayerProjectileBrush : _cache.EnemyProjectileBrush, sx, sy, p.Size.X, p.Size.Y);
            }
        }
    }

    private void DrawDayNightOverlay(Graphics g, Size viewport, GameSession session, HudViewModel hud, Vector2D cam, float alpha)
    {
        float darkness = hud.DayPhase == DayPhase.Night
            ? 0.84f
            : hud.PhaseProgress > 0.75f ? (hud.PhaseProgress - 0.75f) * 2.2f : 0f;

        if (darkness <= 0.02f) return;

        Vector2D playerCenter = session.Player.GetInterpolatedCenter(alpha);
        var playerScreenCenter = new Vector2D(playerCenter.X - cam.X, playerCenter.Y - cam.Y);
        _nightLightmap.Render(g, viewport, darkness, playerScreenCenter, session.Structures, cam, _animTime);
    }

    private void DrawMinimap(Graphics g, Size viewport, GameSession session, Vector2D cam)
    {
        EnsureMinimapTerrainCached(session.Level);
        if (_minimapTerrainCache is null) return;

        int mapW = _minimapTerrainCache.Width;
        int mapH = _minimapTerrainCache.Height;
        int ox = viewport.Width - mapW - 18;
        int oy = 16;

        g.FillRectangle(_cache.HudPanelBrush, ox - 4, oy - 4, mapW + 8, mapH + 22);
        g.DrawRectangle(_cache.HudBorderPen, ox - 4, oy - 4, mapW + 8, mapH + 22);
        g.DrawImage(_minimapTerrainCache, ox, oy, mapW, mapH);

        float scaleX = mapW / session.Level.WorldWidth;
        float scaleY = mapH / session.Level.WorldHeight;

        ChunkCoord centerChunk = session.ChunkManager.CurrentCenterChunk;
        int viewDist = session.ViewDistanceChunks;
        int minChunkCol = Math.Max(0, (centerChunk.ChunkX - viewDist) * session.Level.ChunkSize);
        int minChunkRow = Math.Max(0, (centerChunk.ChunkY - viewDist) * session.Level.ChunkSize);
        int maxChunkCol = Math.Min(session.Level.Width, (centerChunk.ChunkX + viewDist + 1) * session.Level.ChunkSize);
        int maxChunkRow = Math.Min(session.Level.Height, (centerChunk.ChunkY + viewDist + 1) * session.Level.ChunkSize);
        float tilePxX = (float)mapW / session.Level.Width;
        float tilePxY = (float)mapH / session.Level.Height;
        g.DrawRectangle(
            _cache.MinimapLoadedChunksPen,
            ox + (minChunkCol * tilePxX),
            oy + (minChunkRow * tilePxY),
            (maxChunkCol - minChunkCol) * tilePxX,
            (maxChunkRow - minChunkRow) * tilePxY);

        IReadOnlyList<VillageSite> villages = session.Villages;
        for (int i = 0; i < villages.Count; i++)
        {
            VillageSite v = villages[i];
            ChunkCoord vc = session.Level.GetChunkCoordForTile(v.CenterCol, v.CenterRow);
            if (!_paintedMinimapChunks.Contains(vc)) continue;

            float vxCenter = ox + ((v.CenterCol + 0.5f) * tilePxX);
            float vyCenter = oy + ((v.CenterRow + 0.5f) * tilePxY);
            g.FillRectangle(_cache.MinimapVillageBrush, vxCenter - 3f, vyCenter - 3f, 6f, 6f);
            g.DrawRectangle(_cache.DarkOutlinePen, vxCenter - 3f, vyCenter - 3f, 6f, 6f);
        }

        IReadOnlyList<Enemy> enemies = session.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (!e.IsAlive) continue;
            if (!e.IsBoss && session.Level.UsesLazyChunkLoading &&
                !session.Level.IsChunkLoaded(session.Level.GetChunkCoordForWorld(e.Center)))
            {
                continue;
            }

            SolidBrush b = e.IsBoss ? _cache.MinimapBossBrush : _cache.MinimapEnemyBrush;
            float sz = e.IsBoss ? 6f : 3.5f;
            g.FillRectangle(b, ox + (e.Center.X * scaleX) - (sz * 0.5f), oy + (e.Center.Y * scaleY) - (sz * 0.5f), sz, sz);
        }

        float px = ox + (session.Player.Center.X * scaleX);
        float py = oy + (session.Player.Center.Y * scaleY);
        g.FillEllipse(_cache.MinimapPlayerBrush, px - 3f, py - 3f, 6f, 6f);

        float vx = ox + Math.Max(0f, cam.X * scaleX);
        float vy = oy + Math.Max(0f, cam.Y * scaleY);
        float vw = Math.Min(mapW, viewport.Width * scaleX);
        float vh = Math.Min(mapH, viewport.Height * scaleY);
        g.DrawRectangle(_cache.MinimapViewportPen, vx, vy, vw, vh);
        g.DrawString(
            GetMinimapLabel(viewDist, session.Level.LoadedChunkCount, session.Level.TotalChunkCount),
            _cache.SmallFont,
            _cache.TextMutedBrush,
            ox + 2,
            oy + mapH + 2);
    }

    private void EnsureMinimapTerrainCached(IslandLevel level)
    {
        bool needsNewBitmap = _minimapTerrainCache is null || !string.Equals(_cachedMinimapIslandId, level.IslandId, StringComparison.Ordinal);
        if (needsNewBitmap)
        {
            _minimapTerrainCache?.Dispose();
            _minimapTerrainCache = new Bitmap(MINIMAP_SIZE_PX, MINIMAP_SIZE_PX, PixelFormat.Format32bppPArgb);
            _paintedMinimapChunks.Clear();
            _cachedMinimapIslandId = level.IslandId;
            _lastMinimapStructureVersion = -1;

            using Graphics initG = Graphics.FromImage(_minimapTerrainCache);
            initG.Clear(Color.FromArgb(14, 22, 34));
        }

        if (!needsNewBitmap && _lastMinimapStructureVersion == level.StructureVersion)
        {
            return;
        }

        _lastMinimapStructureVersion = level.StructureVersion;
        Graphics? mg = null;
        try
        {
            float tileW = (float)MINIMAP_SIZE_PX / level.Width;
            float tileH = (float)MINIMAP_SIZE_PX / level.Height;
            float drawW = MathF.Max(1f, MathF.Ceiling(tileW));
            float drawH = MathF.Max(1f, MathF.Ceiling(tileH));

            IReadOnlyList<ChunkCoord> loadedCoords = level.LoadedChunkCoords;
            for (int i = 0; i < loadedCoords.Count; i++)
            {
                ChunkCoord coord = loadedCoords[i];
                if (_paintedMinimapChunks.Contains(coord))
                {
                    continue;
                }

                if (!level.TryGetLoadedChunk(coord, out WorldChunk? chunk) || chunk is null)
                {
                    continue;
                }

                mg ??= Graphics.FromImage(_minimapTerrainCache!);
                for (int r = 0; r < chunk.Height; r++)
                {
                    for (int c = 0; c < chunk.Width; c++)
                    {
                        Tile tile = chunk.GetLocalTile(c, r);
                        mg.FillRectangle(
                            _cache.GetHexBrush(tile.ColorHex),
                            tile.Col * tileW,
                            tile.Row * tileH,
                            drawW,
                            drawH);
                    }
                }

                _paintedMinimapChunks.Add(coord);
            }
        }
        finally
        {
            mg?.Dispose();
        }
    }

    private void DrawTopHud(Graphics g, GameSession session, HudViewModel hud)
    {
        g.FillRectangle(_cache.HudPanelBrush, 12, 12, 344, 170);
        g.DrawRectangle(_cache.HudBorderPen, 12, 12, 344, 170);

        g.DrawString(GetTitleText(session.Level.Name, session.Difficulty), _cache.HudTitleFont, _cache.TextGoldBrush, 22, 18);

        int maxHp = Math.Max(1, hud.MaxHp);
        float hpRatio = Math.Clamp((float)hud.CurrentHp / maxHp, 0f, 1f);
        g.FillRectangle(_cache.HpBackgroundBrush, 22, 42, 206, 15);
        g.FillRectangle(_cache.HpFillBrush, 22, 42, 206f * hpRatio, 15);
        g.DrawRectangle(_cache.DarkOutlinePen, 22, 42, 206, 15);
        g.DrawString(GetHpText(hud.CurrentHp, maxHp), _cache.SmallFont, _cache.TextWhiteBrush, 234, 42);

        int curHunger = (int)MathF.Round(session.Player.CurrentHunger);
        int maxHunger = Math.Max(1, (int)MathF.Round(session.Player.MaxHunger));
        float hungerRatio = Math.Clamp(session.Player.HungerRatio, 0f, 1f);
        g.FillRectangle(_cache.HungerBackgroundBrush, 22, 62, 206, 13);
        g.FillRectangle(_cache.HungerFillBrush, 22, 62, 206f * hungerRatio, 13);
        g.DrawRectangle(_cache.DarkOutlinePen, 22, 62, 206, 13);
        g.DrawString(GetHungerText(curHunger, maxHunger), _cache.SmallFont, _cache.TextWhiteBrush, 234, 61);

        SolidBrush phaseBrush = hud.DayPhase == DayPhase.Day ? _cache.TextGreenBrush : _cache.TextGoldBrush;
        g.DrawString(GetScoreDayText(hud.Score, hud.DayNumber, hud.DayPhase), _cache.HudBodyFont, phaseBrush, 22, 82);
        g.DrawString(GetWaveEnemiesText(session.CurrentWaveIndex, session.Enemies.Count, session.Player.TotalDefense), _cache.HudBodyFont, _cache.TextWhiteBrush, 22, 101);

        ChunkCoord centerChunk = session.ChunkManager.CurrentCenterChunk;
        VillageSite? nearbyVillage = session.FindVillageAt(session.Player.Center);
        string chunkLine = GetChunkTelemetryText(
            nearbyVillage?.Name,
            centerChunk,
            session.Level.LoadedChunkCount,
            session.Level.TotalChunkCount,
            session.ViewDistanceChunks);
        g.DrawString(
            chunkLine,
            _cache.SmallFont,
            nearbyVillage is not null ? _cache.TextGoldBrush : _cache.TextMutedBrush,
            22,
            121);
        g.DrawString(
            GetBuffLine(session.Player.Buffs.PrimaryBuffSummary),
            _cache.SmallFont,
            session.Player.Buffs.ActiveBuffCount > 0 ? _cache.BuffTextBrush : _cache.TextMutedBrush,
            22,
            138);
        g.DrawString("[WASD] Move [LMB] Act [RMB] Use/Plant [C] Craft [V] Colony [E] Open/Boss [Esc] Pause", _cache.SmallFont, _cache.TextMutedBrush, 22, 155);
    }

    private void DrawBossBar(Graphics g, Size viewport, HudViewModel hud)
    {
        if (!hud.BossBarVisible) return;

        int barWidth = 340;
        int x = (viewport.Width - barWidth) / 2;
        int y = 16;

        int bossMaxHp = Math.Max(1, hud.BossMaxHp);
        g.FillRectangle(_cache.HudPanelBrush, x - 12, y - 4, barWidth + 24, 48);
        g.DrawRectangle(_cache.HudBorderPen, x - 12, y - 4, barWidth + 24, 48);
        g.DrawString(GetBossText(hud.BossName, hud.BossCurrentHp, bossMaxHp), _cache.BossTitleFont, _cache.TextGoldBrush, x, y);

        float ratio = Math.Clamp((float)hud.BossCurrentHp / bossMaxHp, 0f, 1f);
        g.FillRectangle(_cache.HpBackgroundBrush, x, y + 24, barWidth, 14);
        g.FillRectangle(_cache.BossHpFillBrush, x, y + 24, barWidth * ratio, 14);
        g.DrawRectangle(_cache.DarkOutlinePen, x, y + 24, barWidth, 14);
    }

    /// <summary>Checks whether a screen click landed on a hotbar slot (0..8) and returns that slot index.</summary>
    public bool TryHitTestHotbarSlot(Point screenPoint, Size viewport, out int slotIndex)
    {
        slotIndex = -1;
        int totalWidth = (HOTBAR_DISPLAY_SLOTS * HOTBAR_SLOT_SIZE) + ((HOTBAR_DISPLAY_SLOTS - 1) * HOTBAR_GAP);
        int startX = (viewport.Width - totalWidth) / 2;
        int startY = viewport.Height - HOTBAR_SLOT_SIZE - HOTBAR_BOTTOM_MARGIN;

        if (screenPoint.Y < startY || screenPoint.Y > startY + HOTBAR_SLOT_SIZE)
        {
            return false;
        }

        for (int i = 0; i < HOTBAR_DISPLAY_SLOTS; i++)
        {
            int x = startX + (i * (HOTBAR_SLOT_SIZE + HOTBAR_GAP));
            if (screenPoint.X >= x && screenPoint.X <= x + HOTBAR_SLOT_SIZE)
            {
                slotIndex = i;
                return true;
            }
        }

        return false;
    }

    private void DrawHotbar(Graphics g, Size viewport, GameSession session)
    {
        int totalWidth = (HOTBAR_DISPLAY_SLOTS * HOTBAR_SLOT_SIZE) + ((HOTBAR_DISPLAY_SLOTS - 1) * HOTBAR_GAP);
        int startX = (viewport.Width - totalWidth) / 2;
        int startY = viewport.Height - HOTBAR_SLOT_SIZE - HOTBAR_BOTTOM_MARGIN;

        ItemStack? selectedStack = session.Player.SelectedItemStack;
        string equippedText = GetEquippedText(selectedStack);

        g.FillRectangle(_cache.HudPanelBrush, startX, startY - 26, totalWidth, 22);
        g.DrawRectangle(_cache.HudBorderPen, startX, startY - 26, totalWidth, 22);
        g.DrawString(equippedText, _cache.SmallFont, _cache.TextGoldBrush, startX + 8, startY - 22);

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        Inventory inventory = session.Inventory;
        int selectedIndex = session.Player.SelectedHotbarIndex;
        for (int i = 0; i < HOTBAR_DISPLAY_SLOTS; i++)
        {
            int x = startX + (i * (HOTBAR_SLOT_SIZE + HOTBAR_GAP));
            bool selected = selectedIndex == i;

            g.FillRectangle(selected ? _cache.HotbarSelectedBrush : _cache.HotbarSlotBrush, x, startY, HOTBAR_SLOT_SIZE, HOTBAR_SLOT_SIZE);
            g.DrawRectangle(selected ? _cache.HotbarSelectedPen : _cache.HudBorderPen, x, startY, HOTBAR_SLOT_SIZE, HOTBAR_SLOT_SIZE);
            g.DrawString(SLOT_LABELS[i], _cache.SmallFont, _cache.TextGoldBrush, x + 4, startY + 3);

            ItemStack? stack = inventory.GetStackAt(i);
            if (stack is null) continue;

            Bitmap? icon = _sprites.Get(stack.Item.Id);
            if (icon is not null)
            {
                g.DrawImage(icon, x + 13, startY + 11, 32, 32);
            }

            g.DrawString(GetQuantityLabel(stack.Quantity), _cache.SmallFont, _cache.TextGreenBrush, x + HOTBAR_SLOT_SIZE - 26, startY + HOTBAR_SLOT_SIZE - 16);
        }
    }

    private void DrawMiniHealthBar(Graphics g, float x, float y, float width, int hp, int maxHp, SolidBrush fillBrush)
    {
        float ratio = Math.Clamp((float)hp / Math.Max(1, maxHp), 0f, 1f);
        g.FillRectangle(_cache.HpBackgroundBrush, x, y, width, 4f);
        g.FillRectangle(fillBrush, x, y, width * ratio, 4f);
    }

    private string GetTitleText(string islandName, Difficulty difficulty)
    {
        if (!string.Equals(_lastTitleIslandName, islandName, StringComparison.Ordinal) || _lastTitleDifficulty != difficulty)
        {
            _lastTitleIslandName = islandName;
            _lastTitleDifficulty = difficulty;
            _cachedTitleText = $"{islandName}  [{difficulty}]";
        }

        return _cachedTitleText;
    }

    private string GetHpText(int hp, int maxHp)
    {
        if (_lastHp != hp || _lastMaxHp != maxHp)
        {
            _lastHp = hp;
            _lastMaxHp = maxHp;
            _cachedHpText = $"HP {hp}/{maxHp}";
        }

        return _cachedHpText;
    }

    private string GetHungerText(int hunger, int maxHunger)
    {
        if (_lastHunger != hunger || _lastMaxHunger != maxHunger)
        {
            _lastHunger = hunger;
            _lastMaxHunger = maxHunger;
            _cachedHungerText = $"HUN {hunger}/{maxHunger}";
        }

        return _cachedHungerText;
    }

    private string GetBuffLine(string summary)
    {
        if (!string.Equals(_lastBuffSummary, summary, StringComparison.Ordinal))
        {
            _lastBuffSummary = summary;
            _cachedBuffLine = string.IsNullOrEmpty(summary) ? "Buffs: None" : $"Buff: {summary}";
        }

        return _cachedBuffLine;
    }

    private string GetScoreDayText(int score, int day, DayPhase phase)
    {
        if (_lastScore != score || _lastDay != day || _lastDayPhase != phase)
        {
            _lastScore = score;
            _lastDay = day;
            _lastDayPhase = phase;
            string phaseLabel = phase == DayPhase.Day ? "☀ DAY" : "☾ NIGHT (RAID)";
            _cachedScoreDayText = $"Score: {score}   |   Day {day} • {phaseLabel}";
        }

        return _cachedScoreDayText;
    }

    private string GetWaveEnemiesText(int wave, int enemyCount, int defense)
    {
        if (_lastWave != wave || _lastEnemyCount != enemyCount || _lastDefense != defense)
        {
            _lastWave = wave;
            _lastEnemyCount = enemyCount;
            _lastDefense = defense;
            _cachedWaveEnemiesText = $"Wave: {wave}  |  Enemies: {enemyCount}  |  DEF: {defense}";
        }

        return _cachedWaveEnemiesText;
    }

    private string GetChunkTelemetryText(string? villageName, ChunkCoord center, int loaded, int total, int viewDist)
    {
        if (_lastChunkX != center.ChunkX ||
            _lastChunkY != center.ChunkY ||
            _lastLoadedChunks != loaded ||
            _lastTotalChunks != total ||
            _lastViewDistance != viewDist ||
            !string.Equals(_lastVillageName, villageName, StringComparison.Ordinal))
        {
            _lastChunkX = center.ChunkX;
            _lastChunkY = center.ChunkY;
            _lastLoadedChunks = loaded;
            _lastTotalChunks = total;
            _lastViewDistance = viewDist;
            _lastVillageName = villageName;
            _cachedChunkText = villageName is not null
                ? $"Village: {villageName}  |  Chunk ({center.ChunkX},{center.ChunkY}) {loaded}/{total} R={viewDist}"
                : $"Chunk: ({center.ChunkX},{center.ChunkY})  |  Loaded: {loaded}/{total} (View: {viewDist})";
        }

        return _cachedChunkText;
    }

    private string GetMinimapLabel(int viewDist, int loaded, int total)
    {
        if (_lastMinimapViewDist != viewDist || _lastMinimapLoaded != loaded || _lastMinimapTotal != total)
        {
            _lastMinimapViewDist = viewDist;
            _lastMinimapLoaded = loaded;
            _lastMinimapTotal = total;
            _cachedMinimapText = $"MINIMAP  R={viewDist} ({loaded}/{total} ch)";
        }

        return _cachedMinimapText;
    }

    private string GetBossText(string bossName, int currentHp, int maxHp)
    {
        if (_lastBossHp != currentHp || _lastBossMaxHp != maxHp || !string.Equals(_lastBossName, bossName, StringComparison.Ordinal))
        {
            _lastBossName = bossName;
            _lastBossHp = currentHp;
            _lastBossMaxHp = maxHp;
            _cachedBossText = $"BOSS: {bossName} ({currentHp}/{maxHp})";
        }

        return _cachedBossText;
    }

    private string GetEquippedText(ItemStack? selected)
    {
        if (selected is null)
        {
            if (_lastEquippedId is not null)
            {
                _lastEquippedId = null;
                _lastEquippedName = null;
                _lastEquippedQty = -1;
                _cachedEquippedText = "Equipped: Bare Hands (Empty Slot)";
            }

            return _cachedEquippedText;
        }

        string id = selected.Item.Id;
        string name = selected.Item.Name;
        int qty = selected.Quantity;
        if (_lastEquippedQty != qty ||
            !string.Equals(_lastEquippedId, id, StringComparison.Ordinal) ||
            !string.Equals(_lastEquippedName, name, StringComparison.Ordinal))
        {
            _lastEquippedId = id;
            _lastEquippedName = name;
            _lastEquippedQty = qty;
            _cachedEquippedText = $"Equipped: {name} (x{qty})";
        }

        return _cachedEquippedText;
    }

    private string GetQuantityLabel(int quantity)
    {
        if ((uint)quantity < (uint)QUANTITY_LABELS.Length)
        {
            return QUANTITY_LABELS[quantity];
        }

        if (!_extraQuantityLabels.TryGetValue(quantity, out string? cached))
        {
            cached = $"x{quantity}";
            _extraQuantityLabels[quantity] = cached;
        }

        return cached;
    }

    private static string[] BuildQuantityLabels(int count)
    {
        var labels = new string[count];
        for (int i = 0; i < count; i++)
        {
            labels[i] = $"x{i}";
        }

        return labels;
    }
}
