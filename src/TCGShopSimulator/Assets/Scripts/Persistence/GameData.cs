// Assets/Scripts/Persistence/GameData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// POCO container chứa toàn bộ trạng thái game cần lưu.
/// Class thuần túy — KHÔNG kế thừa MonoBehaviour.
///
/// TUẦN TỰ HÓA:
///   GameData → JsonUtility.ToJson() → Base64 → File
///
/// CẤU TRÚC PAYLOAD BẮT BUỘC:
///   1. PlayerMoney (float)
///   2. Dictionary Inventory (packId → quantity)
///   3. List PlacedShelfData (shelf position, rotation, stock, price)
///
/// LƯU Ý QUAN TRỌNG:
///   - Dùng [Serializable] để Unity serialize được
///   - KHÔNG dùng UnityEngine.Object references (sẽ break khi load)
/// </summary>
[Serializable]
public class GameData
{
    // ========================================================================
    // HEADER — Metadata
    // ========================================================================
    [Header("Save Metadata")]
    public string saveVersion = "1.0.0";
    public long savedAtUnixTimestamp;
    public string savedAtUtc;

    // ========================================================================
    // ECONOMY — PlayerMoney
    // ========================================================================
    [Header("Economy State")]
    public float playerMoney = 500f;
    public float currentExp = 0f;
    public int currentLevel = 1;

    // ========================================================================
    // INVENTORY — Pack Inventory
    // ========================================================================
    [Header("Pack Inventory")]
    public List<PackInventoryEntry> packInventory = new List<PackInventoryEntry>();

    [Serializable]
    public class PackInventoryEntry
    {
        public string packId;
        public int quantity;

        public PackInventoryEntry() { }

        public PackInventoryEntry(string packId, int quantity)
        {
            this.packId = packId;
            this.quantity = quantity;
        }
    }

    // ========================================================================
    // CARD BINDER
    // ========================================================================
    [Header("Card Binder")]
    public List<CardBinderEntry> cardBinder = new List<CardBinderEntry>();

    [Serializable]
    public class CardBinderEntry
    {
        public string cardId;
        public int quantity;

        public CardBinderEntry() { }

        public CardBinderEntry(string cardId, int quantity)
        {
            this.cardId = cardId;
            this.quantity = quantity;
        }
    }

    // ========================================================================
    // PLACED FURNITURE — Shelf placement + runtime state
    // ========================================================================
    [Header("Placed Furniture")]
    public List<PlacedShelfData> placedFurniture = new List<PlacedShelfData>();

    /// <summary>
    /// Dữ liệu của một shelf/furniture đã đặt trong scene.
    /// BẮT BUỘC phải có:
    ///   - instanceId: Định danh duy nhất
    ///   - furnitureTypeName: Enum string để lookup FurnitureDefinition
    ///   - originCellX/Y: int (JsonUtility không serialize Vector2Int)
    ///   - rotation: int độ
    ///   - displayedItemId: packId đang trưng bày (empty = trống)
    ///   - stockCount: số lượng hàng trên kệ
    ///   - sellPrice: giá đang đặt
    /// </summary>
    [Serializable]
    public class PlacedShelfData
    {
        public string instanceId;
        public string furnitureTypeName;
        public int originCellX;
        public int originCellY;
        public int rotation;
        // Shelf-specific state
        public string displayedItemId;
        public int stockCount;
        public float sellPrice;
        public float marketPrice;

        /// <summary>
        /// Computed origin cell. Re-created at load time from originCellX/Y.
        /// </summary>
        public Vector2Int OriginCell => new Vector2Int(originCellX, originCellY);

        public PlacedShelfData() { }

        public static PlacedShelfData FromShelfInstance(
            string instanceId,
            string furnitureTypeName,
            Vector2Int originCell,
            int rotation,
            string itemId,
            int stock,
            float price,
            float market)
        {
            return new PlacedShelfData
            {
                instanceId = instanceId,
                furnitureTypeName = furnitureTypeName,
                originCellX = originCell.x,
                originCellY = originCell.y,
                rotation = rotation,
                displayedItemId = itemId ?? string.Empty,
                stockCount = stock,
                sellPrice = price,
                marketPrice = market
            };
        }
    }

    // ========================================================================
    // SHOP BOUNDS
    // ========================================================================
    [Header("Shop Bounds")]
    public int shopMinCellX;
    public int shopMinCellY;
    public int shopMaxCellX;
    public int shopMaxCellY;

    // ========================================================================
    // TIME / PROGRESSION
    // ========================================================================
    [Header("Time State")]
    public int timeInMinutes = 480; // 8:00 AM (minutes from midnight)

    [Header("Progression")]
    public int currentDay = 1;
    public int expansionLevel = 0;

    // ========================================================================
    // STATS
    // ========================================================================
    [Header("Daily Stats")]
    public float dailyRevenue = 0f;
    public int customersServedToday = 0;
    public int itemsSoldToday = 0;

    // ========================================================================
    // FACTORY — Create from current runtime state
    // ========================================================================
    /// <summary>
    /// Tạo GameData từ trạng thái hiện tại của tất cả hệ thống.
    /// </summary>
    public static GameData CreateFromCurrentState()
    {
        var data = new GameData
        {
            savedAtUnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            savedAtUtc = DateTime.UtcNow.ToString("O")
        };

        // Economy
        if (ShopFloorManager.Instance != null)
        {
            data.playerMoney = ShopFloorManager.Instance.TotalMoney;
            data.dailyRevenue = ShopFloorManager.Instance.DailyRevenue;
            data.customersServedToday = 0;
            data.itemsSoldToday = 0;
        }

        if (GameManager.Instance != null)
        {
            data.currentLevel = GameManager.Instance.CurrentLevel;
            data.currentExp = GameManager.Instance.CurrentExp;
        }

        // Time
        if (TimeManager.Instance != null)
        {
            data.timeInMinutes = TimeManager.Instance.TimeInMinutes;
            data.currentDay = TimeManager.Instance.CurrentDay;
        }

        // Inventory
        if (InventoryManager.Instance != null)
        {
            var inventory = InventoryManager.Instance.GetFullPackInventory();
            data.packInventory.Clear();
            foreach (var kvp in inventory)
            {
                if (kvp.Value > 0)
                    data.packInventory.Add(new PackInventoryEntry(kvp.Key, kvp.Value));
            }

            var binder = InventoryManager.Instance.GetFullBinder();
            data.cardBinder.Clear();
            foreach (var kvp in binder)
            {
                if (kvp.Value > 0)
                    data.cardBinder.Add(new CardBinderEntry(kvp.Key, kvp.Value));
            }
        }

        // Furniture — lấy từ GridSystem
        if (GridSystem.Instance != null)
        {
            var furnitureData = GridSystem.Instance.GetAllPlacedFurnitureData();
            data.placedFurniture = furnitureData;
            data.shopMinCellX = GridSystem.Instance.ShopMinCell.x;
            data.shopMinCellY = GridSystem.Instance.ShopMinCell.y;
            data.shopMaxCellX = GridSystem.Instance.ShopMaxCell.x;
            data.shopMaxCellY = GridSystem.Instance.ShopMaxCell.y;
        }

        // Play Tables
        if (PlayTableManager.Instance != null)
        {
            data.placedTables.Clear();
            var tables = UnityEngine.Object.FindObjectsByType<PlayTableInstance>(FindObjectsSortMode.None);
            foreach (var table in tables)
            {
                var tableData = table.GetSerializableData();
                data.placedTables.Add(new PlacedTableDataEntry
                {
                    instanceId = tableData.instanceId,
                    furnitureTypeName = FurnitureType.PlayTable.ToString(),
                    originCellX = 0,
                    originCellY = 0,
                    rotation = 0,
                    seatCount = tableData.seatCount,
                    occupantIds = tableData.occupantIds,
                    isMatchActive = tableData.isMatchActive,
                    matchStartedAt = tableData.matchStartedAt,
                    matchDuration = tableData.matchDuration
                });
            }
        }

        return data;
    }

    // ========================================================================
    // VALIDATION
    // ========================================================================
    /// <summary>
    /// Kiểm tra dữ liệu có hợp lệ không sau khi deserialize.
    /// </summary>
    public bool IsValid()
    {
        if (playerMoney < 0f) return false;
        if (currentLevel < 1) return false;
        if (savedAtUnixTimestamp <= 0) return false;
        return true;
    }

    /// <summary>
    /// Chuỗi mô tả ngắn gọn cho logging.
    /// </summary>
    public override string ToString() =>
        $"GameData[version={saveVersion}|money=${playerMoney:F0}|" +
        $"packs={packInventory.Count}|furniture={placedFurniture.Count}|tables={placedTables.Count}|saved={savedAtUtc}]";

    // ========================================================================
    // PLAY TABLES
    // ========================================================================

    [Header("Placed Tables")]
    public List<PlacedTableDataEntry> placedTables = new List<PlacedTableDataEntry>();

    [Serializable]
    public class PlacedTableDataEntry
    {
        public string instanceId;
        public string furnitureTypeName;
        public int originCellX;
        public int originCellY;
        public int rotation;
        public int seatCount;
        public List<string> occupantIds;
        public bool isMatchActive;
        public float matchStartedAt;
        public float matchDuration;
    }
}
