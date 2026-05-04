// Assets/Scripts/Grid/GridSystem.cs

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Singleton quản lý ma trận lưới của cửa hàng.
///
/// KIẾN TRÚC:
///   _grid: Dictionary<Vector2Int, GridNode>
///     → Key:   Tọa độ cell (x, y)
///     → Value: GridNode struct lưu trạng thái ô
///
/// TẠI SAO DICTIONARY THAY VÌ MẢNG 2D:
///   - Shop mở rộng động (expansionLevel tăng) → kích thước thay đổi runtime
///   - Lookup O(1) thay vì O(n) của List
///   - Sparse representation: chỉ tạo node cho cells thực sự tồn tại
///
/// MAPPING TỪ HỆ THỐNG CŨ:
///   validatePlacement()     → ValidatePlacement()
///   placeFurniture()      → ConfirmPlacement()
///   shopBounds check      → IsWithinShopBounds()
///   furnitureManager.remove → ReleaseCells()
/// </summary>
public class GridSystem : MonoBehaviour
{
    // =========================================================================
    // SINGLETON
    // =========================================================================

    public static GridSystem Instance { get; private set; }

    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Grid Reference")]
    [Tooltip("Unity Grid component trên scene. PHẢI là Isometric Z as Y layout. " +
             "Dùng API Grid.WorldToCell() và Grid.CellToWorld().")]
    [SerializeField] private Grid isometricGrid;

    [Header("Shop Bounds")]
    [Tooltip("Tọa độ cell góc dưới-trái của shop (inclusive).")]
    [SerializeField] private Vector2Int shopMinCell = new Vector2Int(-10, -10);

    [Tooltip("Tọa độ cell góc trên-phải của shop (inclusive).")]
    [SerializeField] private Vector2Int shopMaxCell = new Vector2Int(10, 10);

    [Header("Debug")]
    [Tooltip("Bật log chi tiết cho mọi placement operation. Tắt trong production.")]
    [SerializeField] private bool verboseLogging = true;

    // =========================================================================
    // GRID STATE
    // =========================================================================

    private Dictionary<Vector2Int, GridNode> _grid;
    private Dictionary<string, List<Vector2Int>> _furnitureFootprints;

    // =========================================================================
    // AWAKE
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (isometricGrid == null)
        {
            Debug.LogError("[GridSystem] isometricGrid chưa được assign! " +
                           "Kéo thả Grid GameObject vào field 'Isometric Grid'.");
            enabled = false;
            return;
        }

        InitializeGrid();
    }

    private void InitializeGrid()
    {
        _grid = new Dictionary<Vector2Int, GridNode>();
        _furnitureFootprints = new Dictionary<string, List<Vector2Int>>();

        int cellCount = 0;
        for (int x = shopMinCell.x; x <= shopMaxCell.x; x++)
        {
            for (int y = shopMinCell.y; y <= shopMaxCell.y; y++)
            {
                var cellCoord = new Vector2Int(x, y);
                _grid[cellCoord] = GridNode.Empty;
                cellCount++;
            }
        }

        Debug.Log($"[GridSystem] Initialized: {cellCount} cells ({shopMinCell} to {shopMaxCell}).");
    }

    // =========================================================================
    // COORDINATE CONVERSION API
    // =========================================================================

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        Vector3Int cell3D = isometricGrid.WorldToCell(worldPosition);
        return new Vector2Int(cell3D.x, cell3D.y);
    }

    public Vector3 CellToWorld(Vector2Int cellCoord)
    {
        Vector3Int cell3D = new Vector3Int(cellCoord.x, cellCoord.y, 0);
        return isometricGrid.CellToWorld(cell3D);
    }

    public Vector3 SnapToGrid(Vector3 worldPosition)
    {
        Vector2Int cell = WorldToCell(worldPosition);
        return CellToWorld(cell);
    }

    /// <summary>
    /// Tinh vi tri trung binh (tam) cua mot nhom cac o (footprint).
    /// Giup cac vat the lon (2x1, 2x2) duoc dat dung giua cac o Isometric.
    /// </summary>
    public Vector3 GetCenteredWorldPosition(Vector2Int originCell, FurnitureDefinition definition, int rotationDegrees)
    {
        if (definition == null) return CellToWorld(originCell);

        List<Vector2Int> footprint = definition.GetFootprintCells(rotationDegrees);
        if (footprint.Count <= 1) return CellToWorld(originCell);

        Vector3 averagePos = Vector3.zero;
        foreach (var relativeCell in footprint)
        {
            Vector2Int absoluteCell = originCell + relativeCell;
            averagePos += CellToWorld(absoluteCell);
        }
        return averagePos / footprint.Count;
    }

    // =========================================================================
    // GRID QUERY API — O(1) lookups
    // =========================================================================

    public GridNode GetNode(Vector2Int cellCoord)
    {
        return _grid.TryGetValue(cellCoord, out GridNode node)
            ? node
            : GridNode.OutOfBounds;
    }

    public bool IsWithinShopBounds(Vector2Int cellCoord)
    {
        return cellCoord.x >= shopMinCell.x && cellCoord.x <= shopMaxCell.x &&
               cellCoord.y >= shopMinCell.y && cellCoord.y <= shopMaxCell.y;
    }

    public bool IsCellPlaceable(Vector2Int cellCoord)
    {
        return GetNode(cellCoord).IsPlaceable;
    }

    // =========================================================================
    // PLACEMENT VALIDATION
    // =========================================================================

    /// <summary>
    /// Kiểm tra một furniture definition có thể đặt tại origin cell không.
    /// Kiểm tra TẤT CẢ cells trong footprint.
    /// </summary>
    public bool ValidatePlacement(
        Vector2Int originCell,
        FurnitureDefinition definition,
        int rotationDegrees,
        out string failReason)
    {
        failReason = string.Empty;

        if (definition == null)
        {
            failReason = "FurnitureDefinition là null.";
            return false;
        }

        List<Vector2Int> footprint = definition.GetFootprintCells(rotationDegrees);

        foreach (Vector2Int relativeOffset in footprint)
        {
            Vector2Int absoluteCell = originCell + relativeOffset;

            if (!IsWithinShopBounds(absoluteCell))
            {
                failReason = $"Vị trí không hợp lệ: cell {absoluteCell} nằm ngoài biên giới cửa hàng.";
                return false;
            }

            GridNode node = GetNode(absoluteCell);
            if (node.IsOccupied)
            {
                failReason = $"Vị trí không hợp lệ, lưới bị chiếm dụng tại {absoluteCell} " +
                             $"bởi [{node.OccupantType}] ID='{node.OccupantId}'.";
                return false;
            }
        }

        return true;
    }

    // =========================================================================
    // PLACEMENT CONFIRMATION
    // =========================================================================

    public void ConfirmPlacement(
        Vector2Int originCell,
        FurnitureDefinition definition,
        int rotationDegrees,
        string furnitureInstanceId)
    {
        List<Vector2Int> footprint = definition.GetFootprintCells(rotationDegrees);
        var occupiedCells = new List<Vector2Int>();

        foreach (Vector2Int relativeOffset in footprint)
        {
            Vector2Int absoluteCell = originCell + relativeOffset;

            if (_grid.ContainsKey(absoluteCell))
            {
                _grid[absoluteCell] = new GridNode
                {
                    IsOccupied = true,
                    OccupantId = furnitureInstanceId,
                    OccupantType = definition.furnitureType,
                    IsWithinShopBounds = true
                };
                occupiedCells.Add(absoluteCell);
            }
        }

        _furnitureFootprints[furnitureInstanceId] = occupiedCells;

        if (verboseLogging)
        {
            Debug.Log($"[GridSystem] Placed [{definition.furnitureType}] " +
                      $"ID='{furnitureInstanceId}' at origin={originCell}, " +
                      $"rotation={rotationDegrees}°, cells occupied: {occupiedCells.Count}.");
        }
    }

    // =========================================================================
    // RELEASE CELLS
    // =========================================================================

    public bool ReleaseCells(string furnitureInstanceId)
    {
        if (!_furnitureFootprints.TryGetValue(furnitureInstanceId, out List<Vector2Int> cells))
        {
            Debug.LogWarning($"[GridSystem] ReleaseCells: Không tìm thấy footprint " +
                             $"cho ID='{furnitureInstanceId}'.");
            return false;
        }

        foreach (Vector2Int cell in cells)
        {
            if (_grid.ContainsKey(cell))
                _grid[cell] = GridNode.Empty;
        }

        _furnitureFootprints.Remove(furnitureInstanceId);

        if (verboseLogging)
        {
            Debug.Log($"[GridSystem] Released {cells.Count} cells " +
                      $"from furniture ID='{furnitureInstanceId}'.");
        }

        return true;
    }

    // =========================================================================
    // SHOP EXPANSION
    // =========================================================================

    public void ExpandShopBounds(Vector2Int newMinCell, Vector2Int newMaxCell)
    {
        int newCellCount = 0;

        for (int x = newMinCell.x; x <= newMaxCell.x; x++)
        {
            for (int y = newMinCell.y; y <= newMaxCell.y; y++)
            {
                var cellCoord = new Vector2Int(x, y);
                if (!_grid.ContainsKey(cellCoord))
                {
                    _grid[cellCoord] = GridNode.Empty;
                    newCellCount++;
                }
            }
        }

        shopMinCell = newMinCell;
        shopMaxCell = newMaxCell;

        Debug.Log($"[GridSystem] Shop expanded to ({newMinCell} → {newMaxCell}). " +
                  $"Added {newCellCount} new cells. Total cells: {_grid.Count}.");
    }

    // =========================================================================
    // DEBUG
    // =========================================================================

    [ContextMenu("Debug: Print Grid State")]
    public void PrintGridState()
    {
        int occupied = 0, empty = 0;
        foreach (var node in _grid.Values)
        {
            if (node.IsOccupied) occupied++;
            else empty++;
        }

        Debug.Log($"[GridSystem] Grid State: " +
                  $"Total={_grid.Count}, Occupied={occupied}, Empty={empty}, " +
                  $"Furniture instances={_furnitureFootprints.Count}");
    }

    public Grid IsometricGrid => isometricGrid;

    // =========================================================================
    // PUBLIC ACCESSORS — For PathfindingGrid
    // =========================================================================

    /// <summary>Public accessor cho PathfindingGrid.</summary>
    public Vector2Int ShopMinCell => shopMinCell;

    /// <summary>Public accessor cho PathfindingGrid.</summary>
    public Vector2Int ShopMaxCell => shopMaxCell;
}
