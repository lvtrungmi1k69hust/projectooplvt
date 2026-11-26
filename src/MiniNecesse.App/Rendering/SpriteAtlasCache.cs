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
        RegisterAnimalSprites();
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
        Register("farm_tilled", () => PixelArtBuilder.CreateTexturedTile(
            Color.FromArgb(92, 62, 36), Color.FromArgb(112, 76, 46), Color.FromArgb(68, 44, 24), seed: 99, speckleCount: 34));

        var sproutPal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(24, 48, 22),
            ['G'] = Color.FromArgb(72, 168, 62),
            ['L'] = Color.FromArgb(134, 218, 92),
            ['Y'] = Color.FromArgb(238, 198, 72),
            ['B'] = Color.FromArgb(198, 148, 42),
            ['O'] = Color.FromArgb(242, 134, 42),
            ['D'] = Color.FromArgb(192, 86, 22)
        };
        Register("crop_stage_0", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "......K..K......",
            ".....KL..LK.....",
            "......KGGK......",
            ".......GG.......",
            ".......KK.......",
            "................",
            "................",
            "................"
        }, sproutPal));
        Register("crop_stage_1", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "................",
            "................",
            ".....K....K.....",
            "....KLK..KLK....",
            ".....KGGGGK.....",
            "....KLGLLGLK....",
            ".....KGGGGK.....",
            "......KGGK......",
            "......KGGK......",
            ".......KK.......",
            "................",
            "................",
            "................",
            "................"
        }, sproutPal));
        Register("crop_stage_2", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "...K...KK...K...",
            "..KLK.KLLK.KLK..",
            "...KGKLLLLKGK...",
            "...KGGLLLLGGK...",
            "....KGGGGGGK....",
            "...KLGGGGGGGLK..",
            "....KGGGGGGK....",
            ".....KGGGGK.....",
            ".....KGGGGK.....",
            "......KKKK......",
            "................",
            "................",
            "................",
            "................"
        }, sproutPal));
        Register("crop_wheat_mature", () => PixelArtBuilder.FromGrid(new[]
        {
            "....K..KK..K....",
            "...KYK.YY.KYK...",
            "...KYB.YY.BYK...",
            "..KYYBKYYKBYYK..",
            "..KYBYBYYBYBYK..",
            "...KBBBBBBBBK...",
            "....KGGGGGGK....",
            "...KLGGGGGGGLK..",
            "....KGGGGGGK....",
            ".....KGGGGK.....",
            "......KGGK......",
            "......KKKK......",
            "................",
            "................",
            "................",
            "................"
        }, sproutPal));
        Register("crop_carrot_mature", () => PixelArtBuilder.FromGrid(new[]
        {
            "....KKKKKKKK....",
            "...KLLGGGGLLK...",
            "....KGGGGGGK....",
            ".....KGGGGK.....",
            "......KGGK......",
            ".....KOOOOK.....",
            "....KOODDOOK....",
            "....KOOODOOK....",
            ".....KOODOK.....",
            ".....KOOOOK.....",
            "......KOOK......",
            "......KOOK......",
            ".......KK.......",
            "................",
            "................",
            "................"
        }, sproutPal));
        Register("crop_pumpkin_mature", () => PixelArtBuilder.FromGrid(new[]
        {
            "......KKKK......",
            "......KGGK......",
            ".....KGGGGK.....",
            "...KKKGGGGKKK...",
            "..KOOODOODOOOK..",
            ".KOOOODOODOOOOK.",
            ".KOOOODOODOOOOK.",
            ".KOOOODOODOOOOK.",
            ".KOOOODOODOOOOK.",
            ".KDDDODDDDODDDK.",
            "..KDDDDDDDDDDK..",
            "...KKKKKKKKKK...",
            "................",
            "................",
            "................",
            "................"
        }, sproutPal));
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
        Register("res_coal", () => BuildRockNode(Color.FromArgb(46, 50, 62), Color.FromArgb(20, 22, 28)));

        Register("res_berry_bush", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "....KKKKKKKK....",
            "..KKGGLLLLGGKK..",
            ".KGGLLRRLLLLGGK.",
            ".KGLRRLGGLLRRGK.",
            "KGGLLLLGGGRRLGDK",
            "KGRRLLGGLLLLGGDK",
            "KDGGGGGRRGGPPGDK",
            "KDDGGGPPGGGGDDDK",
            ".KDDGGGGGGGDDDK.",
            "..KKDDDDDDDDKK..",
            "....KKKKKKKK....",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(22, 36, 24),
            ['D'] = Color.FromArgb(30, 92, 42),
            ['G'] = Color.FromArgb(52, 146, 64),
            ['L'] = Color.FromArgb(96, 186, 82),
            ['R'] = Color.FromArgb(228, 48, 72),
            ['P'] = Color.FromArgb(168, 44, 118)
        }));

        Register("res_wheat", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "....KK....KK....",
            "...KYLK..KYLK...",
            "...KYGK..KYGK...",
            "...KYLK..KYLK...",
            "..KKYLK.KKYLKK..",
            ".KYLKGYKKYLKGYK.",
            ".KYGKGGKKYGKGGK.",
            ".KYLKGGKKYLKGGK.",
            "..KGGGGKKGGGGK..",
            "..KGGGGKKGGGGK..",
            "...KGGK..KGGK...",
            "...KBBK..KBBK...",
            "..KBBBBKKBBBBK..",
            "..KKKKKKKKKKKK..",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(52, 38, 16),
            ['Y'] = Color.FromArgb(248, 214, 78),
            ['L'] = Color.FromArgb(255, 242, 142),
            ['G'] = Color.FromArgb(198, 152, 44),
            ['B'] = Color.FromArgb(116, 86, 42)
        }));

        Register("res_crystal", () => PixelArtBuilder.FromGrid(new[]
        {
            ".......KK.......",
            "......KWWK......",
            ".....KWLCCK.....",
            "..KK.KWLCCK.KK..",
            ".KWLKKLLCCKKCWK.",
            ".KWLCCLLCCKCWLK.",
            ".KCLCCBBCCKCCCK.",
            ".KCCBBBBBBCBDDK.",
            "KKCBBBBBBBBDDDK.",
            "KWLCBBBBBBBDDDKK",
            "KCLBBBBBBBBDDDDK",
            ".KCCDDDDDDDDDDK.",
            ".KDDDDDDDDDDDDK.",
            "..KKKKKKKKKKKK..",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(24, 20, 48),
            ['W'] = Color.FromArgb(224, 252, 255),
            ['L'] = Color.FromArgb(128, 236, 255),
            ['C'] = Color.FromArgb(64, 198, 242),
            ['B'] = Color.FromArgb(88, 124, 224),
            ['D'] = Color.FromArgb(62, 68, 156)
        }));
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

        Register("str_wood_fence", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "..KKK......KKK..",
            ".KLLLK....KLLLK.",
            ".KMMMK....KMMMK.",
            "KKKKKKKKKKKKKKKK",
            "KLLLLLLLLLLLLLLK",
            "KMMMMMMMMMMMMMMK",
            "KKKKKKKKKKKKKKKK",
            ".KMMMK....KMMMK.",
            "KKKKKKKKKKKKKKKK",
            "KLLLLLLLLLLLLLLK",
            "KDDDDDDDDDDDDDDK",
            "KKKKKKKKKKKKKKKK",
            ".KDDDK....KDDDK.",
            ".KKKKK....KKKKK.",
            "................"
        }, woodWallPal));

        Register("str_chest", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            ".KKKKKKKKKKKKKK.",
            ".KLLLLLLLLLLLLK.",
            ".KMMMMMMMMMMMMK.",
            ".KDDDDDDDDDDDDK.",
            ".KKKKKKKKKKKKKK.",
            ".KMMMMMGGMMMMMK.",
            ".KLLLLGGGGLLLLK.",
            ".KMMMMMGGMMMMMK.",
            ".KDDDDDDDDDDDDK.",
            ".KMMMMMMMMMMMMK.",
            ".KDDDDDDDDDDDDK.",
            ".KKKKKKKKKKKKKK.",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(36, 22, 12),
            ['L'] = Color.FromArgb(196, 134, 68),
            ['M'] = Color.FromArgb(156, 98, 46),
            ['D'] = Color.FromArgb(112, 68, 30),
            ['G'] = Color.FromArgb(248, 212, 74)
        }));
        _sprites["wooden_chest"] = _sprites["str_chest"];

        Register("str_door", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "..KKKKKKKKKKKK..",
            "..KLLLLLLLLLLK..",
            "..KMMKMMMMKMMK..",
            "..KMMMMMMMMMMK..",
            "..KMMKMMMMKMMK..",
            "..KMMMMMMMMMMK..",
            "..KMMMMMMGGMMK..",
            "..KMMMMMMGGMMK..",
            "..KMMKMMMMKMMK..",
            "..KMMMMMMMMMMK..",
            "..KMMKMMMMKMMK..",
            "..KDDDDDDDDDDK..",
            "..KKKKKKKKKKKK..",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(40, 24, 14),
            ['L'] = Color.FromArgb(196, 134, 68),
            ['M'] = Color.FromArgb(154, 96, 44),
            ['D'] = Color.FromArgb(110, 66, 30),
            ['G'] = Color.FromArgb(242, 204, 68)
        }));
        _sprites["wooden_door"] = _sprites["str_door"];
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

        Register("settler_miner", () => PixelArtBuilder.FromGrid(new[]
        {
            "....KKKKKKKK....",
            "...KYYYYYYYYK...",
            "..KYYWWWWWWYYK..",
            "..KSSSSSSSSSSK..",
            "..KSSESSSSESKK..",
            "..KSSSSSSSSSSK..",
            "...KKKKKKKKKK...",
            "..KKBBBBBBBBKK..",
            ".KBBBBBBBBBBBBK.",
            ".KSBBGGGGGGGBSK.",
            "..KBBBBBBBBBBK..",
            "...KDDDDDDDDK...",
            "...KPPK..KPPK...",
            "...KFFK..KFFK...",
            "...KKKK..KKKK...",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(24, 24, 34),
            ['Y'] = Color.FromArgb(244, 198, 62),
            ['W'] = Color.FromArgb(255, 244, 186),
            ['S'] = Color.FromArgb(246, 206, 166),
            ['E'] = Color.FromArgb(28, 34, 48),
            ['B'] = Color.FromArgb(72, 128, 184),
            ['G'] = Color.FromArgb(202, 146, 62),
            ['D'] = Color.FromArgb(108, 78, 52),
            ['P'] = Color.FromArgb(66, 74, 92),
            ['F'] = Color.FromArgb(74, 46, 26)
        }));

        Register("settler_hunter", () => PixelArtBuilder.FromGrid(new[]
        {
            "....KKKKKKKK....",
            "...KGGGGGGGGK...",
            "..KGGLLLLLLGGK..",
            "..KSSSSSSSSSSK..",
            "..KSSESSSSESKK..",
            "..KSSSSSSSSSSK..",
            "...KKKKKKKKKK...",
            "..KKGGGGGGGGKK..",
            ".KGGGGGGGGGGGGK.",
            ".KSGGGBBBGGGGGk.".Replace('k', 'K'),
            "..KGGGGGGGGGGK..",
            "...KBBBBBBBBK...",
            "...KPPK..KPPK...",
            "...KFFK..KFFK...",
            "...KKKK..KKKK...",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(24, 28, 24),
            ['G'] = Color.FromArgb(46, 134, 62),
            ['L'] = Color.FromArgb(92, 178, 96),
            ['S'] = Color.FromArgb(246, 206, 166),
            ['E'] = Color.FromArgb(28, 34, 48),
            ['B'] = Color.FromArgb(148, 94, 48),
            ['P'] = Color.FromArgb(62, 68, 56),
            ['F'] = Color.FromArgb(74, 46, 26)
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

    private void RegisterAnimalSprites()
    {
        Register("animal_chicken_0", () => BuildChickenSprite(step: false));
        Register("animal_chicken_1", () => BuildChickenSprite(step: true));
        Register("animal_sheep_0", () => BuildSheepSprite(step: false));
        Register("animal_sheep_1", () => BuildSheepSprite(step: true));
        Register("animal_cow_0", () => BuildCowSprite(step: false));
        Register("animal_cow_1", () => BuildCowSprite(step: true));
        Register("animal_pig_0", () => BuildPigSprite(step: false));
        Register("animal_pig_1", () => BuildPigSprite(step: true));
    }

    private static Bitmap BuildChickenSprite(bool step)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(30, 24, 24),
            ['W'] = Color.FromArgb(248, 246, 238),
            ['S'] = Color.FromArgb(212, 206, 194),
            ['R'] = Color.FromArgb(228, 56, 56),
            ['Y'] = Color.FromArgb(244, 178, 54),
            ['E'] = Color.FromArgb(20, 22, 28)
        };
        return PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "......KKK.......",
            ".....KRRRK......",
            "....KWWWWWK.....",
            "....KWEWWWK.....",
            "...KYWWWWWK.....",
            "...KKWWWRWK.....",
            "....KWWWWWWWKK..",
            "....KWWSSSSWWWK.",
            "....KWWSSSSSSWK.",
            ".....KWWWWWWWSK.",
            "......KSSSSSSK..",
            ".......KKKKKK...",
            step ? "......KYK..KYK.." : ".......KYKKYK...",
            step ? ".....KKK...KKK.." : "......KKKKKKKK..",
            "................"
        }, pal);
    }

    private static Bitmap BuildSheepSprite(bool step)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(28, 28, 34),
            ['W'] = Color.FromArgb(244, 242, 236),
            ['L'] = Color.FromArgb(214, 210, 200),
            ['F'] = Color.FromArgb(88, 82, 78),
            ['D'] = Color.FromArgb(62, 56, 52),
            ['E'] = Color.FromArgb(20, 22, 28)
        };
        return PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "....KKKKKKKK....",
            "...KWWWWWWWWK...",
            "..KWWLLLLLLWWK..",
            ".KWWLLWWWWLLWWK.",
            ".KWLLWFFFFWLLWK.",
            ".KWWKFEFFEFKWWK.",
            ".KWWKFFFFFFKWWK.",
            ".KWLLWWWWWWLLWK.",
            "..KWWLLLLLLWWK..",
            "..KKKWWWWWWKKK..",
            step ? "....KDK..KDK...." : ".....KDKKDK.....",
            step ? "....KKK..KKK...." : ".....KKKKKK.....",
            "................",
            "................"
        }, pal);
    }

    private static Bitmap BuildCowSprite(bool step)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(26, 22, 22),
            ['W'] = Color.FromArgb(244, 240, 234),
            ['B'] = Color.FromArgb(88, 62, 46),
            ['D'] = Color.FromArgb(58, 40, 30),
            ['P'] = Color.FromArgb(236, 174, 174),
            ['Y'] = Color.FromArgb(228, 212, 166),
            ['E'] = Color.FromArgb(18, 20, 26)
        };
        return PixelArtBuilder.FromGrid(new[]
        {
            "................",
            ".KK..........KK.",
            ".KYYKKKKKKKKYYK.",
            "..KKWWWWWWWWKK..",
            ".KBBWWWEWWWWBBK.",
            ".KWWWWWWWWWWBWK.",
            ".KWPPPPPPPPWWWK.",
            "..KKPPPPPPWWBBK.",
            "..KWWWWWWBBBBWK.",
            "..KWBBWWWWWWBWK.",
            "..KBBBWWWWWWWK..",
            "..KWWWWWWWWWWK..",
            "..KKKKKKKKKKKK..",
            step ? "..KDDK....KDDK.." : "...KDDK..KDDK...",
            step ? "..KKKK....KKKK.." : "...KKKK..KKKK...",
            "................"
        }, pal);
    }

    private static Bitmap BuildPigSprite(bool step)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(44, 24, 30),
            ['P'] = Color.FromArgb(244, 158, 174),
            ['L'] = Color.FromArgb(255, 194, 206),
            ['D'] = Color.FromArgb(206, 114, 134),
            ['N'] = Color.FromArgb(234, 134, 152),
            ['E'] = Color.FromArgb(24, 20, 26)
        };
        return PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "...KK......KK...",
            "...KPKKKKKKPK...",
            "..KPPPPPPPPPPK..",
            "..KPPEPPPPEPPK..",
            "..KPPNNNNNNPPK..",
            "..KPPNKNKNKPPK..",
            ".KLLLNNNNNNLLPK.",
            ".KPPPPPPPPPPPPK.",
            ".KPPDDDDDDDDPPK.",
            "..KKDDDDDDDDKK..",
            step ? "....KDK..KDK...." : ".....KDKKDK.....",
            step ? "....KKK..KKK...." : ".....KKKKKK.....",
            "................",
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
        Register("coal", () => _sprites["res_coal"]);
        Register("crystal", () => _sprites["res_crystal"]);
        Register("wheat", () => _sprites["res_wheat"]);
        Register("slime_gel", () => _sprites["enemy_slime_0"]);
        Register("workbench", () => _sprites["str_workbench"]);
        Register("wooden_wall", () => _sprites["str_wood_wall"]);
        Register("stone_wall", () => _sprites["str_stone_wall"]);
        Register("torch", () => _sprites["str_torch_0"]);
        Register("wooden_fence", () => _sprites["str_wood_fence"]);

        Register("berry", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "......KKKK......",
            ".....KGGGGK.....",
            "....KKGGGGKK....",
            "...KRRKGGKRRK...",
            "..KRHHRKKRHHRK..",
            "..KRRRRKKRRRRK..",
            "..KDRRRKKRRRDK..",
            "...KDDKRRKDDK...",
            "....KKRHHRKK....",
            ".....KRRRRK.....",
            ".....KDRRDK.....",
            "......KKKK......",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(30, 18, 28),
            ['G'] = Color.FromArgb(62, 164, 72),
            ['R'] = Color.FromArgb(228, 44, 72),
            ['D'] = Color.FromArgb(164, 24, 52),
            ['H'] = Color.FromArgb(255, 168, 186)
        }));

        Register("bread", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "................",
            "....KKKKKKKK....",
            "..KKLLLLLLLLKK..",
            ".KLLLLMMLLLLLLK.",
            ".KLLLMMLLMMLLMK.",
            "KLLLLLLLLLLLLMMK",
            "KMMMMMMMMMMMMMDK",
            "KDDDDDDDDDDDDDDK",
            ".KKKKKKKKKKKKKK.",
            "................",
            "................",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(54, 32, 16),
            ['L'] = Color.FromArgb(246, 198, 102),
            ['M'] = Color.FromArgb(218, 154, 62),
            ['D'] = Color.FromArgb(168, 102, 34)
        }));

        Register("raw_meat", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "....KKKKKK......",
            "...KWWWWWWKK....",
            "..KWRRRRRRWWK...",
            ".KWRRRWWRRRRRK..",
            ".KWRRWWRRRRRDK..",
            ".KWRRRRRRRRDDK..",
            "..KWRRRRRRDDDK..",
            "...KKDDDDDDKK...",
            ".....KWWWK......",
            ".....KWWK.......",
            "......KK........",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(46, 18, 24),
            ['W'] = Color.FromArgb(248, 238, 232),
            ['R'] = Color.FromArgb(224, 72, 84),
            ['D'] = Color.FromArgb(168, 42, 54)
        }));

        Register("cooked_meat", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "....KKKKKK......",
            "...KLLLLLLKK....",
            "..KLLBBBBBBLLK..",
            ".KLBBBBBBBBBBBK.",
            ".KBBWWBBBBBBDDK.",
            ".KBBBBBBBBDDDDK.",
            "..KBBBBDDDDDDK..",
            "...KKDDDDDDKK...",
            ".....KWWWK......",
            ".....KWWK.......",
            "......KK........",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(38, 20, 14),
            ['L'] = Color.FromArgb(204, 128, 68),
            ['B'] = Color.FromArgb(156, 82, 42),
            ['D'] = Color.FromArgb(108, 52, 24),
            ['W'] = Color.FromArgb(244, 236, 224)
        }));

        Register("wool", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "....KKKKKKKK....",
            "...KWWWWWWWWK...",
            "..KWWLLLLLLWWK..",
            ".KWWLWWWWWWLWWK.",
            ".KWLWWWWWWWWLWK.",
            ".KWWLSSSSSSLWWK.",
            "..KWWLLLLLLWWK..",
            "...KSSSSSSSSK...",
            "....KKKKKKKK....",
            "................",
            "................",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(48, 48, 56),
            ['W'] = Color.FromArgb(248, 248, 242),
            ['L'] = Color.FromArgb(228, 226, 218),
            ['S'] = Color.FromArgb(196, 194, 186)
        }));

        Register("feather", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "..........KKKK..",
            ".........KWWWWK.",
            "........KWWWWSK.",
            ".......KWWWWSK..",
            "......KWWBWWSK..",
            ".....KWWBWWSK...",
            "....KWWBWWSK....",
            "...KWWBWWSK.....",
            "...KWBWWSK......",
            "..KWBWWKK.......",
            "..KBBKK.........",
            ".KBBK...........",
            ".KKK............",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(42, 42, 52),
            ['W'] = Color.FromArgb(248, 248, 252),
            ['S'] = Color.FromArgb(206, 214, 228),
            ['B'] = Color.FromArgb(172, 134, 86)
        }));

        Register("leather", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "..KKK......KKK..",
            ".KLLLK....KLLLK.",
            "KLLLLKKKKKKLLLLK",
            "KMMMMMMMMMMMMMMK",
            "KMMLLLLLLLLLLMMK",
            ".KMMMMMMMMMMMMK.",
            ".KMMMMMMMMMMMMK.",
            ".KMMDDDDDDDDMMK.",
            "KDDDDDDDDDDDDDDK",
            "KDDDK......KDDDK",
            ".KKK........KKK.",
            "................",
            "................",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(42, 24, 14),
            ['L'] = Color.FromArgb(198, 128, 72),
            ['M'] = Color.FromArgb(162, 94, 46),
            ['D'] = Color.FromArgb(118, 64, 28)
        }));

        Register("hunter_bow", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            ".......KKKK.....",
            ".....KKGGGGK....",
            "....KGGK..KSK...",
            "...KBBK....KSK..",
            "...KBK.....KSK..",
            "..KBK......KSK..",
            "..KBK......KSK..",
            "..KBK......KSK..",
            "...KBK.....KSK..",
            "...KBBK....KSK..",
            "....KGGK..KSK...",
            ".....KKGGGGK....",
            ".......KKKK.....",
            "................",
            "................"
        }, new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(34, 22, 12),
            ['B'] = Color.FromArgb(132, 78, 36),
            ['G'] = Color.FromArgb(236, 188, 64),
            ['S'] = Color.FromArgb(236, 232, 214)
        }));

        Register("crystal_staff", () => PixelArtBuilder.FromGrid(new[]
        {
            ".........KKK....",
            "........KCCCK...",
            ".......KCLWLK...",
            ".......KCLCLK...",
            "........KCCCK...",
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
            ['K'] = Color.FromArgb(24, 22, 44),
            ['C'] = Color.FromArgb(64, 198, 244),
            ['L'] = Color.FromArgb(148, 242, 255),
            ['W'] = Color.FromArgb(234, 252, 255),
            ['G'] = Color.FromArgb(242, 198, 68),
            ['B'] = Color.FromArgb(128, 76, 40)
        }));

        Register("wood_axe", () => BuildToolIcon(isAxe: true, Color.FromArgb(182, 128, 72), Color.FromArgb(132, 84, 42)));
        Register("wood_hoe", () => BuildHoeIcon(Color.FromArgb(182, 128, 72), Color.FromArgb(132, 84, 42)));
        Register("wood_pickaxe", () => BuildToolIcon(isAxe: false, Color.FromArgb(182, 128, 72), Color.FromArgb(132, 84, 42)));

        RegisterFoodAndSeedSprites();
        RegisterArmorSprites();
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

    private static Bitmap BuildHoeIcon(Color headLight, Color headDark)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(30, 22, 16),
            ['H'] = headLight,
            ['D'] = headDark,
            ['B'] = Color.FromArgb(148, 94, 48)
        };
        return PixelArtBuilder.FromGrid(new[]
        {
            "................",
            ".....KKKKKK.....",
            "....KHHHHHHK....",
            "....KDDDDHHK....",
            "....KKKK.KBK....",
            ".........KBK....",
            "........KBK.....",
            ".......KBK......",
            "......KBK.......",
            ".....KBK........",
            "....KBK.........",
            "...KBK..........",
            "..KKK...........",
            "................",
            "................",
            "................"
        }, pal);
    }

    private void RegisterFoodAndSeedSprites()
    {
        var seedPal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(44, 34, 22),
            ['P'] = Color.FromArgb(204, 174, 124),
            ['D'] = Color.FromArgb(164, 134, 88),
            ['G'] = Color.FromArgb(236, 198, 72),
            ['O'] = Color.FromArgb(242, 128, 38),
            ['L'] = Color.FromArgb(92, 186, 72)
        };
        Register("wheat_seed", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "....KKKKKKKK....",
            "...KPPPPPPPPK...",
            "...KPGGGGGGPK...",
            "...KPPGGGGPPK...",
            "...KPGGGGGGPK...",
            "...KDDDDDDDDK...",
            "...KPPPPPPPPK...",
            "...KDDDDDDDDK...",
            "....KKKKKKKK....",
            "................",
            "................",
            "................",
            "................",
            "................"
        }, seedPal));
        Register("carrot_seed", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "....KKKKKKKK....",
            "...KPPPPPPPPK...",
            "...KPPPLLPPPK...",
            "...KPPPOOPPPK...",
            "...KPPPOOPPPK...",
            "...KDDDDDDDDK...",
            "...KPPPPPPPPK...",
            "...KDDDDDDDDK...",
            "....KKKKKKKK....",
            "................",
            "................",
            "................",
            "................",
            "................"
        }, seedPal));
        Register("pumpkin_seed", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "....KKKKKKKK....",
            "...KPPPPPPPPK...",
            "...KPPOOOOPPK...",
            "...KPOOOOOOPK...",
            "...KPPOOOOPPK...",
            "...KDDDDDDDDK...",
            "...KPPPPPPPPK...",
            "...KDDDDDDDDK...",
            "....KKKKKKKK....",
            "................",
            "................",
            "................",
            "................",
            "................"
        }, seedPal));

        var cropFoodPal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(36, 24, 18),
            ['G'] = Color.FromArgb(62, 168, 66),
            ['O'] = Color.FromArgb(242, 128, 38),
            ['D'] = Color.FromArgb(196, 84, 24),
            ['H'] = Color.FromArgb(255, 178, 96),
            ['B'] = Color.FromArgb(164, 108, 62),
            ['Y'] = Color.FromArgb(252, 214, 72),
            ['W'] = Color.FromArgb(244, 238, 224),
            ['C'] = Color.FromArgb(228, 174, 96)
        };
        Register("carrot", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            ".........KKK....",
            "........KGGGK...",
            ".......KGGK.....",
            "......KOOOK.....",
            ".....KOOHOK.....",
            "....KOODOK......",
            "....KOODOK......",
            "...KOODOK.......",
            "...KOODK........",
            "..KOODK.........",
            "..KOOK..........",
            "...KK...........",
            "................",
            "................",
            "................"
        }, cropFoodPal));
        Register("pumpkin", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "......KKKK......",
            "......KGGK......",
            "...KKKKKKKKKK...",
            "..KOOOODDOOOOK..",
            ".KOOOHODDOOOOOK.",
            ".KOOOHODDOOOOOK.",
            ".KOOOOODDOOOOOK.",
            ".KOOOOODDOOOODK.",
            ".KDDDDDDDDDDDDK.",
            "..KDDDDDDDDDDK..",
            "...KKKKKKKKKK...",
            "................",
            "................",
            "................",
            "................"
        }, cropFoodPal));
        Register("carrot_stew", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "......WWWW......",
            "....W......W....",
            "..KKKKKKKKKKKK..",
            ".KOOOHHHGOOOOOK.",
            ".KOODDOOOOOODDK.",
            "KKKKKKKKKKKKKKKK",
            "KBBBBBBBBBBBBBBK",
            ".KBBBBBBBBBBBBK.",
            "..KBBBBBBBBBBK..",
            "...KKKKKKKKKK...",
            "................",
            "................",
            "................",
            "................",
            "................"
        }, cropFoodPal));
        Register("pumpkin_pie", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "....KKKKKKKK....",
            "..KKCCCCCCCCKK..",
            ".KCCOOOOOOOOCOK.",
            ".KCOOOOWWOOOOCK.",
            ".KCCOOOOOOOOCOK.",
            ".KCCCCCCCCCCCCK.",
            "..KBBBBBBBBBBK..",
            "...KKKKKKKKKK...",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        }, cropFoodPal));
        Register("golden_bread", () => PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "................",
            "................",
            "....KKKKKKKK....",
            "..KKYYYYYYYYKK..",
            ".KYYYYWWYYYYYYK.",
            ".KYYYYYYYYYYYYK.",
            "KYYYYDDDDDYYYYYK",
            "KDDDDDDDDDDDDDDK",
            ".KKKKKKKKKKKKKK.",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        }, cropFoodPal));
    }

    private void RegisterArmorSprites()
    {
        Register("leather_helmet", () => BuildHelmetIcon(Color.FromArgb(204, 136, 76), Color.FromArgb(158, 94, 46), Color.FromArgb(112, 62, 28)));
        Register("leather_chestplate", () => BuildChestplateIcon(Color.FromArgb(204, 136, 76), Color.FromArgb(158, 94, 46), Color.FromArgb(112, 62, 28)));
        Register("leather_greaves", () => BuildGreavesIcon(Color.FromArgb(204, 136, 76), Color.FromArgb(158, 94, 46), Color.FromArgb(112, 62, 28)));

        Register("copper_helmet", () => BuildHelmetIcon(Color.FromArgb(246, 158, 82), Color.FromArgb(208, 114, 46), Color.FromArgb(154, 72, 26)));
        Register("copper_chestplate", () => BuildChestplateIcon(Color.FromArgb(246, 158, 82), Color.FromArgb(208, 114, 46), Color.FromArgb(154, 72, 26)));
        Register("copper_greaves", () => BuildGreavesIcon(Color.FromArgb(246, 158, 82), Color.FromArgb(208, 114, 46), Color.FromArgb(154, 72, 26)));

        Register("iron_helmet", () => BuildHelmetIcon(Color.FromArgb(228, 238, 248), Color.FromArgb(174, 188, 206), Color.FromArgb(116, 128, 148)));
        Register("iron_chestplate", () => BuildChestplateIcon(Color.FromArgb(228, 238, 248), Color.FromArgb(174, 188, 206), Color.FromArgb(116, 128, 148)));
        Register("iron_greaves", () => BuildGreavesIcon(Color.FromArgb(228, 238, 248), Color.FromArgb(174, 188, 206), Color.FromArgb(116, 128, 148)));
    }

    private static Bitmap BuildHelmetIcon(Color light, Color mid, Color dark)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(28, 24, 30),
            ['L'] = light,
            ['M'] = mid,
            ['D'] = dark
        };
        return PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "....KKKKKKKK....",
            "...KLLLLLLLLK...",
            "..KLLMMMMMMMLK..",
            "..KMMLMMMMMLMMK.",
            "..KMMMMMMMMMMDK.",
            "..KMMKKKKKKMMDK.",
            "..KMMK....KMMDK.",
            "..KDDK....KDDDK.",
            "..KKKK....KKKK..",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................"
        }, pal);
    }

    private static Bitmap BuildChestplateIcon(Color light, Color mid, Color dark)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(28, 24, 30),
            ['L'] = light,
            ['M'] = mid,
            ['D'] = dark
        };
        return PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "..KKK......KKK..",
            ".KLLLK....KLLLK.",
            "KLLMMKKKKKKMMLLK",
            "KMMLLLLLLLLLLMMK",
            "KDDDDMMMMMMDDDDK",
            ".KKKMMMMMMMMKKK.",
            "...KMMLLLLMMK...",
            "...KMMMMMMMMK...",
            "...KMDDDDDDMK...",
            "...KDDDDDDDDK...",
            "...KMMMMMMMMK...",
            "...KKKKKKKKKK...",
            "................",
            "................",
            "................"
        }, pal);
    }

    private static Bitmap BuildGreavesIcon(Color light, Color mid, Color dark)
    {
        var pal = new Dictionary<char, Color>
        {
            ['K'] = Color.FromArgb(28, 24, 30),
            ['L'] = light,
            ['M'] = mid,
            ['D'] = dark
        };
        return PixelArtBuilder.FromGrid(new[]
        {
            "................",
            "...KKKKKKKKKK...",
            "...KLLLLLLLLK...",
            "...KMMMMMMMMK...",
            "...KMMKKKKMMK...",
            "...KMMK..KMMK...",
            "...KMMK..KMMK...",
            "...KDDK..KDDK...",
            "...KMMK..KMMK...",
            "...KDDK..KDDK...",
            "...KDDK..KDDK...",
            "...KKKK..KKKK...",
            "................",
            "................",
            "................",
            "................"
        }, pal);
    }
}
