// Assets/Scripts/Shop/ShopTransactionProcessor.cs

using UnityEngine;

/// <summary>
/// Xử lý hậu kỳ giao dịch: kết nối CashierQueueManager → ShopFloorManager → UI.
///
/// RESPONSIBILITIES:
///   1. Subscribe vào CashierQueueManager.OnTransactionCompleted
///   2. Tạo TransactionReceipt
///   3. Fire GameEconomyEvents.OnTransactionCompleted
///   4. Trigger XP gain
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   serveCustomer() trong customerStore.ts:
///     money += entry.price
///     dailyStats.revenue += entry.price
///     gainExp(5)
/// </summary>
public class ShopTransactionProcessor : MonoBehaviour
{
    // ========================================================================
    // SINGLETON
    // ========================================================================

    public static ShopTransactionProcessor Instance { get; private set; }

    // ========================================================================
    // CONFIGURATION
    // ========================================================================

    [Header("XP Rewards")]
    [Tooltip("XP nhận được khi phục vụ một khách hàng. Tương đương SERVE_CUSTOMER=5 trong hệ cũ.")]
    [SerializeField] private int _xpPerTransaction = 5;

    [Tooltip("Bật verbose logging cho transaction debug.")]
    [SerializeField] private bool _verboseLogging = true;

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

    private void OnEnable()
    {
        if (ShopFloorManager.Instance?.CashierQueue != null)
            ShopFloorManager.Instance.CashierQueue.OnTransactionCompleted += HandleTransactionCompleted;
    }

    private void OnDisable()
    {
        if (ShopFloorManager.Instance?.CashierQueue != null)
            ShopFloorManager.Instance.CashierQueue.OnTransactionCompleted -= HandleTransactionCompleted;
    }

    private void Start()
    {
        Debug.Log("[ShopTransactionProcessor] Initialized. Subscribed to CashierQueue events.");
    }

    // ========================================================================
    // TRANSACTION HANDLER
    // ========================================================================

    private void HandleTransactionCompleted(float amount, string itemId)
    {
        if (_verboseLogging)
            Debug.Log($"[ShopTransactionProcessor] Transaction: +${amount:F2} for {itemId}");

        var receipt = new TransactionReceipt("npc", itemId, amount);

        GameEconomyEvents.FireTransactionCompleted(receipt);

        GainXp(_xpPerTransaction, itemId);
    }

    // ========================================================================
    // XP SYSTEM
    // ========================================================================

    private void GainXp(int amount, string source)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[ShopTransactionProcessor] GameManager.Instance is null — cannot gain XP.");
            return;
        }

        int currentXp = GameManager.Instance.CurrentExp;
        int level = GameManager.Instance.CurrentLevel;
        int newXp = currentXp + amount;
        int xpRequired = GameManager.Instance.XpToNextLevel;
        int newLevel = level;

        while (newXp >= xpRequired && xpRequired > 0)
        {
            newXp -= xpRequired;
            newLevel++;
            xpRequired = newLevel * 1000;
            GameEconomyEvents.FireLevelUp(newLevel);
        }

        GameManager.Instance.SetExp(newXp);
        GameEconomyEvents.FireXpGained(amount, newXp, newLevel);

        if (_verboseLogging)
            Debug.Log($"[ShopTransactionProcessor] +{amount} XP from {source}. " +
                      $"Exp: {newXp}/{xpRequired}, Level: {newLevel}");
    }
}
