// Assets/Scripts/Customer/EconomicDecisionEngine.cs

using UnityEngine;

/// <summary>
/// Tính xác suất mua hàng của NPC dựa trên tỷ lệ giá bán / giá thị trường.
///
/// ============================================================================
/// CÔNG THỨC XÁC SUẤT MUA — MỚI HOÀN TOÀN (không có trong hệ thống Vue/Phaser cũ)
/// ============================================================================
///
/// HỆ THỐNG CŨ: NPC mua bất kể giá. 100% mua nếu kệ có hàng.
///
/// HỆ THỐNG MỚI:
///
///   priceRatio = sellPrice / marketPrice
///
///   IF priceRatio <= 1.0:
///     buyProbability = 0.95  (95% — giá hợp lý hoặc tốt hơn thị trường)
///
///   ELSE:
///     overpricePercent = (priceRatio - 1.0) × 100
///     steps = FLOOR(overpricePercent / 5)
///     reduction = steps × 0.15
///     buyProbability = MAX(0.95 - reduction, 0.0)
///
/// NGƯỠNG PHẢN ỨNG:
///   buyProbability == 0.0  → LUÔN TỪ CHỐI, hiện bong bóng tức giận (Angry)
///   buyProbability < 0.40  → MIỄN CƯỠNG, hiện bong bóng trung lập (Neutral)
///   buyProbability >= 0.40 → CÓ THỂ MUA, hiện bong bóng trái tim (Happy)
///
/// VÍ DỤ:
///   sellPrice=$10, market=$10 → ratio=1.0 → 95% mua → Happy bubble
///   sellPrice=$11, market=$10 → ratio=1.1 → 10% đắt → 2 bước → 95%-30%=65% → Happy
///   sellPrice=$15, market=$10 → ratio=1.5 → 50% đắt → 10 bước → 95%-150%<0 → 0% → Angry
///   sellPrice=$30, market=$10 → ratio=3.0 (đắt 3x) → 0% → Angry bubble (Test case bắt buộc)
///
/// ============================================================================
/// </summary>
public static class EconomicDecisionEngine
{
    private const float BASE_BUY_PROBABILITY = 0.95f;
    private const float PRICE_STEP_PERCENT   = 5f;
    private const float PROBABILITY_REDUCTION_PER_STEP = 0.15f;

    public const float RELUCTANT_THRESHOLD = 0.40f;

    /// <summary>
    /// Tính xác suất mua hàng dựa trên tỷ lệ giá.
    /// </summary>
    /// <param name="sellPrice">Giá bán do người chơi đặt.</param>
    /// <param name="marketPrice">Giá thị trường tham chiếu.</param>
    /// <returns>Xác suất mua trong khoảng [0.0, 0.95].</returns>
    public static float CalculateBuyProbability(float sellPrice, float marketPrice)
    {
        if (marketPrice < 0f)
        {
            Debug.LogWarning($"[EconomicDecisionEngine] marketPrice is negative ({marketPrice}). " +
                            "Returning 0 probability. Check data integrity.");
            return 0f;
        }

        if (Mathf.Approximately(marketPrice, 0f))
        {
            Debug.LogWarning($"[EconomicDecisionEngine] marketPrice is 0. " +
                            "Treating as free item — using BASE_BUY_PROBABILITY (95%).");
            return BASE_BUY_PROBABILITY;
        }

        float priceRatio = sellPrice / marketPrice;

        if (priceRatio <= 1.0f)
        {
            return BASE_BUY_PROBABILITY;
        }

        float overpricePercent = (priceRatio - 1.0f) * 100f;
        int steps = Mathf.FloorToInt(overpricePercent / PRICE_STEP_PERCENT);
        float reduction = steps * PROBABILITY_REDUCTION_PER_STEP;
        float probability = Mathf.Max(BASE_BUY_PROBABILITY - reduction, 0f);

        return probability;
    }

    /// <summary>
    /// Roll dice để quyết định NPC có mua không.
    /// </summary>
    public static bool DecidePurchase(
        float sellPrice,
        float marketPrice,
        out float probability,
        out PurchaseDecision decisionType)
    {
        probability = CalculateBuyProbability(sellPrice, marketPrice);

        if (probability <= 0f)
        {
            decisionType = PurchaseDecision.AbsoluteRefusal;
            return false;
        }

        bool willBuy = Random.value < probability;

        if (willBuy)
        {
            decisionType = probability >= RELUCTANT_THRESHOLD
                ? PurchaseDecision.HappyPurchase
                : PurchaseDecision.ReluctantPurchase;
        }
        else
        {
            decisionType = probability < RELUCTANT_THRESHOLD
                ? PurchaseDecision.AbsoluteRefusal
                : PurchaseDecision.NormalRefusal;
        }

        return willBuy;
    }

    /// <summary>
    /// Lấy BubbleReaction tương ứng với kết quả quyết định.
    /// </summary>
    public static BubbleReactionType GetBubbleReaction(PurchaseDecision decision)
    {
        return decision switch
        {
            PurchaseDecision.HappyPurchase     => BubbleReactionType.Heart,
            PurchaseDecision.ReluctantPurchase => BubbleReactionType.Neutral,
            PurchaseDecision.NormalRefusal     => BubbleReactionType.Neutral,
            PurchaseDecision.AbsoluteRefusal   => BubbleReactionType.Angry,
            _                                 => BubbleReactionType.Neutral
        };
    }
}

/// <summary>
/// Kết quả phân loại quyết định mua.
/// </summary>
public enum PurchaseDecision
{
    HappyPurchase,      // Xác suất cao, quyết định mua → Heart bubble
    ReluctantPurchase,  // Xác suất thấp nhưng vẫn mua → Heart bubble nhỏ hơn
    NormalRefusal,     // Xác suất trung bình, không mua → Neutral bubble
    AbsoluteRefusal    // Xác suất = 0, không bao giờ mua → Angry bubble
}

/// <summary>
/// Loại bong bóng phản ứng hiển thị trên đầu NPC.
/// </summary>
public enum BubbleReactionType
{
    Heart,   // NPC đồng ý mua
    Neutral, // NPC do dự
    Angry    // NPC từ chối mạnh — giá quá cao
}
