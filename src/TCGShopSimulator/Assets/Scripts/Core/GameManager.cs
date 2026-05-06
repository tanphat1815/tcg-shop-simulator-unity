// Assets/Scripts/Core/GameManager.cs

using UnityEngine;

/// <summary>
/// Singleton trung tâm điều phối toàn bộ hệ thống game.
/// Tự khởi tạo trước mọi Scene, tồn tại xuyên suốt vòng đời ứng dụng.
/// 
/// NGUYÊN TẮC THIẾT KẾ:
/// - Không bao giờ dùng GameObject.Find() trong Update().
/// - Các hệ thống con tự đăng ký với GameManager thay vì GameManager đi tìm chúng.
/// - Mọi tham chiếu được cache tại Awake/Start, không tìm kiếm động lúc runtime.
/// </summary>
public class GameManager : MonoBehaviour
{
    // =========================================================================
    // SINGLETON PATTERN
    // =========================================================================

    /// <summary>
    /// Instance duy nhất của GameManager. Truy cập từ bất kỳ đâu qua GameManager.Instance.
    /// </summary>
    public static GameManager Instance { get; private set; }

    /// <summary>
    /// Trạng thái khởi tạo. Các hệ thống con kiểm tra flag này trước khi sử dụng GameManager.
    /// </summary>
    public bool IsReady { get; private set; } = false;

    // =========================================================================
    // THAM CHIẾU CÁC HỆ THỐNG CON (Sẽ được mở rộng ở các bước sau)
    // =========================================================================
    // Ví dụ cấu trúc cho tương lai:
    // public InventorySystem Inventory { get; private set; }
    // public EconomySystem Economy { get; private set; }
    // public CustomerAISystem CustomerAI { get; private set; }

    // =========================================================================
    // KHỞI TẠO
    // =========================================================================

    /// <summary>
    /// RuntimeInitializeOnLoadMethod đảm bảo GameManager tồn tại TRƯỚC KHI
    /// bất kỳ Scene nào được load, kể cả Scene đầu tiên.
    /// SubsystemRegistration là phase sớm nhất có thể.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInstance()
    {
        if (Instance != null) return;

        var go = new GameObject("[GameManager]");
        go.AddComponent<GameManager>();
        DontDestroyOnLoad(go);
    }

    // ========================================================================
    // XP & LEVEL STATE
    // ========================================================================

    [Header("Player Progression")]
    [Tooltip("Current player level.")]
    [SerializeField] private int _currentLevel = 1;

    [Tooltip("Current experience points (XP).")]
    [SerializeField] private int _currentExp = 0;

    [Tooltip("Total packs opened for stats.")]
    [SerializeField] private int _totalPacksOpened = 0;

    public int CurrentLevel => _currentLevel;
    public int CurrentExp => _currentExp;
    public int TotalPacksOpened => _totalPacksOpened;

    /// <summary>
    /// XP required to reach the next level.
    /// Formula: level * 1000
    /// </summary>
    public int XpToNextLevel => _currentLevel * 1000;

    // ========================================================================
    // XP & LEVEL API
    // ========================================================================

    /// <summary>
    /// Set experience points. Called by ShopTransactionProcessor after XP gain.
    /// </summary>
    public void SetExp(int newExp)
    {
        _currentExp = Mathf.Max(0, newExp);
    }

    /// <summary>
    /// Add experience points and handle level up.
    /// </summary>
    public void AddExp(int amount)
    {
        int newExp = _currentExp + amount;
        int level = _currentLevel;
        int xpRequired = XpToNextLevel;

        while (newExp >= xpRequired && xpRequired > 0)
        {
            newExp -= xpRequired;
            level++;
            xpRequired = level * 1000;
            GameEconomyEvents.FireLevelUp(level);
        }

        _currentExp = newExp;
        _currentLevel = level;

        Debug.Log($"[GameManager] +{amount} XP. Exp: {_currentExp}/{XpToNextLevel}, Level: {_currentLevel}");
    }

    /// <summary>
    /// Increment pack opened counter.
    /// </summary>
    public void IncrementPacksOpened()
    {
        _totalPacksOpened++;
    }

    private void Awake()
    {
        // --- Kiểm tra duplicate (trường hợp Scene có sẵn GameManager trong hierarchy) ---
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameManager] Phát hiện instance thứ hai. Đang hủy instance thừa.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // --- Khởi tạo các hệ thống con ---
        InitializeSystems();
    }

    /// <summary>
    /// Khởi tạo tất cả hệ thống con theo thứ tự dependency.
    /// Thứ tự quan trọng: hệ thống A không phụ thuộc hệ thống B phải khởi tạo trước B.
    /// </summary>
    private void InitializeSystems()
    {
        // Bước 1: Bước này chưa có hệ thống con, chỉ đánh dấu ready
        // Các bước sau sẽ thêm: Economy, Inventory, CustomerAI, v.v.

        IsReady = true;

        // Log chính xác theo yêu cầu — KHÔNG được thay đổi chuỗi này
        // vì kịch bản test tìm kiếm chuỗi "[GameManager] Ready." trong Console
        Debug.Log("[GameManager] Ready.");
    }

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void OnApplicationQuit()
    {
        IsReady = false;
        Debug.Log("[GameManager] Application quitting. Shutting down systems.");
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            Debug.Log("[GameManager] Application paused. Saving state...");
            // SaveSystem.Save(); // Sẽ implement ở bước sau
        }
    }

    // =========================================================================
    // TIỆN ÍCH
    // =========================================================================

    /// <summary>
    /// Kiểm tra GameManager có sẵn sàng không trước khi gọi hệ thống con.
    /// Dùng trong mọi hệ thống con: if (!GameManager.Instance.IsReady) return;
    /// </summary>
    public static bool IsAvailable => Instance != null && Instance.IsReady;
}
