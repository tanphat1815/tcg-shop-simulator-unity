// Assets/Scripts/Gacha/GachaEngine.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Engine tính toán kết quả mở pack theo thuật toán Cumulative Weighted Probability.
///
/// ===========================================================================
/// CUMULATIVE WEIGHTED PROBABILITY — Cách hoạt động
/// ===========================================================================
///
/// INPUT: Drop Table Slot với weights:
///   Common    = 60
///   Uncommon  = 30
///   Rare      = 8
///   Ultra     = 2
///   (Tổng = 100, nhưng engine tự normalize nên KHÔNG cần tổng = 100)
///
/// BƯỚC 1: Tính tổng tất cả weights
///   totalWeight = 60 + 30 + 8 + 2 = 100
///
/// BƯỚC 2: Roll một số random trong [0, totalWeight)
///   roll = Random.Range(0f, totalWeight)  (ví dụ: 73.4)
///
/// BƯỚC 3: Duyệt tích lũy để tìm rarity
///   roll = 73.4
///   Common:   cumulativeWeight=60 →  60 > 73.4? NO  → continue
///   Uncommon: cumulativeWeight=90 →  90 > 73.4? YES → chọn Uncommon
///
/// KẾT QUẢ: Với bảng này, mỗi lần roll:
///   60% → Common, 30% → Uncommon, 8% → Rare, 2% → Ultra
///   BẤT KỂ có bao nhiêu thẻ trong pool.
///
/// ===========================================================================
/// THIẾT KẾ CLASS
/// ===========================================================================
/// - Static class: Không cần instance, không cần MonoBehaviour.
/// - Pure functions: Cùng input → phân phối đúng theo weight.
/// - Không có side effects: Không modify bất kỳ state nào bên ngoài.
/// </summary>
public static class GachaEngine
{
    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Mở một pack và trả về kết quả.
    /// Đây là entry point chính của toàn bộ hệ thống Gacha.
    /// </summary>
    /// <param name="pack">Pack cần mở. Phải IsValid() == true.</param>
    /// <param name="seed">
    ///   Seed ngẫu nhiên tùy chọn. Dùng cho reproducible testing.
    ///   -1 = không dùng seed (fully random).
    /// </param>
    /// <returns>GachaResult chứa danh sách thẻ đã sort.</returns>
    public static GachaResult OpenPack(PackData pack, int seed = -1)
    {
        if (pack == null)
        {
            Debug.LogError("[GachaEngine] pack là null!");
            return new GachaResult(null, new List<CardData>());
        }

        if (!pack.IsValid())
        {
            Debug.LogError($"[GachaEngine] Pack '{pack.packId}' không hợp lệ. " +
                           "Kiểm tra availableCards và dropTable đã được điền.");
            return new GachaResult(pack, new List<CardData>());
        }

        Random.State previousRandomState = Random.state;
        if (seed >= 0)
            Random.InitState(seed);

        var droppedCards = new List<CardData>();
        int slotCount = Mathf.Min(pack.dropTable.Count, pack.cardsPerPack);

        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            var slot = pack.dropTable[slotIndex];

            RarityDefinition rolledRarity = RollRarity(slot);

            if (rolledRarity == null)
            {
                Debug.LogWarning($"[GachaEngine] Slot {slotIndex} của pack '{pack.packId}' " +
                                 "không roll được rarity. Bỏ qua slot này.");
                continue;
            }

            List<CardData> rarityPool = pack.GetCardsByRarity(rolledRarity);

            if (rarityPool == null || rarityPool.Count == 0)
            {
                Debug.LogWarning($"[GachaEngine] Không tìm thấy card nào với rarity " +
                                 $"'{rolledRarity.displayName}' trong pack '{pack.packId}'. " +
                                 "Dùng toàn bộ pool làm fallback.");
                rarityPool = pack.availableCards;
            }

            CardData selectedCard = SelectRandomFromPool(rarityPool);
            if (selectedCard != null)
                droppedCards.Add(selectedCard);
        }

        if (seed >= 0)
            Random.state = previousRandomState;

        droppedCards.Sort((a, b) =>
        {
            int rankA = a?.RarityRank ?? 0;
            int rankB = b?.RarityRank ?? 0;
            return rankA.CompareTo(rankB);
        });

        return new GachaResult(pack, droppedCards);
    }

    // =========================================================================
    // THUẬT TOÁN LÕI: CUMULATIVE WEIGHTED PROBABILITY
    // =========================================================================

    /// <summary>
    /// THUẬT TOÁN CHÍNH: Chọn một RarityDefinition từ Drop Table Slot
    /// dựa trên Cumulative Weighted Probability.
    ///
    /// BƯỚC 1: Tính totalWeight = tổng tất cả weight trong slot
    /// BƯỚC 2: Roll một số random trong [0, totalWeight)
    /// BƯỚC 3: Duyệt qua từng rarity, cộng dồn weight
    ///          Khi cộng dồn >= roll → đó là rarity được chọn
    /// </summary>
    private static RarityDefinition RollRarity(DropTableSlot slot)
    {
        if (slot == null || slot.rarityWeights == null || slot.rarityWeights.Count == 0)
        {
            Debug.LogWarning("[GachaEngine] Slot trống hoặc không có rarityWeights.");
            return null;
        }

        float totalWeight = 0f;
        foreach (var entry in slot.rarityWeights)
        {
            if (entry != null && entry.rarity != null && entry.weight > 0f)
                totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
        {
            Debug.LogWarning($"[GachaEngine] Slot '{slot.slotLabel}' có totalWeight = 0. " +
                             "Kiểm tra lại các weight trong Inspector.");
            return null;
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;

        foreach (var entry in slot.rarityWeights)
        {
            if (entry == null || entry.rarity == null || entry.weight <= 0f) continue;

            cumulativeWeight += entry.weight;
            if (roll < cumulativeWeight)
                return entry.rarity;
        }

        for (int i = slot.rarityWeights.Count - 1; i >= 0; i--)
        {
            var lastEntry = slot.rarityWeights[i];
            if (lastEntry != null && lastEntry.rarity != null)
                return lastEntry.rarity;
        }

        Debug.LogError("[GachaEngine] Không thể chọn rarity. Kiểm tra Drop Table configuration.");
        return null;
    }

    /// <summary>
    /// Chọn một CardData ngẫu nhiên đều từ pool.
    /// Đây là uniform random (không có weight) — tất cả thẻ cùng rarity có cơ hội bằng nhau.
    /// </summary>
    private static CardData SelectRandomFromPool(List<CardData> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        if (pool.Count == 1) return pool[0];

        int randomIndex = Random.Range(0, pool.Count);
        return pool[randomIndex];
    }

    // =========================================================================
    // UTILITY API
    // =========================================================================

    /// <summary>
    /// Tính xác suất lý thuyết của từng rarity trong một slot.
    /// Trả về Dictionary[RarityDefinition → xác suất từ 0.0 đến 1.0]
    /// </summary>
    public static Dictionary<RarityDefinition, float> CalculateTheoreticalRates(DropTableSlot slot)
    {
        var rates = new Dictionary<RarityDefinition, float>();

        if (slot?.rarityWeights == null) return rates;

        float totalWeight = 0f;
        foreach (var entry in slot.rarityWeights)
        {
            if (entry?.rarity != null && entry.weight > 0f)
                totalWeight += entry.weight;
        }

        if (totalWeight <= 0f) return rates;

        foreach (var entry in slot.rarityWeights)
        {
            if (entry?.rarity == null || entry.weight <= 0f) continue;
            rates[entry.rarity] = entry.weight / totalWeight;
        }

        return rates;
    }

    /// <summary>
    /// Simulate mở N pack và trả về thống kê phân phối rarity.
    /// Dùng cho GachaDebugTester để verify RNG không bị thiên lệch.
    /// </summary>
    public static Dictionary<string, int> SimulateOpenPacks(PackData pack, int numberOfPacks)
    {
        var rarityCount = new Dictionary<string, int>();

        for (int i = 0; i < numberOfPacks; i++)
        {
            var result = OpenPack(pack);
            foreach (var card in result.DroppedCards)
            {
                string rarityName = card.rarity?.displayName ?? "Unknown";
                if (!rarityCount.ContainsKey(rarityName))
                    rarityCount[rarityName] = 0;
                rarityCount[rarityName]++;
            }
        }

        return rarityCount;
    }
}
