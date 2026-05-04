// Assets/Scripts/Data/PackData.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject định nghĩa một gói Booster Pack.
/// Chứa bảng Drop Table xác định TỶ LỆ RƠI chính xác cho từng slot.
///
/// KIẾN TRÚC DROP TABLE:
/// Mỗi pack có N slot (mặc định 6). Mỗi slot có bảng weight riêng.
/// Slot 1-4: Common/Uncommon guaranteed.
/// Slot 5:   Mix Common-Rare.
/// Slot 6:   Rare guaranteed slot (Ultra/Secret có thể xuất hiện).
///
/// CÁCH TẠO ASSET:
///   Right-click trong Project > Create > TCGShop > Data > Pack Data
/// </summary>
[CreateAssetMenu(
    fileName = "Pack_NewPack_001",
    menuName = "TCGShop/Data/Pack Data",
    order = 2
)]
public class PackData : ScriptableObject
{
    // =========================================================================
    // IDENTITY
    // =========================================================================

    [Header("Identity")]
    [Tooltip("ID duy nhất. Format: 'pack_{setId}'. Vd: 'pack_sv01'. " +
             "Phải khớp với id trong shopItems của hệ thống cũ để migration.")]
    public string packId;

    [Tooltip("Tên hiển thị. Vd: 'Scarlet & Violet Booster Pack'.")]
    public string packName;

    [Tooltip("ID của set mà pack này thuộc về. Vd: 'sv01'. " +
             "Dùng để tìm cards trong CardDatabase.")]
    public string sourceSetId;

    [Tooltip("Tên generation. Vd: 'GENERATION IX'. " +
             "Tương đương generation field trong StockItemInfo cũ.")]
    public string generationName;

    // =========================================================================
    // VISUAL
    // =========================================================================

    [Header("Visual")]
    [Tooltip("Sprite ảnh mặt trước của vỏ pack.")]
    public Sprite packFrontSprite;

    [Tooltip("Sprite ảnh mặt sau của vỏ pack.")]
    public Sprite packBackSprite;

    // =========================================================================
    // ECONOMY
    // =========================================================================

    [Header("Economy")]
    [Tooltip("Giá mua pack từ nhà cung cấp (USD). " +
             "Tương đương buyPrice trong StockItemInfo cũ.")]
    [Min(0.01f)]
    public float buyCost = 4.99f;

    [Tooltip("Giá bán mặc định cho khách (USD). Player có thể điều chỉnh. " +
             "Thường = buyCost × 1.6 (markup 60%).")]
    [Min(0.01f)]
    public float defaultSellPrice = 7.99f;

    [Tooltip("Level shop tối thiểu để mở khóa pack này. " +
             "Tương đương requiredLevel trong StockItemInfo cũ.")]
    [Range(1, 80)]
    public int requiredShopLevel = 1;

    // =========================================================================
    // CARD POOL
    // =========================================================================

    [Header("Card Pool")]
    [Tooltip("Danh sách tất cả thẻ có thể rơi ra từ pack này. " +
             "Kéo thả CardData assets vào đây.")]
    public List<CardData> availableCards = new List<CardData>();

    // =========================================================================
    // DROP TABLE
    // =========================================================================

    [Header("Drop Table (NEW — Replaces ORDER BY RANDOM())")]
    [Tooltip("Số lượng thẻ rơi ra mỗi lần mở pack. Mặc định 6 (giống hệ thống cũ).")]
    [Range(1, 10)]
    public int cardsPerPack = 6;

    [Tooltip("Bảng Drop Table: mỗi phần tử = một slot trong pack. " +
             "Thứ tự slot quan trọng: slot đầu tiên = thẻ đầu tiên lật ra. " +
             "Slot cuối cùng = thẻ hiếm nhất (giống hệ thống cũ để thẻ hiếm ở cuối).")]
    public List<DropTableSlot> dropTable = new List<DropTableSlot>();

    // =========================================================================
    // COMPUTED PROPERTIES
    // =========================================================================

    /// <summary>
    /// Profit margin khi bán ở giá mặc định.
    /// Tương đương công thức profit = sellPrice - buyPrice trong SetPriceModal.vue cũ.
    /// </summary>
    public float DefaultProfitMargin => defaultSellPrice - buyCost;

    /// <summary>
    /// Profit margin theo phần trăm.
    /// Tương đương profitPct = (sellPrice/buyPrice - 1) × 100 trong SetPriceModal.vue cũ.
    /// </summary>
    public float DefaultProfitPercent =>
        buyCost > 0 ? (defaultSellPrice / buyCost - 1f) * 100f : 0f;

    /// <summary>
    /// Kiểm tra Pack có đủ dữ liệu để mở không.
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(packId)) return false;
        if (availableCards == null || availableCards.Count == 0) return false;
        if (dropTable == null || dropTable.Count == 0) return false;
        return true;
    }

    /// <summary>
    /// Tìm tất cả cards trong pool có rarity khớp với một RarityDefinition.
    /// Dùng bởi GachaEngine để lấy pool thẻ cho từng slot.
    /// </summary>
    public List<CardData> GetCardsByRarity(RarityDefinition targetRarity)
    {
        var result = new List<CardData>();
        if (targetRarity == null) return result;

        foreach (var card in availableCards)
        {
            if (card != null && card.rarity == targetRarity)
            {
                result.Add(card);
            }
        }
        return result;
    }

    public override string ToString() =>
        $"Pack[{packId}|{packName}|{cardsPerPack} cards|${buyCost:F2}→${defaultSellPrice:F2}]";
}

// =============================================================================
// DROP TABLE DATA STRUCTURES
// =============================================================================

/// <summary>
/// Một slot trong Drop Table của Pack.
/// Slot = một vị trí thẻ trong pack (pack 6 thẻ → 6 slots).
/// Mỗi slot có bảng weight riêng xác định xác suất ra từng bậc hiếm.
/// </summary>
[System.Serializable]
public class DropTableSlot
{
    [Tooltip("Tên mô tả slot này, dùng cho Inspector. Vd: 'Slot 1-4 (Common)', 'Slot 6 (Rare Guaranteed)'.")]
    public string slotLabel = "Slot";

    [Tooltip("Danh sách các rarity có thể xuất hiện ở slot này, kèm trọng số (weight). " +
             "Tổng weight KHÔNG cần bằng 100 — GachaEngine tự normalize. " +
             "Ví dụ: Common=70, Uncommon=25, Rare=5 → tổng=100, hoặc Common=7, Uncommon=2.5, Rare=0.5 → kết quả như nhau.")]
    public List<RarityWeight> rarityWeights = new List<RarityWeight>();
}

/// <summary>
/// Cặp (RarityDefinition, Weight) cho một entry trong Drop Table.
/// </summary>
[System.Serializable]
public class RarityWeight
{
    [Tooltip("Bậc hiếm này.")]
    public RarityDefinition rarity;

    [Tooltip("Trọng số tương đối. Cao hơn = xuất hiện nhiều hơn. " +
             "Ví dụ: Common=70, Uncommon=25, Rare=5 → Common xuất hiện 70% thời gian.")]
    [Min(0f)]
    public float weight = 1f;
}
