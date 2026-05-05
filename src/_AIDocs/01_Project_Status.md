# 01_Project_Status.md

> **Trạng thái:** Đang cập nhật | **Ngày cập nhật:** 2026-05-04
> **Cập nhật bởi:** AI Senior Unity Architect (Claude Code Audit Session)
> **Phiên bản dự án:** Unity 6 LTS (6000.4.5f1)
> **Quy tắc:** Mọi AI phải đọc `_AIDocs/` TRƯỚC KHI code. Xem `.cursorrules` để biết chi tiết.

---

## 1. Tổng quan dự án

| Trường | Giá trị |
|---|---|
| **Tên dự án** | TCG Card Shop Simulator — Unity Port |
| **Engine** | Unity 6 LTS |
| **Phiên bản** | 6000.4.5f1 |
| **Scripting** | C# (.NET Standard 2.1) |
| **Input System** | Unity New Input System (bắt buộc) |
| **Rendering** | 2D Isometric, URP 17.4.0 |
| **Z-as-Y axis** | `TransparencySortAxis = (0, 1, -0.26)` |
| **Migrate từ** | Phaser.js + Vue 3 (Web) → Unity (Desktop/Mobile) |
| **Source root** | `src/TCGShopSimulator/Assets/Scripts/` |

---

## 2. Tiến độ theo từng Bước

| Bước | Tên | Trạng thái | Ghi chú |
|---|---|---|---|
| **Bước 1** | Foundation (Camera + GameManager) | ✅ Hoàn thành | Camera isometric, singleton pattern |
| **Bước 2** | Grid System + A* Pathfinding | ✅ Hoàn thành | Dictionary-based grid, MinHeap A* |
| **Bước 3** | Data Layer (ScriptableObjects) | ✅ Hoàn thành | CardDatabase, CardData, PackData, RarityDefinition, FurnitureDefinition |
| **Bước 4** | Placement System | ✅ Hoàn thành | GhostObject, PlacementManager, PlacedFurnitureInstance |
| **Bước 5** | Customer FSM + Shop Systems | ✅ Hoàn thành | CustomerFSM, CustomerSpawner, EconomicDecisionEngine, CashierQueueManager, ShopFloorManager, ShelfInstance |
| **Bước 6** | Inventory + Gacha System | 🔄 **ĐANG CẬP NHẬT** | InventoryManager, GachaEngine, GachaResult (code hoàn thành, chưa test đầy đủ) |
| **Bước 7** | UI + Speech Bubbles | ✅ Hoàn thành | SpeechBubble, basic UI scaffolding |
| **Bước 8** | Save/Load + Persistence | ⏳ **Chưa bắt đầu** | PlayerPrefs / JSON serialization |
| **Bước 9+** | Polishing + Mobile | ⏳ **Chưa bắt đầu** | Touch polish, mobile layout |

---

## 3. Các hệ thống đã implement

### 3.1 Core Systems ✅
- **GameManager** — Singleton trung tâm, khởi tạo trước mọi scene (`RuntimeInitializeOnLoadMethod`)
- **SceneBootstrapper** — Validation scene requirements khi load
- **IsometricSortingController** — Z-as-Y sprite sorting tự động

### 3.2 Grid & Pathfinding ✅
- **GridSystem** — Dictionary-based 2D grid (O(1) lookup), hỗ trợ expansion động
- **GridNode** — Struct lưu trạng thái ô (occupancy, bounds)
- **PathfindingCore** — A* với Manhattan heuristic, MinHeap-based
- **PathfindingGrid** — Adapter giữa GridSystem và PathfindingCore
- **MinHeap** — Binary heap với O(log n) insert/extract, IHeapItem interface

### 3.3 Data Layer ✅
- **CardDatabase** — ScriptableObject tổng hợp, lazy-initialize dictionaries
- **CardData** — Thông tin lá bài (HP, attacks, rarity, market value)
- **PackData** — Định nghĩa booster pack + Drop Table (6 slots)
- **RarityDefinition** — Bậc hiếm với sortRank, XP reward, holo flag
- **FurnitureDefinition** — Metadata nội thất (footprint, rotation, shelf config)

### 3.4 Gacha System ✅
- **GachaEngine** — Cumulative weighted probability, seed support cho testing
- **GachaResult** — Data class immutable cho kết quả pack

### 3.5 Customer AI ✅
- **CustomerFSM** — 7-state machine (EnterShop → Wander → SeekingShelf → ExamineShelf → QueueAtCheckout → WaitingInLine → ExitShop)
- **CustomerSpawner** — Spawn interval-based, max customer cap
- **EconomicDecisionEngine** — Price-based purchase probability (95% base, scaling down)
- **SpeechBubble** — Billboard reaction bubbles (Heart/Neutral/Angry)

### 3.6 Placement System ✅
- **PlacementManager** — Drag-and-drop state machine (Idle → Placing → Idle)
- **GhostObject** — Preview với valid/invalid color feedback
- **PlacedFurnitureInstance** — Identity của furniture đã đặt

### 3.7 Shop Floor ✅
- **ShopFloorManager** — RADAR API (nearest shelf), revenue tracking, shelf registry
- **ShelfInstance** — Stock management, price setting, NPC purchase interaction
- **CashierQueueManager** — FIFO queue với slot-based positioning, auto-checkout

### 3.8 Camera ✅
- **CameraController** — Pan (mouse/touch), Zoom (scroll/pinch), world bounds clamping

---

## 4. Debug & Editor Scripts

| File | Mục đích |
|---|---|
| `Step4TestRunner.cs` | Auto-test EconomicDecisionEngine (assertion-based) |
| `GachaDebugTester.cs` | Simulate N pack openings, verify RNG distribution |
| `PathfindingDebugVisualizer.cs` | Scene view gizmos cho walkable nodes + paths |
| `PlacementDebugTester.cs` | Keyboard shortcut placement (E=cashier, B=shelf) |
| `CardDataEditor.cs` | Custom inspector cho CardData |
| `FurnitureDefinitionEditor.cs` | Custom inspector cho FurnitureDefinition |
| `IsometricSetup.cs` | Editor utility cho isometric configuration |

> ⚠️ **Debug scripts không được để trong production build.** Disable hoặc wrap `#if UNITY_EDITOR`.

---

## 5. Kiến trúc Event/Delegate (Cross-System Communication)

```
CustomerFSM
  ├─ CharacterMovement.OnReachedGoal    → FSM state transition
  ├─ CharacterMovement.OnPathNotFound    → Fallback to Wander
  └─ CharacterMovement.OnGoalAbandoned   → Fallback to ExitShop

CharacterMovement
  └─ PathfindingGrid.OnGridChanged       → TriggerPathStale()

PathfindingGrid
  ├─ PlacementManager.OnFurniturePlaced   → Mark cells non-walkable
  └─ PlacementManager.OnPlacementCancelled → (no-op)

ShelfInstance
  ├─ OnStockChanged                      → ShopFloorManager.NotifyShelfUpdated()
  ├─ OnShelfEmptied                       → ShopFloorManager.NotifyShelfEmptied()
  └─ OnPriceChanged                       → (future: re-evaluate in-flight NPCs)

ShopFloorManager
  └─ CashierQueue.OnTransactionCompleted → Add revenue

CashierQueueManager
  └─ CustomerFSM.OnServed()               → NPC exits
```

---

## 6. Điểm mở rộng tiếp theo

1. **Staff AI** — Staff có thể refactor từ `CustomerFSM` base class (nếu tách `EntityBase : MonoBehaviour` với `IAgent` interface)
2. **Play Tables** — ShelfInstance có thể mở rộng với `IInteractable` interface
3. **Save/Load** — InventoryManager + GridSystem + ShopFloorManager cần JSON serialization
4. **Card Battle Mini-game** — Hoàn toàn mới, cần domain riêng

---

## 7. Known Issues (từ Audit 2026-05-04)

| ID | Mức | Mô tả | Trạng thái |
|---|---|---|---|
| #C1 | CAO | Ghost shelf reference sau khi shelf bị destroy | ✅ Đã fix |
| #C2 | CAO | `_checkedShelfIds` không reset khi Initialize | ✅ Đã fix |
| #C3 | CAO | `RequestPath` với null GridSystem → silent return | ✅ Đã fix |
| #C4 | CAO | `Camera.main` trong Awake của PlacementManager | ✅ Đã fix |
| #M1 | TB | CashierQueue race condition | ✅ Đã fix |
| #M2 | TB | InventoryManager không có rollback khi GachaEngine fail | ✅ Đã fix |
| #M6 | TB | SpeechBubble Camera.main null check | ✅ Đã fix |
| #L1 | THẤP | InventoryManager verbose logging | ✅ Đã fix (toggle) |
| #L3 | THẤP | MinHeap ExtractMin throw exception | ✅ Đã fix (log + return default) |

---

## 8. Quy tắc cập nhật file này

**BẮT BUỘC** — Mỗi khi hoàn thành một Bước mới, AI phải cập nhật:
- [ ] Section 2: Tiến độ theo từng Bước
- [ ] Section 3: Danh sách hệ thống đã implement
- [ ] Section 5: Event/Delegate diagram (nếu có thêm event mới)
- [ ] Section 7: Known Issues (thêm issue mới phát hiện)
- [ ] Ghi ngày cập nhật mới ở header

---

*Cập nhật lần cuối: 2026-05-04 bởi AI Architect — Audit Session*
