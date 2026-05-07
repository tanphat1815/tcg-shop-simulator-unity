// Assets/Scripts/UI/EndOfDayPanel.cs

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel hiển thị End of Day summary.
/// Xuất hiện khi shop đóng cửa (20:00), cho thấy:
///   - Daily Revenue
///   - Staff Salary
///   - Rent
///   - New Balance
/// </summary>
public class EndOfDayPanel : MonoBehaviour
{
    // =========================================================================
    // SINGLETON
    // =========================================================================

    public static EndOfDayPanel Instance { get; private set; }

    // =========================================================================
    // UI REFERENCES
    // =========================================================================

    [Header("Stats Display")]
    [SerializeField] private TextMeshProUGUI _dayNumberText;
    [SerializeField] private TextMeshProUGUI _revenueText;
    [SerializeField] private TextMeshProUGUI _salaryText;
    [SerializeField] private TextMeshProUGUI _rentText;
    [SerializeField] private TextMeshProUGUI _totalCostText;
    [SerializeField] private TextMeshProUGUI _previousBalanceText;
    [SerializeField] private TextMeshProUGUI _newBalanceText;

    [Header("Actions")]
    [SerializeField] private Button _nextDayButton;

    [Header("Colors")]
    [SerializeField] private Color _positiveColor = new Color(0.4f, 1f, 0.4f);
    [SerializeField] private Color _negativeColor = new Color(1f, 0.4f, 0.4f);

    [Header("Animation")]
    [SerializeField] private float _fadeInDuration = 0.3f;
    [SerializeField] private CanvasGroup _canvasGroup;

    // =========================================================================
    // STATE
    // =========================================================================

    private float _newBalance;
    private bool _isShowing;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (_nextDayButton != null)
            _nextDayButton.onClick.AddListener(OnNextDayClicked);

        Hide();
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Hiển thị panel End of Day với dữ liệu.
    /// </summary>
    public void Show(
        int dayNumber,
        float dailyRevenue,
        float totalSalary,
        float rent,
        float previousBalance,
        float newBalance)
    {
        _newBalance = newBalance;
        _isShowing = true;

        _dayNumberText.text = $"Day {dayNumber} — Shop Closed";
        _revenueText.text = $"+${dailyRevenue:F2}";
        _revenueText.color = _positiveColor;

        _salaryText.text = $"-${totalSalary:F2}";
        _salaryText.color = _negativeColor;

        _rentText.text = $"-${rent:F2}";
        _rentText.color = _negativeColor;

        float totalCost = totalSalary + rent;
        _totalCostText.text = $"-${totalCost:F2}";
        _totalCostText.color = _negativeColor;

        _previousBalanceText.text = $"${previousBalance:F2}";
        _newBalanceText.text = $"${newBalance:F2}";
        _newBalanceText.color = newBalance >= 0 ? _positiveColor : _negativeColor;

        gameObject.SetActive(true);
        StartCoroutine(FadeInCoroutine());

        if (TimeManager.Instance != null)
            TimeManager.Instance.SetPaused(true);

        Debug.Log($"[EndOfDayPanel] Day {dayNumber} EOD: Revenue=${dailyRevenue:F2}, " +
                  $"Costs=${totalCost:F2}, Balance=${newBalance:F2}");
    }

    /// <summary>Ẩn panel.</summary>
    public void Hide()
    {
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
        _isShowing = false;
    }

    // =========================================================================
    // EVENTS
    // =========================================================================

    private void OnNextDayClicked()
    {
        Hide();

        if (TimeManager.Instance != null)
            TimeManager.Instance.TransitionToNextDay(_newBalance);
    }

    // =========================================================================
    // ANIMATION
    // =========================================================================

    private System.Collections.IEnumerator FadeInCoroutine()
    {
        if (_canvasGroup == null) yield break;

        float elapsed = 0f;
        _canvasGroup.alpha = 0f;

        while (elapsed < _fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeInDuration);
            yield return null;
        }

        _canvasGroup.alpha = 1f;
    }
}
