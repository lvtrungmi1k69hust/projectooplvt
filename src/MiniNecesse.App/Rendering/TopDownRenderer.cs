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
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Rules;

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
    private const int MINIMAP_TILE_PX = 4;

    private readonly RenderResourceCache _cache = new();
    private readonly SpriteAtlasCache _sprites = new();
    private readonly VisualEffectsPool _vfx = new();

    private Bitmap? _minimapTerrainCache;
    private string _cachedMinimapIslandId = string.Empty;
    private float _animTime;
    private bool _disposed;

    /// <summary>Gets the shared pixel-art sprite atlas (also used by Crafting UI for item icons).</summary>
    public SpriteAtlasCache Sprites => _sprites;

    /// <summary>Resets cached minimap and particle VFX when switching islands.</summary>
    public void ResetForNewIsland()
    {
        _vfx.Reset();
        _minimapTerrainCache?.Dispose();
        _minimapTerrainCache = null;
        _cachedMinimapIslandId = string.Empty;
    }

    /// <summary>Advances presentation-only animation clocks and visual hit particles.</summary>
    public void UpdateVisuals(GameSession session, float frameDeltaTime)
    {
        _animTime += Math.Clamp(frameDeltaTime, 0f, 0.1f);
        _vfx.Update(session, frameDeltaTime);
    }

    /// <summary>Computes the camera top-left world offset centered on the player and clamped to island bounds.</summary>
    public Vector2D ComputeCameraOffset(GameSession session, Size viewportSize)
    {
        IslandLevel level = session.Level;
        float camX = session.Player.Center.X - (viewportSize.Width * 0.5f);
        float camY = session.Player.Center.Y - (viewportSize.Height * 0.5f);

        camX = level.WorldWidth <= viewportSize.Width
            ? -((viewportSize.Width - level.WorldWidth) * 0.5f)
            : Math.Clamp(camX, 0f, level.WorldWidth - viewportSize.Width);

        camY = level.WorldHeight <= viewportSize.Height
            ? -((viewportSize.Height - level.WorldHeight) * 0.5f)
            : Math.Clamp(camY, 0f, level.WorldHeight - viewportSize.Height);

        return new Vector2D(camX, camY);
    }

    /// <summary>Renders the complete Necesse-style pixel-art scene and HUD with zero brush/pen allocations.</summary>
    public void Render(Graphics g, Size viewportSize, GameSession session, HudViewModel hud, Vector2D cameraOffset, Vector2D aimWorldPos)
    {
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.None;
        g.Clear(Color.FromArgb(14, 24, 38));

        DrawVisibleTiles(g, viewportSize, session.Level, cameraOffset);
        DrawBuildPlacementPreview(g, session, cameraOffset, aimWorldPos);
        DrawResources(g, session.Resources, cameraOffset);
        DrawStructures(g, session.Structures, cameraOffset);
        DrawDroppedItems(g, session.DroppedItems, cameraOffset);
        DrawEnemies(g, session.Enemies, cameraOffset);
        DrawPlayerWithHeldItem(g, session, cameraOffset, aimWorldPos);
        DrawProjectiles(g, session.Projectiles, cameraOffset);
        _vfx.Render(g, cameraOffset, _cache);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        DrawDayNightOverlay(g, viewportSize, session, hud, cameraOffset);
        DrawTopHud(g, session, hud);
        DrawMinimap(g, viewportSize, session, cameraOffset);
        DrawBossBar(g, viewportSize, hud);
        DrawHotbar(g, viewportSize, session);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _minimapTerrainCache?.Dispose();
        _sprites.Dispose();
        _cache.Dispose();
        _disposed = true;
    }

    private void DrawVisibleTiles(Graphics g, Size viewport, IslandLevel level, Vector2D cam)
    {
        float tileSize = level.TileSize;
        int waterFrame = ((int)(_animTime * 2.2f)) & 1;
        int startCol = Math.Max(0, (int)MathF.Floor(cam.X / tileSize));
        int endCol = Math.Min(level.Width - 1, (int)MathF.Ceiling((cam.X + viewport.Width) / tileSize));
        int startRow = Math.Max(0, (int)MathF.Floor(cam.Y / tileSize));
        int endRow = Math.Min(level.Height - 1, (int)MathF.Ceiling((cam.Y + viewport.Height) / tileSize));

        for (int row = startRow; row <= endRow; row++)
        {
            for (int col = startCol; col <= endCol; col++)
            {
                Tile tile = level.GetTile(col, row);
                float sx = (col * tileSize) - cam.X;
                float sy = (row * tileSize) - cam.Y;

                Bitmap sprite = _sprites.GetTileSprite(tile.TileType, col, row, waterFrame);
                g.DrawImage(sprite, sx, sy, tileSize, tileSize);

                if (tile.TileType == TileType.Water)
                {
                    DrawWaterShorelineFoam(g, level, col, row, sx, sy, tileSize);
                }
            }
        }
    }

    private void DrawWaterShorelineFoam(Graphics g, IslandLevel level, int col, int row, float sx, float sy, float tileSize)
    {
        if (IsLandTile(level, col, row - 1)) g.FillRectangle(_cache.ShoreFoamBrush, sx, sy, tileSize, 3f);
        if (IsLandTile(level, col, row + 1)) g.FillRectangle(_cache.ShoreFoamBrush, sx, sy + tileSize - 3f, tileSize, 3f);
        if (IsLandTile(level, col - 1, row)) g.FillRectangle(_cache.ShoreFoamBrush, sx, sy, 3f, tileSize);
        if (IsLandTile(level, col + 1, row)) g.FillRectangle(_cache.ShoreFoamBrush, sx + tileSize - 3f, sy, 3f, tileSize);
    }

    private static bool IsLandTile(IslandLevel level, int col, int row)
    {
        return level.TryGetTile(col, row, out Tile? neighbor) && neighbor is not null && neighbor.TileType != TileType.Water;
    }

    private void DrawBuildPlacementPreview(Graphics g, GameSession session, Vector2D cam, Vector2D aimWorldPos)
    {
        ItemStack? selected = session.Player.SelectedItemStack;
        if (selected is null || !selected.Item.TryGetPlaceableStructure(out _))
        {
            return;
        }

        (int col, int row) = session.Level.WorldToTile(aimWorldPos);
        if (!session.Level.IsInBounds(col, row)) return;

        float tileSize = session.Level.TileSize;
        bool inReach = Vector2D.Distance(session.Player.Center, aimWorldPos) <= 112f;
        bool freeTile = session.Level.IsTileWalkable(col, row) && session.FindStructureAtTile(new GridPoint(col, row)) is null;
        bool valid = inReach && freeTile;

        float sx = (col * tileSize) - cam.X;
        float sy = (row * tileSize) - cam.Y;
        g.FillRectangle(valid ? _cache.BuildValidBrush : _cache.BuildInvalidBrush, sx, sy, tileSize, tileSize);
        g.DrawRectangle(valid ? _cache.BuildValidPen : _cache.BuildInvalidPen, sx, sy, tileSize, tileSize);
    }

    private void DrawResources(Graphics g, IReadOnlyList<ResourceNode> resources, Vector2D cam)
    {
        for (int i = 0; i < resources.Count; i++)
        {
            ResourceNode node = resources[i];
            if (!node.IsAlive) continue;

            float sx = node.Position.X - cam.X;
            float sy = node.Position.Y - cam.Y;
            float w = node.Size.X;
            float h = node.Size.Y;

            g.FillEllipse(_cache.ShadowBrush, sx + 1, sy + h - 7, w - 2, 9);

            string spriteKey = node.ResourceType switch
            {
                ResourceType.Tree => "res_tree",
                ResourceType.CopperDeposit => "res_copper",
                ResourceType.IronDeposit => "res_iron",
                ResourceType.GoldDeposit => "res_gold",
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

    private void DrawStructures(Graphics g, IReadOnlyList<Structure> structures, Vector2D cam)
    {
        int torchFrame = ((int)(_animTime * 6f)) & 1;

        for (int i = 0; i < structures.Count; i++)
        {
            Structure s = structures[i];
            if (!s.IsAlive) continue;

            float sx = s.Position.X - cam.X;
            float sy = s.Position.Y - cam.Y;
            string key = s.StructureType switch
            {
                StructureType.StoneWall => "str_stone_wall",
                StructureType.Workbench => "str_workbench",
                StructureType.Torch => torchFrame == 0 ? "str_torch_0" : "str_torch_1",
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

    private void DrawDroppedItems(Graphics g, IReadOnlyList<DroppedItem> drops, Vector2D cam)
    {
        float bob = MathF.Sin(_animTime * 5f) * 2.5f;

        for (int i = 0; i < drops.Count; i++)
        {
            DroppedItem d = drops[i];
            if (!d.IsActive) continue;

            float sx = d.Position.X - cam.X;
            float sy = d.Position.Y - cam.Y + bob;
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

    private void DrawEnemies(Graphics g, IReadOnlyList<Enemy> enemies, Vector2D cam)
    {
        int bounceFrame = ((int)(_animTime * 4f)) & 1;

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (!e.IsAlive) continue;

            float sx = e.Position.X - cam.X;
            float sy = e.Position.Y - cam.Y;
            float w = e.Size.X;
            float h = e.Size.Y;

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

    private void DrawPlayerWithHeldItem(Graphics g, GameSession session, Vector2D cam, Vector2D aimWorldPos)
    {
        Player player = session.Player;
        float sx = player.Position.X - cam.X;
        float sy = player.Position.Y - cam.Y;
        float cx = player.Center.X - cam.X;
        float cy = player.Center.Y - cam.Y;

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

        Vector2D lookDir = (aimWorldPos - player.Center).Normalized();
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

    private void DrawProjectiles(Graphics g, IReadOnlyList<Projectile> projectiles, Vector2D cam)
    {
        for (int i = 0; i < projectiles.Count; i++)
        {
            Projectile p = projectiles[i];
            if (!p.IsActive) continue;

            float sx = p.Position.X - cam.X;
            float sy = p.Position.Y - cam.Y;
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

    private void DrawDayNightOverlay(Graphics g, Size viewport, GameSession session, HudViewModel hud, Vector2D cam)
    {
        float darkness = hud.DayPhase == DayPhase.Night
            ? 0.84f
            : hud.PhaseProgress > 0.75f ? (hud.PhaseProgress - 0.75f) * 2.2f : 0f;

        if (darkness <= 0.02f) return;

        g.FillRectangle(_cache.GetNightOverlayBrush(darkness), 0, 0, viewport.Width, viewport.Height);

        float pcx = session.Player.Center.X - cam.X;
        float pcy = session.Player.Center.Y - cam.Y;
        g.FillEllipse(_cache.PlayerGlowBrush, pcx - 105f, pcy - 105f, 210f, 210f);

        float flicker = MathF.Sin(_animTime * 11f) * 6f;
        for (int i = 0; i < session.Structures.Count; i++)
        {
            Structure s = session.Structures[i];
            if (!s.IsAlive || s.LightRadius <= 0f) continue;

            float lcx = s.Center.X - cam.X;
            float lcy = s.Center.Y - cam.Y;
            float r = s.LightRadius + flicker;
            g.FillEllipse(_cache.TorchGlowBrush, lcx - r, lcy - r, r * 2f, r * 2f);
        }
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

        for (int i = 0; i < session.Enemies.Count; i++)
        {
            Enemy e = session.Enemies[i];
            if (!e.IsAlive) continue;
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
        g.DrawString("MINIMAP", _cache.SmallFont, _cache.TextMutedBrush, ox + 2, oy + mapH + 2);
    }

    private void EnsureMinimapTerrainCached(IslandLevel level)
    {
        if (_minimapTerrainCache is not null && string.Equals(_cachedMinimapIslandId, level.IslandId, StringComparison.Ordinal))
        {
            return;
        }

        _minimapTerrainCache?.Dispose();
        int w = level.Width * MINIMAP_TILE_PX;
        int h = level.Height * MINIMAP_TILE_PX;
        _minimapTerrainCache = new Bitmap(w, h, PixelFormat.Format32bppArgb);

        using Graphics mg = Graphics.FromImage(_minimapTerrainCache);
        for (int row = 0; row < level.Height; row++)
        {
            for (int col = 0; col < level.Width; col++)
            {
                Tile tile = level.GetTile(col, row);
                mg.FillRectangle(_cache.GetHexBrush(tile.ColorHex), col * MINIMAP_TILE_PX, row * MINIMAP_TILE_PX, MINIMAP_TILE_PX, MINIMAP_TILE_PX);
            }
        }

        _cachedMinimapIslandId = level.IslandId;
    }

    private void DrawTopHud(Graphics g, GameSession session, HudViewModel hud)
    {
        g.FillRectangle(_cache.HudPanelBrush, 12, 12, 326, 114);
        g.DrawRectangle(_cache.HudBorderPen, 12, 12, 326, 114);

        g.DrawString($"{session.Level.Name}  [{session.Difficulty}]", _cache.HudTitleFont, _cache.TextGoldBrush, 22, 18);

        float hpRatio = Math.Clamp((float)hud.CurrentHp / hud.MaxHp, 0f, 1f);
        g.FillRectangle(_cache.HpBackgroundBrush, 22, 42, 206, 16);
        g.FillRectangle(_cache.HpFillBrush, 22, 42, 206f * hpRatio, 16);
        g.DrawRectangle(_cache.DarkOutlinePen, 22, 42, 206, 16);
        g.DrawString($"HP {hud.CurrentHp}/{hud.MaxHp}", _cache.SmallFont, _cache.TextWhiteBrush, 234, 43);

        string phaseLabel = hud.DayPhase == DayPhase.Day ? "☀ DAY" : "☾ NIGHT (RAID)";
        SolidBrush phaseBrush = hud.DayPhase == DayPhase.Day ? _cache.TextGreenBrush : _cache.TextGoldBrush;
        g.DrawString($"Score: {hud.Score}   |   Day {hud.DayNumber} • {phaseLabel}", _cache.HudBodyFont, phaseBrush, 22, 65);
        g.DrawString($"Wave: {session.CurrentWaveIndex}   |   Enemies: {session.Enemies.Count}", _cache.HudBodyFont, _cache.TextWhiteBrush, 22, 85);
        g.DrawString("[WASD] Move [LMB] Act [RMB] Build/Use [C] Craft [E] Boss [Esc] Pause", _cache.SmallFont, _cache.TextMutedBrush, 22, 105);
    }

    private void DrawBossBar(Graphics g, Size viewport, HudViewModel hud)
    {
        if (!hud.BossBarVisible) return;

        int barWidth = 340;
        int x = (viewport.Width - barWidth) / 2;
        int y = 16;

        g.FillRectangle(_cache.HudPanelBrush, x - 12, y - 4, barWidth + 24, 48);
        g.DrawRectangle(_cache.HudBorderPen, x - 12, y - 4, barWidth + 24, 48);
        g.DrawString($"BOSS: {hud.BossName} ({hud.BossCurrentHp}/{hud.BossMaxHp})", _cache.BossTitleFont, _cache.TextGoldBrush, x, y);

        float ratio = Math.Clamp((float)hud.BossCurrentHp / hud.BossMaxHp, 0f, 1f);
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
        string equippedText = selectedStack is not null
            ? $"Equipped: {selectedStack.Item.Name} (x{selectedStack.Quantity})"
            : "Equipped: Bare Hands (Empty Slot)";

        g.FillRectangle(_cache.HudPanelBrush, startX, startY - 26, totalWidth, 22);
        g.DrawRectangle(_cache.HudBorderPen, startX, startY - 26, totalWidth, 22);
        g.DrawString(equippedText, _cache.SmallFont, _cache.TextGoldBrush, startX + 8, startY - 22);

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        for (int i = 0; i < HOTBAR_DISPLAY_SLOTS; i++)
        {
            int x = startX + (i * (HOTBAR_SLOT_SIZE + HOTBAR_GAP));
            bool selected = session.Player.SelectedHotbarIndex == i;

            g.FillRectangle(selected ? _cache.HotbarSelectedBrush : _cache.HotbarSlotBrush, x, startY, HOTBAR_SLOT_SIZE, HOTBAR_SLOT_SIZE);
            g.DrawRectangle(selected ? _cache.HotbarSelectedPen : _cache.HudBorderPen, x, startY, HOTBAR_SLOT_SIZE, HOTBAR_SLOT_SIZE);
            g.DrawString($"{i + 1}", _cache.SmallFont, _cache.TextGoldBrush, x + 4, startY + 3);

            ItemStack? stack = session.Inventory.GetStackAt(i);
            if (stack is null) continue;

            Bitmap? icon = _sprites.Get(stack.Item.Id);
            if (icon is not null)
            {
                g.DrawImage(icon, x + 13, startY + 11, 32, 32);
            }

            g.DrawString($"x{stack.Quantity}", _cache.SmallFont, _cache.TextGreenBrush, x + HOTBAR_SLOT_SIZE - 26, startY + HOTBAR_SLOT_SIZE - 16);
        }
    }

    private void DrawMiniHealthBar(Graphics g, float x, float y, float width, int hp, int maxHp, SolidBrush fillBrush)
    {
        float ratio = Math.Clamp((float)hp / Math.Max(1, maxHp), 0f, 1f);
        g.FillRectangle(_cache.HpBackgroundBrush, x, y, width, 4f);
        g.FillRectangle(fillBrush, x, y, width * ratio, 4f);
    }
}
