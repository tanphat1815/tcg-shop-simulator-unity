// Assets/Scripts/Persistence/GameDataManager.cs
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton điều phối save/load operations.
///
/// RESPONSIBILITIES:
///   1. Quản lý rehydration sequence (thứ tự khôi phục chính xác)
///   2. Fire GameEconomyEvents.OnGameDataLoaded sau khi load xong
///   3. Auto-save triggers (OnApplicationPause, periodic, etc.)
///
/// REHYDRATION SEQUENCE (thứ tự BẮT BUỘC):
///   Step 1: SetMoney()          — ShopFloorManager
///   Step 2: LoadPackInventory() — InventoryManager
///   Step 3: RestoreFurniture()  — GridSystem (batch restore)
///   Step 4: RestoreShelfStock()  — ShelfInstance (per shelf)
///   Step 5: Fire OnGameDataLoaded — GameEconomyEvents
/// </summary>
public class GameDataManager : MonoBehaviour
{
    // ========================================================================
    // SINGLETON
    // ========================================================================
    public static GameDataManager Instance { get; private set; }

    // ========================================================================
    // STATE
    // ========================================================================
    /// <summary>
    /// Dữ liệu đã load gần nhất. Null nếu chưa load.
    /// </summary>
    public GameData CurrentData { get; private set; }

    /// <summary>
    /// Đang trong quá trình rehydration không.
    /// Dùng để ngăn save trong lúc load.
    /// </summary>
    public bool IsRehydrating { get; private set; }

    /// <summary>
    /// Đã load thành công chưa.
    /// </summary>
    public bool HasLoaded => CurrentData != null;

    [Header("Auto-Save Settings")]
    [Tooltip("Tự động save khi OnApplicationPause (mobile backgrounding).")]
    [SerializeField] private bool autoSaveOnPause = true;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    // ========================================================================
    // REHYDRATION STATE
    // ========================================================================
    private int _rehydrationStep = 0;
    private int _totalFurnitureToRestore = 0;
    private int _furnitureRestored = 0;
    private Action _onFurnitureRestoredCallback;

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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ========================================================================
    // PUBLIC API — LOAD
    // ========================================================================
    /// <summary>
    /// Load game data và bắt đầu rehydration sequence.
    /// Gọi từ GameManager.InitializeSystems() khi scene ready.
    /// </summary>
    public void LoadGame()
    {
        if (verboseLogging)
            Debug.Log("[GameDataManager] === LOAD START ===");

        // Step 0: Đọc file
        CurrentData = SaveSystem.Load();
        if (CurrentData == null)
        {
            if (verboseLogging)
                Debug.Log("[GameDataManager] No valid save found. Starting fresh game.");
            CurrentData = new GameData(); // Fresh game
            FireFreshGameStart();
            return;
        }

        // Step 1-5: Rehydration sequence
        StartCoroutine(RehydrationSequence(CurrentData));
    }

    // ========================================================================
    // REHYDRATION SEQUENCE (Coroutine — BẮT BUỘC)
    // ========================================================================
    /// <summary>
    /// Coroutine thực thi rehydration sequence.
    ///
    /// SEQUENCE CHÍNH XÁC:
    ///   Step 1: SetMoney()          — ShopFloorManager
    ///   Step 2: LoadPackInventory() — InventoryManager
    ///   Step 3: RestoreFurniture()  — GridSystem (batch, Instantiate prefabs)
    ///   Step 4: RestoreShelfStock()  — ShelfInstance (mỗi kệ sau khi Instantiate xong)
    ///   Step 5: Fire OnGameDataLoaded — GameEconomyEvents
    /// </summary>
    private IEnumerator RehydrationSequence(GameData data)
    {
        IsRehydrating = true;
        _rehydrationStep = 0;
        _furnitureRestored = 0;
        _totalFurnitureToRestore = data.placedFurniture != null
            ? data.placedFurniture.Count : 0;

        if (verboseLogging)
            Debug.Log($"[GameDataManager] Rehydration: {data.placedFurniture?.Count ?? 0} furniture, " +
                     $"${data.playerMoney:F0} money, {data.packInventory?.Count ?? 0} pack types.");

        // ─── STEP 1: Set Money ────────────────────────────────────────────
        _rehydrationStep = 1;
        if (verboseLogging) Debug.Log("[GameDataManager] Rehydration Step 1/5: Set Money");
        yield return null; // 1 frame để systems ready
        if (ShopFloorManager.Instance != null)
        {
            ShopFloorManager.Instance.SetMoney(data.playerMoney);
            GameEconomyEvents.FireMoneyChanged(0f, data.playerMoney);
        }

        // ─── STEP 2: Load Inventory ────────────────────────────────────────
        _rehydrationStep = 2;
        if (verboseLogging) Debug.Log("[GameDataManager] Rehydration Step 2/5: Load Inventory");
        yield return null;
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.LoadFromGameData(data);
        }

        // ─── STEP 3: Restore Furniture (batch) ─────────────────────────────
        _rehydrationStep = 3;
        if (verboseLogging) Debug.Log("[GameDataManager] Rehydration Step 3/5: Restore Furniture");
        yield return null;

        if (GridSystem.Instance != null)
        {
            _onFurnitureRestoredCallback = OnFurnitureRestored;
            GridSystem.Instance.RestoreAllFurniture(data, _onFurnitureRestoredCallback);

            // Chờ cho đến khi tất cả furniture được restore
            while (_furnitureRestored < _totalFurnitureToRestore)
            {
                yield return null;
            }
        }

        // ─── STEP 4: Restore Shelf Stock ─────────────────────────────────
        _rehydrationStep = 4;
        if (verboseLogging) Debug.Log("[GameDataManager] Rehydration Step 4/5: Restore Shelf Stock");
        yield return null;

        ShelfInstance[] allShelves = UnityEngine.Object.FindObjectsByType<ShelfInstance>(FindObjectsSortMode.None);
        foreach (var shelf in allShelves)
        {
            shelf.RestoreStockFromGameData(data);
        }

        // ─── STEP 5: Fire GameDataLoaded ────────────────────────────────
        _rehydrationStep = 5;
        if (verboseLogging) Debug.Log("[GameDataManager] Rehydration Step 5/5: Fire OnGameDataLoaded");
        yield return null;
        GameEconomyEvents.FireGameDataLoaded(data);

        IsRehydrating = false;
        _onFurnitureRestoredCallback = null;

        if (verboseLogging)
        {
            Debug.Log($"[GameDataManager] === REHYDRATION COMPLETE === " +
                     $"({_furnitureRestored} furniture, ${data.playerMoney:F0} money)");
        }
    }

    private void OnFurnitureRestored()
    {
        _furnitureRestored++;
        if (verboseLogging && _furnitureRestored % 5 == 0)
            Debug.Log($"[GameDataManager] Furniture restored: {_furnitureRestored}/{_totalFurnitureToRestore}");
    }

    // ========================================================================
    // PUBLIC API — SAVE
    // ========================================================================
    /// <summary>
    /// Save game data ngay lập tức.
    /// </summary>
    public void SaveGame()
    {
        if (IsRehydrating)
        {
            Debug.LogWarning("[GameDataManager] Cannot save while rehydrating.");
            return;
        }

        if (verboseLogging)
            Debug.Log("[GameDataManager] === SAVE START ===");

        GameData data = GameData.CreateFromCurrentState();
        bool success = SaveSystem.Save(data);
        if (success)
        {
            CurrentData = data;
        }
    }

    /// <summary>
    /// Reset game về trạng thái ban đầu (xóa save).
    /// </summary>
    public void ResetGame()
    {
        SaveSystem.DeleteSave();
        CurrentData = new GameData();
        FireFreshGameStart();
        Debug.Log("[GameDataManager] Game reset to fresh state.");
    }

    // ========================================================================
    // AUTO-SAVE TRIGGERS
    // ========================================================================
    /// <summary>
    /// Auto-save khi app bị pause (mobile background, alt-tab).
    /// </summary>
    public void OnApplicationPaused(bool isPaused)
    {
        if (!isPaused) return;
        if (!autoSaveOnPause) return;
        if (verboseLogging)
            Debug.Log("[GameDataManager] Auto-saving on pause...");
        SaveGame();
    }

    /// <summary>
    /// Auto-save khi app quit.
    /// </summary>
    public void OnApplicationQuit()
    {
        if (verboseLogging)
            Debug.Log("[GameDataManager] Auto-saving on quit...");
        SaveGame();
    }

    // ========================================================================
    // EVENT HANDLERS
    // ========================================================================
    private void FireFreshGameStart()
    {
        // Start fresh game — setup default state
        if (ShopFloorManager.Instance != null)
            ShopFloorManager.Instance.SetMoney(500f);
        GameEconomyEvents.FireGameDataLoaded(new GameData
        {
            playerMoney = 500f,
            currentLevel = 1
        });
    }
}
