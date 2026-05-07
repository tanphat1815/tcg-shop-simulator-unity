// Assets/Scripts/Player/PlayerRaycastHandler.cs

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Xử lý raycast từ player camera để tương tác với kệ hàng và delivery boxes.
///
/// INTERACTION PROTOCOL:
///   1. DeliveryBox? → Pickup (nếu không carrying)
///   2. ShelfInstance? → Deposit (nếu carrying) | ManageShelf (nếu không carrying)
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

    // State
    private ShelfInstance _targetShelf;
    private DeliveryBox _targetBox;
    private bool _isNearInteractable = false;
    private Vector2 _lastMousePosition;

    private void Awake()
    {
        if (_playerCamera == null)
            _playerCamera = Camera.main;
    }

    private void Update()
    {
        CheckInteractableProximity();

        // [E] — Interact with shelf (deposit if carrying, manage if idle)
        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame &&
            _isNearInteractable)
        {
            HandlePrimaryInteraction();
        }

        // [G] — Drop carried item
        if (Keyboard.current != null &&
            Keyboard.current.gKey.wasPressedThisFrame &&
            PlayerInventoryState.Instance != null &&
            PlayerInventoryState.Instance.IsCarrying)
        {
            PlayerInventoryState.Instance.DropCarrying();
        }
    }

    private void CheckInteractableProximity()
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

        _targetShelf = null;
        _targetBox = null;
        _isNearInteractable = false;

        if (hit.collider != null)
        {
            // Priority 1: DeliveryBox (only if NOT carrying)
            if (PlayerInventoryState.Instance == null || !PlayerInventoryState.Instance.IsCarrying)
            {
                var box = hit.collider.GetComponent<DeliveryBox>();
                if (box != null)
                {
                    _targetBox = box;
                    _isNearInteractable = true;
                    if (_verboseLogging)
                        Debug.Log($"[PlayerRaycast] Near delivery box: {box.name}");
                    return;
                }
            }

            // Priority 2: ShelfInstance
            var shelf = hit.collider.GetComponent<ShelfInstance>();
            if (shelf != null)
            {
                _targetShelf = shelf;
                _isNearInteractable = true;
                if (_verboseLogging)
                    Debug.Log($"[PlayerRaycast] Near shelf: {shelf.name}");
                return;
            }
        }
    }

    /// <summary>
    /// Xử lý tất cả các loại tương tác theo priority:
    ///   1. DeliveryBox → Pickup
    ///   2. ShelfInstance (carrying) → Deposit
    ///   3. ShelfInstance (idle) → Open ShelfManagementUI
    /// </summary>
    private void HandlePrimaryInteraction()
    {
        // 1. DeliveryBox → PICKUP
        if (_targetBox != null)
        {
            if (PlayerInventoryState.Instance == null)
            {
                Debug.LogWarning("[PlayerRaycast] PlayerInventoryState.Instance is null!");
                return;
            }

            if (PlayerInventoryState.Instance.IsCarrying)
            {
                Debug.Log("[PlayerRaycast] Already carrying a box. Cannot pick up another.");
                return;
            }

            PlayerInventoryState.Instance.PickUpBox(
                _targetBox.ItemId,
                _targetBox.Quantity,
                _targetBox.BoxId
            );

            _targetBox.OnPickedUp();
            DeliveryManager.Instance?.OnBoxPickedUp(_targetBox.BoxId);
            DeliveryEvents.FireBoxPickedUp(_targetBox);
            return;
        }

        // 2. ShelfInstance → DEPOSIT or MANAGE
        if (_targetShelf != null)
        {
            if (PlayerInventoryState.Instance != null && PlayerInventoryState.Instance.IsCarrying)
            {
                // DEPOSIT to shelf
                var carried = PlayerInventoryState.Instance.CurrentCarrying.Value;

                bool success = PlayerInventoryState.Instance.DepositToShelf(_targetShelf);
                if (success)
                {
                    DeliveryEvents.FireBoxDeposited(null, _targetShelf);
                }
            }
            else
            {
                // MANAGE shelf
                _targetShelf.NotifyInteracted();
            }
            return;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_playerCamera == null) return;

        Vector3 mousePos = Mouse.current != null
            ? (Vector3)Mouse.current.position.ReadValue()
            : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

        Ray ray = _playerCamera.ScreenPointToRay(mousePos);
        Gizmos.color = _isNearInteractable ? Color.green : Color.red;
        Gizmos.DrawRay(ray.origin, ray.direction * _maxRayDistance);
    }
#endif
}
