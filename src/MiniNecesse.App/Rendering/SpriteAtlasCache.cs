using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.App.Rendering;

/// <summary>
/// Presentation sprite atlas caching Necesse-style 16x16 pixel-art <see cref="Bitmap"/> assets.
/// Automatically loads external <c>Assets/Sprites/{key}.png</c> overrides when present,
/// falling back seamlessly to built-in procedural pixel art with zero per-frame allocations.
/// </summary>
public sealed class SpriteAtlasCache : IDisposable
{
    private readonly string _spritesDirectory;
    private readonly Dictionary<string, Bitmap> _sprites = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>Initializes and pre-generates all pixel-art sprites once at startup.</summary>
    public SpriteAtlasCache(string? spritesDirectory = null)
    {
        _spritesDirectory = string.IsNullOrWhiteSpace(spritesDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "Assets", "Sprites")
            : spritesDirectory;

        RegisterTerrainSprites();
        RegisterResourceSprites();
        RegisterStructureSprites();
        RegisterCharacterAndEnemySprites();
        RegisterItemAndProjectileSprites();
    }

    /// <summary>Retrieves a cached sprite by key, or <see langword="null"/> if unavailable.</summary>
    public Bitmap? Get(string key) => _sprites.TryGetValue(key, out Bitmap? bmp) ? bmp : null;

    /// <summary>Resolves the animated or variant terrain sprite for a map tile.</summary>
    public Bitmap GetTileSprite(TileType tileType, int col, int row, int waterFrame)
    {
        if (tileType == TileType.Water)
        {
            return _sprites[waterFrame == 0 ? "tile_water_0" : "tile_water_1"];
        }

        if (tileType == TileType.Grass)
        {
            int hash = Math.Abs((col * 31) + (row * 17)) % 5;
            if (hash == 0) return _sprites["tile_grass_flower"];
            if (hash == 1) return _sprites["tile_grass_tuft"];
            return _sprites["tile_grass"];
        }

        string key = tileType switch
        {
            TileType.Sand => "tile_sand",
            TileType.Stone => "tile_stone",
            TileType.Snow => "tile_snow",
            TileType.Swamp => "tile_swamp",
            TileType.DarkStone => "tile_darkstone",
            _ => "tile_grass"
        };

        return _sprites[key];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        var uniqueBitmaps = new HashSet<Bitmap>(ReferenceEqualityComparer.Instance);
        foreach (Bitmap bmp in _sprites.Values)
        {
            if (uniqueBitmaps.Add(bmp))
            {
                bmp.Dispose();
            }
        }

        _sprites.Clear();
        _disposed = true;
    }

    private void Register(string key, Func<Bitmap> fallbackFactory)
    {
        string pngPath = Path.Combine(_spritesDirectory, $"{key}.png");
        if (File.Exists(pngPath))
        {
            try
            {
                using var loaded = new Bitmap(pngPath);
                _sprites[key] = new Bitmap(loaded);
                return;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException)
            {
                Debug.WriteLine($"SpriteAtlasCache PNG fallback for '{key}': {ex.Message}");
            }
        }

        _sprites[key] = fallbackFactory();
    }

    private void RegisterTerrainSprites()
    {
        Register("tile_grass", () => PixelArtBuilder.CreateTexturedTile(
            Color.FromArgb(74, 148, 66), Color.FromArgb(94, 174, 80), Color.FromArgb(58, 124, 52), seed: 11, speckleCount: 28));
        Register("tile_grass_tuft", () => PixelArtBuilder.CreateTexturedTile(
            Color.FromArgb(74, 148, 66), Color.FromArgb(106, 186, 86), Color.FromArgb(52, 112, 48), seed: 22, speckleCount: 34));
        Register("tile_grass_flower", () => PixelArtBuilder.CreateTexturedTile(
            Color.FromArgb(74, 148, 66), Color.FromArgb(98, 178, 82), Color.FromArgb(58, 124, 52), seed: 33, speckleCount: 24,
            accentColor: Color.FromArgb(248, 224, 96), accentCount: 3));
        Register("tile_sand", () => PixelArtBuilder.CreateTexturedTile(
            Color.FromArgb(220, 194, 122), Color.FromArgb(238, 214, 148), Color.FromArgb(192, 164, 96), seed: 44, speckleCount: 30));
        Register("tile_water_0", () => PixelArtBuilder.CreateWaterTile(0));
        Register("tile_water_1", () => PixelArtBuilder.CreateWaterTile(4));
        Register("tile_stone", () => PixelArtBuilder.CreateStoneBrickTile(
            Color.FromArgb(124, 132, 142), Color.FromArgb(90, 96, 108), Color.FromArgb(152, 160, 170), seed: 55));
        Register("tile_snow", () => PixelArtBuilder.CreateTexturedTile(
            Color.FromArgb(224, 238, 248), Color.FromArgb(246, 252, 255), Color.FromArgb(186, 210, 228), seed: 66, speckleCount: 26));
        Register("tile_swamp", () => PixelArtBuilder.CreateTexturedTile(
            Color.FromArgb(68, 92, 54), Color.FromArgb(88, 118, 66), Color.FromArgb(46, 66, 38), seed: 77, speckleCount: 32,
            accentColor: Color.FromArgb(98, 144, 72), accentCount: 2));
        Register("tile_darkstone", () => PixelArtBuilder.CreateStoneBrickTile(
            Color.FromArgb(60, 52, 76), Color.FromArgb(36, 30, 48), Color.FromArgb(88, 76, 112), seed: 88));
    }

    private void RegisterResourceSprites()
    {
        var treePal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(22, 34, 24),
            ['D'] = Color.FromArgb(30, 96, 44),
            ['G'] = Color.FromArgb(52, 144, 62),
            ['L'] = Color.FromArgb(96, 186, 84),
            ['H'] = Color.FromArgb(146, 216, 112),
            ['B'] = Color.FromArgb(108, 68, 38),
            ['T'] = Color.FromArgb(142, 92, 52),
            ['S'] = Color.FromArgb(72, 44, 24)
        };
        Register("res_tree", () => PixelArtBuilder.FromGrid(new[]
        {
            "....KKKKKKKK....",
            "..KKGGLLLLGGKK..",
            ".KGGLLHHHHLLGGK.",
            ".KGLLHHHHLLLLGK.",
            "KGGLLLLLLLLGGGDK",
            "KGGGLLLLGGGGGGDK",
            "KDGGGGGGGGGGDDDK",
            "KDDGGGGGGGGDDDDK",
            ".KDDDGGGGDDDDDK.",
            "..KKDDDDDDDDKK..",
            "....KKKBBKKK....",
            ".....KSBTBK.....",
            ".....KSBTBK.....",
            ".....KSBTBK.....",
            "....KKSBTBKK....",
            "....KKKKKKKK...."
        }, treePal));

        Register("res_stone", () => BuildRockNode(Color.FromArgb(170, 178, 190), Color.FromArgb(128, 136, 148)));
        Register("res_copper", () => BuildRockNode(Color.FromArgb(242, 146, 66), Color.FromArgb(196, 98, 38)));
        Register("res_iron", () => BuildRockNode(Color.FromArgb(216, 228, 242), Color.FromArgb(156, 172, 192)));
        Register("res_gold", () => BuildRockNode(Color.FromArgb(255, 224, 78), Color.FromArgb(218, 162, 32)));
    }

    private static Bitmap BuildRockNode(Color veinLight, Color veinDark)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(28, 32, 40),
            ['D'] = Color.FromArgb(82, 88, 98),
            ['M'] = Color.FromArgb(118, 126, 138),
            ['L'] = Color.FromArgb(156, 164, 176),
            ['V'] = veinLight,
            ['v'] = veinDark
        };
        return PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "....KKKKKKKK....",
            "..KKLLMMMMDDKK..",
            ".KLLMMVVMMMMDDK.",
            ".KLMVVvMMMMMDDK.",
            "KLMMMMMMMVVMMDDK",
            "KMMMVvMMVVvMMDDK",
            "KMMVvMMMMMMDDDDK",
            "KMMMMMMMVvMDDDDK",
            "KDMMMMMVvDDDDDDK",
            ".KDDDDDDDDDDDDK.",
            "..KKDDDDDDDDKK..",
            "....KKKKKKKK....",
            "................",
            "................"
        }, pal);
    }

    private void RegisterStructureSprites()
    {
        var woodWallPal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(46, 28, 16),
            ['D'] = Color.FromArgb(116, 72, 38),
            ['M'] = Color.FromArgb(154, 102, 54),
            ['L'] = Color.FromArgb(188, 132, 74),
            ['N'] = Color.FromArgb(64, 68, 76)
        };
        Register("str_wood_wall", () => PixelArtBuilder.FromGrid(new[]
        {
            "KKKKKKKKKKKKKKKK",
            "KLLLLLLLLLLLLLLK",
            "KMMNMMMMMMMMNMMK",
            "KMMMMMMMMMMMMMMK",
            "KDDDDDDDDDDDDDDK",
            "KKKKKKKKKKKKKKKK",
            "KLLLLLLLLLLLLLLK",
            "KMMMMMNMMMNMMMMK",
            "KMMMMMMMMMMMMMMK",
            "KDDDDDDDDDDDDDDK",
            "KKKKKKKKKKKKKKKK",
            "KLLLLLLLLLLLLLLK",
            "KMMNMMMMMMMMNMMK",
            "KMMMMMMMMMMMMMMK",
            "KDDDDDDDDDDDDDDK",
            "KKKKKKKKKKKKKKKK"
        }, woodWallPal));

        var stoneWallPal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(42, 46, 56),
            ['D'] = Color.FromArgb(88, 96, 108),
            ['M'] = Color.FromArgb(128, 138, 152),
            ['L'] = Color.FromArgb(166, 176, 190)
        };
        Register("str_stone_wall", () => PixelArtBuilder.FromGrid(new[]
        {
            "KKKKKKKKKKKKKKKK",
            "KLLLLLLKLLLLLLLK",
            "KMMMMMMKMMMMMMMK",
            "KDDDDDDKDDDDDDDK",
            "KKKKKKKKKKKKKKKK",
            "KLLLKLLLLLLKLLLK",
            "KMMMKMMMMMMKMMMK",
            "KDDDKDDDDDDKDDDK",
            "KKKKKKKKKKKKKKKK",
            "KLLLLLLKLLLLLLLK",
            "KMMMMMMKMMMMMMMK",
            "KDDDDDDKDDDDDDDK",
            "KKKKKKKKKKKKKKKK",
            "KLLLKLLLLLLKLLLK",
            "KMMMKMMMMMMKMMMK",
            "KKKKKKKKKKKKKKKK"
        }, stoneWallPal));

        var wbPal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(42, 26, 14),
            ['D'] = Color.FromArgb(114, 70, 36),
            ['M'] = Color.FromArgb(164, 108, 56),
            ['L'] = Color.FromArgb(202, 144, 82),
            ['S'] = Color.FromArgb(182, 194, 208),
            ['R'] = Color.FromArgb(216, 78, 62)
        };
        Register("str_workbench", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            ".KKKKKKKKKKKKKK.",
            ".KLLLLLLLLLLLLK.",
            ".KLLSSLLLLRRLLK.",
            ".KLLSSLLLLRRLLK.",
            ".KLLLLLLLLLLLLK.",
            ".KMMMMMMMMMMMMK.",
            ".KKKKKKKKKKKKKK.",
            ".KDDKMMMMMMKDDK.",
            ".KDDKDDDDDDKDDK.",
            ".KDDKKKKKKKKDDK.",
            ".KDDK......KDDK.",
            ".KDDK......KDDK.",
            ".KDDK......KDDK.",
            ".KKKK......KKKK.",
            "................"
        }, wbPal));

        Register("str_torch_0", () => BuildTorch(flickerRight: false));
        Register("str_torch_1", () => BuildTorch(flickerRight: true));
    }

    private static Bitmap BuildTorch(bool flickerRight)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(40, 26, 16),
            ['B'] = Color.FromArgb(132, 82, 42),
            ['I'] = Color.FromArgb(100, 108, 120),
            ['O'] = Color.FromArgb(246, 128, 36),
            ['Y'] = Color.FromArgb(255, 214, 74),
            ['W'] = Color.FromArgb(255, 250, 200)
        };
        string[] rows = flickerRight
            ? new[]
            {
                "........O.......",
                ".......OYO......",
                "......OYYYO.....",
                "......OYWYO.....",
                "......OYYYO.....",
                ".......OOO......",
                "......KIIIK.....",
                "......KBBBK.....",
                ".......KBK......",
                ".......KBK......",
                ".......KBK......",
                ".......KBK......",
                ".......KBK......",
                "......KBBBK.....",
                "......KKKKK.....",
                "................"
            }
            : new[]
            {
                ".......O........",
                "......OYO.......",
                "......OYYYO.....",
                "......OYWYO.....",
                "......OYYYO.....",
                ".......OOO......",
                "......KIIIK.....",
                "......KBBBK.....",
                ".......KBK......",
                ".......KBK......",
                ".......KBK......",
                ".......KBK......",
                ".......KBK......",
                "......KBBBK.....",
                "......KKKKK.....",
                "................"
            };
        return PixelArtBuilder.FromGrid(rows, pal);
    }

    private void RegisterCharacterAndEnemySprites()
    {
        Register("player_idle", () => BuildPlayerSprite(step: 0));
        Register("player_walk_0", () => BuildPlayerSprite(step: -1));
        Register("player_walk_1", () => BuildPlayerSprite(step: 1));

        Register("enemy_slime_0", () => BuildSlimeSprite(squashed: false));
        Register("enemy_slime_1", () => BuildSlimeSprite(squashed: true));

        var zombiePal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(20, 28, 22),
            ['G'] = Color.FromArgb(106, 154, 88),
            ['D'] = Color.FromArgb(72, 110, 62),
            ['R'] = Color.FromArgb(228, 56, 56),
            ['C'] = Color.FromArgb(116, 62, 62),
            ['P'] = Color.FromArgb(62, 70, 92)
        };
        Register("enemy_zombie", () => PixelArtBuilder.FromGrid(new[]
        {
            "....KKKKKKKK....",
            "...KGGGGGGGGK...",
            "...KGGRGGGRGK...",
            "...KGGGGGGGGK...",
            "...KGGDDDDGGK...",
            "..KKCCCCCCCCKK..",
            ".KGGCCCCCCCCGGK.",
            ".KGGCCCCCCCCGGK.",
            "..KKCCCCCCCCKK..",
            "....KPPPPPPK....",
            "....KPPKKPPK....",
            "....KPPKKPPK....",
            "....KDDKKDDK....",
            "....KKKKKKKK....",
            "................",
            "................"
        }, zombiePal));

        var archerPal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(24, 24, 30),
            ['W'] = Color.FromArgb(232, 228, 214),
            ['S'] = Color.FromArgb(182, 176, 162),
            ['H'] = Color.FromArgb(92, 68, 118),
            ['B'] = Color.FromArgb(162, 102, 48),
            ['R'] = Color.FromArgb(238, 74, 74)
        };
        Register("enemy_archer", () => PixelArtBuilder.FromGrid(new[]
        {
            "....KKKKKKKK....",
            "...KHHHHHHHHK...",
            "..KHHWWWWWWKHK..",
            "..KHWKRWWRKWHK..",
            "...KWWWWWWWWK...",
            "..KKHHHHHHHHKK..",
            ".KWHHHHHHHHHKBB.",
            ".KWHHHHHHHHHK.BK",
            "..KKSSSSSSSSK.BK",
            "....KWWKKWWKKBB.",
            "....KWWKKWWK....",
            "....KSSKKSSK....",
            "....KKKKKKKK....",
            "................",
            "................",
            "................"
        }, archerPal));

        var frostPal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(18, 28, 44),
            ['I'] = Color.FromArgb(156, 220, 248),
            ['B'] = Color.FromArgb(74, 148, 206),
            ['D'] = Color.FromArgb(46, 94, 148),
            ['W'] = Color.FromArgb(232, 248, 255),
            ['R'] = Color.FromArgb(255, 86, 112)
        };
        Register("enemy_frost", () => PixelArtBuilder.FromGrid(new[]
        {
            "..KK........KK..",
            ".KWWKKKKKKKKWWK.",
            "..KKIIIIIIIIKK..",
            "...KIIRIIIRIK...",
            "...KIIIIIIIIK...",
            "..KKBBBBBBBBKK..",
            ".KIIBBBBBBBBIIK.",
            ".KIIBBWWWWBBIIK.",
            "..KKDDDDDDDDKK..",
            "....KBBBBBBK....",
            "....KBBKKBBK....",
            "....KDDKKDDK....",
            "....KKKKKKKK....",
            "................",
            "................",
            "................"
        }, frostPal));

        Register("boss_normal", () => BuildBossSprite(enraged: false));
        Register("boss_enraged", () => BuildBossSprite(enraged: true));

        Register("enemy_golem", () => PixelArtBuilder.FromGrid(new[]
        {
            "...KKKKKKKKKK...",
            "..KLLLLLLLLLLK..",
            ".KLLMMMMMMMMLLK.",
            ".KLMYYMMMMYYMLK.",
            ".KLMMMMMMMMMMLK.",
            "KKMMMMMMMMMMMMKK",
            "KLLMMCCMMCCMMLLK",
            "KMMMMCCHHCCMMMMK",
            "KMMMMMMMMMMMMMMK",
            "KDDMMMMMMMMMMDDK",
            ".KKDDDDDDDDDDKK.",
            "..KDDDDKKDDDDK..",
            "..KDDDDKKDDDDK..",
            "..KKKKKKKKKKKK..",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(26, 30, 38),
            ['D'] = Color.FromArgb(84, 92, 104),
            ['M'] = Color.FromArgb(128, 138, 152),
            ['L'] = Color.FromArgb(174, 184, 198),
            ['C'] = Color.FromArgb(228, 134, 56),
            ['H'] = Color.FromArgb(255, 206, 86),
            ['Y'] = Color.FromArgb(255, 232, 92)
        }));

        Register("enemy_orc", () => PixelArtBuilder.FromGrid(new[]
        {
            "...KKKKKKKKKK...",
            "..KGGGGGGGGGGK..",
            "..KGGRRGGGRRGK..",
            "..KGGWGGGGWGGK..",
            "..KGGGDDDDGGGK..",
            ".KKBBBBBBBBBBKK.",
            "KGGBBIIIIIIBBGGK",
            "KGGBBIIIIIIBBGGK",
            ".KKBBBBBBBBBBKK.",
            "...KDDDDDDDDK...",
            "...KDDDKKDDDK...",
            "...KBBBKKBBBK...",
            "...KKKKKKKKKK...",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(22, 30, 18),
            ['G'] = Color.FromArgb(86, 152, 62),
            ['D'] = Color.FromArgb(56, 98, 42),
            ['R'] = Color.FromArgb(236, 62, 62),
            ['W'] = Color.FromArgb(245, 240, 215),
            ['B'] = Color.FromArgb(116, 68, 38),
            ['I'] = Color.FromArgb(164, 174, 188)
        }));

        Register("enemy_mage", () => PixelArtBuilder.FromGrid(new[]
        {
            "......KKKK......",
            ".....KPPPPK.....",
            "....KPPPPPPK....",
            "...KPPGGGGPPK...",
            "..KKPDDDDDDPKK..",
            "...KDYYDDYYDK...",
            "...KDDDDDDDDK...",
            "..KKPPPPPPPPKK..",
            ".KPPPPPGGPPPPPK.",
            ".KPPPPGGGGPPPPK.",
            "..KPPPPGGPPPPK..",
            "...KPPPPPPPPK...",
            "...KDDDDDDDDK...",
            "...KKKKKKKKKK...",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(22, 14, 34),
            ['P'] = Color.FromArgb(98, 46, 154),
            ['D'] = Color.FromArgb(46, 22, 74),
            ['G'] = Color.FromArgb(246, 198, 72),
            ['Y'] = Color.FromArgb(255, 92, 232)
        }));
    }

    private static Bitmap BuildPlayerSprite(int step)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(22, 24, 34),
            ['H'] = Color.FromArgb(124, 74, 38),
            ['S'] = Color.FromArgb(246, 206, 166),
            ['E'] = Color.FromArgb(28, 34, 48),
            ['B'] = Color.FromArgb(52, 134, 218),
            ['L'] = Color.FromArgb(92, 174, 248),
            ['G'] = Color.FromArgb(236, 192, 74),
            ['P'] = Color.FromArgb(62, 74, 102),
            ['F'] = Color.FromArgb(88, 54, 32)
        };
        string legs1 = step < 0 ? "...KPPK..KPPK..." : step > 0 ? "....KPPKKPPK...." : "....KPPKKPPK....";
        string legs2 = step < 0 ? "...KFFK..KKKK..." : step > 0 ? "...KKKK..KFFK..." : "....KFFKKFFK....";

        return PixelArtBuilder.FromGrid(new[]
        {
            "....KKKKKKKK....",
            "...KHHHHHHHHK...",
            "..KHHHHHHHHHHK..",
            "..KHHSSSSSSHHK..",
            "..KHSEESSEESHK..",
            "...KSSSSSSSSK...",
            "..KKLLLLLLLLKK..",
            ".KSSBBBBBBBBSSK.",
            ".KSSBBBBBBBBSSK.",
            "..KKHHHGGHHHKK..",
            "....KPPPPPPK....",
            legs1,
            legs2,
            "....KKKKKKKK....",
            "................",
            "................"
        }, pal);
    }

    private static Bitmap BuildSlimeSprite(bool squashed)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(20, 56, 26),
            ['D'] = Color.FromArgb(48, 152, 58),
            ['G'] = Color.FromArgb(92, 204, 84),
            ['L'] = Color.FromArgb(164, 242, 146),
            ['W'] = Color.FromArgb(242, 255, 242),
            ['E'] = Color.FromArgb(20, 26, 28)
        };
        string[] rows = squashed
            ? new[]
            {
                "................",
                "................",
                "................",
                "................",
                "...KKKKKKKKKK...",
                "..KGGLLGGGGGGK..",
                ".KGLWWGGGGGGGGK.",
                "KGGGGGGGGGGGGGDK",
                "KGGGEGGGGGEGGDDK",
                "KGGGEGGGGGEGGDDK",
                "KGGGGGGGGGGGGDDK",
                "KDDGGGGGGGGDDDDK",
                ".KDDDDDDDDDDDDK.",
                "..KKKKKKKKKKKK..",
                "................",
                "................"
            }
            : new[]
            {
                "................",
                "................",
                "....KKKKKKKK....",
                "...KGGLLGGGGK...",
                "..KGLWWGGGGGGK..",
                ".KGGGGGGGGGGGGK.",
                ".KGGGEGGGGEGGDK.",
                ".KGGGEGGGGEGGDK.",
                ".KGGGGGGGGGGDDK.",
                ".KDDGGGGGGGDDDK.",
                "..KDDDDDDDDDDK..",
                "...KKKKKKKKKK...",
                "................",
                "................",
                "................",
                "................"
            };
        return PixelArtBuilder.FromGrid(rows, pal);
    }

    private static Bitmap BuildBossSprite(bool enraged)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(24, 14, 30),
            ['C'] = Color.FromArgb(250, 204, 62),
            ['J'] = Color.FromArgb(236, 56, 86),
            ['B'] = enraged ? Color.FromArgb(224, 42, 68) : Color.FromArgb(148, 52, 186),
            ['L'] = enraged ? Color.FromArgb(255, 118, 92) : Color.FromArgb(196, 102, 232),
            ['D'] = enraged ? Color.FromArgb(136, 18, 36) : Color.FromArgb(92, 28, 122),
            ['E'] = Color.FromArgb(255, 244, 128)
        };
        return PixelArtBuilder.FromGrid(new[]
        {
            "..K...KKKK...K..",
            "..KCKKCCCCKKCK..",
            "..KCCJCCCCJCCK..",
            "..KKKKKKKKKKKK..",
            ".KLLLLLLLLLLLLK.",
            "KLBBBBBBBBBBBBDK",
            "KBBEEEBBBBEEEBDK",
            "KBBEEEBBBBEEEBDK",
            "KBBBBBBBBBBBBDDK",
            "KBBBBKKKKBBBBDDK",
            "KBBBKCCCCKBBBDDK",
            "KDBBBBBBBBBBBDDK",
            "KDDDBBBBBBBDDDDK",
            ".KDDDDDDDDDDDDK.",
            "..KKKKKKKKKKKK..",
            "................"
        }, pal);
    }

    private void RegisterItemAndProjectileSprites()
    {
        Register("wood", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "....KKKKKKKK....",
            "...KLLLLLLLLK...",
            "..KMMMMMMMMMMK..",
            "..KDDDDDDDDDDK..",
            "..KKKKKKKKKKKK..",
            "..KLLLLLLLLLLK..",
            "..KMMMMMMMMMMK..",
            "..KDDDDDDDDDDK..",
            "...KKKKKKKKKK...",
            "................",
            "................",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(46, 26, 14),
            ['D'] = Color.FromArgb(116, 70, 34),
            ['M'] = Color.FromArgb(158, 102, 52),
            ['L'] = Color.FromArgb(198, 142, 84)
        }));

        Register("stone", () => _sprites["res_stone"]);
        Register("copper_ore", () => _sprites["res_copper"]);
        Register("iron_ore", () => _sprites["res_iron"]);
        Register("gold_ore", () => _sprites["res_gold"]);
        Register("slime_gel", () => _sprites["enemy_slime_0"]);
        Register("workbench", () => _sprites["str_workbench"]);
        Register("wooden_wall", () => _sprites["str_wood_wall"]);
        Register("stone_wall", () => _sprites["str_stone_wall"]);
        Register("torch", () => _sprites["str_torch_0"]);

        Register("wood_axe", () => BuildToolIcon(isAxe: true, Color.FromArgb(182, 128, 72), Color.FromArgb(132, 84, 42)));
        Register("wood_pickaxe", () => BuildToolIcon(isAxe: false, Color.FromArgb(182, 128, 72), Color.FromArgb(132, 84, 42)));
        Register("stone_pickaxe", () => BuildToolIcon(isAxe: false, Color.FromArgb(168, 176, 188), Color.FromArgb(114, 122, 134)));
        Register("copper_pickaxe", () => BuildToolIcon(isAxe: false, Color.FromArgb(242, 148, 72), Color.FromArgb(186, 94, 38)));

        Register("copper_sword", () => BuildSwordIcon(Color.FromArgb(242, 152, 74), Color.FromArgb(196, 102, 40)));
        Register("iron_sword", () => BuildSwordIcon(Color.FromArgb(224, 236, 248), Color.FromArgb(148, 166, 188)));
        Register("gold_greatsword", () => PixelArtBuilder.FromGrid(new[]
        {
            "..........KKKK..",
            ".........KLLLLK.",
            "........KLLGDLK.",
            ".......KLLGDLK..",
            "......KLLGDLK...",
            ".....KLLGDLK....",
            "....KLLGDLK.....",
            "...KLLGDLK......",
            "..KGGGGDK.......",
            "..KGGGGK........",
            "...KBBGK........",
            "..KBBK..........",
            ".KGGK...........",
            ".KKK............",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(44, 28, 12),
            ['L'] = Color.FromArgb(255, 242, 148),
            ['G'] = Color.FromArgb(244, 194, 48),
            ['D'] = Color.FromArgb(192, 132, 24),
            ['B'] = Color.FromArgb(132, 64, 34)
        }));

        Register("fire_staff", () => PixelArtBuilder.FromGrid(new[]
        {
            ".........KKK....",
            "........KOOOK...",
            ".......KOYWYOK..",
            ".......KOYYWOK..",
            "........KOOOK...",
            "........KGGK....",
            ".......KBBK.....",
            "......KBBK......",
            ".....KBBK.......",
            "....KBBK........",
            "...KBBK.........",
            "..KBBK..........",
            ".KGGK...........",
            ".KKK............",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(40, 20, 14),
            ['O'] = Color.FromArgb(238, 92, 32),
            ['Y'] = Color.FromArgb(255, 204, 58),
            ['W'] = Color.FromArgb(255, 250, 206),
            ['G'] = Color.FromArgb(232, 178, 52),
            ['B'] = Color.FromArgb(128, 74, 38)
        }));

        Register("iron_crossbow", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "......KKKK......",
            ".....KIIIIK.....",
            "....KIIKKIIK....",
            "...KIIK..KIIK...",
            "..KIIKSSSSKIIK..",
            "..KIKBBYYBBKIK..",
            "..KKBBBBBBBBKK..",
            "....KKBBBBKK....",
            "......KBBK......",
            ".....KBBBBK.....",
            "....KBBKKBBK....",
            "....KK....KK....",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(26, 28, 36),
            ['I'] = Color.FromArgb(196, 210, 226),
            ['S'] = Color.FromArgb(228, 224, 206),
            ['B'] = Color.FromArgb(146, 92, 46),
            ['Y'] = Color.FromArgb(255, 218, 84)
        }));

        Register("wooden_bow", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            ".......KKKK.....",
            ".....KKBBBBK....",
            "....KBBK..KSK...",
            "...KBBK....KSK..",
            "...KBK.....KSK..",
            "..KBK......KSK..",
            "..KBK......KSK..",
            "..KBK......KSK..",
            "...KBK.....KSK..",
            "...KBBK....KSK..",
            "....KBBK..KSK...",
            ".....KKBBBBK....",
            ".......KKKK.....",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(38, 24, 14),
            ['B'] = Color.FromArgb(168, 108, 52),
            ['S'] = Color.FromArgb(228, 224, 206)
        }));

        Register("health_potion", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "......KKKK......",
            "......KCCK......",
            ".....KKKKKK.....",
            "......KGGK......",
            ".....KGWWGK.....",
            "....KGRRRRGK....",
            "...KGRWRRRRGK...",
            "...KGRRRRRRGK...",
            "...KGRRRRRRGK...",
            "...KGDRRRRDGK...",
            "....KGDDDDGK....",
            ".....KKKKKK.....",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(30, 20, 28),
            ['C'] = Color.FromArgb(174, 122, 68),
            ['G'] = Color.FromArgb(204, 226, 242),
            ['W'] = Color.FromArgb(255, 232, 236),
            ['R'] = Color.FromArgb(234, 52, 72),
            ['D'] = Color.FromArgb(164, 24, 44)
        }));

        Register("proj_player", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "........KK......",
            "..KKKKKKKKKK....",
            ".KWWBBBBBBYYK...",
            "..KKKKKKKKKK....",
            "........KK......",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(32, 24, 18),
            ['W'] = Color.FromArgb(245, 245, 250),
            ['B'] = Color.FromArgb(178, 122, 66),
            ['Y'] = Color.FromArgb(255, 228, 96)
        }));

        Register("proj_enemy", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "................",
            "................",
            ".....KKKKKK.....",
            "....KRRRRRRK....",
            "...KRRYYYYRRK...",
            "...KRYWWWWYRK...",
            "...KRYWWWWYRK...",
            "...KRRYYYYRRK...",
            "....KRRRRRRK....",
            ".....KKKKKK.....",
            "................",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(46, 12, 22),
            ['R'] = Color.FromArgb(232, 48, 78),
            ['Y'] = Color.FromArgb(255, 168, 72),
            ['W'] = Color.FromArgb(255, 248, 210)
        }));
    }

    private static Bitmap BuildToolIcon(bool isAxe, Color headLight, Color headDark)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(30, 22, 16),
            ['H'] = headLight,
            ['D'] = headDark,
            ['B'] = Color.FromArgb(148, 94, 48)
        };
        string[] rows = isAxe
            ? new[]
            {
                "................",
                ".......KKKKK....",
                "......KHHHHHK...",
                ".....KHHHHDHHK..",
                ".....KHHDKDDHK..",
                "......KKK.KDK...",
                ".......KBK.K....",
                "......KBK.......",
                ".....KBK........",
                "....KBK.........",
                "...KBK..........",
                "..KBK...........",
                "..KK............",
                "................",
                "................",
                "................"
            }
            : new[]
            {
                "................",
                ".....KKKKKK.....",
                "...KKHHHHHHKK...",
                "..KHHHDDDDHHHK..",
                "..KKK..KBK.KKK..",
                ".......KBK......",
                "......KBK.......",
                ".....KBK........",
                "....KBK.........",
                "...KBK..........",
                "..KBK...........",
                "..KK............",
                "................",
                "................",
                "................",
                "................"
            };
        return PixelArtBuilder.FromGrid(rows, pal);
    }

    private static Bitmap BuildSwordIcon(Color bladeLight, Color bladeDark)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(28, 26, 36),
            ['L'] = bladeLight,
            ['D'] = bladeDark,
            ['G'] = Color.FromArgb(242, 198, 68),
            ['B'] = Color.FromArgb(136, 82, 42)
        };
        return PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "...........KKK..",
            "..........KLLK..",
            ".........KLLDK..",
            "........KLLDK...",
            ".......KLLDK....",
            "......KLLDK.....",
            "....KKLLDK......",
            "...KGGGDK.......",
            "....KBGK........",
            "...KBBKGK.......",
            "..KBBK..K.......",
            "..KKK...........",
            "................",
            "................",
            "................"
        }, pal);
    }
}
