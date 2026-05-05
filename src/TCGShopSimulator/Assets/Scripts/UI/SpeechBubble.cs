// Assets/Scripts/UI/SpeechBubble.cs

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space bong bóng phản ứng lơ lửng trên đầu NPC.
/// Billboard effect — luôn quay mặt về Camera.
///
/// BILLBOARD EFFECT:
///   Mỗi frame tự xoay để luôn nhìn về Camera.
///   Quan trọng trong Isometric view — không xoay sẽ bị méo hình học.
///
/// VÒNG ĐỜI:
///   1. CustomerFSM.ShowReaction(type) → SpeechBubble.Show(type, duration)
///   2. Bong bóng xuất hiện (scale từ 0 → 1, ease out)
///   3. Lơ lửng nhẹ nhàng (sin wave y offset)
///   4. Sau duration: mờ dần (alpha 1 → 0)
///   5. Tự hủy (Destroy)
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   Không có → Tính năng MỚI HOÀN TOÀN theo yêu cầu bản Unity.
/// </summary>
public class SpeechBubble : MonoBehaviour
{
    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Icons")]
    [SerializeField] private Sprite _heartIcon;
    [SerializeField] private Sprite _neutralIcon;
    [SerializeField] private Sprite _angryIcon;

    [Header("Animation")]
    [SerializeField] private float _floatFrequency   = 1.5f;
    [SerializeField] private float _floatAmplitude  = 0.08f;
    [SerializeField] private float _popInDuration   = 0.2f;
    [SerializeField] private float _fadeOutDuration = 0.5f;
    [SerializeField] private float _verticalOffset  = 1.2f;

    // =========================================================================
    // COMPONENT REFERENCES
    // =========================================================================

    private Image _iconImage;
    private CanvasGroup _canvasGroup;
    private Camera _mainCamera;
    private Transform _followTarget;

    // =========================================================================
    // STATE
    // =========================================================================

    private float _elapsedTime;
    private bool  _isShowing;
    private Coroutine _lifetimeCoroutine;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Awake()
    {
        _iconImage   = GetComponentInChildren<Image>();
        _canvasGroup = GetComponent<CanvasGroup>();

        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogWarning("[SpeechBubble] Camera.main is null. Billboard effect disabled.");
        }

        _canvasGroup.alpha    = 0f;
        transform.localScale = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (!_isShowing) return;

        // BILLBOARD: Luôn nhìn về Camera — quan trọng cho Isometric view
        if (_mainCamera != null)
            transform.rotation = _mainCamera.transform.rotation;

        // Theo NPC nếu có followTarget
        if (_followTarget != null)
        {
            transform.position = _followTarget.position + Vector3.up * _verticalOffset;
        }

        // Lơ lửng sin wave
        _elapsedTime += Time.deltaTime;
        float yOffset = Mathf.Sin(_elapsedTime * _floatFrequency * Mathf.PI * 2f) * _floatAmplitude;
        transform.position += Vector3.up * yOffset * Time.deltaTime * 10f;
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Hiển thị bong bóng với icon tương ứng và tự hủy sau duration.
    /// GỌI TỪ: CustomerFSM sau khi EconomicDecisionEngine.DecidePurchase()
    /// </summary>
    public void Show(BubbleReactionType reactionType, Transform followTarget, float duration = 2f)
    {
        _followTarget = followTarget;
        _isShowing    = true;

        if (_iconImage != null)
        {
            _iconImage.sprite = reactionType switch
            {
                BubbleReactionType.Heart  => _heartIcon,
                BubbleReactionType.Angry  => _angryIcon,
                _                         => _neutralIcon
            };

            _iconImage.color = reactionType switch
            {
                BubbleReactionType.Heart  => Color.red,
                BubbleReactionType.Angry  => new Color(1f, 0.3f, 0.1f),
                _                         => Color.gray
            };
        }

        if (_lifetimeCoroutine != null)
            StopCoroutine(_lifetimeCoroutine);

        _lifetimeCoroutine = StartCoroutine(LifetimeRoutine(duration));
    }

    // =========================================================================
    // ANIMATION COROUTINE
    // =========================================================================

    private IEnumerator LifetimeRoutine(float displayDuration)
    {
        // Pop-in: scale từ 0 → 1
        float elapsed = 0f;
        while (elapsed < _popInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _popInDuration;
            float eased = 1f - Mathf.Pow(1f - t, 3f);  // Ease out cubic
            transform.localScale = Vector3.one * eased;
            _canvasGroup.alpha  = eased;
            yield return null;
        }

        transform.localScale = Vector3.one;
        _canvasGroup.alpha  = 1f;

        // Hiển thị trong displayDuration
        yield return new WaitForSeconds(displayDuration);

        // Fade out: alpha từ 1 → 0
        elapsed = 0f;
        while (elapsed < _fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _fadeOutDuration;
            _canvasGroup.alpha = 1f - t;
            transform.position += Vector3.up * (0.3f * t * Time.deltaTime);
            yield return null;
        }

        Destroy(gameObject);
    }
}
