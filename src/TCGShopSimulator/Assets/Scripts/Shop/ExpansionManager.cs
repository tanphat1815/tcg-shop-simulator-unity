// Assets/Scripts/Shop/ExpansionManager.cs

using System;
using UnityEngine;

/// <summary>
/// Singleton quản lý việc mua shop expansion.
///
/// RESPONSIBILITIES:
///   1. Kiểm tra điều kiện mua (level, money)
///   2. Thực hiện expansion: trừ tiền, tăng expansionLevel
///   3. Gọi GridSystem.Expand() → PathfindingGrid.RebuildGrid()
///   4. Gọi CameraController.UpdateShopBounds()
///   5. Cập nhật GameData.expansionLevel
/// </summary>
public class ExpansionManager : MonoBehaviour
{
    // =========================================================================
    // SINGLETON
    // =========================================================================

    public static ExpansionManager Instance { get; private set; }

    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Config")]
    [Tooltip("ExpansionConfig ScriptableObject. Tạo asset: Create > TCGShop > Data > Expansion Config")]
    [SerializeField] private ExpansionConfig _expansionConfig;

    [Header("Current State")]
    [SerializeField] private int _currentExpansionLevel = 0;

    // =========================================================================
    // EVENTS
    // =========================================================================

    public event Action<int, int> OnExpansionLevelBought;
    public event Action<int> OnExpansionLevelChanged;

    // =========================================================================
    // PROPERTIES
    // =========================================================================

    public int CurrentLevel => _currentExpansionLevel;

    public ExpansionConfig.ExpansionLevel NextLevel =>
        _expansionConfig?.GetNextExpansion(_currentExpansionLevel);

    public bool CanBuyNextExpansion
    {
        get
        {
            var next = NextLevel;
            if (next == null) return false;

            int playerLevel = GameManager.Instance?.CurrentLevel ?? 1;
            float playerMoney = ShopFloorManager.Instance?.TotalMoney ?? 0f;

            return playerLevel >= next.requiredShopLevel && playerMoney >= next.cost;
        }
    }

    public bool IsMaxLevel =>
        _currentExpansionLevel >= (_expansionConfig?.MaxLevel ?? 0);

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

        if (_expansionConfig == null)
        {
            Debug.LogWarning("[ExpansionManager] No ExpansionConfig assigned. Creating default.");
            _expansionConfig = ScriptableObject.CreateInstance<ExpansionConfig>();
            _expansionConfig.CreateDefaultConfig();
        }
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>Thử mua expansion tiếp theo. Trả về true nếu thành công.</summary>
    public bool TryBuyExpansion()
    {
        if (IsMaxLevel)
        {
            Debug.LogWarning("[ExpansionManager] Already at max expansion level.");
            return false;
        }

        var next = NextLevel;
        if (next == null)
        {
            Debug.LogError("[ExpansionManager] Next expansion config is null!");
            return false;
        }

        int playerLevel = GameManager.Instance?.CurrentLevel ?? 1;
        float playerMoney = ShopFloorManager.Instance?.TotalMoney ?? 0f;

        if (playerLevel < next.requiredShopLevel)
        {
            Debug.LogWarning($"[ExpansionManager] Need level {next.requiredShopLevel} to buy expansion. Current: {playerLevel}");
            return false;
        }

        if (playerMoney < next.cost)
        {
            Debug.LogWarning($"[ExpansionManager] Need ${next.cost:F2} to buy expansion. Current: ${playerMoney:F2}");
            return false;
        }

        BuyExpansion(next);
        return true;
    }

    /// <summary>Đặt expansion level (dùng cho rehydration từ save).</summary>
    public void SetExpansionLevel(int level)
    {
        _currentExpansionLevel = Mathf.Clamp(level, 0, _expansionConfig?.MaxLevel ?? 0);
        OnExpansionLevelChanged?.Invoke(_currentExpansionLevel);
    }

    // =========================================================================
    // EXPANSION LOGIC
    // =========================================================================

    private void BuyExpansion(ExpansionConfig.ExpansionLevel config)
    {
        int oldLevel = _currentExpansionLevel;
        float cost = config.cost;

        float prevMoney = ShopFloorManager.Instance?.TotalMoney ?? 0f;
        float newMoney = prevMoney - cost;

        if (ShopFloorManager.Instance != null)
        {
            ShopFloorManager.Instance.SetMoney(newMoney);
            GameEconomyEvents.FireMoneyChanged(prevMoney, newMoney);
        }

        _currentExpansionLevel++;

        Debug.Log($"[ExpansionManager] Bought expansion level {_currentExpansionLevel}. Cost: ${cost:F2}. Balance: ${newMoney:F2}");

        // === BƯỚC QUAN TRỌNG NHẤT: RESIZE GRID ===
        Vector2Int newMinCell = CalculateNewMinCell(config);
        Vector2Int newMaxCell = CalculateNewMaxCell(config);

        Debug.Log($"[ExpansionManager] Old bounds: {GridSystem.Instance?.ShopMinCell} → {GridSystem.Instance?.ShopMaxCell}, " +
                  $"New bounds: {newMinCell} → {newMaxCell}");

        if (GridSystem.Instance != null)
            GridSystem.Instance.ExpandShopBounds(newMinCell, newMaxCell);

        if (PathfindingGrid.Instance != null)
            PathfindingGrid.Instance.RebuildGrid(newMinCell, newMaxCell);

        if (Camera.main != null)
        {
            var cameraController = Camera.main.GetComponent<CameraController>();
            cameraController?.UpdateShopBounds(newMinCell, newMaxCell);
        }

        OnExpansionLevelBought?.Invoke(oldLevel, _currentExpansionLevel);
        OnExpansionLevelChanged?.Invoke(_currentExpansionLevel);

        GameEconomyEvents.FireExpansionBought(_currentExpansionLevel, cost);
    }

    private Vector2Int CalculateNewMinCell(ExpansionConfig.ExpansionLevel config)
    {
        if (GridSystem.Instance == null)
            return new Vector2Int(-10, -10);

        Vector2Int currentMin = GridSystem.Instance.ShopMinCell;
        int expandLeft = config.addedCellsX / 2;

        return new Vector2Int(
            currentMin.x - expandLeft,
            currentMin.y - config.addedCellsY
        );
    }

    private Vector2Int CalculateNewMaxCell(ExpansionConfig.ExpansionLevel config)
    {
        if (GridSystem.Instance == null)
            return new Vector2Int(10, 10);

        Vector2Int currentMax = GridSystem.Instance.ShopMaxCell;
        int expandLeft = config.addedCellsX / 2;
        int expandRight = config.addedCellsX - expandLeft;

        return new Vector2Int(
            currentMax.x + expandRight,
            currentMax.y + config.addedCellsY
        );
    }

    /// <summary>Tính tiền thuê hàng ngày dựa trên expansion level.</summary>
    public float CalculateDailyRent()
    {
        float baseRent = 50f;

        for (int i = 1; i <= _currentExpansionLevel; i++)
        {
            var level = _expansionConfig?.GetLevel(i);
            if (level != null)
                baseRent += level.rentIncrease;
        }

        return baseRent;
    }
}
