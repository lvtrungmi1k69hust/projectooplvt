using System;
using System.Collections.Generic;
using System.Drawing;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;
using MiniNecesse.Core.Math;
using MiniNecesse.Core.Rules;

namespace MiniNecesse.App.Rendering;

/// <summary>
/// Presentation-only visual effects pool (hit particles + floating damage numbers).
/// Uses fixed-size struct arrays to eliminate per-frame heap allocations while observing
/// entity HP deltas strictly for visual polish (zero gameplay math).
/// </summary>
public sealed class VisualEffectsPool
{
    private const int MAX_PARTICLES = 96;
    private const int MAX_FLOATING_TEXTS = 32;

    private struct Particle
    {
        public bool Active;
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public float Life;
        public float MaxLife;
        public float Size;
        public ColorType ColorKind;
    }

    private struct FloatingText
    {
        public bool Active;
        public float X;
        public float Y;
        public float Vy;
        public float Life;
        public string Text;
        public bool IsHeal;
    }

    private enum ColorType
    {
        Wood,
        Stone,
        Gold,
        Hit,
        Slime
    }

    private readonly Particle[] _particles = new Particle[MAX_PARTICLES];
    private readonly FloatingText[] _texts = new FloatingText[MAX_FLOATING_TEXTS];
    private readonly Dictionary<string, int> _lastHpById = new(StringComparer.Ordinal);
    private readonly Random _rng = new(42);
    private int _particleCursor;
    private int _textCursor;

    /// <summary>Resets all visual particles and HP tracking when switching islands.</summary>
    public void Reset()
    {
        Array.Clear(_particles, 0, _particles.Length);
        Array.Clear(_texts, 0, _texts.Length);
        _lastHpById.Clear();
    }

    /// <summary>Observes visual HP changes and advances active particles/floating text.</summary>
    public void Update(GameSession session, float deltaTime)
    {
        ObservePlayerHp(session.Player);
        ObserveEnemyHps(session.Enemies);
        ObserveResourceHps(session.Resources);
        StepParticles(deltaTime);
        StepFloatingTexts(deltaTime);
    }

    /// <summary>Draws active pixel particles and floating damage/heal numbers using cached brushes.</summary>
    public void Render(Graphics g, Vector2D cam, RenderResourceCache cache)
    {
        for (int i = 0; i < MAX_PARTICLES; i++)
        {
            if (!_particles[i].Active) continue;
            Particle p = _particles[i];
            SolidBrush brush = ResolveParticleBrush(p.ColorKind, cache);
            g.FillRectangle(brush, p.X - cam.X, p.Y - cam.Y, p.Size, p.Size);
        }

        for (int i = 0; i < MAX_FLOATING_TEXTS; i++)
        {
            if (!_texts[i].Active) continue;
            FloatingText t = _texts[i];
            float sx = t.X - cam.X;
            float sy = t.Y - cam.Y;
            g.DrawString(t.Text, cache.HudTitleFont, cache.ShadowBrush, sx + 1, sy + 1);
            g.DrawString(t.Text, cache.HudTitleFont, t.IsHeal ? cache.TextGreenBrush : cache.TextGoldBrush, sx, sy);
        }
    }

    private void ObservePlayerHp(Player player)
    {
        if (_lastHpById.TryGetValue(player.Id, out int prev))
        {
            int diff = player.CurrentHp - prev;
            if (diff < 0)
            {
                SpawnText(player.Center.X - 10f, player.Position.Y - 12f, diff.ToString(), isHeal: false);
                SpawnBurst(player.Center, ColorType.Hit, 6);
            }
            else if (diff > 0)
            {
                SpawnText(player.Center.X - 10f, player.Position.Y - 12f, $"+{diff}", isHeal: true);
            }
        }

        _lastHpById[player.Id] = player.CurrentHp;
    }

    private void ObserveEnemyHps(IReadOnlyList<Enemy> enemies)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (_lastHpById.TryGetValue(e.Id, out int prev) && e.CurrentHp < prev)
            {
                int dmg = prev - e.CurrentHp;
                SpawnText(e.Center.X - 8f, e.Position.Y - 10f, $"-{dmg}", isHeal: false);
                ColorType kind = e.Archetype.Contains("Slime", StringComparison.OrdinalIgnoreCase) ? ColorType.Slime : ColorType.Hit;
                SpawnBurst(e.Center, kind, 7);
            }

            _lastHpById[e.Id] = e.CurrentHp;
        }
    }

    private void ObserveResourceHps(IReadOnlyList<ResourceNode> resources)
    {
        for (int i = 0; i < resources.Count; i++)
        {
            ResourceNode r = resources[i];
            if (_lastHpById.TryGetValue(r.Id, out int prev) && r.CurrentHp < prev)
            {
                ColorType kind = r.ResourceType == ResourceType.Tree
                    ? ColorType.Wood
                    : r.ResourceType == ResourceType.GoldDeposit ? ColorType.Gold : ColorType.Stone;
                SpawnBurst(r.Center, kind, 6);
            }

            _lastHpById[r.Id] = r.CurrentHp;
        }
    }

    private void SpawnBurst(Vector2D origin, ColorType colorKind, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2.0;
            float speed = 35f + (float)(_rng.NextDouble() * 65.0);
            _particles[_particleCursor] = new Particle
            {
                Active = true,
                X = origin.X,
                Y = origin.Y,
                Vx = (float)Math.Cos(angle) * speed,
                Vy = (float)Math.Sin(angle) * speed,
                Life = 0.35f,
                MaxLife = 0.35f,
                Size = 3f + (_rng.Next(0, 3)),
                ColorKind = colorKind
            };
            _particleCursor = (_particleCursor + 1) % MAX_PARTICLES;
        }
    }

    private void SpawnText(float x, float y, string text, bool isHeal)
    {
        _texts[_textCursor] = new FloatingText
        {
            Active = true,
            X = x,
            Y = y,
            Vy = -34f,
            Life = 0.75f,
            Text = text,
            IsHeal = isHeal
        };
        _textCursor = (_textCursor + 1) % MAX_FLOATING_TEXTS;
    }

    private void StepParticles(float dt)
    {
        for (int i = 0; i < MAX_PARTICLES; i++)
        {
            if (!_particles[i].Active) continue;
            _particles[i].X += _particles[i].Vx * dt;
            _particles[i].Y += _particles[i].Vy * dt;
            _particles[i].Vx *= 0.90f;
            _particles[i].Vy *= 0.90f;
            _particles[i].Life -= dt;
            if (_particles[i].Life <= 0f)
            {
                _particles[i].Active = false;
            }
        }
    }

    private void StepFloatingTexts(float dt)
    {
        for (int i = 0; i < MAX_FLOATING_TEXTS; i++)
        {
            if (!_texts[i].Active) continue;
            _texts[i].Y += _texts[i].Vy * dt;
            _texts[i].Life -= dt;
            if (_texts[i].Life <= 0f)
            {
                _texts[i].Active = false;
            }
        }
    }

    private static SolidBrush ResolveParticleBrush(ColorType kind, RenderResourceCache cache)
    {
        return kind switch
        {
            ColorType.Wood => cache.WoodWallBrush,
            ColorType.Stone => cache.RockBaseBrush,
            ColorType.Gold => cache.GoldGlintBrush,
            ColorType.Slime => cache.SlimeBrush,
            _ => cache.EnemyProjectileBrush
        };
    }
}
