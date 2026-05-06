// Assets/Scripts/Player/PlayerRaycastHandler.cs

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Xử lý raycast từ player camera để tương tác với kệ hàng.
/// Gọi ShelfInstance.NotifyInteracted() khi player nhấn E gần kệ.
///
/// Sử dụng UnityEngine.InputSystem (New Input System).
/// </summary>
public class PlayerRaycastHandler : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private float _maxRayDistance = 10f;
    [SerializeField] private LayerMask _targetLayerMask;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = false;

    private ShelfInstance _targetShelf;
    private bool _isNearShelf = false;
    private Vector2 _lastMousePosition;

    private void Awake()
    {
        if (_playerCamera == null)
            _playerCamera = Camera.main;
    }

    private void Update()
    {
        CheckShelfProximity();

        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame &&
            _isNearShelf && _targetShelf != null)
        {
            InteractWithShelf(_targetShelf);
        }
    }

    private void CheckShelfProximity()
    {
        if (_playerCamera == null) return;

        Vector2 mousePos = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        _lastMousePosition = mousePos;

        Ray ray = _playerCamera.ScreenPointToRay(mousePos);
        RaycastHit2D hit = Physics2D.Raycast(
            (Vector2)ray.origin,
            (Vector2)ray.direction,
            _maxRayDistance,
            _targetLayerMask
        );

        if (hit.collider != null)
        {
            var shelf = hit.collider.GetComponent<ShelfInstance>();
            if (shelf != null)
            {
                _targetShelf = shelf;
                _isNearShelf = true;

                if (_verboseLogging)
                    Debug.Log($"[PlayerRaycast] Near shelf: {shelf.name}");
                return;
            }
        }

        _targetShelf = null;
        _isNearShelf = false;
    }

    private void InteractWithShelf(ShelfInstance shelf)
    {
        if (ShelfManagementUI.Instance != null && ShelfManagementUI.Instance.IsVisible)
        {
            ShelfManagementUI.Instance.Hide();
            return;
        }

        shelf.NotifyInteracted();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_playerCamera == null) return;

        Vector3 mousePos = Mouse.current != null
            ? (Vector3)Mouse.current.position.ReadValue()
            : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

        Ray ray = _playerCamera.ScreenPointToRay(mousePos);
        Gizmos.color = _isNearShelf ? Color.green : Color.red;
        Gizmos.DrawRay(ray.origin, ray.direction * _maxRayDistance);
    }
#endif
}
