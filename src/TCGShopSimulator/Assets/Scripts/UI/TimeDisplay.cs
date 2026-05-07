// Assets/Scripts/UI/TimeDisplay.cs

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI hiển thị giờ game và ngày hiện tại.
///
/// PLACEMENT: Góc trên bên phải màn hình, gần MoneyDisplay.
/// </summary>
public class TimeDisplay : MonoBehaviour
{
    // =========================================================================
    // COMPONENTS
    // =========================================================================

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _timeText;
    [SerializeField] private TextMeshProUGUI _dayText;
    [SerializeField] private Image _progressBar;

    [Header("Visual")]
    [SerializeField] private Color _warningColor  = new Color(1f, 0.6f, 0.2f);
    [SerializeField] private Color _closingColor  = new Color(1f, 0.2f, 0.2f);

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Start()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnHourChanged    += HandleHourChanged;
            TimeManager.Instance.OnDayChanged    += HandleDayChanged;
            TimeManager.Instance.OnClosingTime   += HandleClosingTime;
            TimeManager.Instance.OnPausedChanged += HandlePausedChanged;
        }

        UpdateDisplay();
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnHourChanged    -= HandleHourChanged;
            TimeManager.Instance.OnDayChanged    -= HandleDayChanged;
            TimeManager.Instance.OnClosingTime   -= HandleClosingTime;
            TimeManager.Instance.OnPausedChanged -= HandlePausedChanged;
        }
    }

    private void Update()
    {
        UpdateDisplay();
    }

    // =========================================================================
    // UPDATE
    // =========================================================================

    private void UpdateDisplay()
    {
        if (TimeManager.Instance == null) return;

        _timeText.text = TimeManager.Instance.FormattedTime;
        _dayText.text = $"Day {TimeManager.Instance.CurrentDay}";

        if (_progressBar != null)
        {
            _progressBar.fillAmount = TimeManager.Instance.DayProgress;

            if (TimeManager.Instance.IsClosingTime)
            {
                _progressBar.color = _closingColor;
            }
            else if (TimeManager.Instance.TimeInMinutes >= 1080) // Sau 18:00
            {
                _progressBar.color = _warningColor;
            }
            else
            {
                _progressBar.color = Color.white;
            }
        }
    }

    // =========================================================================
    // EVENT HANDLERS
    // =========================================================================

    private void HandleHourChanged(int newHour)   => UpdateDisplay();
    private void HandleDayChanged(int newDay)     => UpdateDisplay();
    private void HandleClosingTime()              => UpdateDisplay();
    private void HandlePausedChanged(bool paused)  { /* could show pause icon */ }
}
