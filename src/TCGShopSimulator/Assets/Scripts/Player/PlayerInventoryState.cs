// Assets/Scripts/Player/PlayerInventoryState.cs

using System;
using UnityEngine;

/// <summary>
/// Singleton quản lý trạng thái "Player đang cầm thùng hàng".
///
/// NGUYÊN TẮC THIẾT KẾ:
///   - Đây là state machine đơn giản: IDLE ↔ CARRYING
///   - CARRYING: player đang cầm 1 DeliveryBox
///   - IDLE: player không cầm gì
///
/// MAPPING TỪ HỆ THỐNG CŨ:
///   deliveryStore.carriedBox = { itemId, quantity }
///     → PlayerInventoryState.CarryingItem (nullable struct)
///
/// KHI CARRYING:
///   - CharacterMovement vẫn hoạt động bình thường
///   - Interact với DeliveryBox = SWAP (không cho phép)
///   - Interact với ShelfInstance = DEPOSIT
/// </summary>
public class PlayerInventoryState : MonoBehaviour
{
    // ========================================================================
    // SINGLETON
    // ========================================================================

    public static PlayerInventoryState Instance { get; private set; }

    // ========================================================================
    // CARRYING ITEM DATA
    // ========================================================================

    [Serializable]
    public struct CarriedItem
    {
        public string itemId;          // packId
        public int quantity;          // số lượng trong box
        public string deliveryBoxId;   // ID của box gốc (để track)

        public CarriedItem(string itemId, int quantity, string deliveryBoxId)
        {
            this.itemId = itemId;
            this.quantity = quantity;
            this.deliveryBoxId = deliveryBoxId;
        }
    }

    // ========================================================================
    // STATE
    // ========================================================================

    public CarriedItem? CurrentCarrying { get; private set; }
    public bool IsCarrying => CurrentCarrying.HasValue;

    // ========================================================================
    // EVENTS
    // ========================================================================

    public event Action<CarriedItem?> OnCarryingStateChanged;

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
    }

    // ========================================================================
    // PUBLIC API
    // ========================================================================

    /// <summary>
    /// Player nhặt box lên. Gọi từ PlayerRaycastHandler khi interact với DeliveryBox.
    /// </summary>
    public void PickUpBox(string itemId, int quantity, string deliveryBoxId)
    {
        if (IsCarrying)
        {
            Debug.LogWarning($"[PlayerInventoryState] Already carrying '{CurrentCarrying.Value.itemId}'. " +
                           $"Cannot pick up new box.");
            return;
        }

        CurrentCarrying = new CarriedItem(itemId, quantity, deliveryBoxId);
        OnCarryingStateChanged?.Invoke(CurrentCarrying);

        Debug.Log($"[PlayerInventoryState] Picked up box: {itemId} x{quantity} (BoxId={deliveryBoxId})");
    }

    /// <summary>
    /// Player đặt hàng xuống shelf (hoặc storage).
    /// Gọi từ PlayerRaycastHandler khi interact với ShelfInstance.
    /// Trả về true nếu deposit thành công.
    /// </summary>
    public bool DepositToShelf(ShelfInstance shelf)
    {
        if (!IsCarrying)
        {
            Debug.LogWarning("[PlayerInventoryState] Not carrying anything to deposit!");
            return false;
        }

        if (shelf == null)
        {
            Debug.LogWarning("[PlayerInventoryState] Shelf is null!");
            return false;
        }

        var carried = CurrentCarrying.Value;

        shelf.SetStock(
            carried.itemId,
            carried.quantity,
            shelf.CurrentSellPrice,
            shelf.MarketPrice
        );

        // Xóa carrying state
        CurrentCarrying = null;
        OnCarryingStateChanged?.Invoke(null);

        Debug.Log($"[PlayerInventoryState] Deposited {carried.itemId} x{carried.quantity} to shelf.");
        return true;
    }

    /// <summary>
    /// Player hủy đặt hàng (đặt về kho storage thay vì shelf bán).
    /// </summary>
    public void DepositToStorage(string itemId, int quantity)
    {
        if (!IsCarrying)
        {
            Debug.LogWarning("[PlayerInventoryState] Not carrying anything to deposit!");
            return;
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddPack(itemId, quantity);
        }

        CurrentCarrying = null;
        OnCarryingStateChanged?.Invoke(null);

        Debug.Log($"[PlayerInventoryState] Deposited to storage: {itemId} x{quantity}");
    }

    /// <summary>
    /// Drop carrying item (hủy box không đặt đâu cả).
    /// </summary>
    public void DropCarrying()
    {
        if (!IsCarrying) return;

        Debug.Log($"[PlayerInventoryState] Dropped carrying item.");
        CurrentCarrying = null;
        OnCarryingStateChanged?.Invoke(null);
    }
}
