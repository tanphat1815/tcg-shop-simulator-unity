// Assets/Scripts/Shop/ShelfInventoryBridge.cs

using UnityEngine;

/// <summary>
/// Bridge giữa InventoryManager (kho hàng) và ShelfInstance (kệ trưng bày).
///
/// RESPONSIBILITIES:
///   1. Lấy pack từ Inventory → đặt lên Shelf
///   2. Lấy pack từ Shelf → trả về Inventory (player thu hồi)
///   3. Thông báo cho các system khác qua GameEconomyEvents
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   furnitureStore.fillTier() / furnitureStore.clearTier()
/// </summary>
public class ShelfInventoryBridge : MonoBehaviour
{
    // ========================================================================
    // SINGLETON
    // ========================================================================

    public static ShelfInventoryBridge Instance { get; private set; }

    // ========================================================================
    // VÒNG ĐỜI
    // ========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Debug.Log("[ShelfInventoryBridge] Initialized.");
    }

    private void OnEnable()
    {
        GameEconomyEvents.OnTransactionCompleted += HandleTransactionCompleted;
    }

    private void OnDisable()
    {
        GameEconomyEvents.OnTransactionCompleted -= HandleTransactionCompleted;
    }

    // ========================================================================
    // RESTOCK
    // ========================================================================

    /// <summary>
    /// Đặt pack từ inventory lên kệ hàng.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   fillTier(shelfId, itemId, tierIndex) trong furnitureStore.ts
    ///   + kiểm tra shopInventory[itemId] >= quantity
    /// </summary>
    /// <returns>True nếu thành công.</returns>
    public bool RestockShelf(ShelfInstance shelf, string packId, int quantity, float sellPrice)
    {
        if (shelf == null)
        {
            Debug.LogWarning("[ShelfInventoryBridge] Shelf is null.");
            return false;
        }

        if (string.IsNullOrEmpty(packId) || quantity <= 0)
        {
            Debug.LogWarning("[ShelfInventoryBridge] Invalid packId or quantity.");
            return false;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[ShelfInventoryBridge] InventoryManager.Instance is null!");
            return false;
        }

        int available = InventoryManager.Instance.GetPackCount(packId);
        if (available < quantity)
        {
            Debug.LogWarning($"[ShelfInventoryBridge] Not enough packs. " +
                            $"Need {quantity}, have {available}.");
            return false;
        }

        float marketPrice = GetMarketPrice(packId);

        shelf.SetStock(packId, quantity, sellPrice, marketPrice);

        GameEconomyEvents.FireShelfStockChanged(new ShelfStockChange(
            shelf, packId, 0, quantity, sellPrice,
            ShelfStockChangeReason.PlayerRestocked));

        Debug.Log($"[ShelfInventoryBridge] Restocked shelf with {quantity}x {packId} " +
                  $"at ${sellPrice:F2} (market: ${marketPrice:F2}).");

        return true;
    }

    /// <summary>
    /// Đặt tất cả pack có thể từ inventory lên kệ (fill max).
    /// </summary>
    public bool RestockShelfMax(ShelfInstance shelf, string packId, float sellPrice)
    {
        if (shelf == null || string.IsNullOrEmpty(packId))
            return false;

        if (InventoryManager.Instance == null)
            return false;

        int available = InventoryManager.Instance.GetPackCount(packId);
        if (available <= 0) return false;

        return RestockShelf(shelf, packId, available, sellPrice);
    }

    // ========================================================================
    // WITHDRAW
    // ========================================================================

    /// <summary>
    /// Thu hồi tất cả pack từ kệ về inventory.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   clearTier(shelfId, tierIndex) trong furnitureStore.ts
    /// </summary>
    public void WithdrawAllFromShelf(ShelfInstance shelf)
    {
        if (shelf == null) return;
        if (!shelf.HasStock) return;

        string itemId = shelf.DisplayedItemId;
        int qty = shelf.StockCount;

        if (string.IsNullOrEmpty(itemId) || qty <= 0) return;

        InventoryManager.Instance?.AddPack(itemId, qty);

        float price = shelf.CurrentSellPrice;
        shelf.SetStock(string.Empty, 0, 0f, 0f);

        GameEconomyEvents.FireShelfStockChanged(new ShelfStockChange(
            shelf, itemId, qty, 0, price,
            ShelfStockChangeReason.PlayerCleared));

        Debug.Log($"[ShelfInventoryBridge] Withdrew {qty}x {itemId} from shelf to inventory.");
    }

    // ========================================================================
    // TRANSACTION HANDLER
    // ========================================================================

    private void HandleTransactionCompleted(TransactionReceipt receipt)
    {
        Debug.Log($"[ShelfInventoryBridge] Transaction completed: " +
                  $"Customer {receipt.CustomerId} bought {receipt.ItemId} for ${receipt.Price:F2}.");
    }

    // ========================================================================
    // DELIVERY BRIDGE
    // ========================================================================

    /// <summary>
    /// Đổ hàng từ carried item vào shelf.
    /// Gọi bởi PlayerInventoryState.DepositToShelf().
    /// Equivalent của fillTierFromItem() trong hệ thống cũ.
    ///
    /// LƯU Ý: Shelf phải là Selling shelf (IsSellingShelf = true).
    /// Storage shelf xử lý riêng (DepositToStorage).
    /// </summary>
    public void FillShelfFromDelivery(ShelfInstance shelf, string itemId, int quantity)
    {
        if (shelf == null) return;

        if (!shelf.IsSellingShelf)
        {
            Debug.LogWarning($"[ShelfInventoryBridge] Cannot fill non-selling shelf with delivery.");
            return;
        }

        if (shelf.HasStock && shelf.DisplayedItemId != itemId)
        {
            Debug.LogWarning($"[ShelfInventoryBridge] Shelf already has different item " +
                            $"('{shelf.DisplayedItemId}'). Overwriting with '{itemId}'.");
        }

        float marketPrice = GetMarketPrice(itemId);
        shelf.SetStock(itemId, quantity, shelf.CurrentSellPrice, marketPrice);

        Debug.Log($"[ShelfInventoryBridge] Filled shelf with {itemId} x{quantity} from delivery.");
    }

    // ========================================================================
    // UTILITY
    // ========================================================================

    private float GetMarketPrice(string packId)
    {
        if (InventoryManager.Instance?.Database != null &&
            InventoryManager.Instance.Database.TryGetPack(packId, out var packData))
        {
            return packData.defaultSellPrice;
        }
        return 5f;
    }
}
