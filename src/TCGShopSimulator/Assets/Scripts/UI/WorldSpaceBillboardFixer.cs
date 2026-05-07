// Assets/Scripts/UI/WorldSpaceBillboardFixer.cs
using UnityEngine;

/// <summary>
/// Keeps a World-Space Canvas facing the main camera each frame.
/// Prevents geometry distortion in isometric views by forcing the canvas
/// to mirror the camera's rotation (billboard effect).
///
/// Attach to:
///   - Any Canvas with Render Mode = World Space
///   - (e.g. price tags, shelf labels, item info popups)
/// </summary>
public class WorldSpaceBillboardFixer : MonoBehaviour
{
    [Header("Camera Target")]
    [Tooltip("Camera to face. Uses Camera.main if null.")]
    [SerializeField] private Camera _targetCamera;

    [Header("Behavior")]
    [Tooltip("Enable/disable billboard at runtime.")]
    [SerializeField] private bool _isActive = true;

    [Tooltip("Smooth rotation transitions using Lerp/SmoothDamp.")]
    [SerializeField] private bool _useSmoothing = false;

    [Tooltip("Lerp speed when smoothing is enabled.")]
    [SerializeField] [Range(1f, 20f)]
    private float _lerpSpeed = 10f;

    [Tooltip("Use SmoothDamp instead of Lerp for smoother motion.")]
    [SerializeField] private bool _useSmoothDamp = false;

    [Header("Z-Fight Prevention")]
    [Tooltip("Slight Z offset to prevent z-fighting with world objects.")]
    [SerializeField] private float _zOffset = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = false;

    private Quaternion _currentRotation;
    private Quaternion _targetRotation;
    private Vector3 _smoothVelocity;
    private float _lastDistance = float.MaxValue;

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (_targetCamera == null)
            _targetCamera = Camera.main;

        if (_targetCamera == null)
            Debug.LogWarning("[WorldSpaceBillboardFixer] No target camera found!");

        _currentRotation = transform.rotation;
    }

    private void Start()
    {
        if (_targetCamera != null)
            _targetRotation = _targetCamera.transform.rotation;
    }

    private void LateUpdate()
    {
        if (!_isActive || _targetCamera == null) return;

        // Skip expensive update if distance hasn't changed significantly and no smoothing
        float distance = Vector3.Distance(transform.position, _targetCamera.transform.position);
        if (Mathf.Abs(distance - _lastDistance) < 0.01f && !_useSmoothing) return;
        _lastDistance = distance;

        _targetRotation = _targetCamera.transform.rotation;

        // Horizontal-only billboard: only Y rotation follows camera
        // This prevents UI from flipping upside-down when camera is above
        Quaternion horizontalOnly = Quaternion.Euler(0f, _targetRotation.eulerAngles.y, 0f);

        if (_useSmoothing)
        {
            if (_useSmoothDamp)
            {
                _currentRotation = Quaternion.SmoothDamp(
                    _currentRotation, horizontalOnly,
                    ref _smoothVelocity, 1f / _lerpSpeed);
            }
            else
            {
                _currentRotation = Quaternion.Lerp(
                    _currentRotation, horizontalOnly,
                    Time.deltaTime * _lerpSpeed);
            }
            transform.rotation = _currentRotation;
        }
        else
        {
            transform.rotation = horizontalOnly;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────
    public void Enable() => _isActive = true;
    public void Disable() => _isActive = false;
    public bool IsActive => _isActive;

    public void SetCamera(Camera newCamera)
    {
        _targetCamera = newCamera;
    }

    public void ResetRotation()
    {
        _currentRotation = Quaternion.identity;
        _targetRotation = Quaternion.identity;
        transform.rotation = Quaternion.identity;
    }
}
