// Assets/Scripts/Debug/GachaDebugTester.cs

using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Debug tester tự động chạy khi game start.
/// Mở N pack liên tục và in thống kê tỷ lệ rơi ra Console.
///
/// MỤC ĐÍCH:
///   Chứng minh GachaEngine phân phối đúng theo Drop Table đã cấu hình.
///   So sánh tỷ lệ THỰC TẾ (từ simulation) với tỷ lệ LÝ THUYẾT (từ weights).
///
/// CÁCH DÙNG:
///   1. Gắn script này vào bất kỳ GameObject nào trong GameScene.
///   2. Assign packToTest trong Inspector.
///   3. Nhấn Play → Kiểm tra Console.
///   4. SAU KHI TEST XONG: Disable hoặc xóa component này.
///      Không để component này trong production build.
/// </summary>
public class GachaDebugTester : MonoBehaviour
{
    [Header("Test Configuration")]
    [Tooltip("Pack cần test. Phải có Drop Table được cấu hình đầy đủ.")]
    [SerializeField] private PackData packToTest;

    [Tooltip("Số pack mở trong test. Mặc định 10.")]
    [SerializeField][Range(1, 100)] private int numberOfPacksToOpen = 10;

    [Tooltip("Sai số chấp nhận được giữa tỷ lệ thực tế và lý thuyết. Mặc định 15%.")]
    [SerializeField][Range(0.05f, 0.50f)] private float tolerancePercent = 0.15f;

    [Tooltip("Nếu true, in chi tiết từng pack. Nếu false, chỉ in tổng kết.")]
    [SerializeField] private bool printIndividualPacks = true;

    [Tooltip("Nếu true, tự disable component sau khi test xong.")]
    [SerializeField] private bool disableAfterTest = true;

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    private void Start()
    {
        if (packToTest == null)
        {
            Debug.LogError("[GachaDebugTester] packToTest chưa được assign! " +
                           "Kéo thả một PackData asset vào field 'Pack To Test'.");
            return;
        }

        if (!packToTest.IsValid())
        {
            Debug.LogError($"[GachaDebugTester] Pack '{packToTest.packId}' không hợp lệ. " +
                           "Kiểm tra availableCards và dropTable trong Inspector.");
            return;
        }

        RunFullTest();

        if (disableAfterTest)
            enabled = false;
    }

    // =========================================================================
    // MAIN TEST LOGIC
    // =========================================================================

    private void RunFullTest()
    {
        const string DIVIDER = "════════════════════════════════════════════════════";

        Debug.Log($"[GachaDebugTester] {DIVIDER}");
        Debug.Log($"[GachaDebugTester] PACK OPENING SIMULATION: {numberOfPacksToOpen} packs");
        Debug.Log($"[GachaDebugTester] Pack: \"{packToTest.packName}\" (ID: {packToTest.packId})");
        Debug.Log($"[GachaDebugTester] Cards per pack: {packToTest.cardsPerPack}");
        Debug.Log($"[GachaDebugTester] Cards in pool: {packToTest.availableCards?.Count ?? 0}");
        Debug.Log($"[GachaDebugTester] {DIVIDER}");

        var allResults = new List<GachaResult>();
        var totalRarityCount = new Dictionary<string, int>();
        float totalCardValue = 0f;
        int totalXpGained = 0;

        if (printIndividualPacks)
            Debug.Log($"[GachaDebugTester] INDIVIDUAL PACK RESULTS:");

        for (int i = 0; i < numberOfPacksToOpen; i++)
        {
            GachaResult result = GachaEngine.OpenPack(packToTest);
            allResults.Add(result);

            totalCardValue += result.TotalMarketValue;
            totalXpGained += result.TotalXpGained;

            foreach (var card in result.DroppedCards)
            {
                if (card == null) continue;
                string rarityName = card.rarity?.displayName ?? "Unknown";
                if (!totalRarityCount.ContainsKey(rarityName))
                    totalRarityCount[rarityName] = 0;
                totalRarityCount[rarityName]++;
            }

            if (printIndividualPacks)
                PrintIndividualPackResult(i + 1, result);
        }

        int totalCards = numberOfPacksToOpen * packToTest.cardsPerPack;
        float totalPackCost = packToTest.buyCost * numberOfPacksToOpen;

        Debug.Log($"[GachaDebugTester] {DIVIDER}");
        PrintRarityDistribution(totalRarityCount, totalCards);
        Debug.Log($"[GachaDebugTester] {DIVIDER}");
        PrintEconomyStats(totalPackCost, totalCardValue, totalXpGained, numberOfPacksToOpen);
        Debug.Log($"[GachaDebugTester] {DIVIDER}");

        bool passed = RunBiasCheck(totalRarityCount, totalCards);
        if (passed)
        {
            Debug.Log($"[GachaDebugTester] ✅ RNG BIAS CHECK: PASS — " +
                      $"Distribution within {tolerancePercent * 100f:F0}% tolerance.");
        }
        else
        {
            Debug.LogWarning($"[GachaDebugTester] ⚠️ RNG BIAS CHECK: WARN — " +
                             $"Some rarities outside {tolerancePercent * 100f:F0}% tolerance. " +
                             $"Tăng numberOfPacksToOpen để có kết quả chính xác hơn.");
        }

        Debug.Log($"[GachaDebugTester] {DIVIDER}");
    }

    private void PrintIndividualPackResult(int packNumber, GachaResult result)
    {
        var sb = new StringBuilder();
        sb.Append($"[GachaDebugTester]   Pack #{packNumber:D2}: ");

        for (int i = 0; i < result.DroppedCards.Count; i++)
        {
            var card = result.DroppedCards[i];
            if (card == null) continue;

            string rarityCode = GetRarityCode(card.rarity);

            if (card.IsHighRarity)
                sb.Append($"★{card.cardName}({rarityCode})★");
            else
                sb.Append($"{card.cardName}({rarityCode})");

            if (i < result.DroppedCards.Count - 1)
                sb.Append(", ");
        }

        sb.Append($" | Value: ${result.TotalMarketValue:F2} | XP: +{result.TotalXpGained}");
        Debug.Log(sb.ToString());
    }

    private void PrintRarityDistribution(Dictionary<string, int> rarityCount, int totalCards)
    {
        Debug.Log($"[GachaDebugTester] RARITY DISTRIBUTION " +
                  $"({totalCards} total cards from {numberOfPacksToOpen} packs):");

        Dictionary<string, float> theoreticalRates = CalculateAverageTheoreticalRates();
        var sortedRarities = new List<string>(rarityCount.Keys);
        sortedRarities.Sort();

        foreach (var rarityName in sortedRarities)
        {
            int count = rarityCount[rarityName];
            float actualPercent = totalCards > 0 ? (float)count / totalCards * 100f : 0f;

            string theoreticalStr = "N/A";
            string passStr = "";

            if (theoreticalRates.TryGetValue(rarityName, out float theoreticalRate))
            {
                float theoreticalPercent = theoreticalRate * 100f;
                theoreticalStr = $"~{theoreticalPercent:F2}%";

                float deviation = Mathf.Abs(actualPercent - theoreticalPercent)
                    / (theoreticalPercent > 0f ? theoreticalPercent : 1f);
                passStr = deviation <= tolerancePercent ? "  ✓" : "  ⚠";
            }

            Debug.Log($"[GachaDebugTester]   {rarityName,-30} " +
                      $"{count,4} cards | " +
                      $"{actualPercent,6:F2}% actual | " +
                      $"{theoreticalStr,12} theoretical{passStr}");
        }
    }

    private void PrintEconomyStats(float totalCost, float totalValue,
                                   int totalXp, int packCount)
    {
        float roi = totalCost > 0 ? (totalValue - totalCost) / totalCost * 100f : 0f;
        string roiSign = roi >= 0 ? "+" : "";

        Debug.Log($"[GachaDebugTester] ECONOMY STATS:");
        Debug.Log($"[GachaDebugTester]   Total pack cost:    ${totalCost:F2}  " +
                  $"({packCount} × ${packToTest.buyCost:F2})");
        Debug.Log($"[GachaDebugTester]   Total card value:   ${totalValue:F2}");
        Debug.Log($"[GachaDebugTester]   Total XP gained:    +{totalXp}");
        Debug.Log($"[GachaDebugTester]   Return on invest:   {roiSign}{roi:F1}%");
    }

    private bool RunBiasCheck(Dictionary<string, int> rarityCount, int totalCards)
    {
        if (totalCards <= 0) return false;

        Dictionary<string, float> theoreticalRates = CalculateAverageTheoreticalRates();
        bool allPassed = true;

        foreach (var kvp in rarityCount)
        {
            string rarityName = kvp.Key;
            float actualRate = (float)kvp.Value / totalCards;

            if (!theoreticalRates.TryGetValue(rarityName, out float theoreticalRate))
                continue;

            if (theoreticalRate <= 0f) continue;

            float deviation = Mathf.Abs(actualRate - theoreticalRate)
                / (theoreticalRate > 0f ? theoreticalRate : 1f);
            if (deviation > tolerancePercent)
            {
                allPassed = false;
                Debug.LogWarning($"[GachaDebugTester]   BIAS DETECTED: {rarityName} | " +
                                 $"Actual: {actualRate * 100f:F2}% | " +
                                 $"Expected: {theoreticalRate * 100f:F2}% | " +
                                 $"Deviation: {deviation * 100f:F1}% (limit: {tolerancePercent * 100f:F0}%)");
            }
        }

        return allPassed;
    }

    private Dictionary<string, float> CalculateAverageTheoreticalRates()
    {
        var averageRates = new Dictionary<string, float>();
        int slotCount = packToTest.dropTable?.Count ?? 0;
        if (slotCount == 0) return averageRates;

        foreach (var slot in packToTest.dropTable)
        {
            var slotRates = GachaEngine.CalculateTheoreticalRates(slot);
            foreach (var kvp in slotRates)
            {
                string rarityName = kvp.Key.displayName;
                if (!averageRates.ContainsKey(rarityName))
                    averageRates[rarityName] = 0f;
                averageRates[rarityName] += kvp.Value;
            }
        }

        var keys = new List<string>(averageRates.Keys);
        foreach (var key in keys)
            averageRates[key] /= slotCount;

        return averageRates;
    }

    private string GetRarityCode(RarityDefinition rarity)
    {
        if (rarity == null) return "?";
        return rarity.sortingRank switch
        {
            0  => "C",
            1  => "U",
            3  => "R",
            4  => "2R",
            5  => "UR",
            6  => "SR",
            7  => "IR",
            8  => "SIR",
            9  => "HSR",
            10 => "GR",
            _  => rarity.displayName.Length >= 3
                      ? rarity.displayName[..3]
                      : rarity.displayName
        };
    }
}
