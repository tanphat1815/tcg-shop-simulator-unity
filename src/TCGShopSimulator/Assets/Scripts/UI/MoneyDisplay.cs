// Assets/Scripts/UI/MoneyDisplay.cs

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI hiển thị số tiền hiện tại của cửa hàng.
/// Tự động cập nhật qua Observer Pattern (GameEconomyEvents).
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   Vue template: <span>{{ statsStore.money }}</span>
/// </summary>
public class MoneyDisplay : MonoBehaviour
{
    // ========================================================================
    // UI REFERENCES
    // ========================================================================

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _moneyText;
    [SerializeField] private Image _coinIcon;

    [Header("Animation")]
    [SerializeField] private GameObject _changePopupPrefab;
    [SerializeField] private RectTransform _popupAnchor;

    [Header("Animation Settings")]
    [SerializeField] private float _countUpDuration = 0.5f;
    [SerializeField] private Color _positiveColor = new Color(0.2f, 0.9f, 0.4f);
    [SerializeField] private Color _negativeColor = new Color(0.9f, 0.3f, 0.2f);
    [SerializeField] private Color _neutralColor = Color.white;

    // ========================================================================
    // RUNTIME STATE
    // ========================================================================

    private float _displayedAmount = 0f;
    private float _targetAmount = 0f;
    private float _countUpStartValue = 0f;
    private float _countUpStartTime = 0f;
    private bool _isCountingUp = false;
    private Coroutine _countUpCoroutine;

    // ========================================================================
    // VÒNG ĐỜI
    // ========================================================================

    private void Awake()
    {
        if (_moneyText == null)
            _moneyText = GetComponentInChildren<TextMeshProUGUI>();

        if (ShopFloorManager.Instance != null)
        {
            _displayedAmount = ShopFloorManager.Instance.TotalMoney;
            _targetAmount = _displayedAmount;
            UpdateTextInstant();
        }
    }

    private void OnEnable()
    {
        GameEconomyEvents.OnMoneyChanged += HandleMoneyChanged;
    }

    private void OnDisable()
    {
        GameEconomyEvents.OnMoneyChanged -= HandleMoneyChanged;
    }

    private void Update()
    {
        if (_isCountingUp)
            UpdateCountUp();
    }

    // ========================================================================
    // EVENT HANDLER
    // ========================================================================

    private void HandleMoneyChanged(float previousAmount, float newAmount)
    {
        _countUpStartValue = _displayedAmount;
        _targetAmount = newAmount;
        _countUpStartTime = Time.time;

        if (_countUpCoroutine != null)
            StopCoroutine(_countUpCoroutine);

        _countUpCoroutine = StartCoroutine(CountUpCoroutine());

        SpawnChangePopup(newAmount - previousAmount);
    }

    private System.Collections.IEnumerator CountUpCoroutine()
    {
        _isCountingUp = true;

        float delta = Mathf.Abs(_targetAmount - _countUpStartValue);
        float duration = Mathf.Max(_countUpDuration, delta / 100f);

        while (Time.time - _countUpStartTime < duration)
        {
            float t = (Time.time - _countUpStartTime) / duration;
            float eased = EaseOutCubic(t);
            _displayedAmount = Mathf.Lerp(_countUpStartValue, _targetAmount, eased);
            UpdateTextWithColor(_displayedAmount, _targetAmount > _countUpStartValue);
            yield return null;
        }

        _displayedAmount = _targetAmount;
        UpdateTextWithColor(_displayedAmount, true);
        _isCountingUp = false;
    }

    private void UpdateCountUp()
    {
        float elapsed = Time.time - _countUpStartTime;
        float duration = Mathf.Max(_countUpDuration,
            Mathf.Abs(_targetAmount - _countUpStartValue) / 100f);

        if (elapsed >= duration)
        {
            _displayedAmount = _targetAmount;
            UpdateTextWithColor(_displayedAmount, _targetAmount > _countUpStartValue);
            _isCountingUp = false;
        }
        else
        {
            float t = EaseOutCubic(elapsed / duration);
            _displayedAmount = Mathf.Lerp(_countUpStartValue, _targetAmount, t);
            UpdateTextWithColor(_displayedAmount, _targetAmount > _countUpStartValue);
        }
    }

    private void UpdateTextInstant()
    {
        if (_moneyText != null)
            _moneyText.text = $"${_displayedAmount:N0}";
    }

    private void UpdateTextWithColor(float amount, bool positive)
    {
        if (_moneyText == null) return;

        _moneyText.text = $"${amount:N0}";

        if (amount > _countUpStartValue)
            _moneyText.color = _positiveColor;
        else if (amount < _countUpStartValue)
            _moneyText.color = _negativeColor;
        else
            _moneyText.color = _neutralColor;
    }

    private void SpawnChangePopup(float delta)
    {
        if (_changePopupPrefab == null || _popupAnchor == null) return;
        if (Mathf.Abs(delta) < 0.01f) return;

        GameObject popup = Instantiate(_changePopupPrefab, _popupAnchor);
        var popupText = popup.GetComponentInChildren<TextMeshProUGUI>();

        if (popupText != null)
        {
            popupText.text = delta >= 0 ? $"+${delta:N0}" : $"-${Mathf.Abs(delta):N0}";
            popupText.color = delta >= 0 ? _positiveColor : _negativeColor;
        }

        var anim = popup.AddComponent<MoneyPopupAnim>();
        anim.Initialize(_popupAnchor, delta >= 0);
    }

    private static float EaseOutCubic(float t) =>
        1f - Mathf.Pow(1f - t, 3f);
}

/// <summary>
/// Animation component cho money popup.
/// </summary>
public class MoneyPopupAnim : MonoBehaviour
{
    [SerializeField] private float _floatDistance = 50f;
    [SerializeField] private float _duration = 0.8f;

    private RectTransform _rect;
    private CanvasGroup _canvasGroup;
    private Vector2 _startPos;
    private float _elapsed;

    public void Initialize(RectTransform anchor, bool isPositive)
    {
        _rect = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _startPos = _rect.anchoredPosition;
        _elapsed = 0f;

        transform.SetParent(anchor.parent);
        _rect.anchoredPosition = _startPos;
        _rect.localScale = Vector3.one;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        float t = _elapsed / _duration;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        _rect.anchoredPosition = _startPos + Vector2.up * (_floatDistance * t);

        if (_canvasGroup != null && t > 0.5f)
            _canvasGroup.alpha = 1f - ((t - 0.5f) / 0.5f);
    }
}
