// Assets/Scripts/UI/CanvasSetup.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Configures Canvas Scaler to the responsive standard: Scale With Screen Size,
/// 1920x1080 reference, Match Width Or Height at 0.5.
/// Attach to the root Canvas.
/// </summary>
public class CanvasSetup : MonoBehaviour
{
    public const float REFERENCE_WIDTH = 1920f;
    public const float REFERENCE_HEIGHT = 1080f;

    [Header("Scaler Settings")]
    [Tooltip("Reference resolution. Design target for UI layout.")]
    [SerializeField] private Vector2 _referenceResolution = new Vector2(REFERENCE_WIDTH, REFERENCE_HEIGHT);

    [Tooltip("0=match width, 1=match height, 0.5=blend equal (recommended).")]
    [Range(0f, 1f)]
    [SerializeField] private float _matchWidthOrHeight = 0.5f;

    [Header("Physical Size (Debug)")]
    [SerializeField] private bool _showPhysicalSize = false;

    private Canvas _canvas;
    private CanvasScaler _scaler;
    private GraphicRaycaster _raycaster;

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        _scaler = GetComponent<CanvasScaler>();
        _raycaster = GetComponent<GraphicRaycaster>();

        if (_canvas == null)
        {
            Debug.LogError("[CanvasSetup] Canvas component not found!");
            return;
        }

        AutoConfigure();
        Debug.Log($"[CanvasSetup] Configured: {Screen.width}x{Screen.height}, SafeArea: {Screen.safeArea}");
    }

    private void Start()
    {
    }

    private void OnDestroy()
    {
    }

    private void OnWantsToFullScreenModeChanged()
    {
        Debug.Log($"[CanvasSetup] Fullscreen changed, re-configuring...");
        AutoConfigure();
    }

    // ─────────────────────────────────────────────────────────────────
    // Auto-Configure
    // ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Applies the responsive Canvas Scaler configuration.
    /// Call from Awake and whenever the resolution changes.
    /// </summary>
    public void AutoConfigure()
    {
        // Canvas
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.pixelPerfect = false;
        _canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;

        // Canvas Scaler
        if (_scaler == null)
            _scaler = gameObject.AddComponent<CanvasScaler>();

        _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _scaler.referenceResolution = _referenceResolution;
        _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        _scaler.matchWidthOrHeight = _matchWidthOrHeight;
        _scaler.physicalUnit = CanvasScaler.Unit.Points;
        _scaler.fallbackScreenDPI = 96f;
        _scaler.defaultSpriteDPI = 96f;
        _scaler.dynamicPixelsPerUnit = 1f;

        // Graphic Raycaster
        if (_raycaster == null)
            _raycaster = gameObject.AddComponent<GraphicRaycaster>();
        _raycaster.ignoreReversedGraphics = true;
        _raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.All;

        Debug.Log($"[CanvasSetup] Canvas Scaler:\n" +
                   $"  Mode: Scale With Screen Size\n" +
                   $"  Reference: {_referenceResolution.x}x{_referenceResolution.y}\n" +
                   $"  Match: {_matchWidthOrHeight:P0} (0=width, 1=height)\n" +
                   $"  Current Scale: {GetCurrentUIGraphicsScale():F4}x\n" +
                   $"  Screen: {Screen.width}x{Screen.height} @ {Screen.dpi:F0} DPI");
    }

    // ─────────────────────────────────────────────────────────────────
    // Utility
    // ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Returns the current UI scale factor so callers can convert
    /// screen-pixel positions to Canvas-local positions.
    /// </summary>
    public float GetCurrentUIGraphicsScale()
    {
        if (_scaler == null) return 1f;

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        if (_scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
        {
            float logW = Mathf.Log(screenWidth / _scaler.referenceResolution.x,
                _scaler.matchWidthOrHeight < 0.5f ? 1.5f : 2f);
            float logH = Mathf.Log(screenHeight / _scaler.referenceResolution.y,
                _scaler.matchWidthOrHeight > 0.5f ? 1.5f : 2f);
            float logAvg = Mathf.Lerp(logW, logH, _scaler.matchWidthOrHeight);
            return Mathf.Max(Mathf.Pow(2f, logAvg), 0.0001f);
        }
        return 1f;
    }

    /// <summary>
    /// Converts a screen-space pixel position to Canvas local space.
    /// Use for mapping mouse/touch input to UI positions.
    /// </summary>
    public Vector2 ScreenToCanvasPosition(Vector2 screenPos)
    {
        if (_canvas == null) return screenPos;
        RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPos, null, out Vector2 localPos);
        return localPos;
    }

    /// <summary>
    /// Returns true for extreme aspect ratios (ultrawide > 2.0 or portrait < 0.5).
    /// </summary>
    public bool IsExtremeAspectRatio()
    {
        float ratio = (float)Screen.width / Screen.height;
        return ratio > 2.0f || ratio < 0.5f;
    }

    // ─────────────────────────────────────────────────────────────────
    // Editor Menu
    // ─────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("Auto-Configure All Canvases")]
    private void AutoConfigureAllCanvases()
    {
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        int count = 0;
        foreach (var canvas in allCanvases)
        {
            var setup = canvas.GetComponent<CanvasSetup>();
            if (setup == null)
            {
                setup = canvas.gameObject.AddComponent<CanvasSetup>();
                UnityEditor.EditorUtility.SetDirty(canvas);
                count++;
            }
            setup.AutoConfigure();
        }
        Debug.Log($"[CanvasSetup] Auto-configured {count} canvas(es).");
    }
#endif
}
