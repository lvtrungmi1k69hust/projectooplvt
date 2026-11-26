# BẢNG TRUY VẾT HAI CHIỀU A ↔ B & CHECKLIST KIỂM TOÁN MI4090

> **Dự án:** MiniNecesse — 2D Top-Down Survival Crafting Game (.NET 8 / C# / WinForms)  
> **Solution:** `MiniNecesse_LVT_v2026.sln` (Single Source of Truth `A ↔ B`)  
> **Trạng thái kiểm toán thực tế:** 0 Errors, 0 Warnings, **61/61 xUnit Tests PASS** (`dotnet test MiniNecesse_LVT_v2026.sln`), **0** tham chiếu WinForms/`System.Drawing` trong `MiniNecesse.Core` (144 file `.cs`), 0 Hardcoded Paths, 0 Empty `catch`.

---

## 1. Bảng Đối Chiếu Tiêu Chí OOP / SOLID & Kiến Trúc (Code ↔ Tài liệu)

| STT | Tiêu chí MI4090 & Giai đoạn 2–3 | Trạng thái | Minh chứng cụ thể trong mã nguồn (File `.cs` / `.json`) |
| :-: | :--- | :-: | :--- |
| 1 | Kiến trúc 3 tầng tách biệt (`Core` – `Data` – `App`) & Solution chuẩn | ĐẠT | `MiniNecesse_LVT_v2026.sln` gồm `src/MiniNecesse.Core`, `src/MiniNecesse.Data`, `src/MiniNecesse.App`, `tests/MiniNecesse.Core.Tests`. |
| 2 | Dependency Inversion (DIP): `MiniNecesse.Core` thuần C#, **0%** UI | ĐẠT | 144 file trong `src/MiniNecesse.Core/` dùng `readonly struct Vector2D` & `RectF`; quét regex `System\.Drawing|System\.Windows\.Forms` = **0**. |
| 3 | Phân rã trách nhiệm SRP (> 50 lớp & bản ghi chuyên biệt) | ĐẠT | `GameSession` phối hợp với các coordinator chuyên trách: `FarmingSystem`, `LootTableRegistry`, `SettlementManager`, `HousingValidator`, `TutorialTracker`, `WeatherSystem`, `QuestSystem`, `ChunkManager`, `PathfindingCoordinator`, `PlayerBuffManager`. |
| 4 | Interface Segregation (ISP) & tách biệt tải/sinh thế giới | ĐẠT | `ILevelLoader` giữ nguyên cho 5 đảo JSON; `ILevelGenerator` thêm mới cho Endless seed; `IDamageMitigationStrategy`, `IFoodBuffStrategy`, `IActorAIStrategy<T>`, `IAIStrategy`, `ISettlerStrategy`, `IHighScoreRepository` hỗ trợ lọc bucket. |
| 5 | Đóng gói & Bất biến (Encapsulation & Invariants) | ĐẠT | `0 <= CurrentHp <= MaxHp`, `0 <= CurrentHunger <= MaxHunger`, `Score >= 0`, `0 <= Happiness <= 100`, `DifficultyDamageMitigationDecorator` bảo đảm Tutorial luôn nhận 0 sát thương kể cả đói & tấn công. |
| 6 | Đa hình thực chất (Polymorphism) | ĐẠT | Hệ phân cấp `Item` (`WeaponItem`, `ToolItem`, `ConsumableItem`, `MaterialItem`, `StructureItem`, `SeedItem`, `ArmorItem`), `IActorAIStrategy<T>`, `IFoodBuffStrategy`, `IDamageMitigationStrategy`. |
| 7 | Đủ 6 quan hệ UML 2.5 khớp giữa Code và `docs/class_diagram.puml` | ĐẠT | Inheritance (`Settler/Player/Enemy/Boss : Character`), Realization (`SeededEndlessLevelGenerator : ILevelGenerator`), Composition (`IslandLevel *-- WorldChunk *-- Tile`), Aggregation (`Player o-- Inventory`, `SettlementManager o-- Settler`), Association (`Player --> EquipmentLoadout / IWeapon`), Dependency (`GameSession ..> HousingValidator`). |
| 8 | Thuật toán 1: Va chạm AABB + `SpatialHashGrid` chống tunneling | ĐẠT | `Physics/SpatialHashGrid.cs`, `Physics/CollisionDetector.cs` (`MoveWithTileCollision`, `TryAdvanceProjectileSwept<TTarget>`). |
| 9 | Thuật toán 2: A* theo ngân sách frame & Streaming Chunk | ĐẠT | `AI/AStarPathfinder.cs` + `AI/PathfindingCoordinator.cs` (path cache + max node expansions + stagger repath); `World/ChunkManager.cs` (tầm nhìn 1–8 chunks, hibernate thực thể); `World/SeededEndlessLevelGenerator.cs` (FBM noise tất định 5 biome). |
| 10 | Thuật toán 3: BFS kiểm tra nhà kín & FSM Quái/Settler | ĐẠT | `Settlement/HousingValidator.cs` (BFS giới hạn khử trùng nhiều cửa); `AI/EnemyFSM.cs`, `Settlement/SettlerFSM.cs`. |
| 11 | GoF Creational: **Factory Method** + **Object Pool** | ĐẠT | `ItemFactory`, `EnemyFactory` (data-driven từ JSON/blueprint, không switch cứng); `ObjectPool<Projectile>`, `ObjectPool<DroppedItem>`. |
| 12 | GoF Structural: **Flyweight** & **Decorator** (mở rộng OCP) | ĐẠT | `TileDataFlyweightFactory`; `WeaponUpgradeDecorator` (+`FoodDamageBuffDecorator`); `DifficultyDamageMitigationDecorator : IDamageMitigationStrategy`. |
| 13 | GoF Behavioral: **Strategy**, **State** & **Observer** | ĐẠT | `GameStateMachine`; `IAIStrategy` & `ISettlerStrategy`; `IFoodBuffStrategy`; `IDamageMitigationStrategy`; Observer events trên `GameSession`, `DayNightCycle`, `WeatherSystem`, `QuestSystem`, `TutorialTracker`. |
| 14 | Dữ liệu ngoài JSON & Đường dẫn tương đối | ĐẠT | `DataAssets/`: `config.json`, `recipes.json` (38 công thức), `foods.json`, `crops.json`, `armor.json`, `loot_tables.json`, `settlers.json`, `endless_biomes.json`, `difficulties.json`, `tutorial_steps.json`, `quests.json`, `island_01..05.json`. |
| 15 | Persistence & HighScore tương thích ngược, chịu lỗi | ĐẠT | `JsonSaveStorage.cs` lưu `GameStateDTO` đầy đủ (mode, seed, armor, farm plots, chests, settlers, weather, quests, tutorial step); `JsonHighScoreRepository.cs` lưu Top 10 mỗi `(Mode × Difficulty)`, đọc được file cũ thiếu trường mới và không crash khi JSON hỏng. |
| 16 | Bộ kiểm thử tự động xUnit độc lập GUI | ĐẠT | `tests/MiniNecesse.Core.Tests/` (**61/61 tests PASS**): gồm `SurvivalAndFarmingTests`, `EquipmentLootAndChestTests`, `SettlementTests`, `EndlessWorldGenerationTests`, `DifficultyAndHighScoreTests`, `WeatherAndQuestTests`, cùng các suite gốc & tối ưu. |
| 17 | Giao diện WinForms 60 FPS & GDI+ hiện đại | ĐẠT | `App/UI/UiTheme.cs`, `App/Views/LeaderboardCanvasControl.cs`, `App/Views/SegmentedTabControl.cs` (vẽ GDI+ AntiAlias, huy chương Top 3 vector, hover, highlight bản ghi mới, fade/slide timer Dispose chuẩn, **0** cấp phát trong `OnPaint`), đồng bộ `FrmMainMenu` & `FrmSettingsDialog`. |

---

## 2. Hướng Dẫn Mở Rộng OCP Khi Bảo Vệ (Không Sửa Code Lõi)

1. **Thêm thức ăn / cây trồng / giáp / bảng loot / nghề settler / nhiệm vụ mới:** Chỉ cần bổ sung bản ghi JSON vào file tương ứng trong `src/MiniNecesse.Data/DataAssets/` (`foods.json`, `crops.json`, `armor.json`, `loot_tables.json`, `recipes.json`, `quests.json`).
2. **Thêm độ khó hoặc biome Endless mới:** Bổ sung entry mới trong `difficulties.json` hoặc `endless_biomes.json`, hoặc tạo lớp thực thi mới của `IDamageMitigationStrategy` / `ILevelGenerator` và truyền qua Constructor DI mà không sửa logic chiến đấu của `Player` hay `ILevelLoader`.
