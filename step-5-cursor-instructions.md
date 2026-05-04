```markdown
# Step 5: Cỗ Máy Trạng Thái Khách Hàng — Kinh Tế & Hành Vi Mua Sắm
## Cursor Instructions — TCG Shop Simulator (Unity Port)

**Phiên bản tài liệu:** 1.0  
**Giai đoạn:** AI Behavior & Economy Layer  
**Yêu cầu tiên quyết:** Bước 1–4 hoàn thành. `GridSystem`, `PathfindingGrid`, `PathfindingCore`, `CharacterMovement`, `CustomerAIController` (skeleton từ Bước 4) đang hoạt động. `InventoryManager` và `CardDatabase` sẵn sàng từ Bước 2.

---

## 1. Mục Tiêu Của Bước Này

Bước 4 đã cho NPC biết cách **đi đâu** (A* pathfinding). Bước này dạy NPC biết **muốn gì**, **quyết định thế nào**, và **phản ứng ra sao** khi giá quá đắt.

Hệ thống cũ (NPCManager.ts / Vue stores) quản lý toàn bộ vòng đời khách hàng qua một object JS thuần túy với `state` string và `intent` string. Khi port sang Unity, ta thay thế toàn bộ bằng một **Finite State Machine chính thống** với các state riêng biệt, event-driven transitions, và một **Economic Decision Engine** mới hoàn toàn.

Bốn hệ thống cần xây dựng trong Bước 5:

1. **`ShopFloorManager`** — Singleton quản lý kệ hàng, cashier queue, và cung cấp "Radar" API cho NPC truy vấn
2. **`CustomerFSM`** — Cỗ máy trạng thái chính thức với 6 states, thay thế hoàn toàn `CustomerAIController` từ Bước 4
3. **`EconomicDecisionEngine`** — Thuật toán tính xác suất mua hàng dựa trên công thức giá/thị trường
4. **`SpeechBubble`** — World-space UI hiển thị icon phản ứng lơ lửng trên đầu NPC

**Kết quả mong đợi:** NPC vào cửa hàng, dò tìm kệ có hàng, đi đến kệ, đứng đó 2 giây "suy nghĩ", tính xác suất mua theo giá, nếu giá cao → bong bóng tức giận lơ lửng → đi thẳng ra cửa. Nếu giá hợp lý → bong bóng trái tim → di chuyển đến quầy cashier → xếp hàng với offset tọa độ đúng.

---

## 2. Danh Sách Files Cần Tạo / Sửa

```
Assets/
├── Scripts/
│   ├── Shop/
│   │   ├── ShopFloorManager.cs         ← Singleton: kệ hàng, cashier queue, "Radar" API
│   │   ├── ShelfInstance.cs            ← Component trên mỗi kệ hàng đã đặt
│   │   └── CashierQueueManager.cs      ← Quản lý hàng chờ, tính offset tọa độ
│   ├── Customer/
│   │   ├── CustomerFSM.cs              ← FSM chính (THAY THẾ CustomerAIController cũ)
│   │   ├── EconomicDecisionEngine.cs   ← Thuật toán tính xác suất mua
│   │   └── CustomerSpawner.cs          ← Spawn NPC theo interval, assign intent
│   └── UI/
│       └── SpeechBubble.cs             ← World-space bong bóng phản ứng
│
├── ScriptableObjects/
│   └── Customer/
│       └── CustomerConfig.asset        ← Config: tốc độ, boredom threshold, intent ratio
│
└── Prefabs/
    └── Customer/
        ├── Prefab_Customer.prefab      ← NPC prefab với CustomerFSM, CharacterMovement
        └── Prefab_SpeechBubble.prefab  ← Bong bóng UI world-space
```

**Files cần sửa từ Bước trước:**
- `CustomerAIController.cs` → **XÓA** hoặc disable, thay bằng `CustomerFSM.cs`
- `PlacedFurnitureInstance.cs` → Thêm method `RegisterAsShelf(ShelfInstance)` callback
- `PlacementManager.cs` → Fire event để `ShopFloorManager` biết kệ mới đặt xong

---

## 3. Lý Thuyết Nền: Mapping Từ Hệ Thống Cũ

### 3.1 FSM States — So Sánh Cũ vs Mới

```
HỆ THỐNG CŨ (NPCManager.ts)     →   HỆ THỐNG MỚI (CustomerFSM.cs)
────────────────────────────────────────────────────────────────────
'SPAWN'                          →   CustomerState.EnterShop
'WANDER'                         →   CustomerState.Wander
'SEEK_ITEM'                      →   CustomerState.SeekingShelf
'INTERACT'                       →   CustomerState.ExamineShelf   ← QUAN TRỌNG: state mới
'GO_CASHIER'                     →   CustomerState.QueueAtCheckout
'WAITING'                        →   CustomerState.WaitingInLine
'LEAVE'                          →   CustomerState.ExitShop

State mới KHÔNG có trong hệ thống cũ:
  ExamineShelf — NPC đứng tại kệ, tính xác suất mua, hiện bong bóng
  (Hệ thống cũ chỉ có INTERACT check thẳng shopInventory không có xác suất mua)
```

### 3.2 Intent System

```
HỆ THỐNG CŨ:
  intent: 'BUY' | 'PLAY'
  70% BUY, 30% PLAY
  
HỆ THỐNG MỚI:
  CustomerIntent enum: Buy, Play
  Tỷ lệ cấu hình qua CustomerConfig ScriptableObject (mặc định: 70% Buy, 30% Play)
  Play intent: placeholder cho Bước 6 (PlayTable system)
```

### 3.3 Thuật Toán Giá — Logic MỚI HOÀN TOÀN

Hệ thống cũ **KHÔNG CÓ** Economic Decision Engine — NPC mua bất kể giá. Đây là tính năng mới hoàn toàn:

```
CÔNG THỨC XÁC SUẤT MUA:

Định nghĩa:
  sellPrice    = giá người chơi đặt trên kệ
  marketPrice  = giá thị trường tham chiếu (CardData.marketValue × markup chuẩn)
  priceRatio   = sellPrice / marketPrice

Tính xác suất:
  IF priceRatio <= 1.0:
    buyProbability = 0.95  (95% — giá tốt hoặc bằng thị trường)
  ELSE:
    overpricePercent = (priceRatio - 1.0) × 100  // % đắt hơn thị trường
    steps = FLOOR(overpricePercent / 5)           // Cứ mỗi 5% tăng thêm
    reduction = steps × 0.15                      // Giảm 15% mỗi bước
    buyProbability = MAX(0.95 - reduction, 0.0)   // Không âm

Ví dụ:
  sellPrice = $15, marketPrice = $10 → ratio = 1.5 → 50% đắt hơn
  steps = FLOOR(50/5) = 10 bước
  reduction = 10 × 0.15 = 1.50 (vượt quá 0.95)
  buyProbability = MAX(0.95 - 1.50, 0.0) = 0.0  → 0% (không bao giờ mua)

  sellPrice = $11, marketPrice = $10 → ratio = 1.1 → 10% đắt hơn
  steps = FLOOR(10/5) = 2 bước
  reduction = 2 × 0.15 = 0.30
  buyProbability = MAX(0.95 - 0.30, 0.0) = 0.65  → 65% cơ hội mua

NGƯỠNG GIÁ LUÔN TỪ CHỐI (ANGRY BUBBLE):
  buyProbability <= 0.0 → LUÔN hiện bong bóng tức giận, KHÔNG roll dice
  Tương đương: sellPrice > marketPrice × 2.17 (đắt hơn ~117%)
```

### 3.4 Cashier Queue — Offset Tọa Độ

```
HỆ THỐNG CŨ (NPCManager.ts):
  myIndex = waitingQueue.indexOf(instanceId)
  targetY = cashier.y + 60 + (myIndex × 40)
  
  → NPC xếp hàng theo trục Y (phía trước cashier)
  → Mỗi người đứng cách nhau 40px (world units)

HỆ THỐNG MỚI (CashierQueueManager.cs):
  Giữ nguyên logic nhưng dùng Vector3 offset trong world-space Isometric:
  
  queueSlotWorld = cashierWorldPos + QueueDirection × (QUEUE_SPACING × slotIndex)
  
  Trong đó:
    QueueDirection = Vector3(0, -1, 0) (phía dưới cashier, hướng vào cửa)
    QUEUE_SPACING  = 1.0f (1 cell Unity units, tương đương 40px trong hệ cũ)
    slotIndex      = vị trí trong hàng chờ (0 = sát cashier, 1 = sau 1 bước, ...)
  
  Khi người trước rời hàng:
    Tất cả NPC sau dịch chuyển lên 1 slot (slotIndex--)
    CharacterMovement.RequestPath(newSlotCell) để di chuyển lên
```

---

## 4. Chi Tiết Kỹ Thuật Từng File

### 4.1 `ShelfInstance.cs`

**Vị trí:** `Assets/Scripts/Shop/ShelfInstance.cs`  
**Mục đích:** Component gắn lên mỗi Prefab kệ hàng khi đặt xuống. Lưu trữ thông tin hàng hóa đang trưng bày và cung cấp API để NPC "Radar" truy vấn.

**Mapping từ hệ thống cũ:**
```
ShelfTier (furnitureStore.ts cũ)   →   ShelfInstance.ShelfSlot (mới)
────────────────────────────────────────────────────────────────────
tier.itemId (string | null)        →   displayedPackId (string)
tier.slots.length (số lượng)       →   stockCount (int)
shopItems[itemId].sellPrice        →   currentSellPrice (float)
shopItems[itemId].sellPrice        →   marketPrice (float, tham chiếu)
tier.role == 'selling'             →   isSellingShelf (bool)
```

```csharp
// Assets/Scripts/Shop/ShelfInstance.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component gắn lên mỗi GameObject kệ hàng đã được đặt vào scene.
/// Là "nguồn sự thật duy nhất" (Single Source of Truth) về hàng hóa trên kệ này.
///
/// VÒNG ĐỜI:
///   1. PlacedFurnitureInstance.Initialize() → ShelfInstance.SetupFromDefinition()
///   2. Player mở ShelfUI → SetStock(), SetPrice()
///   3. NPC Radar → ShopFloorManager.GetAvailableShelves() → trả về list ShelfInstance
///   4. NPC mua → TakeOneItem() → stockCount--, fire OnStockChanged event
///   5. stockCount == 0 → OnShelfEmptied event → ShopFloorManager cập nhật danh sách
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   ShelfTier trong furnitureStore.ts + npcTakeItemFromSlot() trong furnitureStore.ts
/// </summary>
[RequireComponent(typeof(PlacedFurnitureInstance))]
public class ShelfInstance : MonoBehaviour
{
    // =========================================================================
    // TRẠNG THÁI KỆ HÀNG
    // =========================================================================

    [Header("Stock State (Runtime — Set by Player via ShelfUI)")]
    [Tooltip("ID của pack/item đang trưng bày trên kệ này. " +
             "Empty string = kệ trống. Tương đương tier.itemId trong hệ thống cũ.")]
    [SerializeField] private string _displayedItemId = string.Empty;

    [Tooltip("Số lượng hàng còn trên kệ. " +
             "Tương đương tier.slots.length trong hệ thống cũ.")]
    [SerializeField] private int _stockCount = 0;

    [Tooltip("Giá bán do người chơi đặt (USD). " +
             "Tương đương shopItems[itemId].sellPrice trong hệ thống cũ.")]
    [SerializeField] private float _currentSellPrice = 0f;

    [Tooltip("Giá thị trường tham chiếu (USD). " +
             "Tương đương PackData.defaultSellPrice. Dùng bởi EconomicDecisionEngine.")]
    [SerializeField] private float _marketPrice = 0f;

    [Tooltip("Kệ này là kệ bán (NPC mua được) hay kệ lưu trữ (chỉ cất hàng). " +
             "Tương đương shelf.role == 'selling' trong furnitureStore.ts cũ.")]
    [SerializeField] private bool _isSellingShelf = true;

    // =========================================================================
    // CACHED REFERENCES
    // =========================================================================

    private PlacedFurnitureInstance _furnitureInstance;

    // =========================================================================
    // EVENTS
    // =========================================================================

    /// <summary>Khi hàng thay đổi số lượng. ShopFloorManager subscribe.</summary>
    public event System.Action<ShelfInstance> OnStockChanged;

    /// <summary>Khi kệ hết hàng hoàn toàn. ShopFloorManager loại khỏi available list.</summary>
    public event System.Action<ShelfInstance> OnShelfEmptied;

    /// <summary>Khi player set giá mới. CustomerFSM đang ExamineShelf cần re-evaluate.</summary>
    public event System.Action<ShelfInstance, float> OnPriceChanged;

    // =========================================================================
    // PROPERTIES — O(1) read
    // =========================================================================

    public string DisplayedItemId => _displayedItemId;
    public int StockCount => _stockCount;
    public float CurrentSellPrice => _currentSellPrice;
    public float MarketPrice => _marketPrice;
    public bool IsSellingShelf => _isSellingShelf;
    public bool HasStock => _isSellingShelf && _stockCount > 0 && !string.IsNullOrEmpty(_displayedItemId);
    public Vector3 WorldPosition => transform.position;

    /// <summary>
    /// Tỷ lệ giá bán / giá thị trường.
    /// EconomicDecisionEngine dùng để tính xác suất mua.
    /// </summary>
    public float PriceRatio => _marketPrice > 0f ? _currentSellPrice / _marketPrice : float.MaxValue;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Awake()
    {
        _furnitureInstance = GetComponent<PlacedFurnitureInstance>();
    }

    private void Start()
    {
        // Tự đăng ký với ShopFloorManager
        if (ShopFloorManager.Instance != null)
            ShopFloorManager.Instance.RegisterShelf(this);
    }

    private void OnDestroy()
    {
        if (ShopFloorManager.Instance != null)
            ShopFloorManager.Instance.UnregisterShelf(this);
    }

    // =========================================================================
    // PUBLIC API — Gọi từ ShelfUI (Player) hoặc từ Delivery system
    // =========================================================================

    /// <summary>
    /// Player đặt hàng lên kệ. Gọi từ ShelfManagementUI.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   fillTier(shelfId, itemId, tierIndex) trong furnitureStore.ts
    /// </summary>
    public void SetStock(string itemId, int quantity, float sellPrice, float marketPrice)
    {
        _displayedItemId = itemId;
        _stockCount = quantity;
        _currentSellPrice = sellPrice;
        _marketPrice = marketPrice;

        OnStockChanged?.Invoke(this);

        if (ShopFloorManager.Instance != null)
            ShopFloorManager.Instance.NotifyShelfUpdated(this);
    }

    /// <summary>
    /// Player thay đổi giá bán. Gọi từ SetPriceUI.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   shopItems[itemId].sellPrice = customPrice trong SetPriceModal.vue
    /// </summary>
    public void SetPrice(float newPrice)
    {
        float oldPrice = _currentSellPrice;
        _currentSellPrice = Mathf.Max(0.01f, newPrice);
        OnPriceChanged?.Invoke(this, _currentSellPrice);
    }

    /// <summary>
    /// NPC lấy 1 món hàng từ kệ. Gọi bởi CustomerFSM khi quyết định mua.
    /// Trả về true nếu lấy thành công, false nếu kệ đã hết hàng (race condition).
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   npcTakeItemFromSlot(shelfId) trong furnitureStore.ts
    ///   pickedTier.slots.pop() và nếu hết: pickedTier.itemId = NULL
    /// </summary>
    public bool TakeOneItem(out string takenItemId, out float paidPrice)
    {
        takenItemId = string.Empty;
        paidPrice = 0f;

        if (!HasStock) return false;

        takenItemId = _displayedItemId;
        paidPrice = _currentSellPrice;
        _stockCount--;

        OnStockChanged?.Invoke(this);

        if (_stockCount <= 0)
        {
            _stockCount = 0;
            _displayedItemId = string.Empty;
            OnShelfEmptied?.Invoke(this);

            if (ShopFloorManager.Instance != null)
                ShopFloorManager.Instance.NotifyShelfEmptied(this);
        }

        return true;
    }

    // =========================================================================
    // SETUP
    // =========================================================================

    /// <summary>
    /// Cấu hình kệ dựa trên FurnitureDefinition khi được đặt xuống.
    /// </summary>
    public void SetupFromDefinition(FurnitureDefinition definition)
    {
        if (definition == null) return;
        _isSellingShelf = definition.shelfRole == ShelfRole.Selling;
    }

    public override string ToString() =>
        $"Shelf[{_displayedItemId}|Qty={_stockCount}|Price=${_currentSellPrice:F2}|Market=${_marketPrice:F2}]";
}
```

---

### 4.2 `CashierQueueManager.cs`

**Vị trí:** `Assets/Scripts/Shop/CashierQueueManager.cs`  
**Mục đích:** Quản lý hàng chờ tại quầy cashier, tính toán offset vị trí xếp hàng, xử lý giao dịch thanh toán.

```csharp
// Assets/Scripts/Shop/CashierQueueManager.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý hàng chờ thanh toán tại quầy cashier.
///
/// THUẬT TOÁN XẾP HÀNG (QUEUE OFFSET):
///   Mỗi NPC được gán một "slot" trong hàng chờ (slotIndex = 0, 1, 2...).
///   Vị trí đứng = cashierWorldPos + QueueDirection × (QUEUE_SPACING × slotIndex)
///
///   Hệ thống cũ (NPCManager.ts):
///     myIndex = waitingQueue.indexOf(instanceId)
///     targetY = cashier.y + 60 + (myIndex × 40)
///
///   Hệ thống mới (Unity world-space):
///     slotWorldPos = cashierPos + Vector3(0, -1, 0) × (1.0f × slotIndex)
///     → NPC đứng thành hàng dọc phía dưới cashier
///     → Mỗi slot cách nhau 1.0 Unity unit (tương đương 40px hệ cũ)
///
/// FIFO: First-In-First-Out — NPC vào trước được phục vụ trước.
/// Khi NPC ở slot 0 được phục vụ và rời đi → mọi NPC sau dịch chuyển lên 1 slot.
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   customerStore.waitingCustomers array trong customerStore.ts
///   serveCustomer() → shift() → trả về instanceId để giải phóng NPC sprite
/// </summary>
public class CashierQueueManager : MonoBehaviour
{
    // =========================================================================
    // CONSTANTS
    // =========================================================================

    /// <summary>
    /// Khoảng cách giữa các slot trong hàng chờ (Unity world units).
    /// Tương đương 40px trong hệ thống cũ (myIndex × 40).
    /// </summary>
    private const float QUEUE_SPACING = 1.0f;

    /// <summary>
    /// Hướng xếp hàng từ cashier. Vector3(0, -1, 0) = hàng kéo dài về phía dưới.
    /// Điều chỉnh theo layout cửa hàng nếu cần.
    /// </summary>
    private static readonly Vector3 QUEUE_DIRECTION = new Vector3(0f, -1f, 0f);

    /// <summary>
    /// Khoảng cách từ cashier đến slot đầu tiên (slot 0).
    /// Tương đương +60 trong targetY = cashier.y + 60 + (myIndex × 40) của hệ cũ.
    /// </summary>
    private const float FIRST_SLOT_OFFSET = 1.5f;

    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Cashier Setup")]
    [Tooltip("World position của quầy cashier. Set tự động khi CashierDesk được đặt.")]
    [SerializeField] private Vector3 _cashierWorldPosition;

    [Tooltip("Thời gian xử lý một giao dịch (giây). " +
             "Tương đương SPEED_TO_MS['Normal'] = 3000ms trong hệ thống cũ.")]
    [SerializeField] private float _checkoutSpeed = 3f;

    [Tooltip("Số lượng NPC tối đa được phép xếp hàng cùng lúc.")]
    [SerializeField] private int _maxQueueSize = 10;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = true;

    // =========================================================================
    // QUEUE STATE
    // =========================================================================

    /// <summary>
    /// Hàng chờ FIFO. Element đầu = người được phục vụ tiếp theo.
    /// Key: CustomerFSM instance, Value: thông tin giao dịch.
    /// Dùng List để giữ thứ tự và tính slotIndex.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   customerStore.waitingCustomers: [{instanceId, price}]
    /// </summary>
    private List<QueueEntry> _queue = new List<QueueEntry>();

    private float _lastCheckoutTime = -999f;
    private bool _hasCashier = false;

    // =========================================================================
    // EVENTS
    // =========================================================================

    /// <summary>Kích hoạt khi giao dịch hoàn thành. ShopFloorManager lắng nghe để cộng tiền.</summary>
    public event System.Action<float, string> OnTransactionCompleted; // (amount, itemId)

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Update()
    {
        ProcessAutoCheckout();
    }

    // =========================================================================
    // QUEUE API — Gọi từ CustomerFSM
    // =========================================================================

    /// <summary>
    /// NPC yêu cầu tham gia hàng chờ.
    /// Trả về slotIndex nếu thành công, -1 nếu hàng đầy.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   customerStore.addWaitingCustomer(targetPrice, instanceId)
    /// </summary>
    public int EnqueueCustomer(CustomerFSM customer, string itemId, float paidPrice)
    {
        if (_queue.Count >= _maxQueueSize) return -1;
        if (!_hasCashier) return -1;

        var entry = new QueueEntry
        {
            Customer = customer,
            ItemId = itemId,
            PaidPrice = paidPrice,
            SlotIndex = _queue.Count
        };

        _queue.Add(entry);

        if (_verboseLogging)
        {
            Debug.Log($"[CashierQueue] {customer.InstanceId} joined queue at slot {entry.SlotIndex}. " +
                      $"Queue size: {_queue.Count}. Item: {itemId}, Price: ${paidPrice:F2}");
        }

        return entry.SlotIndex;
    }

    /// <summary>
    /// Tính world position cho một slot trong hàng chờ.
    ///
    /// CÔNG THỨC:
    ///   slotWorldPos = cashierWorldPos
    ///                + QUEUE_DIRECTION × FIRST_SLOT_OFFSET   (khoảng cách đến slot 0)
    ///                + QUEUE_DIRECTION × (QUEUE_SPACING × slotIndex)  (mỗi slot thêm 1 unit)
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   targetY = cashier.y + 60 + (myIndex × 40)
    ///   → FIRST_SLOT_OFFSET tương đương +60
    ///   → QUEUE_SPACING tương đương × 40
    /// </summary>
    public Vector3 GetSlotWorldPosition(int slotIndex)
    {
        return _cashierWorldPosition
               + QUEUE_DIRECTION * FIRST_SLOT_OFFSET
               + QUEUE_DIRECTION * (QUEUE_SPACING * slotIndex);
    }

    /// <summary>
    /// NPC rời hàng (bị cancelled hoặc đã được phục vụ).
    /// Tất cả NPC sau dịch chuyển lên và cập nhật vị trí đứng.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   Sau khi serveCustomer() → waitingQueue.shift()
    ///   → Các NPC còn lại tự cập nhật myIndex = waitingQueue.indexOf(instanceId)
    ///   → targetY = cashier.y + 60 + (myIndex × 40)
    /// </summary>
    public void DequeueCustomer(CustomerFSM customer)
    {
        int removedIndex = _queue.FindIndex(e => e.Customer == customer);
        if (removedIndex < 0) return;

        _queue.RemoveAt(removedIndex);

        // Cập nhật slotIndex và vị trí đứng cho tất cả NPC phía sau
        for (int i = removedIndex; i < _queue.Count; i++)
        {
            _queue[i].SlotIndex = i;
            // Yêu cầu NPC di chuyển đến vị trí mới (slotIndex giảm 1)
            _queue[i].Customer.UpdateQueuePosition(GetSlotWorldPosition(i));
        }

        if (_verboseLogging)
        {
            Debug.Log($"[CashierQueue] {customer.InstanceId} dequeued. " +
                      $"Queue size: {_queue.Count}.");
        }
    }

    /// <summary>
    /// Lấy slot hiện tại của một NPC trong hàng. -1 nếu không trong hàng.
    /// </summary>
    public int GetSlotIndex(CustomerFSM customer)
    {
        var entry = _queue.Find(e => e.Customer == customer);
        return entry?.SlotIndex ?? -1;
    }

    // =========================================================================
    // AUTO CHECKOUT — Giống Staff CHECKOUT duty trong hệ thống cũ
    // =========================================================================

    /// <summary>
    /// Tự động phục vụ NPC đầu hàng theo cooldown.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   handleAutoCheckout() trong statsStore.ts
    ///   cooldown = SPEED_TO_MS['Normal'] = 3000ms → _checkoutSpeed = 3f giây
    /// </summary>
    private void ProcessAutoCheckout()
    {
        if (_queue.Count == 0) return;
        if (!_hasCashier) return;
        if (Time.time - _lastCheckoutTime < _checkoutSpeed) return;

        ServeNextCustomer();
        _lastCheckoutTime = Time.time;
    }

    /// <summary>
    /// Phục vụ NPC đầu hàng: cộng tiền, giải phóng NPC.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   serveCustomer() trong customerStore.ts:
    ///     entry = waitingQueue.shift()
    ///     money += entry.price
    ///     dailyStats.revenue += entry.price
    ///     dailyStats.customersServed++
    ///     gainExp(5)
    ///     RETURN entry.instanceId
    /// </summary>
    private void ServeNextCustomer()
    {
        if (_queue.Count == 0) return;

        var entry = _queue[0];
        _queue.RemoveAt(0);

        // Cập nhật slot cho tất cả NPC còn lại
        for (int i = 0; i < _queue.Count; i++)
        {
            _queue[i].SlotIndex = i;
            _queue[i].Customer.UpdateQueuePosition(GetSlotWorldPosition(i));
        }

        // Fire event để ShopFloorManager/StatsManager cộng tiền
        OnTransactionCompleted?.Invoke(entry.PaidPrice, entry.ItemId);

        // Thông báo NPC được phục vụ → chuyển sang ExitShop
        entry.Customer.OnServed();

        if (_verboseLogging)
        {
            Debug.Log($"[CashierQueue] Served {entry.Customer.InstanceId}. " +
                      $"Revenue: +${entry.PaidPrice:F2}. Queue remaining: {_queue.Count}.");
        }
    }

    // =========================================================================
    // CASHIER REGISTRATION
    // =========================================================================

    /// <summary>
    /// Đăng ký CashierDesk khi được đặt vào scene.
    /// Gọi từ PlacedFurnitureInstance khi furniture type = CashierDesk.
    /// </summary>
    public void RegisterCashier(Vector3 worldPosition)
    {
        _cashierWorldPosition = worldPosition;
        _hasCashier = true;
        Debug.Log($"[CashierQueue] Cashier registered at {worldPosition}.");
    }

    public void UnregisterCashier()
    {
        _hasCashier = false;
        Debug.Log("[CashierQueue] Cashier unregistered. Queue cleared.");
        // NPC trong hàng chờ sẽ tự xử lý khi không tìm được slot mới
    }

    public bool HasCashier => _hasCashier;
    public int QueueSize => _queue.Count;

    // =========================================================================
    // DATA CLASS
    // =========================================================================

    private class QueueEntry
    {
        public CustomerFSM Customer;
        public string ItemId;
        public float PaidPrice;
        public int SlotIndex;
    }
}
```

---

### 4.3 `ShopFloorManager.cs`

**Vị trí:** `Assets/Scripts/Shop/ShopFloorManager.cs`  
**Mục đích:** Singleton điều phối trung tâm của tầng cửa hàng. Cung cấp "Radar" API cho NPC truy vấn kệ hàng có sẵn hàng.

```csharp
// Assets/Scripts/Shop/ShopFloorManager.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton điều phối toàn bộ hoạt động của tầng cửa hàng.
///
/// "RADAR" API:
///   NPC gọi ShopFloorManager.Instance.GetNearestAvailableShelf(npcPosition)
///   để tìm kệ gần nhất có hàng mà NPC chưa từng kiểm tra trong lần wander này.
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   Trong handleWandering() của NPCManager.ts:
///     Scan all placedShelves WHERE:
///       shelf.id NOT IN checkedShelfIds
///       AND shelf.tiers.some(t => t.slots.length > 0)
///
///   ShopFloorManager.GetNearestAvailableShelf() thay thế scan thủ công này
///   bằng dictionary lookup O(1) và distance sort.
///
/// QUẢN LÝ TIỀN:
///   CashierQueueManager.OnTransactionCompleted → ShopFloorManager.AddRevenue()
///   → Cộng vào dailyRevenue và totalMoney (sẽ kết nối với StatsManager ở Bước 6)
/// </summary>
public class ShopFloorManager : MonoBehaviour
{
    // =========================================================================
    // SINGLETON
    // =========================================================================

    public static ShopFloorManager Instance { get; private set; }

    // =========================================================================
    // SHELF REGISTRY
    // =========================================================================

    /// <summary>
    /// Tập hợp tất cả kệ hàng trong scene.
    /// Key: ShelfInstance reference, Value: cached world position.
    /// Cho phép O(1) register/unregister và O(n) filtered query.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   furnitureStore.placedShelves: Record<id, ShelfData>
    /// </summary>
    private readonly Dictionary<ShelfInstance, Vector3> _registeredShelves =
        new Dictionary<ShelfInstance, Vector3>();

    /// <summary>Cache danh sách kệ có hàng để GetNearestAvailableShelf() nhanh hơn.</summary>
    private readonly HashSet<ShelfInstance> _shelvesWithStock = new HashSet<ShelfInstance>();

    // =========================================================================
    // ECONOMY STATE
    // =========================================================================

    [Header("Economy (Debug View)")]
    [SerializeField] private float _totalMoney = 500f;   // Tiền ban đầu như hệ cũ
    [SerializeField] private float _dailyRevenue = 0f;
    [SerializeField] private int _customersServedToday = 0;
    [SerializeField] private int _itemsSoldToday = 0;

    // =========================================================================
    // REFERENCES
    // =========================================================================

    [Header("References")]
    [SerializeField] private CashierQueueManager _cashierQueue;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_cashierQueue == null)
            _cashierQueue = GetComponentInChildren<CashierQueueManager>();

        if (_cashierQueue != null)
            _cashierQueue.OnTransactionCompleted += HandleTransactionCompleted;

        Debug.Log("[ShopFloorManager] Initialized.");
    }

    private void OnDestroy()
    {
        if (_cashierQueue != null)
            _cashierQueue.OnTransactionCompleted -= HandleTransactionCompleted;
    }

    // =========================================================================
    // SHELF REGISTRATION — Gọi từ ShelfInstance.Start() / OnDestroy()
    // =========================================================================

    public void RegisterShelf(ShelfInstance shelf)
    {
        if (shelf == null || _registeredShelves.ContainsKey(shelf)) return;

        _registeredShelves[shelf] = shelf.WorldPosition;

        if (shelf.HasStock)
            _shelvesWithStock.Add(shelf);

        // Subscribe events
        shelf.OnStockChanged += HandleShelfStockChanged;
        shelf.OnShelfEmptied += HandleShelfEmptied;
    }

    public void UnregisterShelf(ShelfInstance shelf)
    {
        if (shelf == null) return;

        _registeredShelves.Remove(shelf);
        _shelvesWithStock.Remove(shelf);

        shelf.OnStockChanged -= HandleShelfStockChanged;
        shelf.OnShelfEmptied -= HandleShelfEmptied;
    }

    public void NotifyShelfUpdated(ShelfInstance shelf)
    {
        if (shelf == null) return;
        if (shelf.HasStock) _shelvesWithStock.Add(shelf);
        else _shelvesWithStock.Remove(shelf);
    }

    public void NotifyShelfEmptied(ShelfInstance shelf)
    {
        _shelvesWithStock.Remove(shelf);
    }

    // =========================================================================
    // RADAR API — Gọi từ CustomerFSM.HandleWandering()
    // =========================================================================

    /// <summary>
    /// Tìm kệ gần nhất (chim bay thẳng) có hàng và NPC chưa kiểm tra.
    /// Trả về null nếu không tìm thấy.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ (handleWandering trong NPCManager.ts):
    ///   Scan placedShelves WHERE:
    ///     shelf.id NOT IN checkedShelfIds
    ///     AND shelf.tiers.some(t => t.slots.length > 0)
    ///
    ///   IF foundShelf: state = SEEK_ITEM, target = (shelf.x, shelf.y + 45)
    ///
    /// Hệ thống mới thêm: Sắp xếp theo khoảng cách để NPC chọn kệ gần nhất.
    /// </summary>
    /// <param name="npcWorldPosition">Vị trí world hiện tại của NPC.</param>
    /// <param name="checkedShelfIds">Danh sách instance IDs của kệ NPC đã kiểm tra.</param>
    public ShelfInstance GetNearestAvailableShelf(
        Vector3 npcWorldPosition,
        HashSet<string> checkedShelfIds)
    {
        ShelfInstance nearest = null;
        float nearestDistSq = float.MaxValue;

        foreach (var shelf in _shelvesWithStock)
        {
            if (shelf == null || !shelf.HasStock) continue;
            if (checkedShelfIds.Contains(shelf.GetInstanceID().ToString())) continue;

            float distSq = (shelf.WorldPosition - npcWorldPosition).sqrMagnitude;
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                nearest = shelf;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Kiểm tra có kệ nào có hàng không (để quyết định NPC có wander tiếp không).
    /// </summary>
    public bool HasAnyShelfWithStock() => _shelvesWithStock.Count > 0;

    // =========================================================================
    // CASHIER ACCESS
    // =========================================================================

    public CashierQueueManager CashierQueue => _cashierQueue;

    // =========================================================================
    // ECONOMY
    // =========================================================================

    private void HandleTransactionCompleted(float amount, string itemId)
    {
        _totalMoney += amount;
        _dailyRevenue += amount;
        _customersServedToday++;
        _itemsSoldToday++;

        // TODO Bước 6: gainExp(5) + notify StatsManager
        Debug.Log($"[ShopFloorManager] 💰 Transaction: +${amount:F2}. " +
                  $"Daily Revenue: ${_dailyRevenue:F2}. Total: ${_totalMoney:F2}");
    }

    private void HandleShelfStockChanged(ShelfInstance shelf)
    {
        NotifyShelfUpdated(shelf);
    }

    private void HandleShelfEmptied(ShelfInstance shelf)
    {
        NotifyShelfEmptied(shelf);
    }

    // =========================================================================
    // PUBLIC ECONOMY ACCESSORS
    // =========================================================================

    public float TotalMoney => _totalMoney;
    public float DailyRevenue => _dailyRevenue;

    /// <summary>Reset thống kê ngày mới. Gọi bởi TimeManager khi sang ngày mới.</summary>
    public void ResetDailyStats()
    {
        _dailyRevenue = 0f;
        _customersServedToday = 0;
        _itemsSoldToday = 0;
    }
}
```

---

### 4.4 `EconomicDecisionEngine.cs`

**Vị trí:** `Assets/Scripts/Customer/EconomicDecisionEngine.cs`  
**Mục đích:** Static utility class tính xác suất mua hàng theo công thức giá/thị trường. Đây là tính năng **MỚI HOÀN TOÀN** không có trong hệ thống cũ.

```csharp
// Assets/Scripts/Customer/EconomicDecisionEngine.cs

using UnityEngine;

/// <summary>
/// Tính xác suất mua hàng của NPC dựa trên tỷ lệ giá bán / giá thị trường.
///
/// ===========================================================================
/// CÔNG THỨC XÁC SUẤT MUA — MỚI HOÀN TOÀN (không có trong hệ thống Vue/Phaser cũ)
/// ===========================================================================
///
/// HỆ THỐNG CŨ: NPC mua bất kể giá. 100% mua nếu kệ có hàng.
///   "Hiện tại NPC không so sánh giá với 'budget' hay 'willingness to pay'."
///   (Từ tài liệu phân tích: "Đây là điểm thiếu hoàn chỉnh về mặt kinh tế học game")
///
/// HỆ THỐNG MỚI — Công thức chính thức:
///
///   priceRatio = sellPrice / marketPrice
///
///   IF priceRatio <= 1.0:
///     buyProbability = 0.95  (95% — giá hợp lý hoặc tốt hơn thị trường)
///
///   ELSE:
///     overpricePercent = (priceRatio - 1.0) × 100   // % đắt hơn thị trường
///     steps = FLOOR(overpricePercent / 5)            // Mỗi 5% là một "bước"
///     reduction = steps × 0.15                       // Mỗi bước giảm 15% xác suất
///     buyProbability = MAX(0.95 - reduction, 0.0)    // Không xuống dưới 0
///
/// NGƯỠNG PHẢN ỨNG:
///   buyProbability == 0.0  → LUÔN TỪ CHỐI, hiện bong bóng tức giận (Angry)
///   buyProbability < 0.40  → MIỄN CƯỠNG, hiện bong bóng trung lập (Neutral)
///   buyProbability >= 0.40 → CÓ THỂ MUA, hiện bong bóng trái tim (Happy)
///
/// VÍ DỤ:
///   sellPrice=$10, market=$10 → ratio=1.0 → 95% mua → Happy bubble
///   sellPrice=$11, market=$10 → ratio=1.1 → 10% đắt → 2 bước → 95%-30%=65% → Happy bubble
///   sellPrice=$15, market=$10 → ratio=1.5 → 50% đắt → 10 bước → 95%-150%<0 → 0% → Angry bubble
///   sellPrice=$30, market=$10 → ratio=3.0 (đắt 3x) → 0% → Angry bubble (Test case bắt buộc)
///
/// ===========================================================================
/// </summary>
public static class EconomicDecisionEngine
{
    // =========================================================================
    // CONSTANTS
    // =========================================================================

    /// <summary>Xác suất mua tối đa khi giá <= thị trường.</summary>
    private const float BASE_BUY_PROBABILITY = 0.95f;

    /// <summary>Mỗi 5% đắt hơn thị trường = một "bước" giảm xác suất.</summary>
    private const float PRICE_STEP_PERCENT = 5f;

    /// <summary>Mỗi bước giảm 15% xác suất mua.</summary>
    private const float PROBABILITY_REDUCTION_PER_STEP = 0.15f;

    /// <summary>Ngưỡng dưới: buyProbability < này → hiện Neutral bubble (miễn cưỡng).</summary>
    public const float RELUCTANT_THRESHOLD = 0.40f;

    // =========================================================================
    // MAIN API
    // =========================================================================

    /// <summary>
    /// Tính xác suất mua hàng dựa trên tỷ lệ giá.
    ///
    /// </summary>
    /// <param name="sellPrice">Giá bán do người chơi đặt.</param>
    /// <param name="marketPrice">Giá thị trường tham chiếu.</param>
    /// <returns>Xác suất mua trong khoảng [0.0, 0.95].</returns>
    public static float CalculateBuyProbability(float sellPrice, float marketPrice)
    {
        // Edge case: marketPrice = 0 hoặc âm
        if (marketPrice <= 0f)
        {
            Debug.LogWarning("[EconomicDecisionEngine] marketPrice <= 0. Trả về 0 xác suất.");
            return 0f;
        }

        float priceRatio = sellPrice / marketPrice;

        // Giá hợp lý hoặc tốt hơn thị trường
        if (priceRatio <= 1.0f)
        {
            return BASE_BUY_PROBABILITY;
        }

        // Giá đắt hơn thị trường — tính reduction
        float overpricePercent = (priceRatio - 1.0f) * 100f;
        int steps = Mathf.FloorToInt(overpricePercent / PRICE_STEP_PERCENT);
        float reduction = steps * PROBABILITY_REDUCTION_PER_STEP;
        float probability = Mathf.Max(BASE_BUY_PROBABILITY - reduction, 0f);

        return probability;
    }

    /// <summary>
    /// Roll dice để quyết định NPC có mua không.
    /// Trả về true = mua, false = từ chối.
    ///
    /// QUAN TRỌNG: Nếu probability = 0, LUÔN trả về false (không cần roll).
    /// </summary>
    public static bool DecidePurchase(float sellPrice, float marketPrice,
        out float probability, out PurchaseDecision decisionType)
    {
        probability = CalculateBuyProbability(sellPrice, marketPrice);

        if (probability <= 0f)
        {
            decisionType = PurchaseDecision.AbsoluteRefusal;
            return false;
        }

        bool willBuy = UnityEngine.Random.value < probability;

        if (willBuy)
        {
            decisionType = probability >= RELUCTANT_THRESHOLD
                ? PurchaseDecision.HappyPurchase
                : PurchaseDecision.ReluctantPurchase;
        }
        else
        {
            decisionType = probability < RELUCTANT_THRESHOLD
                ? PurchaseDecision.AbsoluteRefusal
                : PurchaseDecision.NormalRefusal;
        }

        return willBuy;
    }

    /// <summary>
    /// Lấy BubbleReaction tương ứng với kết quả quyết định.
    /// CustomerFSM dùng để hiển thị đúng loại bong bóng.
    /// </summary>
    public static BubbleReactionType GetBubbleReaction(PurchaseDecision decision)
    {
        return decision switch
        {
            PurchaseDecision.HappyPurchase     => BubbleReactionType.Heart,
            PurchaseDecision.ReluctantPurchase => BubbleReactionType.Neutral,
            PurchaseDecision.NormalRefusal     => BubbleReactionType.Neutral,
            PurchaseDecision.AbsoluteRefusal   => BubbleReactionType.Angry,
            _                                  => BubbleReactionType.Neutral
        };
    }
}

/// <summary>
/// Kết quả phân loại quyết định mua.
/// </summary>
public enum PurchaseDecision
{
    HappyPurchase,      // Xác suất cao, quyết định mua → Heart bubble
    ReluctantPurchase,  // Xác suất thấp nhưng vẫn mua → Heart bubble nhỏ hơn
    NormalRefusal,      // Xác suất trung bình, không mua → Neutral bubble
    AbsoluteRefusal     // Xác suất = 0, không bao giờ mua → Angry bubble
}

/// <summary>
/// Loại bong bóng phản ứng hiển thị trên đầu NPC.
/// Tương đương icon trong NPCManager.ts cũ nhưng không có bong bóng trong hệ thống cũ
/// → Đây là tính năng MỚI HOÀN TOÀN.
/// </summary>
public enum BubbleReactionType
{
    Heart,    // 💚 Đồng ý mua — giá hợp lý
    Neutral,  // 😐 Do dự
    Angry     // 😠 Từ chối mạnh — giá quá đắt
}
```

---

### 4.5 `SpeechBubble.cs`

**Vị trí:** `Assets/Scripts/UI/SpeechBubble.cs`  
**Mục đích:** World-space UI bong bóng phản ứng lơ lửng trên đầu NPC. Billboard effect — luôn quay mặt về Camera.

```csharp
// Assets/Scripts/UI/SpeechBubble.cs

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space bong bóng phản ứng lơ lửng trên đầu NPC.
/// Hiển thị icon Heart/Neutral/Angry kèm animation nổi lên và mờ dần.
///
/// BILLBOARD EFFECT:
///   Mỗi frame tự xoay để luôn nhìn về Camera.
///   Quan trọng trong Isometric view — không xoay sẽ bị méo hình học.
///
/// VÒNG ĐỜI:
///   1. CustomerFSM.ShowReaction(type) → SpeechBubble.Show(type, duration)
///   2. Bong bóng xuất hiện (scale từ 0 → 1, ease out)
///   3. Lơ lửng nhẹ nhàng (sin wave y offset)
///   4. Sau duration: mờ dần (alpha 1 → 0)
///   5. Tự hủy (Destroy)
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   Không có trong NPCManager.ts cũ → Tính năng MỚI HOÀN TOÀN theo yêu cầu bản Unity.
///
/// CÁCH DÙNG:
///   var bubble = Instantiate(speechBubblePrefab, spawnPos, Quaternion.identity);
///   bubble.GetComponent<SpeechBubble>().Show(BubbleReactionType.Angry, 2f);
/// </summary>
public class SpeechBubble : MonoBehaviour
{
    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Icons")]
    [Tooltip("Sprite icon trái tim — NPC đồng ý mua.")]
    [SerializeField] private Sprite _heartIcon;

    [Tooltip("Sprite icon trung lập — NPC do dự.")]
    [SerializeField] private Sprite _neutralIcon;

    [Tooltip("Sprite icon tức giận — NPC từ chối mạnh do giá quá cao. " +
             "Hiển thị khi sellPrice > marketPrice × 2.17 (probability = 0).")]
    [SerializeField] private Sprite _angryIcon;

    [Header("Animation")]
    [Tooltip("Tốc độ lơ lửng (dao động Y). Hz.")]
    [SerializeField] private float _floatFrequency = 1.5f;

    [Tooltip("Biên độ lơ lửng (đơn vị world).")]
    [SerializeField] private float _floatAmplitude = 0.08f;

    [Tooltip("Thời gian animation xuất hiện (giây).")]
    [SerializeField] private float _popInDuration = 0.2f;

    [Tooltip("Thời gian animation mờ dần trước khi hủy (giây).")]
    [SerializeField] private float _fadeOutDuration = 0.5f;

    [Tooltip("Offset Y trên đầu NPC (world units).")]
    [SerializeField] private float _verticalOffset = 1.2f;

    // =========================================================================
    // COMPONENT REFERENCES — Cache trong Awake
    // =========================================================================

    private Image _iconImage;
    private CanvasGroup _canvasGroup;
    private Canvas _canvas;
    private Transform _followTarget;
    private Camera _mainCamera;

    // =========================================================================
    // STATE
    // =========================================================================

    private float _baseY;
    private float _elapsedTime;
    private bool _isShowing;
    private Coroutine _lifetimeCoroutine;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Awake()
    {
        // Cache — KHÔNG GetComponent trong Update
        _iconImage = GetComponentInChildren<Image>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvas = GetComponent<Canvas>();
        _mainCamera = Camera.main;

        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Bắt đầu ẩn
        _canvasGroup.alpha = 0f;
        transform.localScale = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (!_isShowing) return;

        // BILLBOARD: Luôn nhìn về Camera — quan trọng cho Isometric view
        if (_mainCamera != null)
        {
            transform.rotation = _mainCamera.transform.rotation;
        }

        // Theo NPC nếu có followTarget
        if (_followTarget != null)
        {
            transform.position = _followTarget.position + Vector3.up * _verticalOffset;
        }

        // Lơ lửng sin wave
        _elapsedTime += Time.deltaTime;
        float yOffset = Mathf.Sin(_elapsedTime * _floatFrequency * Mathf.PI * 2f) * _floatAmplitude;
        transform.position += Vector3.up * yOffset * Time.deltaTime * 10f;
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Hiển thị bong bóng với icon tương ứng và tự hủy sau duration.
    ///
    /// GỌI TỪ:
    ///   CustomerFSM.HandleExamineShelf() sau khi tính EconomicDecisionEngine.DecidePurchase()
    ///
    /// THEO YÊU CẦU TEST:
    ///   Đặt giá thẻ ×3 thị trường → BubbleReactionType.Angry
    ///   duration = 2.0f (NPC đứng 2s trước khi rời đi)
    /// </summary>
    public void Show(BubbleReactionType reactionType, Transform followTarget, float duration = 2f)
    {
        _followTarget = followTarget;
        _isShowing = true;

        // Chọn icon
        if (_iconImage != null)
        {
            _iconImage.sprite = reactionType switch
            {
                BubbleReactionType.Heart   => _heartIcon,
                BubbleReactionType.Angry   => _angryIcon,
                _                          => _neutralIcon
            };
        }

        // Đặt màu theo loại
        if (_iconImage != null)
        {
            _iconImage.color = reactionType switch
            {
                BubbleReactionType.Heart  => Color.red,
                BubbleReactionType.Angry  => new Color(1f, 0.3f, 0.1f),
                _                         => Color.gray
            };
        }

        if (_lifetimeCoroutine != null)
            StopCoroutine(_lifetimeCoroutine);

        _lifetimeCoroutine = StartCoroutine(LifetimeRoutine(duration));
    }

    // =========================================================================
    // ANIMATION COROUTINE
    // =========================================================================

    private IEnumerator LifetimeRoutine(float displayDuration)
    {
        // Pop-in: scale từ 0 → 1
        float elapsed = 0f;
        while (elapsed < _popInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _popInDuration;
            // Ease out cubic
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            transform.localScale = Vector3.one * eased;
            _canvasGroup.alpha = eased;
            yield return null;
        }

        transform.localScale = Vector3.one;
        _canvasGroup.alpha = 1f;

        // Hiển thị trong displayDuration
        yield return new WaitForSeconds(displayDuration);

        // Fade out: alpha từ 1 → 0
        elapsed = 0f;
        while (elapsed < _fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _fadeOutDuration;
            _canvasGroup.alpha = 1f - t;
            // Float lên cao hơn khi fade
            transform.position += Vector3.up * (0.3f * t * Time.deltaTime);
            yield return null;
        }

        Destroy(gameObject);
    }
}
```

---

### 4.6 `CustomerFSM.cs` ← FILE QUAN TRỌNG NHẤT

**Vị trí:** `Assets/Scripts/Customer/CustomerFSM.cs`  
**Mục đích:** FSM chính thức thay thế hoàn toàn `CustomerAIController.cs` skeleton từ Bước 4. Sáu states, event-driven transitions, tích hợp EconomicDecisionEngine và SpeechBubble.

**Cursor PHẢI xóa hoặc disable `CustomerAIController.cs` và dùng `CustomerFSM.cs` thay thế.**

```csharp
// Assets/Scripts/Customer/CustomerFSM.cs

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finite State Machine điều khiển toàn bộ vòng đời hành vi của khách hàng NPC.
///
/// SÁU STATES:
/// ┌─────────────────────────────────────────────────────────────────────┐
/// │  EnterShop → Wander → SeekingShelf → ExamineShelf → QueueAtCheckout│
/// │                ↑____________↓              │                        │
/// │                (chưa tìm được kệ)          │ (từ chối mua)          │
/// │                                            ↓                        │
/// │                                       ExitShop ←── WaitingInLine   │
/// └─────────────────────────────────────────────────────────────────────┘
///
/// MAPPING TỪ HỆ THỐNG CŨ (NPCManager.ts):
///   'SPAWN'      → EnterShop   (500ms delay trước khi wander)
///   'WANDER'     → Wander      (scan kệ mỗi DECISION_INTERVAL)
///   'SEEK_ITEM'  → SeekingShelf (di chuyển đến kệ qua A*)
///   'INTERACT'   → ExamineShelf (đứng 2s, tính xác suất, hiện bong bóng) ← NÂNG CẤP
///   'GO_CASHIER' → QueueAtCheckout (di chuyển đến slot cashier)
///   'WAITING'    → WaitingInLine (chờ được phục vụ)
///   'LEAVE'      → ExitShop    (di chuyển đến cửa, tự hủy)
///
/// NÂNG CẤP SO VỚI HỆ THỐNG CŨ:
///   1. ExamineShelf có EconomicDecisionEngine → NPC tính xác suất mua theo giá
///   2. SpeechBubble hiện bong bóng phản ứng (Heart/Neutral/Angry)
///   3. checkedShelfIds dùng instance ID thay vì string ID
///   4. CashierQueueManager quản lý offset xếp hàng chính xác
///   5. Boredom check event-driven thay vì poll mỗi 1500ms
/// </summary>
[RequireComponent(typeof(CharacterMovement))]
[DisallowMultipleComponent]
public class CustomerFSM : MonoBehaviour
{
    // =========================================================================
    // ENUM
    // =========================================================================

    public enum CustomerState
    {
        EnterShop,        // 'SPAWN' cũ — NPC vừa xuất hiện, chờ 0.5s
        Wander,           // 'WANDER' cũ — Tìm kệ có hàng
        SeekingShelf,     // 'SEEK_ITEM' cũ — Di chuyển đến kệ mục tiêu
        ExamineShelf,     // 'INTERACT' cũ + MỚI — Đứng 2s, tính xác suất, bong bóng
        QueueAtCheckout,  // 'GO_CASHIER' cũ — Di chuyển đến slot cashier
        WaitingInLine,    // 'WAITING' cũ — Chờ được phục vụ
        ExitShop          // 'LEAVE' cũ — Đi ra cửa, tự hủy
    }

    public enum CustomerIntent { Buy, Play }

    // =========================================================================
    // CONSTANTS
    // =========================================================================

    /// <summary>
    /// Thời gian NPC "suy nghĩ" tại kệ trước khi quyết định.
    /// Tương đương timer = now + 1000ms trong INTERACT state cũ.
    /// Yêu cầu test: NPC đứng 2s → đặt thành 2f.
    /// </summary>
    private const float EXAMINE_DURATION = 2f;

    /// <summary>
    /// Interval quét kệ trong Wander state.
    /// Tương đương mỗi 1500ms trong handleWandering() cũ.
    /// </summary>
    private const float DECISION_INTERVAL = 1.5f;

    /// <summary>
    /// Ngưỡng thời gian trước khi NPC bỏ cuộc và rời đi do chán.
    /// Tương đương (now - spawnTime) > 45000ms trong hệ thống cũ.
    /// </summary>
    private const float BOREDOM_THRESHOLD = 45f;

    /// <summary>
    /// Thời gian chờ trước khi Wander bắt đầu sau khi Spawn.
    /// Tương đương timer = now + 500 trong SPAWN state cũ.
    /// </summary>
    private const float SPAWN_DELAY = 0.5f;

    /// <summary>
    /// Khoảng cách ngưỡng để coi là "đã đứng gần kệ" (world units).
    /// Tương đương distance < 12px trong hệ thống cũ.
    /// </summary>
    private const float SHELF_INTERACT_DISTANCE = 0.5f;

    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Configuration")]
    [Tooltip("Prefab của SpeechBubble để Instantiate khi cần.")]
    [SerializeField] private GameObject _speechBubblePrefab;

    [Tooltip("Offset Y để spawn bong bóng phía trên đầu NPC.")]
    [SerializeField] private float _bubbleYOffset = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = false;

    // =========================================================================
    // COMPONENT REFERENCES — Cache trong Awake
    // =========================================================================

    private CharacterMovement _movement;

    // =========================================================================
    // FSM STATE
    // =========================================================================

    public CustomerState CurrentState { get; private set; } = CustomerState.EnterShop;
    public CustomerIntent Intent { get; private set; } = CustomerIntent.Buy;
    public string InstanceId { get; private set; }

    // =========================================================================
    // AI VARIABLES
    // =========================================================================

    private float _spawnTime;
    private float _lastDecisionTime;
    private float _examineStartTime;

    /// <summary>
    /// Kệ đang được nhắm đến để đi đến.
    /// Null nếu đang Wander hoặc không có kệ nào.
    /// </summary>
    private ShelfInstance _targetShelf;

    /// <summary>
    /// Danh sách InstanceID của các kệ NPC đã kiểm tra (và thấy hết hàng hoặc từ chối).
    /// Tương đương checkedShelfIds trong hệ thống cũ.
    /// Reset mỗi lần NPC bắt đầu mới.
    /// </summary>
    private HashSet<string> _checkedShelfIds = new HashSet<string>();

    /// <summary>Item đã lấy từ kệ (chờ thanh toán).</summary>
    private string _carriedItemId = string.Empty;
    private float _carriedItemPrice = 0f;

    /// <summary>Slot index trong hàng chờ cashier. -1 = không trong hàng.</summary>
    private int _queueSlotIndex = -1;

    // =========================================================================
    // COROUTINES
    // =========================================================================

    private Coroutine _examineCoroutine;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Awake()
    {
        // Cache — KHÔNG GetComponent trong Update
        _movement = GetComponent<CharacterMovement>();

        // Subscribe movement events
        _movement.OnReachedGoal += HandleMovementReachedGoal;
        _movement.OnPathNotFound += HandleMovementPathNotFound;
        _movement.OnGoalAbandoned += HandleMovementGoalAbandoned;
    }

    private void OnDestroy()
    {
        // Unsubscribe — tránh memory leak
        if (_movement != null)
        {
            _movement.OnReachedGoal -= HandleMovementReachedGoal;
            _movement.OnPathNotFound -= HandleMovementPathNotFound;
            _movement.OnGoalAbandoned -= HandleMovementGoalAbandoned;
        }

        // Rời hàng cashier nếu đang xếp
        if (_queueSlotIndex >= 0 && ShopFloorManager.Instance?.CashierQueue != null)
        {
            ShopFloorManager.Instance.CashierQueue.DequeueCustomer(this);
        }
    }

    private void Start()
    {
        _spawnTime = Time.time;
        _lastDecisionTime = Time.time;
    }

    private void Update()
    {
        // KHÔNG gọi GetComponent hay Find ở đây
        UpdateFSM();
        CheckBoredom();
    }

    // =========================================================================
    // KHỞI TẠO
    // =========================================================================

    /// <summary>
    /// Khởi tạo NPC với ID và intent.
    /// Gọi bởi CustomerSpawner ngay sau khi Instantiate prefab.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   new Customer({ intent: 'BUY', instanceId: uuid() }) trong NPCManager.ts
    /// </summary>
    public void Initialize(string instanceId, CustomerIntent intent)
    {
        InstanceId = instanceId;
        Intent = intent;
        _spawnTime = Time.time;
        TransitionToState(CustomerState.EnterShop);
    }

    // =========================================================================
    // FSM UPDATE
    // =========================================================================

    private void UpdateFSM()
    {
        switch (CurrentState)
        {
            case CustomerState.EnterShop:
                HandleEnterShop();
                break;

            case CustomerState.Wander:
                HandleWander();
                break;

            case CustomerState.ExamineShelf:
                // Xử lý qua coroutine — không cần logic ở đây
                break;

            case CustomerState.WaitingInLine:
                HandleWaitingInLine();
                break;

            // SeekingShelf, QueueAtCheckout, ExitShop:
            // CharacterMovement đang di chuyển → đợi OnReachedGoal
        }
    }

    // =========================================================================
    // STATE HANDLERS
    // =========================================================================

    /// <summary>
    /// ENTERCHOP: Chờ SPAWN_DELAY rồi chuyển sang Wander.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   case 'SPAWN':
    ///     Duration: 500ms
    ///     Next: WANDER (nếu intent==BUY)
    /// </summary>
    private void HandleEnterShop()
    {
        if (Time.time - _spawnTime >= SPAWN_DELAY)
        {
            TransitionToState(CustomerState.Wander);
            // Di chuyển đến điểm ngẫu nhiên đầu tiên trong shop
            MoveToRandomWanderPoint();
        }
    }

    /// <summary>
    /// WANDER: Scan kệ theo interval, di chuyển đến kệ nếu tìm thấy.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ (handleWandering trong NPCManager.ts):
    ///   Decision interval: mỗi 1500ms
    ///   IF intent == 'BUY':
    ///     Scan placedShelves WHERE NOT IN checkedShelfIds AND has stock
    ///     IF foundShelf: state = SEEK_ITEM, target = (shelf.x, shelf.y + 45)
    ///     ELSE IF random() < 0.4: ...40% chance → LEAVE hoặc switch intent
    ///   Boredom check: IF (now - spawnTime) > 45000ms → LEAVE
    ///
    /// HỆ THỐNG MỚI:
    ///   Dùng ShopFloorManager.GetNearestAvailableShelf() thay vì scan thủ công
    /// </summary>
    private void HandleWander()
    {
        if (Time.time - _lastDecisionTime < DECISION_INTERVAL) return;
        _lastDecisionTime = Time.time;

        if (Intent == CustomerIntent.Buy)
        {
            TryScanForShelf();
        }
        // CustomerIntent.Play: TODO Bước 6 (PlayTable system)
    }

    /// <summary>
    /// WAITINGINLINE: Kiểm tra vị trí trong hàng mỗi frame.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   case 'WAITING':
    ///     myIndex = waitingQueue.indexOf(instanceId)
    ///     IF myIndex == -1: → LEAVE (đã được serve hoặc bị remove)
    ///     expectedY = cashier.y + 60 + (myIndex × 40)
    ///     IF |currentY - expectedY| > 5: state = GO_CASHIER (di chuyển lên)
    ///
    /// HỆ THỐNG MỚI:
    ///   Slot updates được push từ CashierQueueManager.UpdateQueuePosition()
    ///   NPC chỉ cần chờ OnServed() callback.
    /// </summary>
    private void HandleWaitingInLine()
    {
        // Logic xử lý qua CashierQueueManager callbacks:
        //   UpdateQueuePosition() → di chuyển lên hàng
        //   OnServed() → chuyển ExitShop
        // Không cần poll ở đây
    }

    // =========================================================================
    // SCAN & TARGETING
    // =========================================================================

    /// <summary>
    /// Dùng ShopFloorManager "Radar" để tìm kệ có hàng gần nhất.
    /// Nếu tìm thấy → chuyển SeekingShelf và RequestPath đến kệ.
    /// Nếu không có kệ nào → random 40% rời cửa hàng.
    /// </summary>
    private void TryScanForShelf()
    {
        if (ShopFloorManager.Instance == null) return;

        ShelfInstance shelf = ShopFloorManager.Instance
            .GetNearestAvailableShelf(transform.position, _checkedShelfIds);

        if (shelf != null)
        {
            _targetShelf = shelf;
            TransitionToState(CustomerState.SeekingShelf);

            // Di chuyển đến vị trí đứng trước kệ (offset nhỏ về phía NPC)
            // Tương đương target = (shelf.x, shelf.y + 45) trong hệ cũ
            Vector3 shelfPos = shelf.WorldPosition;
            Vector2Int shelfCell = GridSystem.Instance.WorldToCell(shelfPos);
            // Đứng ở cell phía trước kệ (cell phía dưới trong isometric)
            Vector2Int targetCell = shelfCell + new Vector2Int(0, -1);

            _movement.RequestPath(targetCell);

            if (_verboseLogging)
                Debug.Log($"[CustomerFSM] {InstanceId}: Found shelf {shelf}. Moving to it.");
        }
        else
        {
            // Không tìm thấy kệ — 40% chance rời đi
            // Tương đương: ELSE IF random() < 0.4: → LEAVE trong hệ cũ
            if (UnityEngine.Random.value < 0.4f)
            {
                if (_verboseLogging)
                    Debug.Log($"[CustomerFSM] {InstanceId}: No shelves found. Leaving (40% chance).");

                TransitionToState(CustomerState.ExitShop);
                MoveToExit();
            }
            else
            {
                // Wander thêm một đoạn rồi thử lại
                MoveToRandomWanderPoint();
            }
        }
    }

    // =========================================================================
    // EXAMINE SHELF — STATE CORE LOGIC
    // =========================================================================

    /// <summary>
    /// Bắt đầu ExamineShelf state:
    /// Đứng tại kệ EXAMINE_DURATION giây, tính xác suất mua, hiện bong bóng, quyết định.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ (INTERACT state):
    ///   Sau 1000ms "suy nghĩ":
    ///     itemId = npcTakeItemFromSlot(shelfId)
    ///     IF itemId != null: targetPrice = sellPrice, state = GO_CASHIER
    ///     ELSE: checkedShelfIds.push(shelfId), state = WANDER
    ///
    /// NÂNG CẤP HỆ THỐNG MỚI:
    ///   1. Thời gian examine = EXAMINE_DURATION (2s theo yêu cầu test)
    ///   2. Tính EconomicDecisionEngine.DecidePurchase(sellPrice, marketPrice)
    ///   3. Hiện SpeechBubble với icon phù hợp
    ///   4. Nếu quyết định mua → TakeOneItem() → QueueAtCheckout
    ///   5. Nếu từ chối → đánh dấu checkedShelfIds → ExitShop (giá ×3 trường hợp)
    /// </summary>
    private IEnumerator ExamineShelfCoroutine()
    {
        if (_targetShelf == null || !_targetShelf.HasStock)
        {
            // Kệ hết hàng trong lúc di chuyển đến
            _checkedShelfIds.Add(_targetShelf?.GetInstanceID().ToString() ?? "null");
            TransitionToState(CustomerState.Wander);
            MoveToRandomWanderPoint();
            yield break;
        }

        _examineStartTime = Time.time;

        // BƯỚC 1: Tính xác suất mua
        bool willBuy = EconomicDecisionEngine.DecidePurchase(
            _targetShelf.CurrentSellPrice,
            _targetShelf.MarketPrice,
            out float probability,
            out PurchaseDecision decisionType
        );

        if (_verboseLogging)
        {
            Debug.Log($"[CustomerFSM] {InstanceId} examining shelf: " +
                      $"SellPrice=${_targetShelf.CurrentSellPrice:F2}, " +
                      $"Market=${_targetShelf.MarketPrice:F2}, " +
                      $"Probability={probability:P0}, " +
                      $"Decision={decisionType}, WillBuy={willBuy}");
        }

        // BƯỚC 2: Hiện bong bóng phản ứng
        BubbleReactionType bubbleType = EconomicDecisionEngine.GetBubbleReaction(decisionType);
        ShowReactionBubble(bubbleType, EXAMINE_DURATION);

        // BƯỚC 3: Đứng chờ EXAMINE_DURATION (2 giây theo yêu cầu test)
        yield return new WaitForSeconds(EXAMINE_DURATION);

        // BƯỚC 4: Thực thi quyết định
        if (willBuy && _targetShelf.HasStock)
        {
            // Lấy hàng từ kệ
            if (_targetShelf.TakeOneItem(out string takenItemId, out float paidPrice))
            {
                _carriedItemId = takenItemId;
                _carriedItemPrice = paidPrice;

                if (_verboseLogging)
                    Debug.Log($"[CustomerFSM] {InstanceId}: Decided to BUY {takenItemId} for ${paidPrice:F2}.");

                TransitionToState(CustomerState.QueueAtCheckout);
                MoveToJoinQueue();
            }
            else
            {
                // Race condition: kệ hết hàng trong lúc decide
                _checkedShelfIds.Add(_targetShelf.GetInstanceID().ToString());
                TransitionToState(CustomerState.Wander);
                MoveToRandomWanderPoint();
            }
        }
        else
        {
            // Từ chối mua
            if (_verboseLogging)
                Debug.Log($"[CustomerFSM] {InstanceId}: REFUSED to buy. " +
                          $"Reason: {decisionType}. Leaving shop.");

            // Đánh dấu kệ này đã kiểm tra
            if (_targetShelf != null)
                _checkedShelfIds.Add(_targetShelf.GetInstanceID().ToString());

            _targetShelf = null;

            // Nếu giá quá đắt (AbsoluteRefusal) → LUÔN rời cửa hàng
            // Nếu từ chối thường → có thể tìm kệ khác
            if (decisionType == PurchaseDecision.AbsoluteRefusal)
            {
                TransitionToState(CustomerState.ExitShop);
                MoveToExit();
            }
            else
            {
                // Tìm kệ khác
                TransitionToState(CustomerState.Wander);
                MoveToRandomWanderPoint();
            }
        }

        _examineCoroutine = null;
    }

    // =========================================================================
    // MOVEMENT HELPERS
    // =========================================================================

    private void MoveToRandomWanderPoint()
    {
        if (GridSystem.Instance == null || PathfindingGrid.Instance == null) return;

        for (int attempt = 0; attempt < 15; attempt++)
        {
            int x = UnityEngine.Random.Range(
                GridSystem.Instance.ShopMinCell.x,
                GridSystem.Instance.ShopMaxCell.x + 1);
            int y = UnityEngine.Random.Range(
                GridSystem.Instance.ShopMinCell.y,
                GridSystem.Instance.ShopMaxCell.y + 1);

            var cell = new Vector2Int(x, y);
            if (PathfindingGrid.Instance.IsWalkable(cell))
            {
                _movement.RequestPath(cell);
                return;
            }
        }
    }

    private void MoveToExit()
    {
        if (GridSystem.Instance == null) return;
        // Di chuyển đến rìa dưới shop (cửa ra)
        var exitCell = new Vector2Int(0, GridSystem.Instance.ShopMinCell.y);
        _movement.RequestPath(exitCell);
    }

    private void MoveToJoinQueue()
    {
        var cashierQueue = ShopFloorManager.Instance?.CashierQueue;
        if (cashierQueue == null || !cashierQueue.HasCashier)
        {
            // Không có cashier → bỏ hàng và rời đi
            Debug.LogWarning($"[CustomerFSM] {InstanceId}: No cashier available. Leaving.");
            TransitionToState(CustomerState.ExitShop);
            MoveToExit();
            return;
        }

        // Tham gia hàng chờ
        _queueSlotIndex = cashierQueue.EnqueueCustomer(this, _carriedItemId, _carriedItemPrice);

        if (_queueSlotIndex < 0)
        {
            // Hàng đầy
            Debug.LogWarning($"[CustomerFSM] {InstanceId}: Queue full. Leaving.");
            TransitionToState(CustomerState.ExitShop);
            MoveToExit();
            return;
        }

        // Di chuyển đến slot trong hàng chờ
        Vector3 slotWorldPos = cashierQueue.GetSlotWorldPosition(_queueSlotIndex);
        Vector2Int slotCell = GridSystem.Instance.WorldToCell(slotWorldPos);
        _movement.RequestPath(slotCell);
    }

    // =========================================================================
    // SPEECH BUBBLE
    // =========================================================================

    private void ShowReactionBubble(BubbleReactionType bubbleType, float duration)
    {
        if (_speechBubblePrefab == null) return;

        Vector3 spawnPos = transform.position + Vector3.up * _bubbleYOffset;
        GameObject bubbleGO = Instantiate(_speechBubblePrefab, spawnPos, Quaternion.identity);
        var bubble = bubbleGO.GetComponent<SpeechBubble>();

        if (bubble != null)
            bubble.Show(bubbleType, transform, duration);
    }

    // =========================================================================
    // MOVEMENT EVENT CALLBACKS
    // =========================================================================

    private void HandleMovementReachedGoal()
    {
        switch (CurrentState)
        {
            case CustomerState.Wander:
                // Đến điểm wander → scan kệ ngay
                _lastDecisionTime = 0f;
                break;

            case CustomerState.SeekingShelf:
                // Đến gần kệ → bắt đầu Examine
                TransitionToState(CustomerState.ExamineShelf);
                if (_examineCoroutine != null) StopCoroutine(_examineCoroutine);
                _examineCoroutine = StartCoroutine(ExamineShelfCoroutine());
                break;

            case CustomerState.QueueAtCheckout:
                // Đến slot trong hàng → chờ
                TransitionToState(CustomerState.WaitingInLine);
                break;

            case CustomerState.ExitShop:
                // Đến cửa → tự hủy
                Destroy(gameObject);
                break;
        }
    }

    private void HandleMovementPathNotFound()
    {
        Debug.LogWarning($"[CustomerFSM] {InstanceId}: Path not found in state {CurrentState}. Wandering.");
        TransitionToState(CustomerState.Wander);
        MoveToRandomWanderPoint();
    }

    private void HandleMovementGoalAbandoned()
    {
        Debug.LogWarning($"[CustomerFSM] {InstanceId}: Goal abandoned. Leaving shop.");
        TransitionToState(CustomerState.ExitShop);
        MoveToExit();
    }

    // =========================================================================
    // CALLBACKS TỪ CashierQueueManager
    // =========================================================================

    /// <summary>
    /// CashierQueueManager gọi khi vị trí slot thay đổi (người trước rời hàng).
    /// NPC di chuyển lên slot mới.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   IF |currentY - expectedY| > 5: state = GO_CASHIER (di chuyển lên)
    /// </summary>
    public void UpdateQueuePosition(Vector3 newSlotWorldPos)
    {
        if (CurrentState != CustomerState.WaitingInLine &&
            CurrentState != CustomerState.QueueAtCheckout) return;

        Vector2Int newSlotCell = GridSystem.Instance.WorldToCell(newSlotWorldPos);
        TransitionToState(CustomerState.QueueAtCheckout);
        _movement.RequestPath(newSlotCell);
    }

    /// <summary>
    /// CashierQueueManager gọi khi NPC được phục vụ.
    /// Chuyển sang ExitShop.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   IF myIndex == -1: → LEAVE (đã được serve)
    /// </summary>
    public void OnServed()
    {
        _queueSlotIndex = -1;
        _carriedItemId = string.Empty;

        if (_verboseLogging)
            Debug.Log($"[CustomerFSM] {InstanceId}: Served! Heading to exit.");

        TransitionToState(CustomerState.ExitShop);
        MoveToExit();
    }

    // =========================================================================
    // UTILITIES
    // =========================================================================

    private void TransitionToState(CustomerState newState)
    {
        if (_verboseLogging)
            Debug.Log($"[CustomerFSM] {InstanceId}: {CurrentState} → {newState}");

        CurrentState = newState;
    }

    private void CheckBoredom()
    {
        if (CurrentState == CustomerState.ExitShop) return;
        if (CurrentState == CustomerState.WaitingInLine) return; // Cho phép xếp hàng dài

        // Boredom: Nếu quá BOREDOM_THRESHOLD mà chưa mua được gì → rời đi
        // Tương đương: IF (now - spawnTime) > 45000ms → LEAVE
        if (Time.time - _spawnTime > BOREDOM_THRESHOLD)
        {
            if (_verboseLogging)
                Debug.Log($"[CustomerFSM] {InstanceId}: Bored after {BOREDOM_THRESHOLD}s. Leaving.");

            TransitionToState(CustomerState.ExitShop);
            MoveToExit();
        }
    }
}
```

---

### 4.7 `CustomerSpawner.cs`

**Vị trí:** `Assets/Scripts/Customer/CustomerSpawner.cs`  
**Mục đích:** Spawn NPC theo interval, quản lý số lượng tối đa, assign intent theo tỷ lệ cấu hình.

```csharp
// Assets/Scripts/Customer/CustomerSpawner.cs

using UnityEngine;

/// <summary>
/// Spawn khách hàng NPC theo interval khi cửa hàng đang mở.
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ (NPCManager.ts):
///   Spawn interval: 3000ms (timer lặp)
///   Max NPC count: 15
///   Spawn conditions: shopState == 'OPEN' AND timeInMinutes < 1200 AND currentNPCCount < 15
///   Spawn location: doorLocation (x, y+50)
///   Intent: 30% PLAY, 70% BUY
///
/// HỆ THỐNG MỚI:
///   Cấu hình qua CustomerConfig ScriptableObject (hoặc SerializeField).
///   Kiểm tra ShopFloorManager.Instance.IsOpen thay vì shopState string.
/// </summary>
public class CustomerSpawner : MonoBehaviour
{
    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Spawn Settings")]
    [Tooltip("Prefab NPC khách hàng. Phải có CustomerFSM và CharacterMovement.")]
    [SerializeField] private GameObject _customerPrefab;

    [Tooltip("Vị trí spawn (cửa vào cửa hàng). " +
             "Tương đương doorLocation trong NPCManager.ts cũ.")]
    [SerializeField] private Transform _spawnPoint;

    [Tooltip("Thời gian giữa mỗi lần spawn (giây). " +
             "Tương đương 3000ms trong hệ thống cũ.")]
    [SerializeField] private float _spawnInterval = 3f;

    [Tooltip("Số NPC tối đa cùng lúc trong shop. " +
             "Tương đương max NPC count = 15 trong hệ thống cũ.")]
    [SerializeField] private int _maxCustomers = 15;

    [Tooltip("Tỷ lệ phần trăm NPC có intent BUY (0-1). " +
             "Phần còn lại là PLAY intent. " +
             "Tương đương 70% BUY, 30% PLAY trong hệ thống cũ.")]
    [SerializeField][Range(0f, 1f)] private float _buyIntentRatio = 0.7f;

    [Header("State")]
    [Tooltip("Shop có đang mở không. Set bởi TimeManager / Player.")]
    [SerializeField] private bool _shopIsOpen = true;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = true;

    // =========================================================================
    // RUNTIME STATE
    // =========================================================================

    private float _nextSpawnTime;
    private int _spawnCounter = 0;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Start()
    {
        _nextSpawnTime = Time.time + _spawnInterval;
    }

    private void Update()
    {
        if (Time.time < _nextSpawnTime) return;
        _nextSpawnTime = Time.time + _spawnInterval;

        TrySpawnCustomer();
    }

    // =========================================================================
    // SPAWN LOGIC
    // =========================================================================

    /// <summary>
    /// Thử spawn một khách hàng mới.
    /// Kiểm tra điều kiện trước khi spawn.
    /// </summary>
    private void TrySpawnCustomer()
    {
        // Điều kiện shop mở
        if (!_shopIsOpen) return;

        // Kiểm tra số lượng NPC hiện tại
        int currentCount = FindObjectsByType<CustomerFSM>(FindObjectsSortMode.None).Length;
        if (currentCount >= _maxCustomers) return;

        // Kiểm tra có cashier (tùy chọn — bỏ comment nếu muốn chỉ spawn khi có cashier)
        // if (ShopFloorManager.Instance?.CashierQueue?.HasCashier == false) return;

        SpawnCustomer();
    }

    /// <summary>
    /// Tạo NPC mới tại spawn point với intent ngẫu nhiên.
    /// </summary>
    private void SpawnCustomer()
    {
        if (_customerPrefab == null || _spawnPoint == null) return;

        _spawnCounter++;
        string instanceId = $"customer_{_spawnCounter:D4}_{Time.time:F0}";

        // Chọn intent: 70% BUY, 30% PLAY
        CustomerFSM.CustomerIntent intent = UnityEngine.Random.value < _buyIntentRatio
            ? CustomerFSM.CustomerIntent.Buy
            : CustomerFSM.CustomerIntent.Play;

        // Instantiate
        GameObject customerGO = Instantiate(
            _customerPrefab,
            _spawnPoint.position,
            Quaternion.identity
        );
        customerGO.name = $"[Customer] {instanceId}";

        // Khởi tạo FSM
        var fsm = customerGO.GetComponent<CustomerFSM>();
        if (fsm != null)
        {
            fsm.Initialize(instanceId, intent);
        }
        else
        {
            Debug.LogError("[CustomerSpawner] CustomerFSM component không tìm thấy trên prefab!");
            Destroy(customerGO);
            return;
        }

        if (_verboseLogging)
        {
            Debug.Log($"[CustomerSpawner] Spawned {instanceId} with intent={intent}. " +
                      $"Total active: {FindObjectsByType<CustomerFSM>(FindObjectsSortMode.None).Length}");
        }
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    public void SetShopOpen(bool isOpen) => _shopIsOpen = isOpen;
    public bool IsShopOpen => _shopIsOpen;
}
```

---

## 5. Editor Script: Auto-Generate Customer Config Asset

```csharp
// Assets/Editor/CustomerDataGenerator.cs

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// Tạo thư mục và cấu hình cần thiết cho Customer system.
/// Chạy qua menu: TCGShop > Setup > Generate Customer Config
/// </summary>
public static class CustomerDataGenerator
{
    [MenuItem("TCGShop/Setup/Generate Customer Config")]
    public static void GenerateCustomerConfig()
    {
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/Customer"))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Customer");

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Customer"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Customer");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Customer Config",
            "Thư mục đã được tạo:\n" +
            "• Assets/ScriptableObjects/Customer/\n" +
            "• Assets/Prefabs/Customer/\n\n" +
            "Tiếp theo:\n" +
            "1. Tạo Prefab_Customer.prefab với CustomerFSM + CharacterMovement\n" +
            "2. Tạo Prefab_SpeechBubble.prefab với Canvas (World Space) + SpeechBubble\n" +
            "3. Assign prefabs vào CustomerSpawner và CustomerFSM",
            "OK");
    }
}
#endif
```

---

## 6. Thiết Lập Scene

```
GameScene Hierarchy (cập nhật từ Bước 4):
│
├── _ShopFloorManager          [ShopFloorManager.cs]         ← MỚI
│   Inspector:
│     Total Money: 500 (tiền ban đầu)
│     Cashier Queue: (kéo _CashierQueueManager vào đây)
│
├── _CashierQueueManager       [CashierQueueManager.cs]      ← MỚI
│   Inspector:
│     Checkout Speed: 3 (giây/khách)
│     Max Queue Size: 10
│     Verbose Logging: true
│
├── _CustomerSpawner           [CustomerSpawner.cs]          ← MỚI
│   Inspector:
│     Customer Prefab: Prefab_Customer
│     Spawn Point: (GameObject tại cửa vào)
│     Spawn Interval: 3 (giây)
│     Max Customers: 15
│     Buy Intent Ratio: 0.7
│     Shop Is Open: true
│
└── NPCs/                      (Empty parent từ Bước 4)
    └── [Spawned at runtime]
```

**Cấu trúc Prefab_Customer:**
```
Prefab_Customer
├── SpriteRenderer          (sprite NPC đơn giản)
├── CharacterMovement.cs    (từ Bước 4)
├── CustomerFSM.cs          (MỚI — THAY THẾ CustomerAIController)
│   Inspector:
│     Speech Bubble Prefab: Prefab_SpeechBubble
│     Bubble Y Offset: 1.2
│     Verbose Logging: true (để test)
└── [Collider2D nếu cần]
```

**Cấu trúc Prefab_SpeechBubble:**
```
Prefab_SpeechBubble
├── Canvas                  (Render Mode: World Space, Scale: 0.005)
│   ├── CanvasGroup         (component để fade alpha)
│   └── Image               (Icon sprite)
└── SpeechBubble.cs
    Inspector:
      Heart Icon:   [Sprite tim đỏ]
      Neutral Icon: [Sprite dấu chấm hỏi]
      Angry Icon:   [Sprite mặt tức]
      Float Frequency: 1.5
      Float Amplitude: 0.08
      Pop In Duration: 0.2
      Fade Out Duration: 0.5
      Vertical Offset: 1.2
```

---

## 7. Giới Hạn & Quy Tắc Bắt Buộc

| Quy tắc | Lý do |
|---------|-------|
| **CẤM** `GameObject.Find()` hay `GetComponent()` trong `Update()` | Quy tắc toàn dự án từ Bước 1 |
| **CẤM** `FindObjectOfType<T>()` trong `Update()` | O(n) mỗi frame — dùng events thay thế |
| **CẤM** hardcode xác suất mua trong `CustomerFSM.cs` | Mọi logic xác suất phải đi qua `EconomicDecisionEngine` |
| **BẮT BUỘC** `SpeechBubble` dùng Billboard effect (xoay về Camera) | Isometric view — không xoay sẽ méo hình |
| **BẮT BUỘC** `CashierQueueManager.GetSlotWorldPosition(slotIndex)` tính đúng offset | Tương đương `cashier.y + 60 + (myIndex × 40)` hệ cũ |
| **BẮT BUỘC** `CustomerFSM` dùng `checkedShelfIds` để không quay lại kệ đã từ chối | Tương đương `checkedShelfIds` trong hệ cũ |
| **BẮT BUỘC** XÓA hoặc disable `CustomerAIController.cs` từ Bước 4 | Không để cả hai FSM cùng chạy |
| **BẮT BUỘC** `EconomicDecisionEngine.DecidePurchase` log ra Console trong test | Để verify công thức đúng |

---

## 8. Kịch Bản Kiểm Thử Đầy Đủ

### 8.1 Chuẩn Bị

```
Bước 1: Menu TCGShop > Setup > Generate Customer Config
        → Tạo thư mục Prefabs/Customer và ScriptableObjects/Customer

Bước 2: Tạo Prefab_Customer với CustomerFSM + CharacterMovement
Bước 3: Tạo Prefab_SpeechBubble với Canvas (World Space) + SpeechBubble + 3 icon sprites
Bước 4: Tạo ShelfInstance trên Prefab kệ hàng, gán icon sprites cho SpeechBubble
Bước 5: Đặt một CashierDesk vào scene → đăng ký CashierQueueManager

Chuẩn bị test Angry Bubble:
  - Đặt ShelfSingle vào scene
  - Gọi ShelfInstance.SetStock("pack_sv01", 5, sellPrice=30f, marketPrice=10f)
    → sellPrice = 30, market = 10 → ratio = 3.0 → đắt ×3 → probability = 0%
```

### 8.2 Test Bắt Buộc — Giá ×3 → Angry Bubble → ExitShop

```
ĐÂY LÀ TEST QUAN TRỌNG NHẤT THEO YÊU CẦU

Setup:
  Kệ có Pack, sellPrice=$30, marketPrice=$10 (đắt gấp 3 lần)
  Shop mở, CustomerSpawner active

Kịch bản:
  NPC xuất hiện ở cửa → EnterShop (0.5s)
  → Wander (di chuyển vào shop)
  → Radar tìm thấy kệ → SeekingShelf (di chuyển đến kệ qua A*)
  → Đến gần kệ → ExamineShelf (đứng yên)

KỲ VỌNG Console trong ExamineShelf:
  "[CustomerFSM] customer_0001_X examining shelf: SellPrice=$30.00, Market=$10.00, Probability=0%, Decision=AbsoluteRefusal, WillBuy=False"

KỲ VỌNG Scene (ĐỒNG THỜI với Console log):
  1. Bong bóng ANGRY (icon tức giận màu cam/đỏ) xuất hiện trên đầu NPC
  2. Bong bóng lơ lửng nhẹ nhàng, animate pop-in mượt
  3. NPC đứng im ĐÚNG 2 GIÂY (EXAMINE_DURATION = 2f)
  4. Sau 2 giây: NPC chuyển ExitShop
  5. NPC di chuyển ra cửa (di chuyển theo A*, không đi thẳng)
  6. Bong bóng mờ dần và biến mất (fade out animation)
  7. Đến cửa: NPC Destroy()

KỲ VỌNG KHÔNG CÓ:
  - NPC mua hàng (TakeOneItem không được gọi)
  - NPC đi đến cashier
  - NullReferenceException
  - Bong bóng Heart hay Neutral (phải là Angry)
```

### 8.3 Test Giá Hợp Lý → Heart Bubble → QueueAtCheckout

```
Setup:
  Kệ có Pack, sellPrice=$10, marketPrice=$10 (bằng thị trường → 95% mua)
  Có CashierDesk đã đăng ký

Kịch bản:
  NPC tìm thấy kệ → ExamineShelf 2s với Heart bubble
  → Quyết định mua (95% → hầu như chắc chắn)
  → TakeOneItem() → stockCount--
  → QueueAtCheckout → di chuyển đến slot 0 của cashier
  → WaitingInLine (đứng chờ)
  → Sau 3s (checkoutSpeed): OnServed() → ExitShop → Destroy

KỲ VỌNG Console:
  "[CashierQueue] customer_0001 joined queue at slot 0. Queue size: 1."
  "[CashierQueue] Served customer_0001. Revenue: +$10.00. Queue remaining: 0."
  "[ShopFloorManager] 💰 Transaction: +$10.00. Daily Revenue: $10.00."

KỲ VỌNG Scene:
  NPC đứng đúng slot 0 (ngay trước cashier)
  Khi NPC rời → NPC thứ 2 (nếu có) tự dịch chuyển lên slot 0
```

### 8.4 Test Queue Offset — Nhiều NPC Xếp Hàng

```
Setup:
  checkoutSpeed = 10s (chậm để nhiều NPC vào hàng)
  Spawn 3 NPC cùng lúc (tắt spawnInterval, dùng debug spawn button)

Kịch bản:
  NPC_1 vào hàng → slot 0 (ngay trước cashier)
  NPC_2 vào hàng → slot 1 (cách NPC_1 là QUEUE_SPACING = 1 unit)
  NPC_3 vào hàng → slot 2 (cách NPC_2 là 1 unit)

KỲ VỌNG Positions:
  cashierPos = (0, 0, 0) (ví dụ)
  NPC_1 tại: cashierPos + (0,-1,0)×1.5 + (0,-1,0)×(1.0×0) = (0, -1.5, 0)
  NPC_2 tại: cashierPos + (0,-1,0)×1.5 + (0,-1,0)×(1.0×1) = (0, -2.5, 0)
  NPC_3 tại: cashierPos + (0,-1,0)×1.5 + (0,-1,0)×(1.0×2) = (0, -3.5, 0)

  → NPC xếp thành hàng dọc, cách đều nhau 1 unit

  Sau khi NPC_1 được serve:
  NPC_2 di chuyển từ (0,-2.5,0) lên (0,-1.5,0) = slot 0
  NPC_3 di chuyển từ (0,-3.5,0) lên (0,-2.5,0) = slot 1

KỲ VỌNG Console:
  "[CashierQueue] customer_0002 joined queue at slot 1."
  "[CashierQueue] customer_0003 joined queue at slot 2."
  (Sau serve): "[CashierQueue] Served customer_0001. Queue remaining: 2."
```

### 8.5 Test Economic Decision Engine — Verify Công Thức

```
Test thủ công qua Console (tạo script Debug tạm thời):

void Start() {
    // Test các mức giá
    float market = 10f;
    float[] prices = { 9f, 10f, 10.5f, 11f, 15f, 20f, 30f };
    
    foreach (float sell in prices) {
        float prob = EconomicDecisionEngine.CalculateBuyProbability(sell, market);
        Debug.Log($"Sell=${sell}, Market=${market}, Ratio={sell/market:F2}, Prob={prob:P0}");
    }
}

KỲ VỌNG Console:
  Sell=$9,  Market=$10, Ratio=0.90, Prob=95%   (giá tốt hơn thị trường)
  Sell=$10, Market=$10, Ratio=1.00, Prob=95%   (bằng thị trường)
  Sell=$10.5, Market=$10, Ratio=1.05, Prob=95% (5% đắt hơn → 1 bước → 95%-15%=80%)
  Sell=$11, Market=$10, Ratio=1.10, Prob=65%   (10% đắt → 2 bước → 95%-30%=65%)
  Sell=$15, Market=$10, Ratio=1.50, Prob=0%    (50% đắt → 10 bước → 0%)
  Sell=$20, Market=$10, Ratio=2.00, Prob=0%    (100% đắt → 0%)
  Sell=$30, Market=$10, Ratio=3.00, Prob=0%    (200% đắt → 0%) ← TEST CASE BẮT BUỘC
```

### 8.6 Test Không Có Lỗi

```
Chạy Play trong 2 phút:
  - Để nhiều NPC spawn (ít nhất 5)
  - Một số kệ có giá bình thường, một kệ giá ×3
  - Có CashierDesk

KỲ VỌNG Console (chỉ filter Errors):
  0 NullReferenceException
  0 MissingReferenceException
  0 IndexOutOfRangeException

KỲ VỌNG Scene:
  NPC di chuyển mượt, không bay qua kệ
  Bong bóng lơ lửng đúng trên đầu NPC (không bị méo do Billboard effect)
  Queue offset đúng khoảng cách
```

---

## 9. Định Nghĩa "Hoàn Thành" (Definition of Done)

Bước 5 được coi là **HOÀN THÀNH** khi và chỉ khi:

- [ ] `CustomerAIController.cs` từ Bước 4 đã bị xóa hoặc disable hoàn toàn
- [ ] `CustomerFSM.cs` có đủ 6 states: `EnterShop`, `Wander`, `SeekingShelf`, `ExamineShelf`, `QueueAtCheckout`, `WaitingInLine`, `ExitShop`
- [ ] `EconomicDecisionEngine.CalculateBuyProbability(30f, 10f)` trả về `0.0f` (đắt ×3 → 0%)
- [ ] `EconomicDecisionEngine.CalculateBuyProbability(10f, 10f)` trả về `0.95f` (giá bằng → 95%)
- [ ] NPC đặt kệ giá ×3 thị trường: đứng tại kệ **đúng 2 giây**, hiện bong bóng **Angry**, rời đi
- [ ] Bong bóng Angry **không xuất hiện** khi giá hợp lý (≤ thị trường)
- [ ] Queue offset: 3 NPC xếp hàng đúng khoảng cách QUEUE_SPACING = 1.0f
- [ ] Khi NPC_1 được serve, NPC_2 và NPC_3 tự **di chuyển lên** 1 slot
- [ ] `ShopFloorManager` log `💰 Transaction` khi giao dịch hoàn thành
- [ ] Billboard effect: bong bóng **luôn nhìn về Camera** không bị méo trong Isometric view
- [ ] `checkedShelfIds`: NPC đã từ chối kệ A không quay lại kệ A trong cùng lần visit
- [ ] Boredom: NPC tự rời đi sau `BOREDOM_THRESHOLD = 45f` giây nếu không mua được gì
- [ ] **Không có** `GetComponent()` hay `GameObject.Find()` trong bất kỳ `Update()` nào
- [ ] **Không có** `NullReferenceException` trong 2 phút test với 5+ NPC

**Chỉ sau khi tất cả checkbox được check, mới chuyển sang Bước 6.**
```