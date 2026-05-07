// Assets/Scripts/UI/UIResponsiveConfig.cs
using UnityEngine;

/// <summary>
/// ScriptableObject that holds responsive UI thresholds and configuration.
/// Create via: Right-click in Project > Create > TCGShop > UI Responsive Config
/// </summary>
[CreateAssetMenu(fileName = "UIResponsiveConfig", menuName = "TCGShop/UI/Responsive Config")]
public class UIResponsiveConfig : ScriptableObject
{
    [Header("Reference Resolution")]
    [Tooltip("The resolution the UI was designed for.")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("Hitbox — Minimum Touch Target")]
    [Tooltip("Minimum button size in Canvas units. 100x60 ~44pt @2x.")]
    public float minButtonWidth = 100f;
    public float minButtonHeight = 60f;
    public float minIconSize = 32f;

    [Header("Font Sizes")]
    public int fontSizeBodyMin = 24;
    public int fontSizeCaptionMin = 18;
    public int fontSizeHeaderMin = 32;

    [Header("Spacing")]
    public float buttonPaddingX = 24f;
    public float buttonPaddingY = 12f;
    public float panelPadding = 16f;

    [Header("Safe Area")]
    [Tooltip("Automatically apply SafeAreaFitter when aspect ratio < 1.5 (portrait).")]
    public bool autoSafeAreaOnMobile = true;

    [Header("Responsive Thresholds")]
    [Tooltip("Extreme aspect ratio boundaries.")]
    public float extremeRatioMin = 0.5f;
    public float extremeRatioMax = 2.0f;

    /// <summary>
    /// Calculates the UI scale factor for a given resolution.
    /// </summary>
    public float CalculateScaleFactor(int screenWidth, int screenHeight)
    {
        float scaleW = (float)screenWidth / referenceResolution.x;
        float scaleH = (float)screenHeight / referenceResolution.y;
        float scale = Mathf.Min(scaleW, scaleH);
        return Mathf.Max(scale, 0.25f);
    }
}
