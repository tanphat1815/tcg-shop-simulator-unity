// Assets/Scripts/Placement/GhostObject.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Component dieu khien ghost preview khi dang trong placement mode.
/// Tu cap nhat vi tri theo con tro chuot va doi mau xanh/do.
///
/// VONG DOI:
///   1. PlacementManager.StartPlacement() -> Instantiate ghost
///   2. Moi frame: ghost.UpdatePreview(mouseWorldPos) -> snap + validate + recolor
///   3. PlacementManager.ConfirmPlacement() -> Destroy ghost
///   4. PlacementManager.CancelPlacement()  -> Destroy ghost
/// </summary>
public class GhostObject : MonoBehaviour
{
    [Header("Color Settings")]
    [Tooltip("Mau ghost khi vi tri HOP LE. Xanh la ban trong suot.")]
    [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 0.6f);

    [Tooltip("Mau ghost khi vi tri KHONG HOP LE. Do ban trong suot.")]
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.6f);

    [Tooltip("Toc do di chuyen cua ghost ve vi tri moi. Cao hon = muot hon.")]
    [SerializeField][Range(1f, 30f)] private float followSpeed = 15f;

    // RUNTIME STATE
    private FurnitureDefinition _definition;
    private int _currentRotation = 0;
    private bool _isCurrentPositionValid = false;
    private Vector2Int _currentCellPosition;
    private Vector3 _targetWorldPosition;

    private List<SpriteRenderer> _spriteRenderers = new List<SpriteRenderer>();
    private List<MeshRenderer> _meshRenderers = new List<MeshRenderer>();
    private List<Material> _materialInstances = new List<Material>();

    // LIFECYCLE
    private void Awake()
    {
        GetComponentsInChildren(true, _spriteRenderers);
        GetComponentsInChildren(true, _meshRenderers);

        foreach (var sr in _spriteRenderers)
        {
            if (sr.material != null)
            {
                var matInstance = new Material(sr.material);
                sr.material = matInstance;
                _materialInstances.Add(matInstance);
            }
        }

        foreach (var mr in _meshRenderers)
        {
            if (mr.material != null)
            {
                var matInstance = new Material(mr.material);
                mr.material = matInstance;
                _materialInstances.Add(matInstance);
            }
        }

        ApplyColor(invalidColor);
    }

    private void OnDestroy()
    {
        foreach (var mat in _materialInstances)
        {
            if (mat != null)
                Destroy(mat);
        }
        _materialInstances.Clear();
    }

    // PUBLIC API
    public void Initialize(FurnitureDefinition definition)
    {
        _definition = definition;
        _currentRotation = 0;
    }

    public void UpdatePreview(Vector3 mouseWorldPosition)
    {
        if (_definition == null || GridSystem.Instance == null) return;

        _currentCellPosition = GridSystem.Instance.WorldToCell(mouseWorldPosition);
        _targetWorldPosition = GridSystem.Instance.CellToWorld(_currentCellPosition);
        _targetWorldPosition = AdjustForFootprintCenter(_targetWorldPosition);

        transform.position = Vector3.Lerp(
            transform.position,
            _targetWorldPosition,
            followSpeed * Time.deltaTime
        );

        bool isValid = GridSystem.Instance.ValidatePlacement(
            _currentCellPosition,
            _definition,
            _currentRotation,
            out string _
        );

        if (isValid != _isCurrentPositionValid)
        {
            _isCurrentPositionValid = isValid;
            ApplyColor(isValid ? validColor : invalidColor);
        }
    }

    public void Rotate()
    {
        if (_definition == null || !_definition.canRotate) return;

        _currentRotation = (_currentRotation + 90) % 360;

        Debug.Log($"[GhostObject] Rotated to {_currentRotation} deg. " +
                  $"New footprint: {_definition.GetFootprintCells(_currentRotation).Count} cells.");
    }

    // GETTERS
    public bool IsCurrentPositionValid => _isCurrentPositionValid;
    public Vector2Int CurrentCellPosition => _currentCellPosition;
    public int CurrentRotation => _currentRotation;

    // PRIVATE HELPERS
    private Vector3 AdjustForFootprintCenter(Vector3 originWorldPos)
    {
        if (_definition == null) return originWorldPos;

        var (minBounds, maxBounds) = _definition.GetFootprintBounds(_currentRotation);

        float centerOffsetX = (minBounds.x + maxBounds.x) * 0.5f;
        float centerOffsetY = (minBounds.y + maxBounds.y) * 0.5f;

        Vector3 cellSize = GridSystem.Instance.IsometricGrid.cellSize;
        return originWorldPos - new Vector3(
            centerOffsetX * cellSize.x,
            centerOffsetY * cellSize.y,
            0f
        );
    }

    private void ApplyColor(Color color)
    {
        foreach (var sr in _spriteRenderers)
        {
            if (sr != null)
                sr.color = color;
        }

        foreach (var mr in _meshRenderers)
        {
            if (mr != null && mr.material != null)
                mr.material.color = color;
        }
    }
}
