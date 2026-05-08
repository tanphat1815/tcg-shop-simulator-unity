// Assets/Scripts/Data/WorkerDefinition.cs

using UnityEngine;

/// <summary>
/// ScriptableObject khai báo metadata của một loại worker.
/// Tạo asset: Right-click > Create > TCGShop > Data > Worker Definition
///
/// CẤU TRÚC TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   WORKERS[] = [{ id, name, levelUnlocked, checkoutSpeed, hiringFee, salary }]
/// </summary>
[CreateAssetMenu(
    fileName = "Worker_New",
    menuName = "TCGShop/Data/Worker Definition",
    order = 6
)]
public class WorkerDefinition : ScriptableObject
{
    // =========================================================================
    // IDENTITY
    // =========================================================================

    [Header("Identity")]
    public string workerId;
    public string displayName;
    [TextArea(2, 3)]
    public string description;

    // =========================================================================
    // UNLOCK REQUIREMENT
    // =========================================================================

    [Header("Unlock")]
    [Tooltip("Shop level tối thiểu để thuê nhân viên này.")]
    [Range(1, 80)]
    public int requiredShopLevel = 1;

    // =========================================================================
    // ECONOMY
    // =========================================================================

    [Header("Economy")]
    [Tooltip("Chi phí thuê (một lần).")]
    public float hiringFee = 200f;

    [Tooltip("Lương mỗi ngày ($). Trừ khi End of Day.")]
    public float dailySalary = 40f;

    // =========================================================================
    // ABILITIES
    // =========================================================================

    [Header("Checkout Speed")]
    public CheckoutSpeed checkoutSpeed = CheckoutSpeed.Normal;

    [Tooltip("Mô tả tốc độ phục vụ.")]
    public string speedDescription;

    // =========================================================================
    // COMPUTED
    // =========================================================================

    public float CheckoutCooldownSeconds => checkoutSpeed switch
    {
        CheckoutSpeed.Slow      => 5.0f,
        CheckoutSpeed.Normal    => 3.0f,
        CheckoutSpeed.Fast      => 1.5f,
        CheckoutSpeed.VeryFast  => 0.8f,
        _                        => 3.0f
    };
}

/// <summary>
/// Tốc độ checkout của worker.
/// </summary>
public enum CheckoutSpeed
{
    Slow,     // 5000ms
    Normal,   // 3000ms
    Fast,     // 1500ms
    VeryFast  // 800ms
}
