# MiniNecesse (v2026) — 2D Top-Down Survival Crafting Game

> **Bài tập lớn Lập trình hướng đối tượng (MI4090 – v2026)**  
> **Ngôn ngữ & Nền tảng:** C# / .NET 8.0 (`net8.0` & `net8.0-windows` WinForms 2D Custom Canvas)  
> **Kiến trúc:** 3-Tier Clean Architecture (`Core` – `Data` – `App`) áp dụng SOLID, GoF Design Patterns, Fixed Timestep Game Loop và kiểm thử tự động xUnit.

---

## 1. Giới thiệu trò chơi

**MiniNecesse** là trò chơi sinh tồn – khai thác – chế tạo – xây dựng căn cứ – đánh Boss góc nhìn từ trên xuống (2D Top-Down) lấy cảm hứng từ *Necesse*:
- **Ban ngày:** Khám phá hòn đảo dạng lưới `Tilemap`, dùng Rìu chặt cây lấy Gỗ, dùng Cuốc đào Đá và Quặng Đồng/Sắt/Vàng, chế tạo Bàn chế tạo (`Workbench`), vũ khí, công cụ, Bình máu, Đuốc và xây tường gỗ/đá bảo vệ căn cứ.
- **Ban đêm:** Ánh sáng giảm dần (hiệu ứng màn tối bán trong suốt + vùng sáng quanh Nhân vật & Đuốc). Các đợt quái tập kích (`RaidWaveSpawner`) tìm đường vòng qua tường bằng thuật toán **A\*** để truy đuổi người chơi (tự động chuyển sang phá tường nếu bị vây kín).
- **Đánh Boss & Qua đảo:** Triệu hồi và hạ gục Boss của đảo (tự động kích hoạt pha **Enraged** đổi AI Strategy khi HP < 30%) để giành **Victory**, ghi tên vào Bảng điểm cao Top 10 và tiến sang đảo tiếp theo qua **5 hòn đảo cấu hình bằng JSON**:
  1. `ISLAND_01` — *Greenwood Isle* (Rừng Xanh) $\rightarrow$ Boss: **King Slime**
  2. `ISLAND_02` — *Copper Hills* (Đồi Đồng, Quái cung) $\rightarrow$ Boss: **Forest Guardian**
  3. `ISLAND_03` — *Murkwood Mire* (Đầm Lầy) $\rightarrow$ Boss: **Swamp Witch**
  4. `ISLAND_04` — *Frostpeak Expanse* (Băng Tuyết) $\rightarrow$ Boss: **Cryo Golem**
  5. `ISLAND_05` — *Shadow Citadel* (Pháo Đài Hắc Ám) $\rightarrow$ Boss: **Shadow Lord**

---

## 2. Cấu trúc Solution & Kiến trúc 3 tầng

```text
MiniNecesse_NhomXX_v2026.sln
├── src/
│   ├── MiniNecesse.Core/          # [LAYER 2] Pure C# Class Library (0% UI/Engine dependencies)
│   │   ├── Math/                  # Vector2D, RectF (thuần C#)
│   │   ├── Enums/                 # GameState, TileType, ItemCategory, ToolType, EnemyState, Direction, Difficulty...
│   │   ├── Exceptions/            # InvalidLevelDataException, SaveCorruptedException, InsufficientMaterialException
│   │   ├── Interfaces/            # IDamageable, IMovable, IHarvestable, ICollectible, IWeapon, IInputProvider, IHudObserver, ISaveStorage, ILevelLoader, IHighScoreRepository, IAIStrategy, IGameState, IEntityFactory
│   │   ├── Entities/              # Entity, Character, Player, Enemy, Boss, Tile, IslandLevel, ResourceNode, Structure, Projectile, DroppedItem
│   │   ├── Crafting/              # Item (+5 lớp con), ItemStack, Inventory, CraftingRecipe, CraftingSystem
│   │   ├── Physics/               # SpatialHashGrid<T>, CollisionDetector (AABB + Swept Anti-Tunneling + Axis Sliding)
│   │   ├── AI/                    # AStarPathfinder, EnemyFSM, RaidWaveSpawner, MeleeChaseStrategy, RangedKiteStrategy, BossEnrageStrategy
│   │   ├── Patterns/              # Factory Method, ObjectPool<T>, Flyweight, WeaponUpgradeDecorator, GameStateMachine (5 States)
│   │   └── Rules/                 # DayNightCycle, CollisionInfo, RuleEngine, GameLoopController (60 Hz), GameSession, GameStateDTO
│   ├── MiniNecesse.Data/          # [LAYER 3] JSON Persistence & Relative DataAssets/
│   │   ├── DataAssets/            # config.json, recipes.json, island_01.json .. island_05.json
│   │   ├── JsonLevelLoader.cs     # Thực thi ILevelLoader + validate schema
│   │   ├── JsonSaveStorage.cs     # Thực thi ISaveStorage (save_slot_1.json nguyên tử)
│   │   └── JsonHighScoreRepository.cs # Thực thi IHighScoreRepository (Top 10 highscore.json)
│   └── MiniNecesse.App/           # [LAYER 1] WinForms 2D Presentation (DoubleBuffered 60 FPS)
│       ├── Input/                 # WinFormsInputAdapter (IInputProvider), KeyBindingConfig
│       ├── Audio/                 # AudioManager (Observer OnSoundEffectRequested)
│       ├── Rendering/             # RenderResourceCache (0 GC in Paint), TopDownRenderer
│       ├── Views/                 # HudViewModel (IHudObserver), CraftingOverlayPanel
│       ├── Forms/                 # FrmMainMenu, FrmGamePlay, FrmSettingsDialog, FrmHighScoresDialog
│       └── Program.cs             # Composition Root (Constructor DI)
├── tests/
│   └── MiniNecesse.Core.Tests/    # 27 xUnit Tests (100% PASS, Mocks: MockInputProvider, InMemorySaveStorage, SpyHudObserver)
└── docs/
    ├── class_diagram.puml         # Sơ đồ lớp UML 2.5 khớp 100% mã nguồn
    ├── state_diagram.puml         # Sơ đồ trạng thái GameState & Enemy/Boss AI FSM
    ├── sequence_diagram.puml      # Sơ đồ tuần tự FixedUpdate(dt) & CraftItem
    └── TRACEABILITY_A_B.md        # Bảng truy vết A <-> B (20 tiêu chí MI4090) & hướng dẫn mở rộng OCP
```

*(Lưu ý: Bạn có thể đổi tên `MiniNecesse_NhomXX_v2026.sln` thành mã nhóm/mã sinh viên của mình trước khi nộp).*

---

## 3. Hướng dẫn Điều khiển

| Phím / Chuột | Chức năng |
| :--- | :--- |
| `W / A / S / D` hoặc phím mũi tên | Di chuyển 8 hướng chuẩn hóa vector |
| `Chuột trái (LMB)` | Tấn công quái / bắn cung / chặt cây / đào mỏ theo hướng trỏ chuột |
| `Chuột phải (RMB)` | Đặt công trình (`Workbench`, `WoodenWall`, `StoneWall`, `Torch`) hoặc uống `HealthPotion` |
| Phím `1` – `5` | Chọn nhanh ô trên thanh Hotbar |
| Phím `C` hoặc `Tab` | Bật / tắt bảng Túi đồ & Chế tạo (Crafting Overlay) |
| Phím `E` | Triệu hồi Boss của đảo hiện tại |
| Phím `Esc` | Mở Menu Tạm dừng (Tiếp tục / Lưu JSON / Tải JSON / Cài đặt / Về Menu) |

---

## 4. Hướng dẫn Build, Chạy Test & Đóng gói (CLI)

Yêu cầu: **.NET 8.0 SDK** (hoặc .NET 10 SDK trên Windows).

### Biên dịch toàn bộ Solution
```bash
dotnet build MiniNecesse_NhomXX_v2026.sln
```

### Chạy toàn bộ 27 Unit & Integration Tests (không cần GUI)
```bash
dotnet test MiniNecesse_NhomXX_v2026.sln --verbosity normal
```

### Chạy game trực tiếp
```bash
dotnet run --project src/MiniNecesse.App/MiniNecesse.App.csproj
```

### Đóng gói bản build độc lập vào thư mục `publish/`
```bash
dotnet publish src/MiniNecesse.App/MiniNecesse.App.csproj -c Release -o publish
```
Sau khi publish, chạy `publish/MiniNecesse.App.exe` (thư mục `publish/DataAssets/` chứa toàn bộ `config.json`, `recipes.json`, `island_01..05.json` được tự động đóng gói đi kèm).

---

## 5. Tài liệu Kiến trúc & Bảo vệ Đồ án
- [Sơ đồ lớp UML 2.5 (PlantUML)](docs/class_diagram.puml)
- [Sơ đồ trạng thái Game & AI (PlantUML)](docs/state_diagram.puml)
- [Sơ đồ tuần tự Game Loop & Crafting (PlantUML)](docs/sequence_diagram.puml)
- [Bảng kiểm toán & Truy vết hai chiều A ↔ B](docs/TRACEABILITY_A_B.md)
