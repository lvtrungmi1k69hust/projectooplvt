# BÁO CÁO KIỂM TOÁN MÃ NGUỒN & HIỆU NĂNG — MININECESSE (v2026)

> **Dự án:** MiniNecesse (`MiniNecesse_LVT_v2026.sln`) — C# / .NET 8 (`Core` – `Data` – `App`)  
> **Phạm vi kiểm toán:** 100% mã nguồn thực tế trong `src/`, `tests/`, `DataAssets/`, `docs/`, `README.md` (không tin tài liệu nếu không khớp code).  
> **Baseline Build & Test tại thời điểm kiểm toán:** `0 Errors`, `0 Warnings`, `37/37` xUnit Tests PASS (`dotnet test MiniNecesse_LVT_v2026.sln`).  
> **Lưu ý môi trường:** Thư mục `.git/` có lịch sử đến commit `702833d` nhưng `git.exe` chưa được cài trên máy hiện tại (sẽ liệt kê hướng dẫn gom commit ở cuối Giai đoạn 2 theo thỏa thuận với người dùng).

---

## 1. ĐỐI CHIẾU `README.md` & TÀI LIỆU (`docs/`) VỚI MÃ NGUỒN THẬT

| Hạng mục / Khẳng định | Vị trí tuyên bố | Đánh giá | Bằng chứng mã nguồn thực tế (`file:line`) |
| :--- | :--- | :---: | :--- |
| Tên file Solution `MiniNecesse_NhomXX_v2026.sln` | `README.md:26,84,89`, `TRACEABILITY_A_B.md:13` | **SAI** | File thực tế ở root là [MiniNecesse_LVT_v2026.sln](file:///c:/codegameoop/MiniNecesse_LVT_v2026.sln); lệnh CLI trong README báo lỗi `MSB1009` nếu copy nguyên văn. |
| Kiến trúc 3 tầng, `MiniNecesse.Core` **0%** phụ thuộc WinForms / `System.Drawing` | `README.md:28`, `TRACEABILITY_A_B.md:14` | **ĐÚNG** | [MiniNecesse.Core.csproj](file:///c:/codegameoop/src/MiniNecesse.Core/MiniNecesse.Core.csproj#L1-L9) không tham chiếu UI; quét toàn bộ `src/MiniNecesse.Core/` chỉ có 1 chuỗi `"WinForms"` nằm trong XML doc comment ở [IInputProvider.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Interfaces/IInputProvider.cs#L7). |
| Bộ kiểm thử "27 xUnit Tests" | `README.md:52,87`, `TRACEABILITY_A_B.md:5,30` | **SAI (Lỗi thời)** | Bộ test hiện có **37/37 test PASS** (đã bổ sung [ChunkStreamingTests.cs](file:///c:/codegameoop/tests/MiniNecesse.Core.Tests/ChunkStreamingTests.cs) và [WorldContentTests.cs](file:///c:/codegameoop/tests/MiniNecesse.Core.Tests/WorldContentTests.cs)). |
| "10 công thức" & "Phím `1`–`5` chọn Hotbar" | `TRACEABILITY_A_B.md:27`, `README.md:71` | **SAI (Lỗi thời)** | [recipes.json](file:///c:/codegameoop/src/MiniNecesse.Data/DataAssets/recipes.json) hiện có **20 công thức**; Hotbar dùng **9 ô** (`1–9` + cuộn chuột) tại [config.json](file:///c:/codegameoop/src/MiniNecesse.Data/DataAssets/config.json#L10) và [TopDownRenderer.cs](file:///c:/codegameoop/src/MiniNecesse.App/Rendering/TopDownRenderer.cs#L25). |
| **"0 GC in Paint"** (`RenderResourceCache`) | `README.md:47`, `TRACEABILITY_A_B.md:31`, `RenderResourceCache.cs:10` | **SAI** | Baseline đo thực tế: **1,880 B/frame (Day)** và **8,002 B/frame (Night + Boss)**. Nguyên nhân: ~25 lần nối chuỗi `$"..."` trong [TopDownRenderer.cs](file:///c:/codegameoop/src/MiniNecesse.App/Rendering/TopDownRenderer.cs#L571-L744), gọi `.AsReadOnly()` bên trong vòng lặp `for` ở dòng `489`, `546`, và boxing enumerator `LoadedChunkCoords` ở dòng `599`. |
| Thuật toán **A\*** trên lưới `IslandLevel` | `README.md:13,36`, `TRACEABILITY_A_B.md:21` | **CHƯA ĐẦY ĐỦ** | [AStarPathfinder.cs](file:///c:/codegameoop/src/MiniNecesse.Core/AI/AStarPathfinder.cs#L92-L283) dùng Manhattan + fallback phá tường có `maxIterations=500`. Tuy nhiên mỗi `Enemy` tạo instance `AStarPathfinder` riêng ([EnemyFactory.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Patterns/Factory/EnemyFactory.cs#L181)), không có shared path cache, không stagger repath (gây spike 18 quái repath cùng tick), và `RangedKiteStrategy` / `BossEnrageStrategy` không dùng A\*. |
| **`SpatialHashGrid<T>`** tăng tốc va chạm $O(n)$ | `README.md:35`, `TRACEABILITY_A_B.md:20` | **ĐÚNG (Có dư thừa)** | [SpatialHashGrid.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Physics/SpatialHashGrid.cs#L27-L172) hoạt động $O(k+m)$ với **0 B/op** steady-state. Nhưng `_enemySpatialGrid` trong [GameSession.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L39) vẫn `Clear`/`Insert` mỗi tick dù không còn nơi nào `Query`, và `_buckets` không tự tỉa khi đi xa trên map 320×320. |
| **Boss Enrage** (< 30% HP đổi Strategy) | `README.md:14`, `state_diagram.puml:38` | **ĐÚNG (Có bug)** | [Boss.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Entities/Boss.cs#L88-L114) kích hoạt `EnterEnragedPhase()`. Nhưng [BossEnrageStrategy.cs](file:///c:/codegameoop/src/MiniNecesse.Core/AI/BossEnrageStrategy.cs#L89-L100) bắn đạn với **tầm vô hạn** (`> AttackRange` không chặn trần) và xả đồng thời cả đạn + melee trên cùng 1 tick ở cự ly gần; ngoài ra [GameSession.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L598) bỏ qua chỉ số `BossConfiguration` từ JSON! |
| **GoF Factory Method** (không `switch-case`, data-driven JSON) | `TRACEABILITY_A_B.md:23`, `EnemyFactory.cs:14` | **CHƯA ĐẦY ĐỦ** | 0 `switch` trong Core (ĐÚNG), nhưng [ItemFactory.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Patterns/Factory/ItemFactory.cs#L95-L137) và [EnemyFactory.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Patterns/Factory/EnemyFactory.cs#L222-L261) **hardcode toàn bộ blueprint trong C#** thay vì đọc từ `DataAssets/`. |
| **GoF Object Pool** (`ObjectPool<T>`) | `TRACEABILITY_A_B.md:23`, `ObjectPool.cs:8` | **CHƯA ĐẦY ĐỦ** | Đã dùng cho `Projectile` & `DroppedItem` trong `GameSession`, nhưng [RuleEngine.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/RuleEngine.cs#L145) vẫn `new DroppedItem(...)` khi thu hoạch (rồi bị bỏ đi lấy mỗi tọa độ/itemId), và `ClearActiveWorldEntities` ([GameSession.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L1636-L1637)) xóa list mà không `Release` về pool. |
| **GoF Flyweight** (`TileDataFlyweightFactory`) | `TRACEABILITY_A_B.md:24` | **ĐÚNG** | [TileDataFlyweightFactory.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Patterns/Flyweight/TileDataFlyweightFactory.cs) chia sẻ trạng thái nội tại qua toàn bộ `Tile` của map 320×320. |
| **GoF Decorator** (`WeaponUpgradeDecorator`) | `TRACEABILITY_A_B.md:24,46-49` | **CHƯA ĐẦY ĐỦ** | `FireDamageDecorator` & `SwiftAttackDecorator` chạy trong unit test ([DesignPatternsTests.cs](file:///c:/codegameoop/tests/MiniNecesse.Core.Tests/DesignPatternsTests.cs#L17-L29)), có cả trường `WeaponUpgradeLevel` ở [GameStateDTO.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameStateDTO.cs#L112), nhưng **chưa được nối vào gameplay hay save/load**. |
| **GoF State / Strategy / Observer** | `TRACEABILITY_A_B.md:25-26` | **ĐÚNG** | `GameStateMachine` (5 state), `IAIStrategy` (3 chiến lược), `IHudObserver` + event đều tách khỏi UI. Có bug `NotifyDayPhase()` bị gọi thừa 60 Hz ([GameSession.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L722)) và `Dispose()` thiếu `-=` entity. |
| **Fixed Timestep Game Loop & Render Interpolation** | `GameLoopController.cs:29`, `FrmGamePlay.cs:25` | **CHƯA ĐẦY ĐỦ** | [GameLoopController.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameLoopController.cs#L29) đã cài `Stopwatch` + accumulator + `InterpolationAlpha`, nhưng `FrmGamePlay.cs:365-370` và `TopDownRenderer.cs` **không dùng `InterpolationAlpha`**, còn `Entity` chưa lưu `PreviousPosition`. |
| **Lưu/Tải trạng thái 100% (`GameStateDTO`)** | `TRACEABILITY_A_B.md:28` | **CHƯA ĐẦY ĐỦ** | Khi load save: [GameSession.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L629-L668) **bỏ qua `snapshot.Difficulty`** và gọi `DayNightCycle.RestoreState(...)` phát `OnDayPhaseChanged` khi `Night` khiến `DispatchNightWave()` bắn thêm đợt quái mới; `WeaponUpgradeLevel` không được lưu/khôi phục. |
| **Sơ đồ UML & Traceability (`docs/*.puml`)** | `README.md:54-57`, `docs/` | **LỖI THỜI** | Chưa cập nhật các lớp mới: `ChunkCoord`, `WorldChunk`, `ChunkManager`, `ProceduralIslandGenerator`, `VillageSite`, `Animal`, `SpriteAtlasCache`, `VisualEffectsPool`, `AppSettings`. |

---

## 2. PHÂN LOẠI LỖI, CODE SMELL, VI PHẠM SOLID & RÒ RỈ TÀI NGUYÊN

### Mức độ CRITICAL (Ưu tiên xử lý ngay ở Giai đoạn 2)

1. **`[C1]` Rò rỉ nghiêm trọng USER Handles & GDI+ `Font`/`Control` trong Crafting UI**  
   - **Vị trí:** [CraftingOverlayPanel.cs](file:///c:/codegameoop/src/MiniNecesse.App/Views/CraftingOverlayPanel.cs#L47-L68), [CreateRecipeRow](file:///c:/codegameoop/src/MiniNecesse.App/Views/CraftingOverlayPanel.cs#L184-L195), [ClearRecipeRows](file:///c:/codegameoop/src/MiniNecesse.App/Views/CraftingOverlayPanel.cs#L217-L226)  
   - **Hiện trạng:** Mỗi lần mở/craft gọi `RefreshView()` tạo 20 `Panel` + 60 `Label`/`PictureBox`/`Button` + **40 đối tượng `new Font(...)` mới**, rồi `ClearRecipeRows()` chỉ gọi `_recipeListPanel.Controls.Clear()` **mà không `.Dispose()` control cũ hay `Font`**.  
   - **Tác động đo được:** **+1,001 USER handles & +13 GDI handles rò rỉ chỉ sau 10 lần refresh (~100 handle/lần, ~296 KB/lần)**. Chơi lâu (~95 lần mở/craft) sẽ chạm trần 10,000 handle của Windows gây crash `Win32Exception`.  
   - **Đề xuất sửa:** Tạo sẵn 4 `Font` dùng chung trên `CraftingOverlayPanel` và dispose trong `Dispose(bool)`; trong `ClearRecipeRows()` tách `pb.Image = null` (tránh dispose sprite dùng chung của `SpriteAtlasCache`) rồi gọi `.Dispose()` toàn bộ row/control đã gỡ.

2. **`[C2]` Cấp phát bộ nhớ (GC churn) mỗi frame trong `OnPaint` và vòng lặp 60 Hz**  
   - **Vị trí:** [GameSession.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L131-L149), [Inventory.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Crafting/Inventory.cs#L26), [IslandLevel.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Entities/IslandLevel.cs#L81,L108), [WorldChunk.cs](file:///c:/codegameoop/src/MiniNecesse.Core/World/WorldChunk.cs#L35), [TopDownRenderer.cs](file:///c:/codegameoop/src/MiniNecesse.App/Rendering/TopDownRenderer.cs#L489-L744), [GameSession.StepPlayingSimulation](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L715-L720)  
   - **Hiện trạng:**  
     - `Enemies`, `Animals`, `Resources`, `Structures`, `Projectiles`, `DroppedItems`, `Inventory.Stacks`, `IslandLevel.Tiles`, `WorldChunk.Tiles` đều trả về `.AsReadOnly()` mỗi lần đọc getter — tạo mới `ReadOnlyCollection<T>` trên managed heap mỗi lần truy cập! Đặc biệt [TopDownRenderer.cs:489-491](file:///c:/codegameoop/src/MiniNecesse.App/Rendering/TopDownRenderer.cs#L489-L491) và [546-548](file:///c:/codegameoop/src/MiniNecesse.App/Rendering/TopDownRenderer.cs#L546-L548) gọi `session.Structures[i]` và `session.Enemies[i]` **ngay trong thân vòng `for`** (cấp phát N `ReadOnlyCollection` mỗi frame!).  
     - `TopDownRenderer` nội suy ~25 chuỗi `$"..."` mỗi frame ở HUD, BossBar, Minimap, Hotbar (`1,880 B/frame` ban ngày, `8,002 B/frame` ban đêm có Boss).  
     - `IslandLevel.LoadedChunkCoords` trả về `Dictionary<ChunkCoord, WorldChunk>.KeyCollection`, gây boxing struct enumerator khi duyệt `foreach` trong `EnsureMinimapTerrainCached` (`TopDownRenderer.cs:599`).  
     - `GameSession.StepPlayingSimulation` truyền method group (`ActivateChunkResource`, ...) tạo mới 4 delegate mỗi tick 60 Hz (~280 B/tick).  
   - **Đề xuất sửa:** Cache `IReadOnlyList<T>` 1 lần trong constructor; cache `List<ChunkCoord>` đồng bộ cho `LoadedChunkCoords`; cache 4 delegate chunk 1 lần; cache các chuỗi HUD/Boss/Minimap/Hotbar theo dirty key để đưa `Paint` về **0 B/frame steady-state**.

3. **`[C3]` Không cache Chunk Tile Bitmap (vẽ lại 700–1,400 tile + bọt sóng từng ô mỗi frame)**  
   - **Vị trí:** [TopDownRenderer.cs](file:///c:/codegameoop/src/MiniNecesse.App/Rendering/TopDownRenderer.cs#L116-L158)  
   - **Hiện trạng:** Mỗi frame `OnPaint` gọi `g.DrawImage` cho từng tile `32×32` + kiểm tra 4 hướng bọt sóng cho từng ô nước + vẽ từng ô fog 16×16 cho chunk chưa load.  
   - **Tác động đo được:** Riêng pass ban ngày đã tốn **11.70 ms Mean / 13.45 ms P95** (chiếm 70–80% quỹ thời gian 16.67 ms của 60 FPS).  
   - **Đề xuất sửa:** Tạo `ChunkTerrainCache` dựng sẵn `Bitmap` (`Format32bppPArgb`, 256×256 px cho chunk 16×16) cho `frame0` (và `frame1` nếu chunk có nước), kèm bitmask chunk lân cận cho bọt sóng biên; tự `Dispose()` khi chunk bị unload. Khi render chỉ còn ~4–9 lệnh `DrawImage`.

4. **`[C4]` Overlay ánh sáng ban đêm vẽ thô toàn màn hình mỗi frame không có Lightmap**  
   - **Vị trí:** [TopDownRenderer.cs](file:///c:/codegameoop/src/MiniNecesse.App/Rendering/TopDownRenderer.cs#L474-L500)  
   - **Hiện trạng:** Phủ 1 hình chữ nhật tối lên toàn viewport 1024×680 rồi đè các `FillEllipse` bán trong suốt lên trên (vừa sai về mặt quang học vì không đục thủng màn tối quanh đuốc, vừa gây thêm **+4.57 ms/frame**, đẩy tổng Paint ban đêm lên **16.27 ms Mean / 18.94 ms P95 > 16.67 ms**).  
   - **Đề xuất sửa:** Tạo `NightLightmapRenderer` dùng `Bitmap` độ phân giải thấp (1/8 kích thước viewport = `128×85`), chỉ tính lại buffer pixel khi mức tối, vị trí nguồn sáng lượng tử hóa, danh sách đuốc hiển thị hoặc nhịp flicker 4 Hz thay đổi, sau đó upscale bằng `InterpolationMode.Bilinear`.

---

### Mức độ HIGH (Bug logic, thiết kế SOLID, rò rỉ tài nguyên & hiệu năng AI)

5. **`[H1]` Bug Logic: Thông số `BossConfiguration` trong `island_01..05.json` bị bỏ qua hoàn toàn**  
   - **Vị trí:** [GameSession.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L587-L601), [IEntityFactory.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Interfaces/IEntityFactory.cs#L17), [EnemyFactory.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Patterns/Factory/EnemyFactory.cs#L95-L117)  
   - **Chi tiết:** `JsonLevelLoader` đọc & kiểm tra đầy đủ `BossConfiguration` (`maxHp: 280` ở `island_01.json:57`, `360` ở đảo 2, `420` ở đảo 3, `500` ở đảo 4, `640` ở đảo 5), nhưng `TrySummonIslandBoss` chỉ truyền `bossConfig.BossType` cho `CreateBoss(string, ...)` nên boss thực tế nhận HP hardcode của `EnemyFactory` (`260 / 340 / 390 / 480 / 600`)!  
   - **Đề xuất sửa:** Bổ sung `CreateBoss(BossConfiguration bossConfig, ...)` trên `IEntityFactory` và `EnemyFactory` để dùng đúng chỉ số JSON.

6. **`[H2]` Bug Save/Load: Nhân đôi đợt quái khi load vào ban đêm & bỏ qua `Difficulty` đã lưu**  
   - **Vị trí:** [GameSession.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L612,L629-L639), [DayNightCycle.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/DayNightCycle.cs#L106)  
   - **Chi tiết:** `RestoreFromSnapshot` gọi `_dayNightCycle.RestoreState(...)` — hàm này luôn invoke `OnDayPhaseChanged`, kích hoạt `GameSession.HandleDayPhaseChanged` (`GameSession.cs:1235-1243`): nếu file save đang ở ban đêm, game vừa khôi phục quái cũ xong lại gọi `DispatchNightWave()` sinh thêm cả 1 wave mới! Đồng thời `snapshot.Difficulty` được ghi ra JSON nhưng không bao giờ đọc lại khi restore.  
   - **Đề xuất sửa:** Thêm `bool suppressEvents = false` vào `DayNightCycle.RestoreState`, gọi `NotifyDayPhase(force: true)` sau restore, và khôi phục `_difficulty` từ `snapshot.Difficulty`.

7. **`[H3]` Rò rỉ `Font` không `Dispose` rải rác trong toàn bộ WinForms `Forms/`**  
   - **Vị trí:** [FrmGamePlay.cs](file:///c:/codegameoop/src/MiniNecesse.App/Forms/FrmGamePlay.cs#L651,L681,L691,L734), [FrmMainMenu.cs](file:///c:/codegameoop/src/MiniNecesse.App/Forms/FrmMainMenu.cs#L61,L69,L79,L112,L232), [FrmSettingsDialog.cs](file:///c:/codegameoop/src/MiniNecesse.App/Forms/FrmSettingsDialog.cs#L46,L121,L133,L197), [FrmHighScoresDialog.cs](file:///c:/codegameoop/src/MiniNecesse.App/Forms/FrmHighScoresDialog.cs#L32,L39)  
   - **Chi tiết:** WinForms `Control.Dispose()` KHÔNG tự `Dispose()` đối tượng `Font` gán vào thuộc tính `Font` vì `Font` không thuộc quyền sở hữu của `Control`. Cần thu gom mọi `Font` tạo riêng qua helper và `Dispose()` trong `Dispose(bool)` / `OnFormClosing`.

8. **`[H4]` AI A\* thiếu Shared Cache, thiếu ngân sách repath/tick và không Stagger**  
   - **Vị trí:** [EnemyFactory.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Patterns/Factory/EnemyFactory.cs#L181), [MeleeChaseStrategy.cs](file:///c:/codegameoop/src/MiniNecesse.Core/AI/MeleeChaseStrategy.cs#L29,L92-L112), [AStarPathfinder.cs](file:///c:/codegameoop/src/MiniNecesse.Core/AI/AStarPathfinder.cs#L94)  
   - **Chi tiết:** Mỗi enemy tạo riêng 1 `AStarPathfinder` với bảng hash riêng và khởi đầu `_pathCooldown = 0f`, khiến cả 18 quái cùng tìm đường trong 1 tick định kỳ mỗi 0.35s. Đo thực tế: **Peak Burst 2.14 ms/tick**, **12 lần vượt >0.25 ms** chỉ trong 3 giây, và **3,375 B/tick** do hash table mỗi con tự phình to.  
   - **Đề xuất sửa:** Tạo `PathfindingCoordinator` dùng chung 1 `AStarPathfinder`, giới hạn `MaxNodeIterations = 250`, ngân sách `MaxRepathsPerTick = 3`, ring-cache `(start, goal, StructureVersion)` 64 slot (0 GC), và lệch pha `_pathCooldown` theo quái khi tạo trong `EnemyFactory`.

9. **`[H5]` Bug AI: `BossEnrageStrategy` bắn đạn tầm vô hạn & tấn công kép khi áp sát**  
   - **Vị trí:** [BossEnrageStrategy.cs](file:///c:/codegameoop/src/MiniNecesse.Core/AI/BossEnrageStrategy.cs#L89-L100)  
   - **Chi tiết:** Khi `distance <= enemy.AttackRange`, Boss vừa gọi `enemy.TryAttack(target)` (cận chiến) vừa `SpawnVolley(...)` (bắn 3–5 viên đạn ngay sát mặt người chơi trong cùng 1 frame); còn khi `distance > AttackRange`, Boss vẫn bắn liên tục dù cách người chơi cả nghìn pixel (`maxVolleyRange` chưa có).

10. **`[H6]` Rò rỉ Event Subscription & Object Pool trong `GameSession.Dispose()` và `ClearActiveWorldEntities()`**  
    - **Vị trí:** [GameSession.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L671-L686,L1606-L1642)  
    - **Chi tiết:** `Dispose()` chỉ hủy đăng ký event trên `_player` mà quên `OnDefeated` trên `_enemies`, `_structures`, `_animals` và `OnHealthChanged`/`OnEnraged` trên `_activeBoss`. `ClearActiveWorldEntities()` không hủy event trên `_enemies`/`_activeBoss` và gọi `_projectiles.Clear()` / `_droppedItems.Clear()` mà không trả đối tượng đang hoạt động về `_projectilePool` / `_droppedItemPool`.

11. **`[H7]` `_enemySpatialGrid` chết nhưng vẫn cập nhật mỗi tick**  
    - **Vị trí:** [GameSession.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L39,L967,L982)  
    - **Chi tiết:** Sau khi chuyển projectile của player sang `_playerTargetGrid` (bắn cả Enemy và Animal), `_enemySpatialGrid` vẫn bị `Clear()` + `Insert()` cho tất cả quái mỗi frame dù không ai query.

12. **`[H8]` God Class & Vi phạm SRP ở `GameSession.cs` (1,716 dòng)**  
    - **Vị trí:** [GameSession.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs)  
    - **Chi tiết:** Một lớp giữ toàn bộ trách nhiệm: điều phối state machine, chiến đấu melee/ranged/thu hoạch/đặt công trình, stream chunk, spatial hash grid, object pool, raid wave, boss, observer HUD và serialize/deserialize `GameStateDTO`. Nên tách dần thành các coordinator nhỏ (Combat, Persistence/Snapshot, Spawning) sau khi ổn định hiệu năng.

13. **`[H9]` Game Loop thiếu Render Interpolation (`InterpolationAlpha` chưa nối)**  
    - **Vị trí:** [GameLoopController.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameLoopController.cs#L29), [FrmGamePlay.cs](file:///c:/codegameoop/src/MiniNecesse.App/Forms/FrmGamePlay.cs#L365-L370), [Entity.cs](file:///c:/codegameoop/src/MiniNecesse.Core/Entities/Entity.cs), [TopDownRenderer.cs](file:///c:/codegameoop/src/MiniNecesse.App/Rendering/TopDownRenderer.cs#L62,L80)  
    - **Chi tiết:** WinForms `Timer(16ms)` không khớp chính xác 60 Hz khiến mỗi tick có thể chạy 0, 1 hoặc 2 bước fixed update. Không nội suy vị trí theo `InterpolationAlpha` khiến camera và sprite có hiện tượng giật nhẹ (micro-stutter).

---

### Mức độ MEDIUM

- **`[M1]` Allocation không cần thiết trong `RuleEngine`:** `ResolveHarvest` ([RuleEngine.cs:145](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/RuleEngine.cs#L145)) `new DroppedItem(...)` dù `GameSession` bỏ instance đó để lấy từ pool ([GameSession.cs:865](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L865)); `ResolvePickup` ([RuleEngine.cs:157](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/RuleEngine.cs#L157)) cấp phát delegate closure `(itemId, quantity) => ...` mỗi lần nhặt.
- **`[M2]` `AudioManager` chạm đĩa `File.Exists` lặp lại:** [AudioManager.cs:108-112](file:///c:/codegameoop/src/MiniNecesse.App/Audio/AudioManager.cs#L108-L112) kiểm tra `File.Exists` mỗi lần phát cue âm thanh dù file `.wav` không tồn tại.
- **`[M3]` Thuộc tính `ToolItem.HarvestRange` & `SwingCooldown` chưa được dùng:** `TryHarvestResourceNear` ([GameSession.cs:835,868](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L835)) hardcode tầm `68f` và gọi cooldown vũ khí (`0.45f`), bỏ qua tốc độ/tầm của rìu và cuốc.
- **`[M4]` Ô nước (`TileType.Water`) chặn đạn:** `CollisionDetector.HitsSolidTileOrBoundary` ([CollisionDetector.cs:123,233](file:///c:/codegameoop/src/MiniNecesse.Core/Physics/CollisionDetector.cs#L123)) kiểm tra `IsTileWalkable`, vô tình làm tên/đạn không bay qua được mặt nước.
- **`[M5]` Bất nhất hợp đồng `Equals` / `GetHashCode` của `Vector2D`:** [Vector2D.cs:145-154](file:///c:/codegameoop/src/MiniNecesse.Core/Math/Vector2D.cs#L145-L154) so sánh `Equals` bằng epsilon `1e-6f` nhưng `GetHashCode` băm giá trị float chính xác (vi phạm quy tắc hash table nếu dùng làm khóa).
- **`[M6]` `SpatialHashGrid._buckets` phình không giới hạn trên map 320×320:** [SpatialHashGrid.cs:30,58-66](file:///c:/codegameoop/src/MiniNecesse.Core/Physics/SpatialHashGrid.cs#L30) giữ vĩnh viễn mọi bucket từng chạm suốt phiên chơi dài.
- **`[M7]` `NotifyDayPhase()` gọi thừa 60 lần/giây:** [GameSession.cs:722](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameSession.cs#L722) phát sự kiện qua toàn bộ observer mỗi fixed tick bất kể tiến độ chưa đổi đáng kể.
- **`[M8]` `WeaponUpgradeDecorator` là dead code trong gameplay** (dù `GameStateDTO` đã có `WeaponUpgradeLevel` ở [GameStateDTO.cs:112](file:///c:/codegameoop/src/MiniNecesse.Core/Rules/GameStateDTO.cs#L112)).
- **`[M9]` Blueprint Item/Enemy/Animal/Resource hardcode trong C# Factory** ([ItemFactory.cs:95](file:///c:/codegameoop/src/MiniNecesse.Core/Patterns/Factory/ItemFactory.cs#L95), [EnemyFactory.cs:222](file:///c:/codegameoop/src/MiniNecesse.Core/Patterns/Factory/EnemyFactory.cs#L222)). *(Sẽ giữ nguyên cấu trúc `DataAssets` trong pha này theo chốt phạm vi với user).*
- **`[M10]` LINQ & chuỗi trong `App`:** [CraftingOverlayPanel.cs:157,250](file:///c:/codegameoop/src/MiniNecesse.App/Views/CraftingOverlayPanel.cs#L157) dùng LINQ `Select`; [VisualEffectsPool.cs:138,143,158,175](file:///c:/codegameoop/src/MiniNecesse.App/Rendering/VisualEffectsPool.cs#L138) cấp phát string số sát thương/hồi máu mỗi hit; dòng `90-106` đọc lại `.AsReadOnly()` nhiều lần.

---

### Mức độ LOW

- **`[L1]`** Tài liệu `README.md`, `docs/*.puml`, `TRACEABILITY_A_B.md` lỗi thời so với bản mở rộng 320×320, 37 test, 20 công thức, 9 ô hotbar, động vật và làng.
- **`[L2]`** `TopDownRenderer.cs:642,678` chia `hud.MaxHp` / `hud.BossMaxHp` không kẹp `Math.Max(1, ...)` như `DrawMiniHealthBar`.
- **`[L3]`** `AudioManager.Volume` chỉ kiểm tra `<= 0` như công tắc tắt tiếng (`SoundPlayer` không hỗ trợ chỉnh âm lượng trực tiếp).
- **`[L4]`** `RenderResourceCache.cs` còn ~15 `SolidBrush` hình học cũ không còn dùng sau khi chuyển sang `SpriteAtlasCache` (tuy đã dispose đúng khi đóng).
- **`[L5]`** `RangedKiteStrategy` & `BossEnrageStrategy` di chuyển thẳng hướng player, không dùng A* né tường/nước nếu bị kẹt góc xa.

---

## 3. SỐ LIỆU PROFILING BASELINE (TRƯỚC TỐI ƯU)

**Môi trường đo:** Release build, `.NET Runtime 10.0.12` (targeting `net8.0` / `net8.0-windows`), Windows NT 10.0.26300, kịch bản `ISLAND_01` (320×320, `ViewDistance = 3` → 49 chunk loaded), viewport `1024×680`, đo bằng `Stopwatch` + `GC.GetAllocatedBytesForCurrentThread()` + `GetGuiResources` (`MiniNecesse.Profiler`).

| Hạng mục đo | Mean Time | P95 / Max Time | Heap Allocation | Ghi chú / Phát hiện |
| :--- | ---: | ---: | ---: | :--- |
| **1. `GameSession.Update`** (600 ticks, `Playing`, 19 enemies) | **0.1347 ms** | P95 `0.2609 ms` / Max `1.9393 ms` | **280.7 B/tick** | 4 delegate chunk/tick + `NotifyDayPhase` 60 Hz + `_enemySpatialGrid` thừa |
| **2. `SpatialHashGrid`** (`Clear` + 50 `Insert` + 12 `Query`, 2000 iters) | **43.25 µs** | P95 `71.90 µs` | **0.0 B/op** | Nhanh & 0 GC steady-state; nhưng `_buckets` không cắt tỉa khi map lớn |
| **3a. A\* Single Path** (20+ tiles vòng tường, 1000 iters) | **290.95 µs** | P95 `1,061.40 µs` | **0.0 B/call** | Tốt khi gọi đơn lẻ nhưng chưa share/cache kết quả |
| **3b. A\* Walled-In Worst-Case** (tối đa node, 200 iters) | **154.24 µs** | P95 `249.70 µs` | **159.1 B/call** | Chạm trần mở rộng mỗi lần |
| **3c. 18-Enemy Chase AI** (180 ticks sau tường chắn) | **0.0739 ms/tick** | P95 `0.4380 ms` / **Max `2.1410 ms`** | **3,375.0 B/tick** | **12 spike > 0.25 ms** do cả 18 quái repath cùng tick & phình 18 hash table riêng |
| **4a. `TopDownRenderer.Render` — Ban ngày** (200 frames) | **11.699 ms** | P95 `13.447 ms` | **1,879.9 B/frame** | Vẽ 700–1,400 tile + nối chuỗi HUD/Hotbar + `.AsReadOnly()` mỗi frame |
| **4b. `TopDownRenderer.Render` — Ban đêm** (12 Đuốc + Boss) | **16.273 ms** | P95 **18.938 ms** | **8,002.0 B/frame** | **Vượt ngưỡng 16.67 ms (60 FPS)**; `"0 GC in Paint"` không đúng |
| **4c. Chi phí Overlay Ban đêm (`Night - Day`)** | **+4.574 ms/frame** | — | **+6,122.1 B/frame** | Bao gồm `FillRectangle` + 12 `FillEllipse` + BossBar + `.AsReadOnly()` |
| **5. `CraftingOverlayPanel.RefreshView` ×10** (20 công thức) | — | — | **296,031 B/refresh** | **Rò rỉ +1,001 USER handles & +13 GDI handles** sau 10 lần refresh! |

---

## 4. LỘ TRÌNH THỰC THI GIAI ĐOẠN 2 (ƯU TIÊN CRITICAL & HIGH)

1. **Gói 2.1 — Bộ nhớ Core & Sửa lỗi Critical/High Logic (`C2, H1, H2, H5, H6, H7, M1, M5, M6, M7`):**
   - Cache `IReadOnlyList<T>` 1 lần, bỏ `.AsReadOnly()` động; cache `LoadedChunkCoords` không boxing; cache 4 delegate chunk.
   - Xóa `_enemySpatialGrid` thừa; sửa `GameSession.Dispose()` và `ClearActiveWorldEntities()` trả Pool + gỡ event; tối ưu `RuleEngine.TryResolveHarvest` & `DroppedItem.TryCollect` không cấp phát.
   - Sửa `BossConfiguration` JSON bị bỏ qua (`H1`), bug load save ban đêm sinh thêm wave & bỏ qua `Difficulty` (`H2`), `BossEnrageStrategy` tầm bắn & melee trùng tick (`H5`), `NotifyDayPhase` throttle (`M7`), `SpatialHashGrid` prune bucket (`M6`), `Vector2D` `Equals`/`GetHashCode` (`M5`).
2. **Gói 2.2 — Tối ưu AI Pathfinding (`H4`):** `PathfindingCoordinator` với `MaxNodeIterations=250`, ring-cache 64 slot 0-GC theo `StructureVersion`, `MaxRepathsPerTick=3`, và stagger cooldown trên `MeleeChaseStrategy`.
3. **Gói 2.3 — Game Loop & Render Interpolation (`H9`):** Thêm `PreviousPosition` + `GetInterpolatedPosition` / `SnapPosition` trên `Entity`, nối `GameLoopController.InterpolationAlpha` vào `FrmGamePlay` & `TopDownRenderer`.
4. **Gói 2.4 — Cache Chunk Tile Bitmap, 0-GC Paint & Sửa toàn bộ GDI+ Leaks (`C1, C2, C3, H3, M2, M10, L2`):** `ChunkTerrainCache` (`Format32bppRgb` 1:1 + LRU 32 chunk + chunk neighbor foam), cache chuỗi HUD/Boss/Minimap/Hotbar/Damage, cấu hình `CompositingQuality.HighSpeed`, fix triệt để rò rỉ `Font`/`Control` ở `CraftingOverlayPanel` và 4 WinForms Forms.
5. **Gói 2.5 — Ánh sáng Ban đêm Lightmap độ phân giải thấp (`C4`):** Sau khi đo thử nghiệm GDI+ phát hiện `Bilinear DrawImage` phóng to bitmap alpha tốn **14.78 ms/frame**, phương án được tối ưu sang `NightLightmapRenderer` dùng lưới `16px` (`64×43` ô) gộp span ngang/dọc với 17+7 `SolidBrush` pre-cache → **0.0 B/frame**.
6. **Gói 2.6 — Kiểm thử hồi quy (bổ sung test mới) & Đo lại Profiler SAU tối ưu** để lập bảng đối chiếu Trước / Sau.

---

## 5. KẾT QUẢ THỰC THI GIAI ĐOẠN 2 (SAU TỐI ƯU)

### 5.1 Bảng Số Liệu Đối Chiếu Trước / Sau (Release Build — `MiniNecesse.Profiler`)

| Hạng mục đo | Trước (Baseline) | Sau Tối Ưu | Mức Cải Thiện |
| :--- | :--- | :--- | :--- |
| **1. `GameSession.Update`** (600 ticks, `Playing`, 19 enemies) | Mean `0.1347 ms` / P95 `0.2609 ms` / Max `1.9393 ms` / **`280.7 B/tick`** | Mean **`0.1199 ms`** / P95 **`0.2050 ms`** / Max **`0.3670 ms`** / **`24.5 B/tick`** | Max **-81%**, Alloc **-91.3%**, P95 **-21%** |
| **2. `SpatialHashGrid`** (`Clear` + 50 `Insert` + 12 `Query`, 2000 iters) | Mean `43.25 µs` / P95 `71.90 µs` / `0.0 B/op` | Mean **`42.59 µs`** / P95 **`52.10 µs`** / `0.0 B/op` (+ prune >512 buckets) | P95 **-27.5%**, giới hạn bộ nhớ map 320×320 |
| **3a. A\* Single Path** (1000 iters) | Mean `290.95 µs` / P95 `1,061.40 µs` | Mean `362.64 µs` / P95 **`957.10 µs`** | Giảm tail P95 **-9.8%** |
| **3b. A\* Walled-In Worst-Case** (200 iters) | Mean `154.24 µs` / P95 `249.70 µs` | Mean **`132.12 µs`** / P95 **`214.80 µs`** | Mean **-14.3%**, P95 **-14.0%** |
| **3c. 18-Enemy Chase AI** (180 ticks sau tường chắn) | Mean `0.0739 ms/tick` / P95 `0.4380 ms` / Max **`2.1410 ms`** (`12 spikes > 0.25 ms`) / **`3,375.0 B/tick`** | Mean **`0.0242 ms/tick`** / P95 **`0.0211 ms`** / Max **`0.2962 ms`** (**`1 spike > 0.25 ms`**) / **`330.3 B/tick`** | Mean **-67.3%**, P95 **nhanh gấp 20.7×**, Alloc **-90.2%** |
| **4a. `TopDownRenderer.Render` — Ban ngày** (`1024×680`, 200 frames) | Mean **`11.699 ms`** / P95 `13.447 ms` / **`1,879.9 B/frame`** | Mean **`5.892 ms`** / P95 **`6.771 ms`** / **`0.0 B/frame`** | Nhanh gấp **~2.0× (-49.6%)**, **0 GC in Paint** |
| **4b. `TopDownRenderer.Render` — Ban đêm** (12 Đuốc + Boss, 200 frames) | Mean **`16.273 ms`** / P95 **`18.938 ms`** *(vượt 16.67ms)* / **`8,002.0 B/frame`** | Mean **`10.330 ms`** / P95 **`12.583 ms`** *(đạt chuẩn 60 FPS)* / **`0.0 B/frame`** | Mean **-36.5%**, P95 **-33.6%**, **0 GC in Paint** |
| **4c. Chi phí Overlay Ban đêm (`Night - Day`)** | `+4.574 ms/frame` (`+6,122 B/frame`) | **`+4.438 ms/frame`** (**`0.0 B/frame`**) | Có thêm ánh vàng quanh đuốc + **0 B/frame** |
| **5. `CraftingOverlayPanel.RefreshView` ×10** (20 công thức) | **`+1,001 USER handles`**, `+13 GDI handles`, `296,031 B/refresh` | **`-1 USER handles`**, `+4 GDI handles`, `278,915 B/refresh` | **Triệt tiêu 100% rò rỉ USER handle** |

*(Ghi chú benchmark kỹ thuật: Lần thử ban đầu với lightmap `128×85` `Format32bppPArgb` dùng `InterpolationMode.Bilinear` gây hồi quy `Night Render = 22.293 ms` do GDI+ chạy resampler phần mềm trên bitmap có alpha khi phóng to `8×`. Kết quả GDI+ micro-probe xác nhận `128×85 Bilinear` tốn `14.780 ms/frame` trong khi `FillRectangle` span ngang chỉ tốn `1.70–1.83 ms/frame`, vì vậy phương án cuối dùng lưới `16px` gộp span kết hợp `ChunkTerrainCache` 1:1 `Format32bppRgb` đạt hiệu năng tốt nhất).*

### 5.2 Kết Quả Kiểm Chứng Kiến Trúc, Build, Test & Publish

- **Kiểm tra phụ thuộc UI trong `MiniNecesse.Core`:** **0** tham chiếu `System.Drawing` / `System.Windows.Forms` / `UnityEngine` (**SẠCH**).
- **Kiểm tra LINQ trong vòng lặp nóng (`Core`, `Rendering`, `Views`):** **0** truy vấn LINQ (**SẠCH**).
- **`dotnet build MiniNecesse_LVT_v2026.sln --no-restore`:** **0 Errors, 0 Warnings**.
- **`dotnet test MiniNecesse_LVT_v2026.sln --no-build --verbosity normal`:** **44/44 Tests PASS** (37 test gốc + 7 test mới tại [OptimizationAndAuditFixesTests.cs](file:///c:/codegameoop/tests/MiniNecesse.Core.Tests/OptimizationAndAuditFixesTests.cs)).
- **`dotnet publish src/MiniNecesse.App/MiniNecesse.App.csproj -c Release -o publish --no-restore`:** Thành công ra `publish/MiniNecesse.App.exe`.

### 5.3 Hướng Dẫn Gom File & Commit Từng Tính Năng (Khi máy có `git.exe`)

1. `docs(audit): add comprehensive source audit report and baseline profiling metrics`
   - `git add docs/AUDIT_REPORT.md`
2. `perf(core): eliminate hot-loop allocations and fix Critical/High boss/save/pool bugs`
   - `git add src/MiniNecesse.Core/Crafting/Inventory.cs src/MiniNecesse.Core/Crafting/CraftingRecipe.cs src/MiniNecesse.Core/World/WorldChunk.cs src/MiniNecesse.Core/World/ProceduralIslandGenerator.cs src/MiniNecesse.Core/World/VillageSite.cs src/MiniNecesse.Core/World/ChunkManager.cs src/MiniNecesse.Core/Entities/IslandLevel.cs src/MiniNecesse.Core/Entities/Boss.cs src/MiniNecesse.Core/Entities/DroppedItem.cs src/MiniNecesse.Core/Interfaces/IEntityFactory.cs src/MiniNecesse.Core/Patterns/Factory/EnemyFactory.cs src/MiniNecesse.Core/AI/BossEnrageStrategy.cs src/MiniNecesse.Core/AI/RaidWaveSpawner.cs src/MiniNecesse.Core/Physics/SpatialHashGrid.cs src/MiniNecesse.Core/Math/Vector2D.cs src/MiniNecesse.Core/Rules/DayNightCycle.cs src/MiniNecesse.Core/Rules/RuleEngine.cs src/MiniNecesse.Core/Rules/GameSession.cs`
3. `perf(ai): add bounded PathfindingCoordinator cache, tick budget and staggered repathing`
   - `git add src/MiniNecesse.Core/AI/PathfindingCoordinator.cs src/MiniNecesse.Core/AI/MeleeChaseStrategy.cs src/MiniNecesse.Core/Patterns/Factory/EnemyFactory.cs src/MiniNecesse.Core/Rules/GameSession.cs src/MiniNecesse.App/Forms/FrmGamePlay.cs`
4. `perf(loop): integrate fixed-timestep cap and entity position interpolation in renderer`
   - `git add src/MiniNecesse.Core/Entities/Entity.cs src/MiniNecesse.Core/Entities/Character.cs src/MiniNecesse.Core/Entities/Projectile.cs src/MiniNecesse.Core/Entities/DroppedItem.cs src/MiniNecesse.Core/Rules/GameLoopController.cs src/MiniNecesse.Core/Rules/GameSession.cs src/MiniNecesse.App/Forms/FrmGamePlay.cs src/MiniNecesse.App/Rendering/TopDownRenderer.cs`
5. `perf(render): cache 1:1 chunk terrain bitmaps, 0-GC HUD strings and fix UI GDI+ leaks`
   - `git add src/MiniNecesse.App/Rendering/ChunkTerrainCache.cs src/MiniNecesse.App/Rendering/PixelArtBuilder.cs src/MiniNecesse.App/Rendering/TopDownRenderer.cs src/MiniNecesse.App/Rendering/VisualEffectsPool.cs src/MiniNecesse.App/Views/CraftingOverlayPanel.cs src/MiniNecesse.App/Audio/AudioManager.cs src/MiniNecesse.App/Forms/FrmGamePlay.cs src/MiniNecesse.App/Forms/FrmMainMenu.cs src/MiniNecesse.App/Forms/FrmSettingsDialog.cs src/MiniNecesse.App/Forms/FrmHighScoresDialog.cs`
6. `perf(lighting): add zero-allocation span-merged low-res night lightmap renderer`
   - `git add src/MiniNecesse.App/Rendering/NightLightmapRenderer.cs src/MiniNecesse.App/Rendering/TopDownRenderer.cs`
7. `test(core): add regression tests for audit fixes, AI coordinator, pooling and interpolation`
   - `git add tests/MiniNecesse.Core.Tests/OptimizationAndAuditFixesTests.cs`

