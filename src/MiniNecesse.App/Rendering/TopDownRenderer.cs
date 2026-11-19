using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using MiniNecesse.App.Views;
using MiniNecesse.Core.Crafting;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.App.Rendering;

/// <summary>
/// 2D Top-down procedural renderer drawing tilemaps, resources, structures, entities,
/// day/night lighting overlays, HUD, boss bar, and hotbar using pre-cached GDI+ resources.
/// Contains zero gameplay math or rule mutations.
/// </summary>
public sealed class TopDownRenderer : IDisposable
{
    private const int HOTBAR_DISPLAY_SLOTS = 5;

    private readonly RenderResourceCache _cache = new();

    /// <summary>Computes the camera top-left world offset centered on the player and clamped to island bounds.</summary>
    public Vector2D ComputeCameraOffset(GameSession session, Size viewportSize)
    {
        IslandLevel level = session.Level;
        float camX = session.Player.Center.X - (viewportSize.Width * 0.5f);
        float camY = session.Player.Center.Y - (viewportSize.Height * 0.5f);

        if (level.WorldWidth <= viewportSize.Width)
        {
            camX = -((viewportSize.Width - level.WorldWidth) * 0.5f);
        }
        else
        {
            camX = Math.Clamp(camX, 0f, level.WorldWidth - viewportSize.Width);
        }

        if (level.WorldHeight <= viewportSize.Height)
        {
            camY = -((viewportSize.Height - level.WorldHeight) * 0.5f);
        }
        else
        {
            camY = Math.Clamp(camY, 0f, level.WorldHeight - viewportSize.Height);
        }

        return new Vector2D(camX, camY);
    }

    /// <summary>Renders the full 2D top-down world and HUD onto <paramref name="g"/> with zero brush/pen allocations.</summary>
    public void Render(Graphics g, Size viewportSize, GameSession session, HudViewModel hud, Vector2D cameraOffset, Vector2D aimWorldPos)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(14, 24, 38));

        DrawVisibleTiles(g, viewportSize, session.Level, cameraOffset);
        DrawResources(g, session.Resources, cameraOffset);
        DrawStructures(g, session.Structures, cameraOffset);
        DrawDroppedItems(g, session.DroppedItems, cameraOffset);
        DrawEnemies(g, session.Enemies, cameraOffset);
        DrawPlayer(g, session, cameraOffset, aimWorldPos);
        DrawProjectiles(g, session.Projectiles, cameraOffset);
        DrawDayNightOverlay(g, viewportSize, session, hud, cameraOffset);
        DrawTopHud(g, session, hud);
        DrawBossBar(g, viewportSize, hud);
        DrawHotbar(g, viewportSize, session);
    }

    /// <inheritdoc />
    public void Dispose() => _cache.Dispose();

    private void DrawVisibleTiles(Graphics g, Size viewport, IslandLevel level, Vector2D cam)
    {
        float tileSize = level.TileSize;
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
                g.FillRectangle(_cache.GetHexBrush(tile.ColorHex), sx, sy, tileSize, tileSize);
                g.DrawRectangle(_cache.TileGridPen, sx, sy, tileSize, tileSize);
            }
        }
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

            g.FillEllipse(_cache.ShadowBrush, sx + 2, sy + h - 8, w - 4, 10);
            if (node.ResourceType == ResourceType.Tree)
            {
                g.FillRectangle(_cache.TreeTrunkBrush, sx + (w * 0.36f), sy + (h * 0.50f), w * 0.28f, h * 0.45f);
                g.FillEllipse(_cache.TreeLeafDarkBrush, sx - 2, sy - 4, w + 4, h * 0.78f);
                g.FillEllipse(_cache.TreeLeafLightBrush, sx + 3, sy - 1, w - 6, h * 0.56f);
            }
            else
            {
                g.FillEllipse(_cache.RockBaseBrush, sx + 1, sy + 3, w - 2, h - 6);
                g.DrawEllipse(_cache.DarkOutlinePen, sx + 1, sy + 3, w - 2, h - 6);
                DrawOreGlint(g, node.ResourceType, sx, sy, w, h);
            }

            if (node.CurrentHp < node.MaxHp)
            {
                DrawMiniHealthBar(g, sx, sy - 7, w, node.CurrentHp, node.MaxHp, _cache.ResourceHpFillBrush);
            }
        }
    }

    private void DrawOreGlint(Graphics g, ResourceType type, float sx, float sy, float w, float h)
    {
        SolidBrush? glint = type switch
        {
            ResourceType.CopperDeposit => _cache.CopperGlintBrush,
            ResourceType.IronDeposit => _cache.IronGlintBrush,
            ResourceType.GoldDeposit => _cache.GoldGlintBrush,
            _ => null
        };

        if (glint is null) return;
        g.FillRectangle(glint, sx + 7, sy + 9, 6, 6);
        g.FillRectangle(glint, sx + w - 13, sy + h - 14, 5, 5);
    }

    private void DrawStructures(Graphics g, IReadOnlyList<Structure> structures, Vector2D cam)
    {
        for (int i = 0; i < structures.Count; i++)
        {
            Structure s = structures[i];
            if (!s.IsAlive) continue;

            float sx = s.Position.X - cam.X;
            float sy = s.Position.Y - cam.Y;
            float w = s.Size.X;
            float h = s.Size.Y;

            if (s.StructureType == StructureType.Torch)
            {
                g.FillRectangle(_cache.TreeTrunkBrush, sx + 13, sy + 12, 6, 15);
                g.FillEllipse(_cache.TorchFlameBrush, sx + 10, sy + 4, 12, 12);
            }
            else
            {
                SolidBrush fill = s.StructureType == StructureType.StoneWall
                    ? _cache.StoneWallBrush
                    : s.IsCraftingStation ? _cache.WorkbenchBrush : _cache.WoodWallBrush;
                g.FillRectangle(fill, sx + 1, sy + 1, w - 2, h - 2);
                g.DrawRectangle(_cache.DarkOutlinePen, sx + 1, sy + 1, w - 2, h - 2);
                if (s.IsCraftingStation)
                {
                    g.DrawString("WB", _cache.SmallFont, _cache.TextWhiteBrush, sx + 5, sy + 8);
                }
            }

            if (s.CurrentHp < s.MaxHp)
            {
                DrawMiniHealthBar(g, sx, sy - 6, w, s.CurrentHp, s.MaxHp, _cache.ResourceHpFillBrush);
            }
        }
    }

    private void DrawDroppedItems(Graphics g, IReadOnlyList<DroppedItem> drops, Vector2D cam)
    {
        for (int i = 0; i < drops.Count; i++)
        {
            DroppedItem d = drops[i];
            if (!d.IsActive) continue;

            float sx = d.Position.X - cam.X;
            float sy = d.Position.Y - cam.Y;
            g.FillEllipse(_cache.DropBrush, sx, sy, d.Size.X, d.Size.Y);
            g.DrawEllipse(_cache.DarkOutlinePen, sx, sy, d.Size.X, d.Size.Y);
        }
    }

    private void DrawEnemies(Graphics g, IReadOnlyList<Enemy> enemies, Vector2D cam)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (!e.IsAlive) continue;

            float sx = e.Position.X - cam.X;
            float sy = e.Position.Y - cam.Y;
            float w = e.Size.X;
            float h = e.Size.Y;

            g.FillEllipse(_cache.ShadowBrush, sx + 2, sy + h - 6, w - 4, 8);
            SolidBrush body = ResolveEnemyBrush(e);
            g.FillEllipse(body, sx, sy, w, h);
            g.DrawEllipse(_cache.DarkOutlinePen, sx, sy, w, h);

            if (e.IsBoss && e.State == EnemyState.Enraged)
            {
                g.DrawEllipse(_cache.BossAuraPen, sx - 4, sy - 4, w + 8, h + 8);
            }

            DrawMiniHealthBar(g, sx, sy - 8, w, e.CurrentHp, e.MaxHp, e.IsBoss ? _cache.BossHpFillBrush : _cache.EnemyHpFillBrush);
        }
    }

    private SolidBrush ResolveEnemyBrush(Enemy enemy)
    {
        if (enemy.IsBoss)
        {
            return enemy.State == EnemyState.Enraged ? _cache.BossEnragedBrush : _cache.BossBrush;
        }

        if (enemy.Archetype.Contains("Archer", StringComparison.OrdinalIgnoreCase))
        {
            return _cache.ArcherBrush;
        }

        return enemy.Archetype.Contains("Slime", StringComparison.OrdinalIgnoreCase)
            ? _cache.SlimeBrush
            : _cache.RaiderBrush;
    }

    private void DrawPlayer(Graphics g, GameSession session, Vector2D cam, Vector2D aimWorldPos)
    {
        Player player = session.Player;
        float sx = player.Position.X - cam.X;
        float sy = player.Position.Y - cam.Y;
        float cx = player.Center.X - cam.X;
        float cy = player.Center.Y - cam.Y;

        g.FillEllipse(_cache.ShadowBrush, sx + 2, sy + player.Size.Y - 6, player.Size.X - 4, 8);
        g.FillEllipse(_cache.PlayerBodyBrush, sx, sy, player.Size.X, player.Size.Y);
        g.DrawEllipse(_cache.DarkOutlinePen, sx, sy, player.Size.X, player.Size.Y);

        Vector2D lookDir = (aimWorldPos - player.Center).Normalized();
        if (lookDir == Vector2D.Zero) lookDir = session.LastAimDirection;
        float vx = cx + (lookDir.X * 7f) - 3f;
        float vy = cy + (lookDir.Y * 7f) - 3f;
        g.FillEllipse(_cache.PlayerVisorBrush, vx, vy, 6f, 6f);

        if (session.SwingEffectRemaining > 0f)
        {
            float angleDeg = MathF.Atan2(lookDir.Y, lookDir.X) * (180f / MathF.PI);
            g.DrawArc(_cache.SwingArcPen, cx - 26f, cy - 26f, 52f, 52f, angleDeg - 42f, 84f);
        }
    }

    private void DrawProjectiles(Graphics g, IReadOnlyList<Projectile> projectiles, Vector2D cam)
    {
        for (int i = 0; i < projectiles.Count; i++)
        {
            Projectile p = projectiles[i];
            if (!p.IsActive) continue;

            float sx = p.Position.X - cam.X;
            float sy = p.Position.Y - cam.Y;
            SolidBrush brush = p.IsFromPlayer ? _cache.PlayerProjectileBrush : _cache.EnemyProjectileBrush;
            g.FillEllipse(brush, sx, sy, p.Size.X, p.Size.Y);
            g.DrawEllipse(_cache.DarkOutlinePen, sx, sy, p.Size.X, p.Size.Y);
        }
    }

    private void DrawDayNightOverlay(Graphics g, Size viewport, GameSession session, HudViewModel hud, Vector2D cam)
    {
        float darkness = ComputeDarknessFactor(hud);
        if (darkness <= 0.02f) return;

        g.FillRectangle(_cache.GetNightOverlayBrush(darkness), 0, 0, viewport.Width, viewport.Height);

        float pcx = session.Player.Center.X - cam.X;
        float pcy = session.Player.Center.Y - cam.Y;
        g.FillEllipse(_cache.PlayerGlowBrush, pcx - 95f, pcy - 95f, 190f, 190f);

        for (int i = 0; i < session.Structures.Count; i++)
        {
            Structure s = session.Structures[i];
            if (!s.IsAlive || s.LightRadius <= 0f) continue;

            float lcx = s.Center.X - cam.X;
            float lcy = s.Center.Y - cam.Y;
            float r = s.LightRadius;
            g.FillEllipse(_cache.TorchGlowBrush, lcx - r, lcy - r, r * 2f, r * 2f);
        }
    }

    private static float ComputeDarknessFactor(HudViewModel hud)
    {
        if (hud.DayPhase == DayPhase.Night)
        {
            return 0.85f;
        }

        return hud.PhaseProgress > 0.75f ? (hud.PhaseProgress - 0.75f) * 2.2f : 0f;
    }

    private void DrawTopHud(Graphics g, GameSession session, HudViewModel hud)
    {
        g.FillRectangle(_cache.HudPanelBrush, 12, 12, 315, 114);
        g.DrawRectangle(_cache.HudBorderPen, 12, 12, 315, 114);

        g.DrawString($"{session.Level.Name}  [{session.Difficulty}]", _cache.HudTitleFont, _cache.TextGoldBrush, 22, 18);

        float hpRatio = Math.Clamp((float)hud.CurrentHp / hud.MaxHp, 0f, 1f);
        g.FillRectangle(_cache.HpBackgroundBrush, 22, 42, 200, 16);
        g.FillRectangle(_cache.HpFillBrush, 22, 42, 200f * hpRatio, 16);
        g.DrawRectangle(_cache.DarkOutlinePen, 22, 42, 200, 16);
        g.DrawString($"HP {hud.CurrentHp}/{hud.MaxHp}", _cache.SmallFont, _cache.TextWhiteBrush, 228, 43);

        string phaseLabel = hud.DayPhase == DayPhase.Day ? "DAY" : "NIGHT (RAID)";
        SolidBrush phaseBrush = hud.DayPhase == DayPhase.Day ? _cache.TextGreenBrush : _cache.TextGoldBrush;
        g.DrawString($"Score: {hud.Score}   |   Day {hud.DayNumber} • {phaseLabel}", _cache.HudBodyFont, phaseBrush, 22, 65);
        g.DrawString($"Wave: {session.CurrentWaveIndex}   |   Enemies: {session.Enemies.Count}", _cache.HudBodyFont, _cache.TextWhiteBrush, 22, 85);
        g.DrawString("[WASD] Move [LMB] Act [RMB] Build/Heal [C] Craft [E] Boss [Esc] Pause", _cache.SmallFont, _cache.TextMutedBrush, 22, 105);
    }

    private void DrawBossBar(Graphics g, Size viewport, HudViewModel hud)
    {
        if (!hud.BossBarVisible) return;

        int barWidth = 360;
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

    private void DrawHotbar(Graphics g, Size viewport, GameSession session)
    {
        const int SLOT_SIZE = 74;
        const int GAP = 8;
        int totalWidth = (HOTBAR_DISPLAY_SLOTS * SLOT_SIZE) + ((HOTBAR_DISPLAY_SLOTS - 1) * GAP);
        int startX = (viewport.Width - totalWidth) / 2;
        int startY = viewport.Height - SLOT_SIZE - 16;

        for (int i = 0; i < HOTBAR_DISPLAY_SLOTS; i++)
        {
            int x = startX + (i * (SLOT_SIZE + GAP));
            bool selected = session.Player.SelectedHotbarIndex == i;

            g.FillRectangle(selected ? _cache.HotbarSelectedBrush : _cache.HotbarSlotBrush, x, startY, SLOT_SIZE, SLOT_SIZE);
            g.DrawRectangle(selected ? _cache.HotbarSelectedPen : _cache.HudBorderPen, x, startY, SLOT_SIZE, SLOT_SIZE);
            g.DrawString($"{i + 1}", _cache.SmallFont, _cache.TextGoldBrush, x + 5, startY + 4);

            ItemStack? stack = session.Inventory.GetStackAt(i);
            if (stack is not null)
            {
                string shortName = stack.Item.Name.Length > 10 ? stack.Item.Name[..10] : stack.Item.Name;
                g.DrawString(shortName, _cache.SmallFont, _cache.TextWhiteBrush, x + 5, startY + 26);
                g.DrawString($"x{stack.Quantity}", _cache.SmallFont, _cache.TextGreenBrush, x + SLOT_SIZE - 28, startY + SLOT_SIZE - 18);
            }
        }
    }

    private void DrawMiniHealthBar(Graphics g, float x, float y, float width, int hp, int maxHp, SolidBrush fillBrush)
    {
        float ratio = Math.Clamp((float)hp / Math.Max(1, maxHp), 0f, 1f);
        g.FillRectangle(_cache.HpBackgroundBrush, x, y, width, 4f);
        g.FillRectangle(fillBrush, x, y, width * ratio, 4f);
    }
}
