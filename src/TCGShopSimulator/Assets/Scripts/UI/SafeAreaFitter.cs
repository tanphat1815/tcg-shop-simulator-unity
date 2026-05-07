// Assets/Scripts/UI/SafeAreaFitter.cs
using UnityEngine;

/// <summary>
/// Pads a RectTransform to respect Screen.safeArea, keeping content
/// out of the notch, Dynamic Island, and Home Indicator on iOS/Android.
///
/// Attach to:
///   - Canvas root (applies to all children)
///   - Individual panel (applies selectively)
///
/// The component converts safe-area pixels into normalized anchor coordinates
/// and stretches the RectTransform to fill only the safe area.
/// </summary>
public class SafeAreaFitter : MonoBehaviour
{
    [Header("Safe Area Edges")]
    [Tooltip("Apply safe area to the top edge (notch/Dynamic Island).")]
    [SerializeField] private bool _applyTop = true;

    [Tooltip("Apply safe area to the bottom edge (Home Indicator).")]
    [SerializeField] private bool _applyBottom = true;

    [Tooltip("Apply safe area to the left edge (rounded corner).")]
    [SerializeField] private bool _applyLeft = true;

    [Tooltip("Apply safe area to the right edge (rounded corner).")]
    [SerializeField] private bool _applyRight = true;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = false;

    // Exposed as properties so other scripts can modify them cleanly
    public bool ApplyTop    { get => _applyTop;    set { _applyTop = value; } }
    public bool ApplyBottom { get => _applyBottom; set { _applyBottom = value; } }
    public bool ApplyLeft   { get => _applyLeft;   set { _applyLeft = value; } }
    public bool ApplyRight  { get => _applyRight;  set { _applyRight = value; } }

    private RectTransform _rect;
    private Rect _lastSafeArea;
    private bool _initialized;

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        if (_rect == null)
        {
            Debug.LogError("[SafeAreaFitter] RectTransform not found!");
            return;
        }
        _initialized = true;
        ApplySafeArea();
    }

    private void OnEnable()
    {
        ApplySafeArea();
    }

    /// <summary>
    /// Called by Unity when the RectTransform dimensions change,
    /// which fires on orientation changes and safe-area updates.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        ApplySafeArea();
    }

    // ─────────────────────────────────────────────────────────────────
    // Core Logic
    // ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Converts Screen.safeArea to normalized anchor coordinates and
    /// applies them to the RectTransform, respecting per-edge flags.
    ///
    /// Algorithm:
    ///   anchorMin = safeArea.position / screenSize
    ///   anchorMax = (safeArea.position + safeArea.size) / screenSize
    /// </summary>
    public void ApplySafeArea()
    {
        if (!_initialized || _rect == null) return;

        Rect safeArea = Screen.safeArea;

        if (safeArea == _lastSafeArea) return;
        _lastSafeArea = safeArea;

        // Convert to normalized (0-1) anchor space
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // Respect per-edge toggles
        if (!_applyLeft)   anchorMin.x = 0f;
        if (!_applyRight)  anchorMax.x = 1f;
        if (!_applyBottom) anchorMin.y = 0f;
        if (!_applyTop)    anchorMax.y = 1f;

        _rect.anchorMin = anchorMin;
        _rect.anchorMax = anchorMax;

        // Reset offsets so anchors fully control position/size
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;

        if (_verboseLogging)
        {
            Debug.Log($"[SafeAreaFitter] Applied: " +
                     $"x={safeArea.x:F0} y={safeArea.y:F0} " +
                     $"w={safeArea.width:F0} h={safeArea.height:F0}\n" +
                     $"  AnchorMin: {anchorMin:F3}  AnchorMax: {anchorMax:F3}");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Editor Support
    // ─────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && _rect != null)
                ApplySafeArea();
        };
    }
#endif
}

/// <summary>
/// Convenience component for Canvas root: ensures every child respects safe area.
/// Attach to the Canvas root GameObject.
/// </summary>
public class GlobalSafeAreaManager : MonoBehaviour
{
    private SafeAreaFitter _rootFitter;

    private void Awake()
    {
        _rootFitter = GetComponent<SafeAreaFitter>();
        if (_rootFitter == null)
            _rootFitter = gameObject.AddComponent<SafeAreaFitter>();

        Debug.Log($"[GlobalSafeAreaManager] Safe Area: {Screen.safeArea}");
    }
}
