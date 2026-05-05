# 03_Data_Structures.md

> **Trạng thái:** Đang cập nhật | **Ngày cập nhật:** 2026-05-04
> **Cập nhật bởi:** AI Senior Unity Architect (Claude Code Audit Session)

---

## 1. ScriptableObjects (Data Assets)

### 1.1 CardDatabase

```csharp
// Asset: Right-click > Create > TCGShop > Data > Card Database
// File: CardDatabase_Main.asset (tạo một file duy nhất)

public class CardDatabase : ScriptableObject
{
    public List<PackData>        allPacks;       // Tất cả PackData assets
    public List<RarityDefinition> allRarities;    // Tất cả RarityDefinition assets

    // --- Runtime dictionaries (built on Initialize()) ---
    // Key: packId → Value: PackData
    private Dictionary<string, PackData> _packLookup;

    // Key: cardId → Value: CardData
    private Dictionary<string, CardData> _cardLookup;

    // Key: setId → Value: List<CardData>
    private Dictionary<string, List<CardData>> _cardsBySetId;

    // --- Public API ---
    public void Initialize();                          // Gọi 1 lần trong InventoryManager.Awake()
    public bool TryGetPack(string packId, out PackData pack);
    public bool TryGetCard(string cardId, out CardData card);
    public List<CardData> GetCardsBySet(string setId);
    public List<PackData> GetAvailablePacks(int currentShopLevel);
}
```

**Initialization flow:**
```
InventoryManager.Awake()
  → cardDatabase.Initialize()
      → Build _packLookup (from allPacks list)
      → Build _cardLookup (from allPacks[].availableCards)
      → Build _cardsBySetId (from allPacks[].availableCards)
      → _isInitialized = true
```

---

### 1.2 CardData

```csharp
// Asset: Right-click > Create > TCGShop > Data > Card Data
// File: Card_*.asset (nhiều file, một file mỗi lá bài)

public class CardData : ScriptableObject
{
    // --- Identity ---
    public string cardId;       // Format: "{setId}-{number}". Vd: "sv01-001"
    public string cardName;     // Vd: "Charizard ex"
    public string setId;        // Vd: "sv01"
    public string seriesId;     // Vd: "sv"
    public string cardNumber;   // Vd: "001"

    // --- Visual ---
    public Sprite cardSprite;   // Mặt trước (Import: Sprite 2D, Max Size 512)
    public Sprite cardBackSprite;

    // --- Rarity ---
    public RarityDefinition rarity;  // Reference đến RarityDefinition asset

    // --- Battle Stats ---
    [Range(10, 340)] public int baseHp;
    public EnergyType[] cardTypes;
    [Range(0, 5)] public int retreatCost;
    public AttackData[] attacks;
    public TypeModifier[] weaknesses;
    public TypeModifier[] resistances;

    // --- Economy ---
    [Min(0f)] public float marketValue = 0.99f; // USD reference price
    public string artistName;

    // --- Computed ---
    public bool   IsHighRarity => rarity != null && rarity.IsHighRarity;
    public int    RarityRank   => rarity != null ? rarity.sortingRank : 0;
    public int    XpReward    => rarity != null ? rarity.xpReward : 2;
    public bool   IsValid()   => !string.IsNullOrEmpty(cardId) && !string.IsNullOrEmpty(cardName) && rarity != null;
}

public enum EnergyType { Colorless, Fire, Water, Grass, Lightning, Psychic, Fighting, Darkness, Metal, Dragon, Fairy }

[System.Serializable]
public class AttackData
{
    public string attackName;
    [Min(0)] public int baseDamage;
    public EnergyType[] energyCost;
    [TextArea(2, 4)] public string effectDescription;
}

[System.Serializable]
public class TypeModifier
{
    public EnergyType energyType;
    public string value; // "×2" hoặc "-30"
}
```

---

### 1.3 PackData

```csharp
// Asset: Right-click > Create > TCGShop > Data > Pack Data
// File: Pack_*.asset

public class PackData : ScriptableObject
{
    // --- Identity ---
    public string packId;         // Format: "pack_{setId}". Vd: "pack_sv01"
    public string packName;      // Vd: "Scarlet & Violet Booster Pack"
    public string sourceSetId;   // Vd: "sv01"
    public string generationName; // Vd: "GENERATION IX"

    // --- Visual ---
    public Sprite packFrontSprite;
    public Sprite packBackSprite;

    // --- Economy ---
    [Min(0.01f)] public float buyCost  = 4.99f;       // Giá mua từ nhà cung cấp
    [Min(0.01f)] public float defaultSellPrice = 7.99f; // Giá bán mặc định
    [Range(1, 80)] public int requiredShopLevel = 1;

    // --- Content ---
    public List<CardData> availableCards; // Tất cả cards trong pool này

    // --- NEW: Drop Table ---
    [Range(1, 10)] public int cardsPerPack = 6;
    public List<DropTableSlot> dropTable; // 6 slots = 6 thẻ

    // --- Computed ---
    public float DefaultProfitMargin  => defaultSellPrice - buyCost;
    public float DefaultProfitPercent => buyCost > 0 ? (defaultSellPrice / buyCost - 1f) * 100f : 0f;
    public bool  IsValid() => !string.IsNullOrEmpty(packId) && availableCards?.Count > 0 && dropTable?.Count > 0;
    public List<CardData> GetCardsByRarity(RarityDefinition targetRarity);
}

// --- Drop Table Structures ---

[System.Serializable]
public class DropTableSlot
{
    public string slotLabel;                              // Vd: "Slot 1-4 (Common)", "Slot 6 (Rare Guaranteed)"
    public List<RarityWeight> rarityWeights;              // Bảng weight cho slot này
}

[System.Serializable]
public class RarityWeight
{
    public RarityDefinition rarity; // Bậc hiếm
    [Min(0f)] public float weight = 1f; // Trọng số tương đối
}

/*
DROP TABLE EXAMPLE — Scarlet & Violet Pack (6 slots):

Slot 1: [Common: 70, Uncommon: 25, Rare: 5]
Slot 2: [Common: 70, Uncommon: 25, Rare: 5]
Slot 3: [Common: 70, Uncommon: 25, Rare: 5]
Slot 4: [Common: 70, Uncommon: 25, Rare: 5]
Slot 5: [Common: 60, Uncommon: 25, Rare: 15]
Slot 6: [Common: 0,  Uncommon: 0,  Rare: 70, UltraRare: 20, SecretRare: 10]
*/
```

---

### 1.4 RarityDefinition

```csharp
// Asset: Right-click > Create > TCGShop > Data > Rarity Definition
// File: Rarity_*.asset

public class RarityDefinition : ScriptableObject
{
    // --- Identity ---
    public string displayName;    // Vd: "Ultra Rare"
    public Color rarityColor;     // Màu hiển thị trong UI
    public Sprite rarityIcon;    // Icon (tùy chọn)

    // --- Sorting ---
    [Range(0, 10)] public int sortingRank = 0;
    public bool isHighRarity;    // Trigger holo animation

    // --- Gacha ---
    public int xpReward = 2;     // XP khi mở được thẻ này

    // --- Computed ---
    public bool IsHighRarity => sortingRank >= 3;
}

/*
RARITY TIERS (theo chuẩn Pokémon TCG):

Rank  0  ─ Common        (isHighRarity: false, xpReward: 2)
Rank  1  ─ Uncommon      (isHighRarity: false, xpReward: 2)
Rank  3  ─ Rare          (isHighRarity: true,  xpReward: 5)
Rank  4  ─ Double Rare   (isHighRarity: true,  xpReward: 7)
Rank  5  ─ Ultra Rare    (isHighRarity: true,  xpReward: 10)
Rank  6  ─ Secret Rare  (isHighRarity: true,  xpReward: 15)
Rank  7  ─ Illustration Rare    (isHighRarity: true,  xpReward: 15)
Rank  8  ─ Special Illustration Rare (isHighRarity: true,  xpReward: 20)
Rank  9  ─ Hyper Rare    (isHighRarity: true,  xpReward: 15)
Rank  10 ─ Ghost Rare    (isHighRarity: true,  xpReward: 25)
*/
```

---

### 1.5 FurnitureDefinition

```csharp
// Asset: Right-click > Create > TCGShop > Data > Furniture Definition
// File: Furniture_*.asset

public class FurnitureDefinition : ScriptableObject
{
    // --- Identity ---
    public FurnitureType furnitureType = FurnitureType.ShelfSingle;
    public string displayName = "New Furniture";
    [TextArea(2, 3)] public string description;

    // --- Visual ---
    public GameObject furniturePrefab;  // Prefab khi đặt xuống
    public GameObject ghostPrefab;      // Preview (nếu null, dùng furniturePrefab)
    public Sprite menuIcon;

    // --- Grid ---
    [Range(1, 4)] public int footprintWidth  = 1;
    [Range(1, 4)] public int footprintHeight = 1;
    public bool canRotate = false;

    // --- Economy ---
    [Min(0f)] public float buyCost = 300f;
    [Range(1, 80)] public int requiredShopLevel = 1;

    // --- Shelf Config (shelf types only) ---
    [Range(0, 6)] public int numberOfTiers = 0;
    [Range(0, 64)] public int slotsPerTier = 0;
    public ShelfRole shelfRole = ShelfRole.Selling;

    // --- Computed ---
    public int TotalCells => footprintWidth * footprintHeight;
    public List<Vector2Int> GetFootprintCells(int rotationDegrees = 0);
    public (Vector2Int min, Vector2Int max) GetFootprintBounds(int rotationDegrees = 0);
    public bool IsValid() => furniturePrefab != null && !string.IsNullOrEmpty(displayName);
}

public enum FurnitureType { None, ShelfSingle, ShelfDouble, StorageShelf, PlayTable, CashierDesk }

public enum ShelfRole { Selling, Storage }
```

---

## 2. Runtime Data Structures

### 2.1 GridNode (struct)

```csharp
// Assets/Scripts/Grid/GridNode.cs

public struct GridNode
{
    public bool         IsOccupied;        // Cell có bị chiếm bởi furniture không
    public string       OccupantId;        // furnitureInstanceId của vật thể
    public FurnitureType OccupantType;    // Loại vật thể
    public bool         IsWithinShopBounds; // Cell có trong biên shop không

    public static GridNode Empty        => new GridNode { IsOccupied=false, OccupantId=string.Empty, OccupantType=FurnitureType.None, IsWithinShopBounds=true };
    public static GridNode OutOfBounds  => new GridNode { IsOccupied=false, OccupantId=string.Empty, OccupantType=FurnitureType.None, IsWithinShopBounds=false };

    public bool IsPlaceable => IsWithinShopBounds && !IsOccupied;
}

// GridSystem._grid declaration:
private Dictionary<Vector2Int, GridNode> _grid;

// GridSystem._furnitureFootprints declaration:
private Dictionary<string, List<Vector2Int>> _furnitureFootprints;
```

---

### 2.2 PathNode (class)

```csharp
// Assets/Scripts/Pathfinding/PathNode.cs

public class PathNode : System.IComparable<PathNode>, IHeapItem
{
    public Vector2Int CellCoord { get; }
    public int       GCost { get; set; }     // Chi phí từ START
    public int       HCost { get; set; }     // Heuristic đến GOAL
    public int       FCost => GCost + HCost; // Tổng G + H (key của MinHeap)
    public PathNode  Parent { get; set; }     // Node cha (trace back to start)
    public int       HeapIndex { get; set; } // Index trong heap (cho O(log n) update)
    public bool      IsWalkable { get; set; }

    public int CompareTo(PathNode other)
    {
        int compare = FCost.CompareTo(other.FCost);
        if (compare == 0)
            compare = HCost.CompareTo(other.HCost); // Tiebreak: gần goal hơn
        return compare;
    }
}

// PathfindingGrid._pathNodes declaration:
private Dictionary<Vector2Int, PathNode> _pathNodes;
```

---

### 2.3 QueueEntry (private class in CashierQueueManager)

```csharp
// Assets/Scripts/Shop/CashierQueueManager.cs

private class QueueEntry
{
    public CustomerFSM Customer;   // Reference đến NPC
    public string      ItemId;     // ID của món đã mua
    public float      PaidPrice;  // Giá đã trả
    public int        SlotIndex;  // Vị trí trong hàng (0 = đầu tiên)
}

// Queue list:
private List<QueueEntry> _queue = new List<QueueEntry>();

// CashierQueue slot position formula:
public Vector3 GetSlotWorldPosition(int slotIndex)
{
    return _cashierWorldPosition
           + QUEUE_DIRECTION * FIRST_SLOT_OFFSET
           + QUEUE_DIRECTION * (QUEUE_SPACING * slotIndex);
}
// Constants:
//   QUEUE_SPACING = 1.0f
//   FIRST_SLOT_OFFSET = 1.5f
//   QUEUE_DIRECTION = (0, -1, 0)
```

---

### 2.4 InventoryEntry (struct)

```csharp
// Assets/Scripts/Inventory/InventoryEntry.cs

public struct InventoryEntry
{
    public readonly string            itemId;       // packId hoặc cardId
    public readonly int              quantity;
    public readonly float            currentPrice; // USD (chỉ dùng cho pack)
    public readonly InventoryEntryType entryType;  // Pack hoặc Card

    public InventoryEntry(string itemId, int quantity, float currentPrice, InventoryEntryType entryType);
    public InventoryEntry WithQuantity(int newQuantity);
    public InventoryEntry WithPrice(float newPrice);
}

public enum InventoryEntryType { Pack, Card }

// InventoryManager runtime state:
private Dictionary<string, int> _packInventory; // packId → count
private Dictionary<string, int> _cardBinder;    // cardId → count
```

---

### 2.5 GachaResult (data class)

```csharp
// Assets/Scripts/Gacha/GachaResult.cs

public class GachaResult
{
    public List<CardData> DroppedCards { get; }  // Sort by RarityRank ASC
    public PackData      SourcePack { get; }
    public int           TotalXpGained { get; }   // Tổng XP từ tất cả cards
    public float         OpenedAtTime { get; }    // Time.time tại lúc mở

    public CardData HighlightCard => DroppedCards.Count > 0 ? DroppedCards[^1] : null;
    public bool    HasHighRarityCard { get; }     // Trigger special animation
    public float   TotalMarketValue { get; }      // Tổng marketValue

    public string ToDebugString();               // In ra console
}
```

---

## 3. Customer FSM State

```csharp
// Assets/Scripts/Customer/CustomerFSM.cs

public enum CustomerState
{
    EnterShop,        // Spawn delay (0.5s)
    Wander,           // Scan shelves every 1.5s
    SeekingShelf,     // A* pathfinding to shelf
    ExamineShelf,     // 2s examine + economic decision
    QueueAtCheckout,  // Pathfinding to queue slot
    WaitingInLine,    // Wait for auto-checkout
    ExitShop          // Pathfinding to exit, then Destroy
}

public enum CustomerIntent
{
    Buy,  // 70% của spawns
    Play  // 30% của spawns (future feature)
}

// FSM Constants:
private const float EXAMINE_DURATION    = 2f;
private const float DECISION_INTERVAL    = 1.5f;
private const float BOREDOM_THRESHOLD   = 45f;
private const float SPAWN_DELAY         = 0.5f;
private const float SHELF_INTERACT_DISTANCE = 0.5f;
```

---

## 4. Economic Decision Formula

```csharp
// Assets/Scripts/Customer/EconomicDecisionEngine.cs

/*
CÔNG THỨC XÁC SUẤT MUA:

priceRatio = sellPrice / marketPrice

IF priceRatio <= 1.0:
    buyProbability = 0.95 (95% — giá hợp lý hoặc tốt)

ELSE:
    overpricePercent = (priceRatio - 1.0) × 100
    steps = FLOOR(overpricePercent / 5)
    reduction = steps × 0.15
    buyProbability = MAX(0.95 - reduction, 0.0)

NGƯỠNG PHẢN ỨNG:
  buyProbability == 0.0  → AbsoluteRefusal → Angry bubble
  buyProbability < 0.40  → NormalRefusal  → Neutral bubble
  buyProbability >= 0.40 → HappyPurchase  → Heart bubble

VÍ DỤ:
  $10 / $10 = 1.0  → 95% mua → Heart
  $11 / $10 = 1.1  → steps=2, reduction=0.30 → 65% mua → Heart
  $15 / $10 = 1.5  → steps=10, reduction=1.50 → 0% mua → Angry
  $30 / $10 = 3.0  → steps=40, reduction=6.00 → 0% mua → Angry

BASE CONSTANTS:
  BASE_BUY_PROBABILITY = 0.95
  PRICE_STEP_PERCENT   = 5f
  PROBABILITY_REDUCTION_PER_STEP = 0.15f
  RELUCTANT_THRESHOLD = 0.40f
*/
```

---

## 5. A* Pathfinding State

```csharp
// Assets/Scripts/Pathfinding/PathfindingCore.cs

/*
A* NODE COSTS:
  STRAIGHT_COST = 10   (di chuyển 1 cell ngang/dọc)
  DIAGONAL_COST = 14   (di chuyển 1 cell chéo, = 10√2 ≈ 14)

HEURISTIC: Manhattan Distance
  H(node) = (|ax - bx| + |ay - by|) × STRAIGHT_COST
  Admissible: không overestimate → đảm bảo tìm đường ngắn nhất

COMPLEXITY:
  Time:  O(n log n) với n = số nodes được visited
  Space: O(n) cho openList + closedSet

GRID DIRECTIONS:
  FourDirections = [(1,0), (-1,0), (0,1), (0,-1)]  (mặc định)
  EightDirections = [(1,0), (-1,0), (0,1), (0,-1), (1,1), (-1,1), (1,-1), (-1,-1)]

PATH RESULT:
  Return: List<Vector2Int> từ startCell+1 đến goalCell (không bao gồm start)
  Null: Không có đường đi

STALE PATH HANDLING:
  MAX_RECALCULATIONS = 3
  MAX_STUCK_WAIT = 5f
  STALE_PAUSE_DURATION = 0.1f
*/
```

---

## 6. Dictionary Memory Reference

```
GridSystem._grid
  Key:   Vector2Int (cell coordinate)
  Value: GridNode (occupancy + occupant info)
  Size:  shopMinCell → shopMaxCell (ví dụ: 21×21 = 441 entries)
  O(1) lookup

GridSystem._furnitureFootprints
  Key:   furnitureInstanceId (string)
  Value: List<Vector2Int> (cells occupied by this furniture)
  O(1) lookup

PathfindingGrid._pathNodes
  Key:   Vector2Int (cell coordinate)
  Value: PathNode (walkability + A* costs)
  Size:  Bằng GridSystem._grid.Count
  O(1) lookup

ShopFloorManager._registeredShelves
  Key:   ShelfInstance (reference)
  Value: Vector3 (cached world position)
  O(1) lookup

ShopFloorManager._shelvesWithStock
  Key:   ShelfInstance (reference)
  HashSet — O(1) membership

CardDatabase._packLookup
  Key:   packId (string)
  Value: PackData
  Built: on Initialize()

CardDatabase._cardLookup
  Key:   cardId (string)
  Value: CardData
  Built: on Initialize()

CardDatabase._cardsBySetId
  Key:   setId (string)
  Value: List<CardData>
  Built: on Initialize()
```

---

*Cập nhật lần cuối: 2026-05-04 bởi AI Architect — Audit Session*
