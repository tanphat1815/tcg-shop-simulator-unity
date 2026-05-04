// Assets/Scripts/Placement/PlacementManager.cs

using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Quan ly toan bo luong dat noi that (Build Mode).
///
/// STATE MACHINE:
///   IDLE
///     ↓ StartPlacement(definition)
///   PLACING
///     - Moi frame: UpdateGhostPreview(mousePos)
///     - Phim R:    ghost.Rotate()
///     - Click:     TryConfirmPlacement()
///       ↓ Valid    -> ConfirmPlacement() -> IDLE
///       ↓ Invalid  -> Log warning -> tiep tuc PLACING
///     - ESC/RClick: CancelPlacement() -> IDLE
///
/// INPUT SYSTEM: Unity New Input System (khong dung Input.* cu)
/// </summary>
public class PlacementManager : MonoBehaviour
{
    // SINGLETON
    public static PlacementManager Instance { get; private set; }

    [Header("Placement Settings")]
    [Tooltip("Layer mask cho Raycast tim vi tri chuot tren world plane.")]
    [SerializeField] private LayerMask groundLayerMask;

    [Tooltip("Camera chinh. Duoc cache trong Awake.")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("Cooldown toi thieu giua hai lan placement (giay).")]
    [SerializeField][Range(0.1f, 1f)] private float placementCooldown = 0.3f;

    [Header("Ghost Settings")]
    [Tooltip("Parent Transform de chua ghost object.")]
    [SerializeField] private Transform ghostParent;

    [Header("Furniture Parent")]
    [Tooltip("Parent Transform de chua tat ca placed furniture instances.")]
    [SerializeField] private Transform furnitureParent;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    // PLACEMENT STATE
    public enum PlacementState { Idle, Placing }
    public PlacementState CurrentState { get; private set; } = PlacementState.Idle;

    private FurnitureDefinition _activeFurnitureDefinition;
    private GhostObject _activeGhost;
    private float _lastPlacementTime = -999f;

    private Mouse _mouse;
    private Keyboard _keyboard;

    // EVENTS
    public event Action<PlacedFurnitureInstance> OnFurniturePlaced;
    public event Action OnPlacementCancelled;
    public event Action<string> OnPlacementFailed;

    // LIFECYCLE
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("[PlacementManager] mainCamera khong tim thay!");
            enabled = false;
            return;
        }

        _mouse = Mouse.current;
        _keyboard = Keyboard.current;

        Debug.Log("[PlacementManager] Initialized.");
    }

    private void Update()
    {
        if (_mouse == null) _mouse = Mouse.current;
        if (_keyboard == null) _keyboard = Keyboard.current;

        switch (CurrentState)
        {
            case PlacementState.Idle:
                break;
            case PlacementState.Placing:
                HandlePlacingState();
                break;
        }
    }

    // STATE HANDLERS
    private void HandlePlacingState()
    {
        if (_activeGhost == null || _mouse == null) return;

        Vector3 mouseWorldPosition = GetMouseWorldPosition();
        if (mouseWorldPosition == Vector3.negativeInfinity) return;

        _activeGhost.UpdatePreview(mouseWorldPosition);

        if (_keyboard != null && _keyboard.rKey.wasPressedThisFrame)
            HandleRotation();

        if (_mouse.leftButton.wasPressedThisFrame)
        {
            if (Time.time - _lastPlacementTime >= placementCooldown)
                TryConfirmPlacement();
        }

        if (_mouse.rightButton.wasPressedThisFrame ||
            (_keyboard != null && _keyboard.escapeKey.wasPressedThisFrame))
        {
            CancelPlacement();
        }
    }

    // PUBLIC API
    public void StartPlacement(FurnitureDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogError("[PlacementManager] StartPlacement: definition la null!");
            return;
        }

        if (!definition.IsValid())
        {
            Debug.LogError($"[PlacementManager] StartPlacement: Definition '{definition.name}' khong hop le.");
            return;
        }

        if (CurrentState == PlacementState.Placing)
            CancelPlacement();

        _activeFurnitureDefinition = definition;
        SpawnGhost(definition);
        CurrentState = PlacementState.Placing;

        if (verboseLogging)
            Debug.Log($"[PlacementManager] Started placement: {definition.furnitureType} " +
                     $"({definition.footprintWidth}x{definition.footprintHeight}).");
    }

    public void CancelPlacement()
    {
        DestroyGhost();
        _activeFurnitureDefinition = null;
        CurrentState = PlacementState.Idle;

        OnPlacementCancelled?.Invoke();

        if (verboseLogging)
            Debug.Log("[PlacementManager] Placement cancelled.");
    }

    // PLACEMENT EXECUTION
    private void TryConfirmPlacement()
    {
        if (_activeGhost == null || _activeFurnitureDefinition == null) return;

        Vector2Int targetCell = _activeGhost.CurrentCellPosition;
        int rotation = _activeGhost.CurrentRotation;

        bool isValid = GridSystem.Instance.ValidatePlacement(
            targetCell,
            _activeFurnitureDefinition,
            rotation,
            out string failReason
        );

        if (!isValid)
        {
            Debug.LogWarning($"[PlacementManager] {failReason}");
            OnPlacementFailed?.Invoke(failReason);
            return;
        }

        ConfirmPlacement(targetCell, rotation);
    }

    private void ConfirmPlacement(Vector2Int targetCell, int rotation)
    {
        string instanceId = GenerateInstanceId(_activeFurnitureDefinition.furnitureType);
        Vector3 worldPosition = GridSystem.Instance.CellToWorld(targetCell);

        Transform parent = furnitureParent != null ? furnitureParent : transform;
        GameObject furnitureGO = Instantiate(
            _activeFurnitureDefinition.furniturePrefab,
            worldPosition,
            Quaternion.identity,
            parent
        );

        PlacedFurnitureInstance instance = furnitureGO.GetComponent<PlacedFurnitureInstance>();
        if (instance == null)
        {
            instance = furnitureGO.AddComponent<PlacedFurnitureInstance>();
            Debug.LogWarning($"[PlacementManager] Prefab '{_activeFurnitureDefinition.name}' " +
                             "thieu PlacedFurnitureInstance component. Da auto-add.");
        }

        instance.Initialize(instanceId, _activeFurnitureDefinition, targetCell, rotation);

        GridSystem.Instance.ConfirmPlacement(
            targetCell,
            _activeFurnitureDefinition,
            rotation,
            instanceId
        );

        _lastPlacementTime = Time.time;
        OnFurniturePlaced?.Invoke(instance);

        if (verboseLogging)
            Debug.Log($"[PlacementManager] Placed {instance}");

        DestroyGhost();
        _activeFurnitureDefinition = null;
        CurrentState = PlacementState.Idle;
    }

    // ROTATION
    private void HandleRotation()
    {
        if (_activeGhost == null) return;

        if (!_activeFurnitureDefinition.canRotate)
        {
            Debug.Log($"[PlacementManager] {_activeFurnitureDefinition.furnitureType} " +
                      "khong the xoay (canRotate = false).");
            return;
        }

        _activeGhost.Rotate();

        Debug.Log($"[PlacementManager] Rotated to {_activeGhost.CurrentRotation} deg. " +
                  $"New footprint: " +
                  $"{_activeFurnitureDefinition.GetFootprintCells(_activeGhost.CurrentRotation).Count} cells.");
    }

    // GHOST MANAGEMENT
    private void SpawnGhost(FurnitureDefinition definition)
    {
        DestroyGhost();

        GameObject ghostSource = definition.ghostPrefab != null
            ? definition.ghostPrefab
            : definition.furniturePrefab;

        if (ghostSource == null)
        {
            Debug.LogError($"[PlacementManager] Khong co prefab de tao ghost " +
                           $"cho '{definition.furnitureType}'!");
            return;
        }

        Transform parent = ghostParent != null ? ghostParent : transform;
        GameObject ghostGO = Instantiate(ghostSource, Vector3.zero, Quaternion.identity, parent);
        ghostGO.name = "[Ghost] " + definition.furnitureType;

        _activeGhost = ghostGO.GetComponent<GhostObject>();
        if (_activeGhost == null)
            _activeGhost = ghostGO.AddComponent<GhostObject>();

        _activeGhost.Initialize(definition);

        foreach (var col in ghostGO.GetComponentsInChildren<Collider2D>())
            col.enabled = false;
        foreach (var col in ghostGO.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    private void DestroyGhost()
    {
        if (_activeGhost != null)
        {
            Destroy(_activeGhost.gameObject);
            _activeGhost = null;
        }
    }

    // RAYCAST
    private Vector3 GetMouseWorldPosition()
    {
        if (_mouse == null || mainCamera == null) return Vector3.negativeInfinity;

        Vector2 screenPos = _mouse.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayerMask))
            return hit.point;

        if (Mathf.Abs(ray.direction.z) > 0.001f)
        {
            float t = -ray.origin.z / ray.direction.z;
            return ray.origin + ray.direction * t;
        }

        return Vector3.negativeInfinity;
    }

    // UTILITIES
    private string GenerateInstanceId(FurnitureType type)
    {
        string timestamp = DateTime.UtcNow.Ticks.ToString();
        string random = UnityEngine.Random.Range(1000, 9999).ToString();
        string tsShort = timestamp.Length >= 8
            ? timestamp.Substring(timestamp.Length - 8)
            : timestamp;
        return $"furniture_{type}_{tsShort}_{random}";
    }

    // PUBLIC QUERY
    public bool IsInPlacementMode => CurrentState == PlacementState.Placing;
    public FurnitureDefinition ActiveDefinition => _activeFurnitureDefinition;
}
