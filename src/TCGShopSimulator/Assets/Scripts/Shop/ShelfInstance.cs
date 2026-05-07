// Assets/Scripts/Shop/ShelfInstance.cs

using System;
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
             "Empty string = kệ trống.")]
    [SerializeField] private string _displayedItemId = string.Empty;

    [Tooltip("Số lượng hàng còn trên kệ.")]
    [SerializeField] private int _stockCount = 0;

    [Tooltip("Giá bán do người chơi đặt (USD).")]
    [SerializeField] private float _currentSellPrice = 0f;

    [Tooltip("Giá thị trường tham chiếu (USD). Dùng bởi EconomicDecisionEngine.")]
    [SerializeField] private float _marketPrice = 0f;

    [Tooltip("Kệ này là kệ bán (NPC mua được) hay kệ lưu trữ (chỉ cất hàng).")]
    [SerializeField] private bool _isSellingShelf = true;

    // =========================================================================
    // CACHED REFERENCES
    // =========================================================================

    private PlacedFurnitureInstance _furnitureInstance;

    // ========================================================================
    // PERSISTENCE — Restore state (before Start() runs)
    // ========================================================================
    private bool _skipFirstRegister = false;
    private ShelfRestoredState _pendingRestoredState;

    private struct ShelfRestoredState
    {
        public string itemId;
        public int qty;
        public float price;
        public float market;
    }

    // ========================================================================
    // EVENTS
    // ========================================================================

    /// <summary>Khi hàng thay đổi số lượng. ShopFloorManager subscribe.</summary>
    public event Action<ShelfInstance> OnStockChanged;

    /// <summary>Khi kệ hết hàng hoàn toàn. ShopFloorManager loại khỏi available list.</summary>
    public event Action<ShelfInstance> OnShelfEmptied;

    /// <summary>Khi player set giá mới. CustomerFSM đang ExamineShelf cần re-evaluate.</summary>
    public event Action<ShelfInstance, float> OnPriceChanged;

    /// <summary>
    /// Kích hoạt khi Player raycast vào kệ hàng này.
    /// ShelfManagementUI subscribe để mở panel.
    /// </summary>
    public event Action<ShelfInstance> OnShelfInteracted;

    // =========================================================================
    // PROPERTIES
    // =========================================================================

    public string DisplayedItemId => _displayedItemId;
    public int    StockCount      => _stockCount;
    public float CurrentSellPrice => _currentSellPrice;
    public float MarketPrice     => _marketPrice;
    public bool  IsSellingShelf  => _isSellingShelf;

    public bool HasStock =>
        _isSellingShelf && _stockCount > 0 && !string.IsNullOrEmpty(_displayedItemId);

    public Vector3 WorldPosition => transform.position;

    /// <summary>Tỷ lệ giá bán / giá thị trường. EconomicDecisionEngine dùng để tính xác suất mua.</summary>
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
        // Nếu đang restore từ save, bỏ qua đăng ký lần đầu
        if (_skipFirstRegister)
        {
            _skipFirstRegister = false;
            if (_pendingRestoredState.qty > 0)
            {
                SetStock(
                    _pendingRestoredState.itemId,
                    _pendingRestoredState.qty,
                    _pendingRestoredState.price,
                    _pendingRestoredState.market
                );
            }
            _pendingRestoredState = default;
            return;
        }

        if (ShopFloorManager.Instance != null)
            ShopFloorManager.Instance.RegisterShelf(this);
    }

    private void OnDestroy()
    {
        if (ShopFloorManager.Instance != null)
            ShopFloorManager.Instance.UnregisterShelf(this);
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Player đặt hàng lên kệ. Gọi từ ShelfManagementUI.
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ: fillTier(shelfId, itemId, tierIndex)
    /// </summary>
    public void SetStock(string itemId, int quantity, float sellPrice, float marketPrice)
    {
        _displayedItemId   = itemId;
        _stockCount         = quantity;
        _currentSellPrice   = sellPrice;
        _marketPrice        = marketPrice;

        OnStockChanged?.Invoke(this);

        if (ShopFloorManager.Instance != null)
            ShopFloorManager.Instance.NotifyShelfUpdated(this);
    }

    /// <summary>
    /// Player thay đổi giá bán. Gọi từ SetPriceUI.
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ: shopItems[itemId].sellPrice = customPrice
    /// </summary>
    public void SetPrice(float newPrice)
    {
        float oldPrice = _currentSellPrice;
        _currentSellPrice = Mathf.Max(0.01f, newPrice);
        OnPriceChanged?.Invoke(this, _currentSellPrice);
    }

    /// <summary>
    /// NPC lấy 1 món hàng từ kệ. Trả về true nếu thành công, false nếu kệ đã hết hàng.
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ: npcTakeItemFromSlot(shelfId)
    /// </summary>
    public bool TakeOneItem(out string takenItemId, out float paidPrice)
    {
        takenItemId = string.Empty;
        paidPrice   = 0f;

        if (!HasStock) return false;

        takenItemId = _displayedItemId;
        paidPrice   = _currentSellPrice;
        _stockCount--;

        OnStockChanged?.Invoke(this);

        if (_stockCount <= 0)
        {
            _stockCount        = 0;
            _displayedItemId   = string.Empty;
            OnShelfEmptied?.Invoke(this);

            if (ShopFloorManager.Instance != null)
                ShopFloorManager.Instance.NotifyShelfEmptied(this);
        }

        return true;
    }

    /// <summary>
    /// Cấu hình kệ dựa trên FurnitureDefinition khi được đặt xuống.
    /// </summary>
    public void SetupFromDefinition(FurnitureDefinition definition)
    {
        if (definition == null) return;
        _isSellingShelf = definition.shelfRole == ShelfRole.Selling;
    }

    /// <summary>
    /// Unique entity ID cho shelf này. Dùng để track trong HashSet.
    /// </summary>
    public new int GetEntityId() => base.GetEntityId();

    /// <summary>
    /// Instance ID of the underlying PlacedFurnitureInstance.
    /// Used for save/load matching.
    /// </summary>
    public string InstanceId => _furnitureInstance?.InstanceId ?? string.Empty;

    /// <summary>
    /// Gọi từ PlayerRaycastHandler khi player click vào kệ.
    /// Fire OnShelfInteracted event.
    /// </summary>
    public void NotifyInteracted()
    {
        OnShelfInteracted?.Invoke(this);
        
        // Direct call to UI singleton to ensure it opens
        if (ShelfManagementUI.Instance != null)
        {
            ShelfManagementUI.Instance.Show(this);
        }
        else
        {
            Debug.LogWarning("[ShelfInstance] ShelfManagementUI.Instance is null! Cannot show UI.");
        }
    }

    public override string ToString() =>
        $"Shelf[{_displayedItemId}|Qty={_stockCount}|Price=${_currentSellPrice:F2}|Market=${_marketPrice:F2}]";

    // ========================================================================
    // PERSISTENCE
    // ========================================================================

    /// <summary>
    /// Setup shelf sau khi được restore từ save.
    /// Gọi bởi GridSystem.RestoreSingleFurniture() TRƯỚC KHI Start().
    /// Lưu state vào _pendingRestoredState, Start() sẽ đọc và áp dụng.
    /// </summary>
    public void SetupRestoredStock(string itemId, int qty, float price, float market)
    {
        _skipFirstRegister = true;
        _pendingRestoredState = new ShelfRestoredState
        {
            itemId = itemId,
            qty = qty,
            price = price,
            market = market
        };
    }

    /// <summary>
    /// Restore stock từ GameData (gọi sau khi tất cả furniture instantiated).
    /// Dùng cho trường hợp shelf đã Start rồi nhưng stock chưa restore.
    /// </summary>
    public void RestoreStockFromGameData(GameData data)
    {
        if (data.placedFurniture == null) return;

        foreach (var shelfData in data.placedFurniture)
        {
            if (shelfData.instanceId == InstanceId)
            {
                SetStock(
                    shelfData.displayedItemId,
                    shelfData.stockCount,
                    shelfData.sellPrice,
                    shelfData.marketPrice
                );
                return;
            }
        }
    }
}
