// Assets/Scripts/UI/CardRevealEffect.cs

using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiệu ứng lật lá bài trong pack opening.
/// Dùng scale X từ 1 → 0 (nửa sau) → 1 kết hợp với đổi sprite.
///
/// NOTE: Logic lật chính nằm trong PackOpeningUI.AnimateCardFlip().
/// File này chứa các helper method cho card animation.
/// </summary>
public static class CardRevealEffect
{
    /// <summary>
    /// Tính scaleX cho animation lật bài tại thời điểm t.
    /// t = 0..1, t = 0.5 là midpoint khi bài quay ngang.
    /// </summary>
    public static float FlipScaleX(float t)
    {
        if (t < 0.5f)
            return 1f - (t / 0.5f);
        return (t - 0.5f) / 0.5f;
    }

    /// <summary>
    /// Kiểm tra tại thời điểm t có nên đổi sprite chưa.
    /// Đổi sprite khi t >= 0.5 (sau khi bài quay ngang).
    /// </summary>
    public static bool ShouldSwapSprite(float t)
    {
        return t >= 0.5f;
    }

    /// <summary>
    /// Màu theo bậc hiếm dựa trên RarityRank.
    /// </summary>
    public static Color RarityColor(int rarityRank)
    {
        return rarityRank switch
        {
            >= 5 => new Color(1f, 0.8f, 0f),    // Gold — Ultra/Special Rare
            4    => new Color(0.6f, 0.3f, 1f), // Purple — Rare Holo
            3    => new Color(0.2f, 0.6f, 1f), // Blue — Rare
            2    => new Color(0.4f, 0.4f, 0.4f), // Gray — Uncommon
            _    => new Color(0.9f, 0.9f, 0.9f)  // White — Common
        };
    }
}
