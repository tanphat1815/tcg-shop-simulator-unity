// Assets/Scripts/Data/RarityDefinition.cs

using UnityEngine;

/// <summary>
/// ScriptableObject định nghĩa một bậc hiếm (Rarity Tier) trong game.
/// 
/// CÁCH TẠO ASSET:
///   Right-click trong Project > Create > TCGShop > Data > Rarity Definition
///
/// MAPPING TỪ HỆ THỐNG CŨ (Vue/Phaser rarityRegistry.ts):
///   'Common'                    → sortingRank = 0
///   'Uncommon'                  → sortingRank = 1
///   'Rare' / 'Rare Holo'        → sortingRank = 3
///   'Double Rare'               → sortingRank = 4
///   'Ultra Rare'                → sortingRank = 5
///   'Illustration Rare'         → sortingRank = 7
///   'Special Illustration Rare' → sortingRank = 8
///   'Secret Rare'               → sortingRank = 6
///   'Ghost Rare'                → sortingRank = 10
/// </summary>
[CreateAssetMenu(
    fileName = "Rarity_New",
    menuName = "TCGShop/Data/Rarity Definition",
    order = 0
)]
public class RarityDefinition : ScriptableObject
{
    // =========================================================================
    // THÔNG TIN CƠ BẢN
    // =========================================================================

    [Header("Identity")]
    [Tooltip("Tên hiển thị trong UI. Khớp với chuỗi rarity trong database cũ.")]
    public string displayName = "Common";

    [Tooltip("Màu sắc đại diện cho bậc hiếm này trong UI.")]
    public Color rarityColor = Color.white;

    [Tooltip("Icon đại diện (tùy chọn).")]
    public Sprite rarityIcon;

    // =========================================================================
    // THÔNG SỐ SẮP XẾP
    // =========================================================================

    [Header("Sorting")]
    [Tooltip("Thứ hạng để sort thẻ theo độ hiếm. Cao hơn = hiếm hơn. " +
             "Khớp với rarityRank trong inventoryStore.ts cũ.")]
    [Range(0, 10)]
    public int sortingRank = 0;

    [Tooltip("Nếu true, thẻ này được coi là High Rarity và có hiệu ứng holo. " +
             "Khớp với isHighRarity() trong rarityRegistry.ts cũ.")]
    public bool isHighRarity = false;

    // =========================================================================
    // THÔNG SỐ GACHA
    // =========================================================================

    [Header("XP Reward")]
    [Tooltip("XP người chơi nhận được khi mở được thẻ bậc này. " +
             "High rarity → 15 XP, Common/Uncommon → 2 XP (từ inventoryStore.ts cũ).")]
    public int xpReward = 2;

    // =========================================================================
    // TIỆN ÍCH
    // =========================================================================

    /// <summary>
    /// Kiểm tra nhanh xem đây có phải bậc hiếm cao không.
    /// Tương đương isHighRarity() trong rarityRegistry.ts cũ (rank >= 3).
    /// </summary>
    public bool IsHighRarity => sortingRank >= 3;

    /// <summary>
    /// Chuỗi hiển thị đầy đủ dùng cho logging và debug.
    /// </summary>
    public override string ToString() => $"Rarity[{displayName}, Rank={sortingRank}]";
}
