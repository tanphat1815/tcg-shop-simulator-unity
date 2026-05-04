// Assets/Scripts/Data/CardData.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject lưu trữ toàn bộ dữ liệu tĩnh của một lá bài TCG.
///
/// THIẾT KẾ:
/// - Dữ liệu TĨNH (không thay đổi trong gameplay): lưu ở đây.
/// - Dữ liệu ĐỘNG (số lượng trong kho, giá đang bán): lưu trong InventoryManager.
///
/// CÁCH TẠO ASSET:
///   Right-click trong Project > Create > TCGShop > Data > Card Data
///
/// CÁCH TẠO HÀNG LOẠT:
///   Dùng CardDataEditor (custom Inspector) hoặc
///   Dùng CardDataFactory (Editor script) để import từ CSV/JSON.
/// </summary>
[CreateAssetMenu(
    fileName = "Card_NewCard_000",
    menuName = "TCGShop/Data/Card Data",
    order = 1
)]
public class CardData : ScriptableObject
{
    // =========================================================================
    // IDENTITY
    // =========================================================================

    [Header("Identity")]
    [Tooltip("ID duy nhất của thẻ. Format: {setId}-{number}. Vd: 'sv01-001'. " +
             "Phải khớp với id trong database cũ để migration đúng.")]
    public string cardId;

    [Tooltip("Tên hiển thị của thẻ. Vd: 'Charizard ex'.")]
    public string cardName;

    [Tooltip("ID của set chứa thẻ này. Vd: 'sv01'. Dùng để group thẻ theo pack.")]
    public string setId;

    [Tooltip("ID của series. Vd: 'sv'. Dùng để tính requiredLevel của pack.")]
    public string seriesId;

    [Tooltip("Số thứ tự trong set. Vd: '001'. Dùng để sort và display.")]
    public string cardNumber;

    // =========================================================================
    // VISUAL
    // =========================================================================

    [Header("Visual")]
    [Tooltip("Sprite mặt trước của thẻ. Import texture với settings: Sprite (2D and UI), Max Size 512.")]
    public Sprite cardSprite;

    [Tooltip("Sprite mặt sau thẻ bài (dùng chung cho mọi thẻ cùng set).")]
    public Sprite cardBackSprite;

    // =========================================================================
    // RARITY
    // =========================================================================

    [Header("Rarity")]
    [Tooltip("Bậc hiếm của thẻ. Reference đến RarityDefinition ScriptableObject.")]
    public RarityDefinition rarity;

    // =========================================================================
    // BATTLE STATS
    // =========================================================================

    [Header("Battle Stats")]
    [Tooltip("HP của thẻ. Trong SQLite cũ lưu dạng string '120', đã parse thành int.")]
    [Range(10, 340)]
    public int baseHp = 60;

    [Tooltip("Hệ năng lượng của thẻ. Tương đương mảng types trong SQLite cũ.")]
    public EnergyType[] cardTypes;

    [Tooltip("Chi phí rút lui. Tương đương retreatCost INTEGER trong SQLite cũ.")]
    [Range(0, 5)]
    public int retreatCost = 1;

    [Tooltip("Danh sách đòn tấn công.")]
    public AttackData[] attacks;

    [Tooltip("Điểm yếu của thẻ (×2 damage).")]
    public TypeModifier[] weaknesses;

    [Tooltip("Kháng cự của thẻ (-30 damage).")]
    public TypeModifier[] resistances;

    // =========================================================================
    // ECONOMY
    // =========================================================================

    [Header("Economy")]
    [Tooltip("Giá thị trường tham chiếu (USD). " +
             "Tương đương tcgplayer.normal.marketPrice trong pricing JSON cũ.")]
    [Min(0f)]
    public float marketValue = 0.99f;

    [Tooltip("Họa sĩ vẽ thẻ. Dùng cho UI chi tiết.")]
    public string artistName;

    // =========================================================================
    // TIỆN ÍCH
    // =========================================================================

    /// <summary>
    /// Kiểm tra nhanh thẻ có phải High Rarity không.
    /// Tương đương isHighRarity() trong rarityRegistry.ts cũ.
    /// </summary>
    public bool IsHighRarity => rarity != null && rarity.IsHighRarity;

    /// <summary>
    /// Sorting rank của thẻ. Dùng để sort kết quả gacha (thẻ hiếm nhất ở cuối).
    /// Tương đương getRarityRank() trong inventoryStore.ts cũ.
    /// </summary>
    public int RarityRank => rarity != null ? rarity.sortingRank : 0;

    /// <summary>
    /// XP người chơi nhận khi mở được thẻ này.
    /// Tương đương logic gainExp trong tearPack() của inventoryStore.ts cũ.
    /// </summary>
    public int XpReward => rarity != null ? rarity.xpReward : 2;

    /// <summary>
    /// Validation: Kiểm tra tất cả field bắt buộc đã được điền.
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(cardId)) return false;
        if (string.IsNullOrEmpty(cardName)) return false;
        if (rarity == null) return false;
        return true;
    }

    public override string ToString() =>
        $"Card[{cardId}|{cardName}|{rarity?.displayName ?? "NoRarity"}|${marketValue:F2}]";
}

// =============================================================================
// SUPPORTING DATA STRUCTURES
// =============================================================================

/// <summary>
/// Enum các hệ năng lượng trong Pokémon TCG.
/// Tương đương EnergyType type trong battle/types/index.ts cũ.
/// </summary>
public enum EnergyType
{
    Colorless,
    Fire,
    Water,
    Grass,
    Lightning,
    Psychic,
    Fighting,
    Darkness,
    Metal,
    Dragon,
    Fairy
}

/// <summary>
/// Dữ liệu một đòn tấn công.
/// Tương đương ParsedAttack interface trong battle/types/index.ts cũ.
/// </summary>
[System.Serializable]
public class AttackData
{
    [Tooltip("Tên đòn đánh.")]
    public string attackName;

    [Tooltip("Sát thương cơ bản. 0 nếu đòn không gây damage trực tiếp.")]
    [Min(0)]
    public int baseDamage;

    [Tooltip("Chi phí năng lượng để dùng đòn này.")]
    public EnergyType[] energyCost;

    [Tooltip("Mô tả hiệu ứng đặc biệt của đòn.")]
    [TextArea(2, 4)]
    public string effectDescription;
}

/// <summary>
/// Modifier cho Weakness hoặc Resistance.
/// Tương đương {type, value} object trong weaknesses/resistances JSON cũ.
/// </summary>
[System.Serializable]
public class TypeModifier
{
    public EnergyType energyType;

    [Tooltip("Weakness: nhập '×2'. Resistance: nhập '-30'.")]
    public string value;
}
