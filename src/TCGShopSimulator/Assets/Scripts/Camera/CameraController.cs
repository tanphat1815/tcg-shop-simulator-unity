// Assets/Scripts/Camera/CameraController.cs

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Điều khiển Camera 2D Orthographic cho Isometric view.
/// Hỗ trợ đầy đủ Desktop (chuột) và Mobile (cảm ứng).
/// 
/// DESKTOP:
///   - Nhấn giữ chuột giữa (hoặc chuột phải) + kéo → Pan
///   - Cuộn bánh xe chuột → Zoom
///
/// MOBILE:
///   - Chạm một ngón + kéo → Pan  
///   - Pinch hai ngón → Zoom
///
/// THIẾT KẾ: 
///   - Không dùng GameObject.Find() trong Update().
///   - Camera reference được cache tại Awake().
///   - Tất cả tính toán dùng world-space để tránh lỗi khi resolution thay đổi.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    // =========================================================================
    // INSPECTOR SETTINGS
    // =========================================================================

    [Header("Pan Settings")]
    [Tooltip("Tốc độ pan cơ bản. Giá trị càng lớn, camera di chuyển càng nhanh theo ngón tay/chuột.")]
    [SerializeField] private float panSpeed = 1f;

    [Tooltip("Độ mượt của pan. 0 = không mượt (immediate), 1 = không bao giờ đến đích. Khuyến nghị: 0.08-0.15")]
    [SerializeField] [Range(0.01f, 0.5f)] private float panSmoothTime = 0.1f;

    [Header("Zoom Settings")]
    [Tooltip("Kích thước Orthographic Size tối thiểu (phóng to tối đa).")]
    [SerializeField] private float minZoom = 2f;

    [Tooltip("Kích thước Orthographic Size tối đa (thu nhỏ tối đa).")]
    [SerializeField] private float maxZoom = 10f;

    [Tooltip("Tốc độ zoom khi cuộn chuột. Giá trị âm để đảo chiều.")]
    [SerializeField] private float mouseZoomSpeed = 1f;

    [Tooltip("Tốc độ zoom khi pinch mobile.")]
    [SerializeField] private float pinchZoomSpeed = 0.05f;

    [Tooltip("Độ mượt của zoom. Khuyến nghị: 0.08-0.2")]
    [SerializeField] [Range(0.01f, 0.5f)] private float zoomSmoothTime = 0.12f;

    [Header("World Bounds")]
    [Tooltip("Bật/tắt giới hạn camera trong thế giới game.")]
    [SerializeField] private bool useBounds = true;

    [Tooltip("Vùng thế giới mà camera được phép nhìn. Đặt theo kích thước map.")]
    [SerializeField] private Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(50f, 50f, 0f));

    // =========================================================================
    // PRIVATE STATE — Không serialize, quản lý nội bộ
    // =========================================================================

    private Camera _camera;

    // Pan state
    private Vector3 _targetPosition;
    private Vector3 _panVelocity;
    private Vector3 _lastPanWorldPosition;
    private bool _isPanning;

    // Zoom state  
    private float _targetOrthographicSize;
    private float _zoomVelocity;
    private float _lastPinchDistance;

    // Input device references
    private Mouse _mouse;
    private Keyboard _keyboard;

    // =========================================================================
    // VÒNG ĐỜI UNITY
    // =========================================================================

    private void Awake()
    {
        _camera = GetComponent<Camera>();

        if (_camera == null)
        {
            Debug.LogError("[CameraController] Không tìm thấy Camera component! " +
                           "CameraController phải được gắn vào GameObject có Camera.");
            enabled = false;
            return;
        }

        if (!_camera.orthographic)
        {
            Debug.LogWarning("[CameraController] Camera không phải Orthographic. Đang chuyển đổi...");
            _camera.orthographic = true;
        }

        _targetPosition = transform.position;
        _targetOrthographicSize = _camera.orthographicSize;
        _targetOrthographicSize = Mathf.Clamp(_targetOrthographicSize, minZoom, maxZoom);

        _mouse = Mouse.current;
        _keyboard = Keyboard.current;
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        if (_mouse == null) _mouse = Mouse.current;
        if (_keyboard == null) _keyboard = Keyboard.current;

        HandleDesktopInput();
        HandleMobileInput();
        ApplySmoothMovement();
    }

    // =========================================================================
    // XỬ LÝ INPUT DESKTOP (Chuột)
    // =========================================================================

    private void HandleDesktopInput()
    {
        if (_mouse == null) return;
        HandleMousePan();
        HandleMouseZoom();
    }

    private void HandleMousePan()
    {
        bool panButtonPressed = _mouse.middleButton.isPressed || _mouse.rightButton.isPressed;

        if (panButtonPressed)
        {
            if (!_isPanning)
            {
                _isPanning = true;
                _lastPanWorldPosition = ScreenToWorldPosition(_mouse.position.ReadValue());
            }
            else
            {
                Vector3 currentWorldPosition = ScreenToWorldPosition(_mouse.position.ReadValue());
                Vector3 worldDelta = currentWorldPosition - _lastPanWorldPosition;

                _targetPosition -= worldDelta * panSpeed;

                if (useBounds)
                    _targetPosition = ClampPositionToBounds(_targetPosition);

                _lastPanWorldPosition = currentWorldPosition;
            }
        }
        else
        {
            _isPanning = false;
        }
    }

    private void HandleMouseZoom()
    {
        if (_mouse == null) return;

        float scrollValue = _mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollValue, 0f)) return;

        Vector3 mouseWorldBefore = ScreenToWorldPosition(_mouse.position.ReadValue());

        float zoomDelta = -scrollValue * mouseZoomSpeed * 0.1f;
        _targetOrthographicSize += zoomDelta;
        _targetOrthographicSize = Mathf.Clamp(_targetOrthographicSize, minZoom, maxZoom);

        float previousSize = _camera.orthographicSize;
        _camera.orthographicSize = _targetOrthographicSize;
        Vector3 mouseWorldAfter = ScreenToWorldPosition(_mouse.position.ReadValue());
        _camera.orthographicSize = previousSize;

        _targetPosition += mouseWorldBefore - mouseWorldAfter;

        if (useBounds)
            _targetPosition = ClampPositionToBounds(_targetPosition);
    }

    // =========================================================================
    // XỬ LÝ INPUT MOBILE (Cảm ứng)
    // =========================================================================

    private void HandleMobileInput()
    {
        var activeTouches = Touch.activeTouches;

        switch (activeTouches.Count)
        {
            case 1:
                HandleSingleTouchPan(activeTouches[0]);
                break;

            case 2:
                HandlePinchZoom(activeTouches[0], activeTouches[1]);
                break;

            default:
                _lastPinchDistance = 0f;
                if (activeTouches.Count == 0)
                    _isPanning = false;
                break;
        }
    }

    private void HandleSingleTouchPan(Touch touch)
    {
        _lastPinchDistance = 0f;

        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            _isPanning = true;
            _lastPanWorldPosition = ScreenToWorldPosition(touch.screenPosition);
        }
        else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved && _isPanning)
        {
            Vector3 currentWorldPosition = ScreenToWorldPosition(touch.screenPosition);
            Vector3 worldDelta = currentWorldPosition - _lastPanWorldPosition;

            _targetPosition -= worldDelta * panSpeed;

            if (useBounds)
                _targetPosition = ClampPositionToBounds(_targetPosition);

            _lastPanWorldPosition = currentWorldPosition;
        }
        else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                 touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
        {
            _isPanning = false;
        }
    }

    private void HandlePinchZoom(Touch touch0, Touch touch1)
    {
        _isPanning = false;

        float currentPinchDistance = Vector2.Distance(touch0.screenPosition, touch1.screenPosition);

        if (_lastPinchDistance <= 0f)
        {
            _lastPinchDistance = currentPinchDistance;
            return;
        }

        float pinchDelta = currentPinchDistance - _lastPinchDistance;
        float zoomChange = -pinchDelta * pinchZoomSpeed;
        _targetOrthographicSize += zoomChange;
        _targetOrthographicSize = Mathf.Clamp(_targetOrthographicSize, minZoom, maxZoom);

        if (useBounds)
            _targetPosition = ClampPositionToBounds(_targetPosition);

        _lastPinchDistance = currentPinchDistance;
    }

    // =========================================================================
    // ÁP DỤNG CHUYỂN ĐỘNG MƯỢT
    // =========================================================================

    private void ApplySmoothMovement()
    {
        Vector3 currentPos = transform.position;
        Vector3 newPos = Vector3.SmoothDamp(currentPos, _targetPosition, ref _panVelocity, panSmoothTime);
        newPos.z = -10f;
        transform.position = newPos;

        _camera.orthographicSize = Mathf.SmoothDamp(
            _camera.orthographicSize,
            _targetOrthographicSize,
            ref _zoomVelocity,
            zoomSmoothTime
        );
    }

    // =========================================================================
    // TIỆN ÍCH TÍNH TOÁN
    // =========================================================================

    private Vector3 ScreenToWorldPosition(Vector2 screenPosition)
    {
        Vector3 screenPos3D = new Vector3(screenPosition.x, screenPosition.y, 0f);
        return _camera.ScreenToWorldPoint(screenPos3D);
    }

    private Vector3 ClampPositionToBounds(Vector3 position)
    {
        float cameraHalfHeight = _camera.orthographicSize;
        float cameraHalfWidth = cameraHalfHeight * _camera.aspect;

        float minX = worldBounds.min.x + cameraHalfWidth;
        float maxX = worldBounds.max.x - cameraHalfWidth;
        float minY = worldBounds.min.y + cameraHalfHeight;
        float maxY = worldBounds.max.y - cameraHalfHeight;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);

        return position;
    }

    // =========================================================================
    // API CÔNG KHAI
    // =========================================================================

    public void FocusOn(Vector3 worldPosition, float duration = 0.5f)
    {
        _targetPosition = new Vector3(worldPosition.x, worldPosition.y, -10f);
        if (useBounds)
            _targetPosition = ClampPositionToBounds(_targetPosition);
    }

    public void SetZoomImmediate(float size)
    {
        float clampedSize = Mathf.Clamp(size, minZoom, maxZoom);
        _camera.orthographicSize = clampedSize;
        _targetOrthographicSize = clampedSize;
    }

    // =========================================================================
    // DEBUG GIZMOS (Chỉ hiển thị trong Editor)
    // =========================================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!useBounds) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(worldBounds.center, worldBounds.size);

        if (_camera != null)
        {
            Gizmos.color = Color.yellow;
            float h = _camera.orthographicSize;
            float w = h * _camera.aspect;
            Gizmos.DrawWireCube(transform.position, new Vector3(w * 2f, h * 2f, 0f));
        }
    }
#endif
}
