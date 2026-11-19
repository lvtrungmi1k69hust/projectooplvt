# BẢNG TRUY VẾT HAI CHIỀU A ↔ B & CHECKLIST KIỂM TOÁN MI4090 (1 THÀNH VIÊN)

> **Dự án:** MiniNecesse — 2D Top-Down Survival Crafting Game (.NET 8 / C# / WinForms)  
> **Tiêu chuẩn kiểm toán:** Lập trình hướng đối tượng MI4090 – v2026 (Single Source of Truth `A ↔ B`)  
> **Trạng thái kiểm toán:** 0 Errors, 0 Warnings, 27/27 xUnit Tests PASS (~1.2s), 0% UI Dependency trong `MiniNecesse.Core`, 0 Hardcoded Paths, 0 Empty `catch`.

---

## 1. Bảng Đối Chiếu 20 Tiêu Chí Checklist MI4090 (Code ↔ Tài liệu)

| STT | Tiêu chí chấm điểm (Checklist MI4090) | Trạng thái | Minh chứng cụ thể trong mã nguồn (File `.cs` & Lớp / Phương thức) |
| :-: | :--- | :-: | :--- |
| 1 | Kiến trúc 3 tầng tách biệt (`Core` – `Data` – `App`) & Solution chuẩn `.sln` | ĐẠT | `MiniNecesse_NhomXX_v2026.sln` gồm `src/MiniNecesse.Core`, `src/MiniNecesse.Data`, `src/MiniNecesse.App`, `tests/MiniNecesse.Core.Tests`. |
| 2 | Dependency Inversion (DIP): `MiniNecesse.Core` thuần C#, **0%** `WinForms`/`System.Drawing`/`UnityEngine` | ĐẠT | Toàn bộ 80 file trong `src/MiniNecesse.Core/` dùng `Vector2D` (`Math/Vector2D.cs`) & `RectF` (`Math/RectF.cs`); kết quả quét regex = **0** tham chiếu UI. |
| 3 | Phân rã đủ $\ge 12$ lớp trừu tượng & cụ thể có trách nhiệm SRP rõ ràng | ĐẠT | `Entity`, `Character`, `Player`, `Enemy`, `Boss`, `Tile`, `IslandLevel`, `ResourceNode`, `Structure`, `Projectile`, `DroppedItem`, `Item` (+5 lớp con), `Inventory`, `CraftingSystem`, `RuleEngine`, `GameSession` (> 35 lớp). |
| 4 | Interface Segregation (ISP): các interface nhỏ gọn, đơn mục đích | ĐẠT | `IDamageable`, `IMovable`, `IHarvestable`, `ICollectible`, `IWeapon`, `IInputProvider`, `IHudObserver`, `ISaveStorage`, `ILevelLoader`, `IHighScoreRepository`, `IAIStrategy`, `IGameState`, `IEntityFactory`. |
| 5 | Đóng gói & Bất biến (Encapsulation & Invariants) | ĐẠT | `Character.TakeDamage()` / `Heal()` giữ $0 \le HP \le MaxHP$; `RuleEngine.AddScore()` giữ $Score \ge 0$; `Inventory.Stacks` và `IslandLevel.Tiles` chỉ trả về `IReadOnlyList<T>`. |
| 6 | Đa hình thực chất (Polymorphism), **0** ép kiểu `is`/`as` thay cho đa hình | ĐẠT | `Item.ApplyHotbarSelection(Player)`, `Item.TryUse(Player)`, `Item.TryGetPlaceableStructure(...)` override trên 5 lớp con; `Enemy.IsBoss` override trên `Boss`. |
| 7 | Đủ 6 quan hệ UML 2.5 khớp 100% giữa Code và `docs/class_diagram.puml` | ĐẠT | **Inheritance:** `Entity<-Character<-Enemy<-Boss`; **Realization:** `IDamageable`, `IAIStrategy`; **Composition:** `IslandLevel *-- Tile` (`internal Tile`); **Aggregation:** `Inventory o-- ItemStack`; **Association:** `Player --> IWeapon`, `Enemy --> IAIStrategy`; **Dependency:** `RuleEngine.ResolveCollision(CollisionInfo)`, `CraftingSystem.TryCraft(Inventory, CraftingRecipe, ...)`. |
| 8 | Thuật toán 1: Va chạm AABB + `SpatialHashGrid` chống tunneling & kẹt góc | ĐẠT | `Physics/SpatialHashGrid.cs` (`Insert`, `Query` tái sử dụng buffer $O(n)$) & `Physics/CollisionDetector.cs` (`MoveWithTileCollision` tách trục X/Y, `TryAdvanceProjectileSwept<TTarget>` chia nhỏ bước). |
| 9 | Thuật toán 2: Tìm đường A* trên lưới `IslandLevel` (Heuristic Manhattan) | ĐẠT | `AI/AStarPathfinder.cs` (`FindPath`, `ComputeManhattan`, $O((V+E)\log V)$), xử lý biên khi người chơi xây tường bịt kín bằng `BlockingWallTile` và giới hạn `maxIterations`. |
| 10 | Thuật toán 3: Sinh đợt quái theo độ khó & FSM AI Quái/Boss | ĐẠT | `AI/RaidWaveSpawner.cs` (`SpawnNextWave`, `TryFindSafeSpawnPosition` tránh nước/tường/vùng an toàn) & `AI/EnemyFSM.cs` (`Idle->Patrol->Chase->Attack->Enraged->Dead`). |
| 11 | GoF Creational: **Factory Method** (không `switch-case` cứng) + **Object Pool** | ĐẠT | `Patterns/Factory/ItemFactory.cs`, `Patterns/Factory/EnemyFactory.cs` (dùng `Dictionary` registry, 0 `switch` trong Core); `Patterns/Pool/ObjectPool.cs` tái sử dụng `Projectile` & `DroppedItem` trong `GameSession`. |
| 12 | GoF Structural: **Flyweight** & **Decorator** (mở rộng OCP) | ĐẠT | `Patterns/Flyweight/TileDataFlyweightFactory.cs` chia sẻ dữ liệu tĩnh cho mọi `Tile`; `Patterns/Decorator/WeaponUpgradeDecorator.cs` + `FireDamageDecorator` + `SwiftAttackDecorator`. |
| 13 | GoF Behavioral: **State Pattern** & **Strategy Pattern** | ĐẠT | `Patterns/State/GameStateMachine.cs` (`MainMenuState`, `PlayingState`, `PausedState`, `VictoryState`, `GameOverState`); `AI/MeleeChaseStrategy.cs`, `RangedKiteStrategy.cs`, `BossEnrageStrategy.cs` hoán đổi qua `Enemy.SetAIStrategy()`. |
| 14 | GoF Behavioral: **Observer Pattern** tách hoàn toàn Core khỏi UI/Audio | ĐẠT | C# `event` trên `GameSession` (`OnHealthChanged`, `OnInventoryChanged`, `OnItemCrafted`, `OnScoreChanged`, `OnGameStateChanged`, `OnSoundEffectRequested`) nối tới `HudViewModel` (`IHudObserver`) và `AudioManager`. |
| 15 | Dữ liệu ngoài JSON & Đường dẫn tương đối (Zero Magic Numbers / Zero `C:\`) | ĐẠT | `src/MiniNecesse.Data/DataAssets/` chứa `config.json`, `recipes.json` (10 công thức), `island_01..05.json` (5 đảo); copy tự động qua `MiniNecesse.Data.csproj`; `JsonLevelLoader.cs` dùng `AppContext.BaseDirectory`. |
| 16 | Lưu/Tải trạng thái 100% (`GameStateDTO`) & Top 10 High Scores | ĐẠT | `Data/JsonSaveStorage.cs` (`Save`/`Load` nguyên tử ra `save_slot_1.json`), `GameSession.CreateSnapshot()` / `RestoreFromSnapshot()`, `Data/JsonHighScoreRepository.cs` (`highscore.json`). |
| 17 | Xử lý Ngoại lệ tường minh (Custom Exceptions, 0 `catch` rỗng) | ĐẠT | `Exceptions/InvalidLevelDataException.cs`, `SaveCorruptedException.cs`, `InsufficientMaterialException.cs`; mọi khối `catch` đều bọc ngoại lệ miền hoặc hiển thị thông báo UI/Debug log. |
| 18 | Bộ kiểm thử tự động xUnit độc lập GUI ($\ge 18$ test cases, 100% PASS) | ĐẠT | `tests/MiniNecesse.Core.Tests/` với `MockInputProvider`, `InMemorySaveStorage`, `SpyHudObserver` và **27/27 tests PASS** trong ~1.2 giây. |
| 19 | Giao diện WinForms 2D mượt 60 FPS (Fixed Timestep, DoubleBuffered, hủy event `-=`) | ĐẠT | `App/Forms/FrmMainMenu.cs`, `FrmGamePlay.cs` (`DoubleBufferedCanvas`, `Stopwatch` + `GameLoopController` 16.67ms, hủy sự kiện trong `OnFormClosing`), `RenderResourceCache.cs` (0 cấp phát `Brush`/`Pen`/`Font` khi vẽ), `TopDownRenderer.cs`, `CraftingOverlayPanel.cs`. |
| 20 | Quy trình Git sạch (nhánh feature, commit chuẩn Conventional Commits, `.gitignore`) | ĐẠT | `.gitignore` loại trừ `bin/`, `obj/`, `.vs/`, `publish/`, `Saves/`; lịch sử Git $\ge 15$ commits trên các nhánh `feat/*`, `docs/*` được merge rõ ràng về `main`. |

---

## 2. Hướng Dẫn Mở Rộng Trực Tiếp Khi Vấn Đáp (Chứng Minh OCP — Không Sửa Code Cũ)

Khi giảng viên yêu cầu **"hãy thêm ngay 1 loại Quái mới hoặc 1 Vũ khí / Cường hóa mới mà không sửa các lớp lõi cũ"**, bạn thực hiện đúng **3 bước** sau trong chưa đầy 2 phút:

### A. Thêm 1 Quái mới (VD: `MagmaSpider` — Nhện Dung Nham)
1. **Đăng ký Blueprint mới (Data-Driven, không sửa `Enemy` hay `EnemyFactory`):**
   Gọi `enemyFactory.RegisterEnemyBlueprint(new EnemyBlueprint("MagmaSpider", "Magma Spider", BaseMaxHp: 75, BaseMoveSpeed: 105f, BaseAttackDamage: 16, AttackRange: 34f, AttackCooldown: 0.85f, ScoreReward: 90, StrategyKey: "melee"));` tại Composition Root hoặc cấu hình khởi tạo.
2. *(Tùy chọn)* **Thêm hành vi AI riêng bằng Strategy mới:** Tạo 1 file lớp mới `LeapAmbushStrategy : IAIStrategy` trong `MiniNecesse.Core/AI/` và đăng ký qua `enemyFactory.RegisterStrategyCreator("leap", () => new LeapAmbushStrategy())`.
3. **Đưa vào màn chơi JSON:** Mở `src/MiniNecesse.Data/DataAssets/island_02.json` (hoặc bất kỳ đảo nào), thêm `{ "type": "MagmaSpider", "count": 3 }` vào mảng `"waves"` rồi chạy `dotnet build` & `dotnet run` — quái mới tự động xuất hiện và hoạt động!

### B. Thêm 1 Vũ khí mới hoặc 1 Cường hóa mới (VD: `PoisonDamageDecorator` / `GoldSword`)
1. **Thêm Vũ khí mới qua `ItemFactory`:** Đăng ký `new ItemBlueprint("gold_sword", "Gold Sword", ItemCategory.Weapon, 1, "Radiant golden blade.", BaseDamage: 48, AttackRange: 54f, BaseCooldown: 0.26f)` vào `itemFactory.RegisterBlueprint(...)` và thêm 1 mục công thức trong `DataAssets/recipes.json` (không sửa `Item`, `WeaponItem`, `CraftingSystem` hay `CraftingOverlayPanel`).
2. **Thêm Cường hóa mới qua GoF Decorator:** Tạo lớp mới `PoisonDamageDecorator : WeaponUpgradeDecorator` trong `MiniNecesse.Core/Patterns/Decorator/`, override `ComputeDamage()` và `Name`.
3. **Bọc động tại Runtime:** Gọi `player.EquipWeapon(new PoisonDamageDecorator(player.EquippedWeapon, bonusDamage: 10))` và kiểm chứng ngay bằng 1 unit test mới trong `DesignPatternsTests.cs` (`dotnet test`).
