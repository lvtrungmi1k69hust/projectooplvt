# MiniNecesse (v2026) — 2D Top-Down Survival Crafting Game

> **Bài tập lớn Lập trình hướng đối tượng (MI4090 – v2026)**  
> **Solution:** `MiniNecesse_LVT_v2026.sln` (.NET 8.0 `net8.0` & `net8.0-windows` WinForms 2D Custom Canvas)  
> **Kiến trúc:** 3-Tier Clean Architecture (`Core` – `Data` – `App`) áp dụng SOLID, GoF Design Patterns, Fixed Timestep Game Loop, Chunk Streaming và kiểm thử tự động **65/65 xUnit tests PASS**.

---

## 1. Giới thiệu & Tính năng hiện có

**MiniNecesse** là trò chơi sinh tồn – khai thác – nông trại – chế tạo – xây dựng khu định cư – đánh Boss góc nhìn từ trên xuống (2D Top-Down) lấy cảm hứng từ *Necesse*:

### Hai trục Chế độ chơi × Độ khó độc lập
- **Chế độ Thế giới (`GameMode`):**
  - **Campaign (`5` đảo JSON, mỗi đảo `320×320` tiles = `400` chunks `16×16`, có giới hạn ngày & Auto-Boss):** sinh địa hình, làng mạc ngẫu nhiên (`VillageSite`), chuồng trại gia súc (`Chicken`, `Sheep`, `Cow`, `Pig`), tài nguyên phân tầng và Boss riêng (`ISLAND_01..02`: `3` ngày, `ISLAND_03..04`: `4` ngày, `ISLAND_05`: `5` ngày; bước sang **đêm ngày cuối** hệ thống tự động triệu hồi Boss để kết thúc màn chơi).
  - **Endless (`ILevelGenerator` / `SeededEndlessLevelGenerator`):** sinh thế giới tất định **384×384 tiles (`576` chunks `16×16`)** từ `WorldSeed`, **không giới hạn số ngày (`maxDays = 0`)**, kết hợp **5 quần xã (`endless_biomes.json`: Forest, Desert, Swamp, Tundra, Volcano)**, tiền đồn và sóng quái tăng dần; cùng seed luôn tái tạo bản đồ giống hệt nhau.
- **Độ khó (`Difficulty` trong `difficulties.json`, áp dụng qua `DifficultyDamageMitigationDecorator`):**
  - **Tutorial:** Quái vẫn sinh và tấn công nhưng sát thương nhận vào (`DamageTakenMultiplier`) bằng `0` (người chơi không thể thua), kèm **chuỗi hướng dẫn 5 bước (`tutorial_steps.json`)** hiển thị trên HUD.
  - **Normal & Hard:** Khác biệt hệ số HP/sát thương quái, cường độ đợt tập kích (`RaidWaveSpawner`) và hệ số sát thương người chơi nhận vào.

### Vòng lặp Sinh tồn, Kinh tế, Âm thanh & Túi đồ vô hạn
- **Đói, Tốc chạy theo Đói & Thức ăn Buff (`config.json`, `foods.json`, `crops.json`):** Thanh đói giảm dần theo thời gian; cạn đói mất máu định kỳ (qua `Player.TakeDirectDamage`). Tốc độ chạy cơ bản của người chơi là `165.0` (nhanh hơn Boss `120–128`), **giảm dần tỷ lệ thuận theo điểm đói** (`minHungerSpeedMultiplier: 0.52`, tại ~`50/100` đói tốc chạy `~125.4` ngang Boss và dưới `50/100` sẽ chậm hơn Boss). Thức ăn hồi đói/HP và kích hoạt buff tốc độ/công/thủ/hồi phục thông qua `IFoodBuffStrategy` và `FoodDamageBuffDecorator`.
- **Túi đồ vô hạn & Hotbar `Replace` (`[C]` Recipes / Backpack):** Túi đồ (`Inventory.CreateUnlimited()`) lưu không giới hạn số lượng vật phẩm nhặt/săn/chế tạo. Bảng `[C]` có tab **Recipes** và **Backpack (All)** cùng nút **`Replace`** (cạnh `Craft` và trên mọi dòng Backpack) để gán nhanh vào 9 ô Hotbar: **3 ô đầu (`1W–3W`) chỉ nhận Vũ khí (`Weapon`)**, **6 ô sau (`4–9`) nhận mọi vật phẩm** (kể cả vũ khí mạnh hơn, thức ăn, công cụ, công trình).
- **Âm thanh thời gian thực (`WaveOutAudioEngine`, `ProceduralSoundBank`, `AudioManager`):** Hòa âm PCM 22.05 kHz qua Win32 `winmm.dll` (không cần thư viện ngoài), tự phát **nhạc nền tuần hoàn theo pha Ngày / Đêm / Boss (`bgm_day`, `bgm_night`, `bgm_boss`)** ngay khi vào bản đồ và phân biệt âm thanh riêng cho từng công cụ/hành động (`tool_axe`, `tool_pickaxe`, `tool_hoe`, `tool_sword`, `tool_bow`, `eat`, `potion`, `armor_equip`...); hỗ trợ ghi đè bằng file `.wav` đặt trong `Assets/Audio/` và chỉnh **Volume / Mute** ngay trong Cài đặt.
- **Giáp, Loot Table & Rương chứa đồ (`armor.json`, `loot_tables.json`):** 3 ô giáp `Head` / `Body` / `Legs` tổng hợp chỉ số `DEF` giảm sát thương qua `IDamageMitigationStrategy`; bảng rơi đồ theo xác suất cho mọi quái/Boss; cấu trúc `Chest` có `Inventory` lưu/nạp cùng thế giới.
- **Khu định cư Lite (`settlers.json`):** `HousingValidator` kiểm tra phòng kín bằng BFS giới hạn (tường + cửa `WoodenDoor`, khử trùng phòng nhiều cửa), quản lý NPC `Miner` / `Hunter` qua `SettlerFSM` + `ISettlerStrategy`, độ hài lòng ảnh hưởng năng suất & giá trao đổi hàng (`TryTrade`).
- **Thời tiết & Nhiệm vụ (`WeatherSystem`, `quests.json`):** Thời tiết (`Clear` / `Rain` / `Fog`) đồng bộ với `DayNightCycle` và 4 nhiệm vụ sinh tồn dữ liệu JSON thưởng điểm/vật phẩm.
- **Bảng xếp hạng hiện đại & Tối ưu 60 FPS:** Top 10 tách riêng từng cặp `(Mode × Difficulty)` (điểm Tutorial không lẫn với Normal/Hard), tương thích ngược `highscore.json` cũ và chịu lỗi JSON hỏng không crash; giao diện `FrmHighScoresDialog` tự vẽ GDI+ (`UiTheme`, huy chương vector Top 3, tab lọc bàn phím, 0 cấp phát trong `OnPaint`). Hỗ trợ Chunk Streaming (tầm nhìn 1–8 chunks chỉnh trong Cài đặt), `ChunkTerrainCache`, `NightLightmapRenderer`, `ObjectPool<T>`, và giới hạn node A* qua `PathfindingCoordinator`.

---

## 2. Cấu trúc Solution & Kiến trúc 3 tầng

```text
MiniNecesse_LVT_v2026.sln
├── src/
│   ├── MiniNecesse.Core/          # [LAYER 2] 144 files C# thuần (0% tham chiếu System.Drawing / WinForms)
│   │   ├── Math/                  # Vector2D, RectF (readonly struct)
│   │   ├── Enums/                 # GameState, GameMode, Difficulty, WeatherType, TileType, ItemCategory, ArmorSlot, SettlerProfession/State...
│   │   ├── Exceptions/            # InvalidLevelDataException, SaveCorruptedException, InsufficientMaterialException
│   │   ├── Interfaces/            # ILevelLoader, ILevelGenerator, IHighScoreRepository, ISaveStorage, IDamageMitigationStrategy, IFoodBuffStrategy, IAIStrategy, ISettlerStrategy, ...
│   │   ├── Entities/              # Entity, Character, Player, Enemy, Boss, Animal, Settler, Tile, IslandLevel, ResourceNode, Structure, Projectile, DroppedItem
│   │   ├── Crafting/              # Item (+SeedItem, ArmorItem, ConsumableItem...), Inventory, CraftingRecipe, CraftingSystem, EquipmentLoadout
│   │   ├── Farming/               # CropDefinition, CropRegistry, FarmPlot, FarmingSystem
│   │   ├── Loot/                  # LootDropResult, LootEntry, LootTable, LootTableRegistry
│   │   ├── Settlement/            # HousingValidator, SettlerCatalog, SettlerFSM, Miner/HunterSettlerStrategy, SettlementManager
│   │   ├── Tutorial/              # TutorialTriggerType, TutorialStepDefinition, TutorialTracker
│   │   ├── Quests/                # QuestObjectiveType, QuestDefinition, QuestProgress, QuestSystem
│   │   ├── Physics/               # SpatialHashGrid<T>, CollisionDetector (AABB + Swept Anti-Tunneling + Axis Sliding)
│   │   ├── AI/                    # AStarPathfinder, PathfindingCoordinator, EnemyFSM, RaidWaveSpawner, MeleeChase/RangedKite/BossEnrageStrategy
│   │   ├── World/                 # ChunkCoord, WorldChunk, ChunkManager, DeterministicNoise, ProceduralIslandGenerator, SeededEndlessLevelGenerator, EndlessBiomeCatalog
│   │   ├── Patterns/              # Factory Method, ObjectPool<T>, Flyweight, WeaponUpgradeDecorator (+FoodDamageBuffDecorator), GameStateMachine
│   │   └── Rules/                 # DayNightCycle, WeatherSystem, DifficultyProfile, DifficultyDamageMitigationDecorator, PlayerBuffManager, RuleEngine, GameLoopController, GameSession, GameStateDTO
│   ├── MiniNecesse.Data/          # [LAYER 3] JSON Persistence & Relative DataAssets/
│   │   ├── DataAssets/            # config.json, recipes.json (38 công thức), foods.json, crops.json, armor.json, loot_tables.json, settlers.json, endless_biomes.json, difficulties.json, tutorial_steps.json, quests.json, island_01..05.json
│   │   ├── JsonLevelLoader.cs     # Thực thi ILevelLoader + nạp toàn bộ catalog JSON & validate schema
│   │   ├── JsonSaveStorage.cs     # Thực thi ISaveStorage (save_slot_1.json nguyên tử)
│   │   └── JsonHighScoreRepository.cs # Thực thi IHighScoreRepository (Top 10 theo bucket Mode×Difficulty, tương thích ngược & chịu lỗi JSON)
│   └── MiniNecesse.App/           # [LAYER 1] WinForms 2D Presentation (DoubleBuffered 60 FPS)
│       ├── UI/                    # UiTheme, UiThemeResources (tập trung bảng màu/font/radius)
│       ├── Input/                 # WinFormsInputAdapter (IInputProvider), KeyBindingConfig
│       ├── Audio/                 # AudioManager, WaveOutAudioEngine, ProceduralSoundBank (Nhạc nền Day/Night/Boss & SFX từng công cụ qua Win32 winmm + hỗ trợ Assets/Audio/*.wav)
│       ├── Rendering/             # RenderResourceCache, SpriteAtlasCache, ChunkTerrainCache, NightLightmapRenderer, VisualEffectsPool, TopDownRenderer (0 GC Paint)
│       ├── Views/                 # HudViewModel, CraftingOverlayPanel, ChestOverlayPanel, SettlerOverlayPanel, SegmentedTabControl, LeaderboardCanvasControl
│       ├── Forms/                 # FrmMainMenu, FrmGamePlay, FrmSettingsDialog, FrmHighScoresDialog
│       └── Program.cs             # Composition Root (Constructor DI)
├── tests/
│   └── MiniNecesse.Core.Tests/    # 65 xUnit Tests (100% PASS)
└── docs/
    ├── AUDIT_REPORT.md            # Báo cáo kiểm toán Giai đoạn 1 & số liệu tối ưu Giai đoạn 2
    ├── class_diagram.puml         # Sơ đồ lớp UML 2.5 khớp mã nguồn
    ├── state_diagram.puml         # Sơ đồ trạng thái Game, AI Quái/Boss & SettlerFSM
    ├── sequence_diagram.puml      # Sơ đồ tuần tự Game Loop, Crafting, Endless & HighScores
    └── TRACEABILITY_A_B.md        # Bảng truy vết A ↔ B & chứng minh mở rộng OCP
```

---

## 3. Hướng dẫn Điều khiển

| Phím / Chuột | Chức năng |
| :--- | :--- |
| `W / A / S / D` hoặc phím mũi tên | Di chuyển 8 hướng chuẩn hóa vector |
| `Chuột trái (LMB)` | Tấn công quái / bắn cung / chặt cây / đào mỏ / cuốc đất / thu hoạch cây trồng |
| `Chuột phải (RMB)` | Dùng thuốc / ăn thức ăn / gieo hạt / đặt công trình (`Workbench`, `Wall`, `Door`, `Chest`, `Torch`, `Fence`) |
| `1` – `9` hoặc `Cuộn chuột` | Chọn nhanh ô trên thanh Hotbar |
| `C` hoặc `Tab` | Bật / tắt bảng Túi đồ vô hạn (`Backpack`), Chế tạo (`Recipes`), Trang bị Giáp & nút `Replace` gán Hotbar (`1W–3W` Vũ khí, `4–9` Tự do) |
| `V` | Bật / tắt bảng Khu định cư (kiểm tra nhà ở, tuyển & giao dịch Settler) |
| `E` | Tương tác theo ngữ cảnh: Mở Rương gần nhất / Giao dịch Settler / Triệu hồi Boss |
| `Esc` | Tạm dừng game (Tiếp tục / Lưu JSON / Tải JSON / Cài đặt / Về Menu) hoặc đóng bảng |
| Bảng xếp hạng (`FrmHighScoresDialog`) | `← / →` đổi Độ khó, `↑ / ↓ / Tab` đổi Chế độ, `Esc` đóng |

---

## 4. Hướng dẫn Build, Chạy Test & Đóng gói (CLI)

Yêu cầu: **.NET 8.0 SDK**.

### Biên dịch toàn bộ Solution
```bash
dotnet build MiniNecesse_LVT_v2026.sln
```

### Chạy toàn bộ 65 Unit & Integration Tests (không cần GUI)
```bash
dotnet test MiniNecesse_LVT_v2026.sln --verbosity normal
```

### Chạy game trực tiếp
```bash
dotnet run --project src/MiniNecesse.App/MiniNecesse.App.csproj
```

### Đóng gói bản build độc lập vào thư mục `publish/`
```bash
dotnet publish src/MiniNecesse.App/MiniNecesse.App.csproj -c Release -o publish
```

---

## 5. Tài liệu Kiến trúc & Bảo vệ Đồ án
- [Báo cáo kiểm toán & tối ưu hiệu năng](docs/AUDIT_REPORT.md)
- [Sơ đồ lớp UML 2.5 (PlantUML)](docs/class_diagram.puml)
- [Sơ đồ trạng thái Game, AI & Settler (PlantUML)](docs/state_diagram.puml)
- [Sơ đồ tuần tự Game Loop, Crafting, Endless & HighScores (PlantUML)](docs/sequence_diagram.puml)
- [Bảng kiểm toán & Truy vết hai chiều A ↔ B](docs/TRACEABILITY_A_B.md)
