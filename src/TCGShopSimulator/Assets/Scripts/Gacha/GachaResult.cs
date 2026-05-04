// Assets/Scripts/Gacha/GachaResult.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kết quả của một lần mở Pack.
/// Data class thuần — không kế thừa MonoBehaviour hay ScriptableObject.
///
/// TƯƠNG ĐƯƠNG trong hệ thống cũ:
///   sortedCards trong tearPack() của inventoryStore.ts
///   Thẻ được sort theo rarityRank ASC (thẻ hiếm nhất ở cuối)
///   để UI lật từ Common → Rare (reveal climax ở cuối)
/// </summary>
public class GachaResult
{
    /// <summary>
    /// Danh sách thẻ rơi ra, đã sort theo rarityRank ASC.
    /// Index 0 = thẻ thường nhất (lật đầu tiên).
    /// Index Count-1 = thẻ hiếm nhất (lật cuối cùng — climax reveal).
    /// </summary>
    public List<CardData> DroppedCards { get; private set; }

    /// <summary>
    /// Pack nào đã được mở.
    /// </summary>
    public PackData SourcePack { get; private set; }

    /// <summary>
    /// Tổng XP nhận được từ lần mở này.
    /// </summary>
    public int TotalXpGained { get; private set; }

    /// <summary>
    /// Timestamp khi mở pack (dùng cho logging và analytics).
    /// </summary>
    public float OpenedAtTime { get; private set; }

    // =========================================================================
    // COMPUTED PROPERTIES
    // =========================================================================

    /// <summary>
    /// Thẻ hiếm nhất trong kết quả (thẻ cuối cùng hiển thị).
    /// </summary>
    public CardData HighlightCard =>
        DroppedCards != null && DroppedCards.Count > 0
            ? DroppedCards[DroppedCards.Count - 1]
            : null;

    /// <summary>
    /// Có thẻ High Rarity không (để trigger animation đặc biệt).
    /// </summary>
    public bool HasHighRarityCard
    {
        get
        {
            if (DroppedCards == null) return false;
            foreach (var card in DroppedCards)
            {
                if (card != null && card.IsHighRarity) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Tổng giá trị thị trường của tất cả thẻ trong kết quả.
    /// </summary>
    public float TotalMarketValue
    {
        get
        {
            float total = 0f;
            if (DroppedCards == null) return total;
            foreach (var card in DroppedCards)
            {
                if (card != null) total += card.marketValue;
            }
            return total;
        }
    }

    // =========================================================================
    // CONSTRUCTOR
    // =========================================================================

    public GachaResult(PackData sourcePack, List<CardData> droppedCards)
    {
        SourcePack = sourcePack;
        DroppedCards = droppedCards ?? new List<CardData>();
        OpenedAtTime = Time.time;

        TotalXpGained = 0;
        foreach (var card in DroppedCards)
        {
            if (card != null)
                TotalXpGained += card.XpReward;
        }
    }

    // =========================================================================
    // DEBUG
    // =========================================================================

    /// <summary>
    /// Tạo chuỗi mô tả đầy đủ kết quả cho logging.
    /// </summary>
    public string ToDebugString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== GachaResult [{SourcePack?.packName ?? "Unknown Pack"}] ===");
        sb.AppendLine($"  Cards ({DroppedCards?.Count ?? 0}), XP: +{TotalXpGained}, " +
                      $"Total Value: ${TotalMarketValue:F2}");

        if (DroppedCards != null)
        {
            for (int i = 0; i < DroppedCards.Count; i++)
            {
                var card = DroppedCards[i];
                string highlight = card.IsHighRarity ? " ★ HIGH RARITY!" : "";
                sb.AppendLine($"  [{i + 1}] {card.cardName} " +
                              $"({card.rarity?.displayName ?? "?"}) " +
                              $"${card.marketValue:F2}{highlight}");
            }
        }

        return sb.ToString();
    }
}
