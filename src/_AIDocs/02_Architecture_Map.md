# 02_Architecture_Map.md

> **Trạng thái:** Đang cập nhật | **Ngày cập nhật:** 2026-05-04
> **Cập nhật bởi:** AI Senior Unity Architect (Claude Code Audit Session)

---

## 1. Tổng quan kiến trúc

### 1.1 Design Principles

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          TCG SHOP SIMULATOR — ARCHITECTURE                  │
├─────────────────────────────────────────────────────────────────────────────┤
│  DATA-DRIVEN          │  ScriptableObjects là Single Source of Truth        │
│  EVENT-DRIVEN         │  Delegate/Event cho cross-system communication      │
│  O(1) LOOKUP          │  Dictionary<Vector2Int, GridNode> cho grid          │
│  THREAD-AWARE         │  A* ResetForNewQuery() sequential, not parallel     │
│  GRACEFUL FALLBACK    │  Mọi lỗi có log + fallback, không crash game        │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Layer Architecture

```
┌─────────────────────────────────────────────────────┐
│  UI LAYER         (SpeechBubble, future ShopUI)     │
├─────────────────────────────────────────────────────┤
│  GAMEPLAY LAYER   (CustomerFSM, CharacterMovement)  │
│                    (CustomerSpawner, GachaEngine)   │
├─────────────────────────────────────────────────────┤
│  SHOP LAYER       (ShopFloorManager, ShelfInstance  │
│                    CashierQueueManager)             │
├─────────────────────────────────────────────────────┤
│  PLACEMENT LAYER  (PlacementManager, GhostObject)   │
│                    (PlacedFurnitureInstance)        │
├─────────────────────────────────────────────────────┤
│  PATHFINDING LAYER(PathfindingCore, PathfindingGrid │
│                    MinHeap)                         │
├─────────────────────────────────────────────────────┤
│  GRID LAYER       (GridSystem, GridNode)            │
├─────────────────────────────────────────────────────┤
│  DATA LAYER       (CardDatabase, CardData,          │
│                    PackData, RarityDefinition,      │
│                    FurnitureDefinition)             │
├─────────────────────────────────────────────────────┤
│  CORE LAYER       (GameManager, SceneBootstrapper)  │
└─────────────────────────────────────────────────────┘
```

---

## 2. Sơ đồ luồng dữ liệu (Data Flow)

### 2.1 Khởi tạo Game (Scene Load)

```
Load GameScene.unity
       │
       ▼
SceneBootstrapper.Awake()
       │ Validate scene setup
       │ ├── Camera.main exists?
       │ ├── Camera has CameraController?
       │ └── Camera is Orthographic?
       ▼
GameManager.RuntimeInitializeOnLoadMethod (BeforeSceneLoad)
       │ Tạo [GameManager] GameObject
       │ DontDestroyOnLoad
       ▼
GameManager.Awake()
       │ Singleton guard
       │ InitializeSystems()
       ▼
GridSystem.Awake() ──→ InitializeGrid() (Build Dictionary)
       │
       ▼
PathfindingGrid.Awake() → BuildFromGridSystem() (Build PathNode graph)
       │
       ▼
PlacementManager.Awake() ──→ Cache references
InventoryManager.Awake() ──→ cardDatabase.Initialize()
ShopFloorManager.Awake() ──→ Setup cashier + events
       │
       ▼
SceneBootstrapper.Start() ──→ Validate GameManager.IsAvailable
       │
       ▼
[Scene Ready]
```

### 2.2 NPC Spawn & Shopping Lifecycle

```
CustomerSpawner.Update()
       │ SpawnInterval reached?
       ▼
SpawnCustomer()
       │ Instantiate prefab
       │ Assign CustomerFSM + CharacterMovement
       ▼
CustomerFSM.Initialize(instanceId, intent)
       │
       ▼
CustomerFSM.UpdateFSM() — State Machine loop:
  ┌───────────────────────────────────────────────────────────────┐
  │ ENTER_SHOP (0.5s delay)                                       │
  │       │                                                       │
  │       ▼                                                       │
  │ WANDER (scan every 1.5s)                                      │
  │       │ ShopFloorManager.GetNearestAvailableShelf()           │
  │       │   → HashSet<_shelvesWithStock> O(n) scan              │
  │       │   → filtered by _checkedShelfIds                      │
  │       │   → return nearest by sqrMagnitude                    │
  │       ├─ [Found shelf] → SEekingShelf                         │
  │       └─ [No shelf] 40% → EXIT_SHOP                           │
  │                          60% → wander again                   │
  │                                                               │
  │ SEEKING_SHELF (CharacterMovement)                             │
  │       │ PathfindingCore.FindPath()                            │
  │       │   → A* với MinHeap                                    │
  │       │   → Return List<Vector2Int>                           │
  │       ├─ [Path found] → CharacterMovement di chuyển           │
  │       └─ [No path] → HandleMovementPathNotFound()             │
  │                    → Fallback: Wander                         │
  │                                                               │
  │ EXAMINE_SHELF (Coroutine, 2s)                                 │
  │       │ EconomicDecisionEngine.DecidePurchase()               │
  │       │   → priceRatio = sellPrice / marketPrice              │
  │       │   → probability = BASE 95% - reduction steps          │
  │       │   → Random.value < probability → Buy/Refuse           │
  │       │ ShowReactionBubble() → Heart/Neutral/Angry            │
  │       ├─ [Will Buy + Has Stock]                               │
  │       │   → ShelfInstance.TakeOneItem()                       │
  │       │   → _carriedItemId / _carriedItemPrice stored         │
  │       │   → QUEUE_AT_CHECKOUT                                 │
  │       └─ [Refuse] → EXIT_SHOP (AbsoluteRefusal)               │
  │                   or WANDER (NormalRefusal)                   │
  │                                                               │
  │ QUEUE_AT_CHECKOUT                                             │
  │       │ CashierQueueManager.EnqueueCustomer()                 │
  │       │   → Assigned slotIndex                                │
  │       │   → CharacterMovement → slotWorldPos                  │
  │       │                                                       │
  │ WAITING_IN_LINE ── CashierQueueManager.ProcessAutoCheckout()  │
  │       │ (every _checkoutSpeed seconds)                        │
  │       │   → OnTransactionCompleted(amount, itemId)            │
  │       │   → ShopFloorManager.AddRevenue()                     │
  │       │   → CustomerFSM.OnServed()                            │
  │       │                                                       │
  │ EXIT_SHOP                                                     │
  │       │ CharacterMovement → exitCell                          │
  │       │ OnReachedGoal → Destroy(gameObject)                   │
  └───────────────────────────────────────────────────────────────┘
```

### 2.3 Furniture Placement Flow

```
Player nhấn phím (PlacementDebugTester)
       │
       ▼
PlacementManager.StartPlacement(FurnitureDefinition)
       │ Spawn ghost from definition.ghostPrefab / .furniturePrefab
       │ GhostObject.Initialize()
       │ State → Placing
       ▼
Mỗi Frame (PlacementManager.Update):
       │ GetMouseWorldPosition() → Raycast2D
       │ GhostObject.UpdatePreview(mouseWorldPos)
       │   → GridSystem.WorldToCell()
       │   → GridSystem.ValidatePlacement() — kiểm tra tất cả footprint cells
       │   → ApplyColor(validColor / invalidColor)
       │
       │ Player nhấn LeftClick:
       │   │ GridSystem.ValidatePlacement() → failReason
       │   ├─ [Invalid] → OnPlacementFailed(failReason)
       │   └─ [Valid] → ConfirmPlacement()
       │                 ├── GridSystem.ConfirmPlacement()
       │                 │     → Mark cells occupied in _grid
       │                 │     → _furnitureFootprints[id] = cells
       │                 ├── Instantiate furniturePrefab
       │                 ├── PlacedFurnitureInstance.Initialize()
       │                 ├── PathfindingGrid.HandleFurniturePlaced()
       │                 │     → Set PathNode.IsWalkable = false
       │                 │     → OnGridChanged event → CharacterMovements
       │                 └── OnFurniturePlaced event → All subscribers
       │
       │ Player nhấn ESC / RightClick:
       │       → CancelPlacement()
       │         → Destroy ghost
       │         → OnPlacementCancelled event
       │         → State → Idle
```

### 2.4 Gacha Opening Flow

```
Player requests open pack
       │
       ▼
InventoryManager.OpenPack(packId)
       │ Check pack count > 0
       │ Decrement _packInventory[packId]
       ▼
GachaEngine.OpenPack(PackData)
       │ Save Random.state (for seed restoration)
       │ Loop over dropTable slots:
       │   ├── RollRarity(slot)
       │   │   → Cumulative weighted probability
       │   │   → Return RarityDefinition
       │   └── SelectRandomFromPool(cardsByRarity)
       │         → Uniform random from pool
       │ Sort droppedCards by RarityRank ASC
       ▼
GachaResult
       │ TotalXpGained = sum of card.XpReward
       │ TotalMarketValue = sum of card.marketValue
       ▼
InventoryManager: AddCardToBinder() cho mỗi card
InventoryManager: Debug.Log result
```

---

## 3. Sơ đồ phụ thuộc file (File Dependencies)

```
                    ┌─────────────────────────────────────────┐
                    │          GameManager (Singleton)        │
                    │  (Chỉ khởi tạo, không có dependency)    │
                    └───────────────┬─────────────────────────┘
                                    │ (Optional registry pattern)
                                    ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                           GRID LAYER                                     │
│  ┌─────────────────┐      ┌────────────────────┐                         │
│  │  GridSystem     │←─────│ GridNode (struct)  │                         │
│  │  (Singleton)    │      └────────────────────┘                         │
│  │  _grid: Dict    │                                                     │
│  │  _furnitureFootprints                                                 │
│  └────────┬────────┘                                                     │
│           │ Build PathNode graph                                         │
│           ▼                                                              │
│  ┌────────────────────┐      ┌────────────────┐                          │
│  │  PathfindingGrid   │←─────│ PathNode (class)│                         │
│  │  (Singleton)       │      └────────────────┘                          │
│  │  _pathNodes: Dict  │                                                  │
│  │  OnGridChanged ─────────► CharacterMovement[] (multi-cast)            │
│  └────────┬───────────┘                                                  │
│           │ A* query                                                     │
│           ▼                                                              │
│  ┌────────────────────┐      ┌────────────────┐     ┌──────────────┐     │
│  │  PathfindingCore   │←─────│ MinHeap<T>     │─────│ IHeapItem    │     │
│  │  (static)          │      │ (generic)      │     │ (interface)  │     │
│  │  FindPath()        │      └────────────────┘     └──────────────┘     │
│  └────────────────────┘                                                  │
└──────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                        PLACEMENT LAYER                                   │
│  ┌─────────────────────┐     ┌──────────────────────────┐                │
│  │  PlacementManager   │←────│  FurnitureDefinition     │                │
│  │  (Singleton)        │     │  (ScriptableObject)      │                │
│  │  OnFurniturePlaced──┼────→│  OnPlacementCancelled    │                │
│  └────────┬────────────┘     └──────────────────────────┘                │
│           │                                                              │
│           ├──► GhostObject                                               │
│           │         (Preview, color feedback)                            │
│           │                                                              │
│           └──► PlacedFurnitureInstance                                   │
│                    (Identity: ID, Cell, Rotation)                        │
│                           │                                              │
│                           ▼                                              │
│                    ShelfInstance (if shelf type)                         │
│                    (Stock, Price, NPC interaction)                       │
└──────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                         SHOP LAYER                                       │
│  ┌─────────────────────────┐     ┌────────────────────┐                  │
│  │  ShopFloorManager       │←────│ ShelfInstance[]    │                  │
│  │  (Singleton)            │     │ (registered)       │                  │
│  │  _registeredShelves     │     └─────────┬──────────┘                  │
│  │  _shelvesWithStock      │               │                             │
│  │  GetNearestAvailableShelf◄──────────────┘                             │
│  │  OnTransactionCompleted─► CashierQueueManager                         │
│  └────────────┬────────────┘     (FIFO queue, slot positions)            │
│               │                                                          │
│               └──► CustomerFSM ──► CharacterMovement ──► PathfindingGrid │
│                       (7-state FSM)   (A* movement)                      │
└──────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                         DATA LAYER                                       │
│  ┌─────────────────────┐                                                 │
│  │  CardDatabase       │ ←── InventoryManager                            │
│  │  (ScriptableObject) │     (runtime lookups)                           │
│  │  _packLookup        │                                                 │
│  │  _cardLookup        │                                                 │
│  │  _cardsBySetId      │                                                 │
│  └────────┬───────────┘                                                  │
│           │                                                              │
│           ├──► PackData[] (ScriptableObject)                             │
│           │         └──► CardData[] (ScriptableObject)                   │
│           │                   └──► RarityDefinition                      │
│           └──► RarityDefinition[]                                        │
│  ┌─────────────────────┐                                                 │
│  │  InventoryManager   │ ←── GachaEngine.OpenPack()                      │
│  │  (Singleton)        │     (runtime)                                   │
│  │  _packInventory     │                                                 │
│  │  _cardBinder        │                                                 │
│  └────────┬───────────┘                                                  │
│           │                                                              │
│           ▼                                                              │
│  ┌─────────────────────┐                                                 │
│  │  GachaEngine        │ (static)                                        │
│  │  GachaResult        │ (data class)                                    │
│  └─────────────────────┘                                                 │
└──────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                        UI / CAMERA                                       │
│  ┌─────────────────────┐                                                 │
│  │  CameraController   │ (pan/zoom, desktop + mobile)                    │
│  └─────────────────────┘                                                 │ 
│  ┌─────────────────────┐                                                 │
│  │  SpeechBubble       │ ←── CustomerFSM.ExamineShelfCoroutine()         │
│  │  (Billboard effect) │     (BubbleReactionType: Heart/Neutral/Angry)   │
│  └─────────────────────┘                                                 │
│  ┌─────────────────────┐                                                 │
│  │  IsometricSorting   │ (Z-as-Y sprite sorting)                         │
│  │  Controller         │                                                 │
│  └─────────────────────┘                                                 │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 4. Singleton Registry

Tất cả Singleton instances được quản lý qua `Instance` property với lazy initialization trong Awake.

| Class | Singleton? | Thread-safe Awake | Destroy on load |
|---|---|---|---|
| `GameManager` | ✅ | ✅ (RuntimeInitialize) | ✅ DontDestroyOnLoad |
| `GridSystem` | ✅ | ✅ | ❌ (scene-local) |
| `PathfindingGrid` | ✅ | ✅ | ❌ (scene-local) |
| `InventoryManager` | ✅ | ✅ | ❌ (scene-local) |
| `ShopFloorManager` | ✅ | ✅ | ❌ (scene-local) |
| `PlacementManager` | ✅ | ✅ | ❌ (scene-local) |
| `PathfindingCore` | ❌ (static) | N/A | N/A |
| `GachaEngine` | ❌ (static) | N/A | N/A |
| `EconomicDecisionEngine` | ❌ (static) | N/A | N/A |

---

## 5. Coordinate System

### Isometric Grid
```
    +Y (North)
      │
      │  Cell (0, 0) → World (0, 0, 0) (sau khi Grid.CellToWorld)
      │
      │
+X ───┼─── -X (West)  (East)
      │
      │
      │
    -Y (South)
```

### Z-as-Y Sorting
```
SortingOrder = round(worldY × -100) + sortingOrderOffset
```
- Sprite ở Y=5 → sortingOrder = -500
- Sprite ở Y=-3 → sortingOrder = +300
- Sprite ở Y=-3 vẽ SAU (trên) sprite ở Y=5

---

## 6. Điểm mở rộng cho Staff AI & Play Tables

### Staff AI (mở rộng từ CustomerFSM)
```
EntityBase (MonoBehaviour)
  ├─ CharacterMovement
  ├─ IAgent (interface)
  │     ├─ Evaluate() → AgentAction
  │     ├─ GetPriority() → float
  │     └─ CanInteract(Target) → bool
  ├─ CustomerFSM : EntityBase       (existing)
  └─ StaffAI : EntityBase          (NEW)
        ├─ StaffGoal (enum): Restock, Clean, Greet
        ├─ Evaluate() → kiểm tra shelf stock, queue length
        └─ Interact() → ShelfInstance.RestockFromStorage()
```

### Play Tables (mở rộng từ ShelfInstance)
```
IInteractable (interface)
  ├─ Interact(AgentBase agent) → InteractionResult
  ├─ CanInteract(AgentBase agent) → bool
  └─ GetInteractionPrompt() → string

ShelfInstance : IInteractable          (existing)
PlayTableInstance : IInteractable     (NEW)
  ├─ CurrentGame (từ PlayTableDefinition)
  ├─ AvailableSlots
  └─ EvaluatePlayer WantToPlay(NPC)
```

---

*Cập nhật lần cuối: 2026-05-04 bởi AI Architect — Audit Session*
