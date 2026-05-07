// Assets/Scripts/Shop/ShopFloorManager.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton điều phối toàn bộ hoạt động của tầng cửa hàng.
///
/// "RADAR" API:
///   NPC gọi ShopFloorManager.Instance.GetNearestAvailableShelf(npcPosition)
///   để tìm kệ gần nhất có hàng mà NPC chưa từng kiểm tra trong lần visit này.
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ (handleWandering trong NPCManager.ts):
///   Scan placedShelves WHERE shelf.id NOT IN checkedShelfIds AND has stock
///
/// QUẢN LÝ TIỀN:
///   CashierQueueManager.OnTransactionCompleted → ShopFloorManager.AddRevenue()
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

    private readonly Dictionary<ShelfInstance, Vector3> _registeredShelves =
        new Dictionary<ShelfInstance, Vector3>();

    private readonly HashSet<ShelfInstance> _shelvesWithStock =
        new HashSet<ShelfInstance>();

    // =========================================================================
    // ECONOMY STATE
    // =========================================================================

    [Header("Economy (Debug View)")]
    [SerializeField] private float _totalMoney         = 500f;
    [SerializeField] private float _dailyRevenue      = 0f;
    [SerializeField] private int   _customersServedToday = 0;
    [SerializeField] private int   _itemsSoldToday    = 0;

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

        if (ShopTransactionProcessor.Instance == null)
        {
            var processor = gameObject.AddComponent<ShopTransactionProcessor>();
            Debug.Log("[ShopFloorManager] Auto-added ShopTransactionProcessor.");
        }

        Debug.Log("[ShopFloorManager] Initialized.");
    }

    private void OnDestroy()
    {
        if (_cashierQueue != null)
            _cashierQueue.OnTransactionCompleted -= HandleTransactionCompleted;
    }

    // =========================================================================
    // SHELF REGISTRATION
    // =========================================================================

    public void RegisterShelf(ShelfInstance shelf)
    {
        if (shelf == null || _registeredShelves.ContainsKey(shelf)) return;

        _registeredShelves[shelf] = shelf.WorldPosition;

        if (shelf.HasStock)
            _shelvesWithStock.Add(shelf);

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
    // RADAR API — Gọi từ CustomerFSM.HandleWander()
    // =========================================================================

    /// <summary>
    /// Tìm kệ gần nhất (chim bay) có hàng và NPC chưa kiểm tra.
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ: scan placedShelves WHERE NOT IN checkedShelfIds AND has stock
    /// </summary>
    public ShelfInstance GetNearestAvailableShelf(
        Vector3 npcWorldPosition,
        HashSet<string> checkedShelfIds)
    {
        ShelfInstance nearest      = null;
        float         nearestDistSq = float.MaxValue;

        foreach (var shelf in _shelvesWithStock)
        {
            if (shelf == null || !shelf.HasStock) continue;
            if (checkedShelfIds.Contains(shelf.GetEntityId().ToString())) continue;

            float distSq = (shelf.WorldPosition - npcWorldPosition).sqrMagnitude;
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                nearest       = shelf;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Kiểm tra có kệ nào có hàng không.
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
        float prevMoney = _totalMoney;

        _totalMoney            += amount;
        _dailyRevenue          += amount;
        _customersServedToday  ++;
        _itemsSoldToday        ++;

        GameEconomyEvents.FireMoneyChanged(prevMoney, _totalMoney);

        Debug.Log($"[ShopFloorManager] Transaction: +${amount:F2}. " +
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

    public float TotalMoney   => _totalMoney;
    public float DailyRevenue => _dailyRevenue;

    /// <summary>
    /// Set money trực tiếp (dùng cho rehydration).
    /// KHÔNG fire event ở đây — GameDataManager fire sau khi tất cả rehydrated.
    /// </summary>
    public void SetMoney(float amount)
    {
        _totalMoney = amount;
    }

    /// <summary>Reset thống kê ngày mới. Gọi bởi TimeManager khi sang ngày mới.</summary>
    public void ResetDailyStats()
    {
        _dailyRevenue         = 0f;
        _customersServedToday = 0;
        _itemsSoldToday       = 0;
    }
}
