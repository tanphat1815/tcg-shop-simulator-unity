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
/// Hệ thống cũ (NPCManager.ts):
///   targetY = cashier.y + 60 + (myIndex × 40)
///
/// Hệ thống mới (Unity world-space):
///   slotWorldPos = cashierPos + Vector3(0, -1, 0) × (1.0f × slotIndex)
///
/// FIFO: First-In-First-Out — NPC vào trước được phục vụ trước.
/// Khi NPC ở slot 0 được phục vụ và rời đi → mọi NPC sau dịch chuyển lên 1 slot.
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   customerStore.waitingCustomers array trong customerStore.ts
/// </summary>
public class CashierQueueManager : MonoBehaviour
{
    // =========================================================================
    // CONSTANTS
    // =========================================================================

    private const float QUEUE_SPACING     = 1.0f;
    private const float FIRST_SLOT_OFFSET = 1.5f;
    private static readonly Vector3 QUEUE_DIRECTION = new Vector3(0f, -1f, 0f);

    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Cashier Setup")]
    [SerializeField] private Vector3 _cashierWorldPosition;
    [SerializeField] private float _checkoutSpeed    = 3f;   // Giây/khách
    [SerializeField] private int   _maxQueueSize    = 10;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = true;

    // =========================================================================
    // QUEUE STATE
    // =========================================================================

    private List<QueueEntry> _queue           = new List<QueueEntry>();
    private float _lastCheckoutTime            = -999f;
    [SerializeField] private bool  _hasCashier                 = false;

    // =========================================================================
    // EVENTS
    // =========================================================================

    /// <summary>Kích hoạt khi giao dịch hoàn thành. ShopFloorManager lắng nghe để cộng tiền.</summary>
    public event System.Action<float, string> OnTransactionCompleted;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Update()
    {
        ProcessAutoCheckout();
    }

    // =========================================================================
    // QUEUE API
    // =========================================================================

    /// <summary>
    /// NPC yêu cầu tham gia hàng chờ.
    /// Trả về slotIndex nếu thành công, -1 nếu hàng đầy.
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ: customerStore.addWaitingCustomer(targetPrice, instanceId)
    /// </summary>
    public int EnqueueCustomer(CustomerFSM customer, string itemId, float paidPrice)
    {
        if (_queue.Count >= _maxQueueSize) return -1;
        if (!_hasCashier) return -1;

        var entry = new QueueEntry
        {
            Customer   = customer,
            ItemId     = itemId,
            PaidPrice  = paidPrice,
            SlotIndex  = _queue.Count
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
    /// CÔNG THỨC:
    ///   slotWorldPos = cashierWorldPos + QueueDirection × FIRST_SLOT_OFFSET
    ///                + QueueDirection × (QUEUE_SPACING × slotIndex)
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
    /// </summary>
    public void DequeueCustomer(CustomerFSM customer)
    {
        if (customer == null)
        {
            Debug.LogWarning("[CashierQueue] DequeueCustomer called with null customer. Ignoring.");
            return;
        }

        int removedIndex = _queue.FindIndex(e => e.Customer == customer);
        if (removedIndex < 0)
        {
            Debug.LogWarning($"[CashierQueue] Customer '{customer.InstanceId}' " +
                            "was not found in the queue. Ignoring dequeue.");
            return;
        }

        var removedCustomerRef = _queue[removedIndex].Customer;
        _queue.RemoveAt(removedIndex);

        for (int i = removedIndex; i < _queue.Count; i++)
        {
            _queue[i].SlotIndex = i;
            if (_queue[i].Customer != null)
                _queue[i].Customer.UpdateQueuePosition(GetSlotWorldPosition(i));
        }

        if (_verboseLogging)
        {
            Debug.Log($"[CashierQueue] {(removedCustomerRef != null ? removedCustomerRef.InstanceId : "Unknown")} " +
                     $"dequeued. Queue size: {_queue.Count}.");
        }
    }

    /// <summary>Lấy slot hiện tại của một NPC trong hàng. -1 nếu không trong hàng.</summary>
    public int GetSlotIndex(CustomerFSM customer)
    {
        var entry = _queue.Find(e => e.Customer == customer);
        return entry?.SlotIndex ?? -1;
    }

    // =========================================================================
    // AUTO CHECKOUT
    // =========================================================================

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
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ: serveCustomer() trong customerStore.ts
    /// </summary>
    private void ServeNextCustomer()
    {
        if (_queue.Count == 0) return;

        var entry = _queue[0];
        _queue.RemoveAt(0);

        for (int i = 0; i < _queue.Count; i++)
        {
            _queue[i].SlotIndex = i;
            _queue[i].Customer.UpdateQueuePosition(GetSlotWorldPosition(i));
        }

        OnTransactionCompleted?.Invoke(entry.PaidPrice, entry.ItemId);
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

    public void RegisterCashier(Vector3 worldPosition)
    {
        _cashierWorldPosition = worldPosition;
        _hasCashier           = true;
        Debug.Log($"[CashierQueue] Cashier registered at {worldPosition}.");
    }

    public void UnregisterCashier()
    {
        _hasCashier = false;
        Debug.Log("[CashierQueue] Cashier unregistered. Queue cleared.");
    }

    /// <summary>
    /// Xóa toàn bộ hàng chờ (gọi khi bắt đầu ngày mới).
    /// </summary>
    public void ClearQueue()
    {
        if (_queue.Count > 0)
        {
            Debug.Log($"[CashierQueue] Clearing queue of {_queue.Count} customers.");
        }
        _queue.Clear();
    }

    public bool HasCashier => _hasCashier;
    public int  QueueSize  => _queue.Count;

    // =========================================================================
    // DATA CLASS
    // =========================================================================

    private class QueueEntry
    {
        public CustomerFSM Customer;
        public string      ItemId;
        public float       PaidPrice;
        public int         SlotIndex;
    }
}
