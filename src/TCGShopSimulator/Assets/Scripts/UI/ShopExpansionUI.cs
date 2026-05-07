// Assets/Scripts/UI/ShopExpansionUI.cs

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI panel cho shop expansion.
/// Hiển thị nút mua expansion, thông tin next level, và trạng thái.
/// </summary>
public class ShopExpansionUI : MonoBehaviour
{
    // =========================================================================
    // UI REFERENCES
    // =========================================================================

    [Header("Info Display")]
    [SerializeField] private TextMeshProUGUI _levelText;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private TextMeshProUGUI _requiredLevelText;
    [SerializeField] private TextMeshProUGUI _rentIncreaseText;
    [SerializeField] private TextMeshProUGUI _currentMoneyText;

    [Header("Actions")]
    [SerializeField] private Button _buyButton;

    [Header("Colors")]
    [SerializeField] private Color _canAffordColor   = new Color(0.4f, 1f, 0.4f);
    [SerializeField] private Color _cannotAffordColor = new Color(1f, 0.4f, 0.4f);
    [SerializeField] private Color _maxLevelColor    = new Color(0.7f, 0.7f, 0.7f);

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Start()
    {
        if (_buyButton != null)
            _buyButton.onClick.AddListener(OnBuyClicked);

        if (ExpansionManager.Instance != null)
        {
            ExpansionManager.Instance.OnExpansionLevelChanged += _ => UpdateUI();
            ExpansionManager.Instance.OnExpansionLevelBought += (_, _) => UpdateUI();
        }

        GameEconomyEvents.OnMoneyChanged += (_, _) => UpdateUI();
        GameEconomyEvents.OnLevelUp += _ => UpdateUI();

        UpdateUI();
    }

    private void OnDestroy()
    {
        if (ExpansionManager.Instance != null)
        {
            ExpansionManager.Instance.OnExpansionLevelChanged -= _ => UpdateUI();
            ExpansionManager.Instance.OnExpansionLevelBought -= (_, _) => UpdateUI();
        }

        GameEconomyEvents.OnMoneyChanged -= (_, _) => UpdateUI();
        GameEconomyEvents.OnLevelUp -= _ => UpdateUI();
    }

    // =========================================================================
    // UPDATE
    // =========================================================================

    private void UpdateUI()
    {
        if (ExpansionManager.Instance == null) return;

        int currentLevel = ExpansionManager.Instance.CurrentLevel;
        float money = ShopFloorManager.Instance?.TotalMoney ?? 0f;
        int playerLevel = GameManager.Instance?.CurrentLevel ?? 1;

        _levelText.text = $"Expansion Level: {currentLevel}";

        var next = ExpansionManager.Instance.NextLevel;

        if (ExpansionManager.Instance.IsMaxLevel || next == null)
        {
            _costText.text = "MAX LEVEL";
            _requiredLevelText.text = "";
            _rentIncreaseText.text = "";
            _buyButton.interactable = false;
            SetButtonColor(_maxLevelColor);
            return;
        }

        _costText.text = $"${next.cost:F0}";
        _requiredLevelText.text = $"Requires Level {next.requiredShopLevel}";
        _rentIncreaseText.text = $"+${next.rentIncrease:F0}/day rent";

        bool canBuy = ExpansionManager.Instance.CanBuyNextExpansion;
        _buyButton.interactable = canBuy;
        SetButtonColor(canBuy ? _canAffordColor : _cannotAffordColor);
    }

    private void SetButtonColor(Color color)
    {
        var colors = _buyButton.colors;
        colors.normalColor = color;
        _buyButton.colors = colors;
    }

    // =========================================================================
    // EVENTS
    // =========================================================================

    private void OnBuyClicked()
    {
        if (ExpansionManager.Instance == null) return;

        bool success = ExpansionManager.Instance.TryBuyExpansion();

        if (!success)
        {
            Debug.Log("[ShopExpansionUI] Cannot buy expansion: conditions not met.");
        }
    }
}
