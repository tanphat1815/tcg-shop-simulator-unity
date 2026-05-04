// Assets/Scripts/Pathfinding/PathfindingGrid.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adapter giữa GridSystem (game world) và PathfindingCore (A* engine).
///
/// TRÁCH NHIỆM:
///   1. Đọc GridNode.IsOccupied từ GridSystem → chuyển thành PathNode.IsWalkable
///   2. Cập nhật PathNode.IsWalkable khi furniture được đặt/dỡ (dynamic obstacles)
///   3. Cung cấp PathNode lookups O(1) cho PathfindingCore
///   4. Thông báo cho tất cả CharacterMovement khi grid thay đổi
///
/// THIẾT KẾ:
///   PathfindingGrid KHÔNG tự tạo PathNode mới mỗi lần A* chạy.
///   Thay vào đó, duy trì một bộ PathNode được reuse qua nhiều query.
///   Điều này tránh GC pressure từ việc tạo/hủy hàng nghìn objects/frame.
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   Không có. Hệ thống cũ dùng Phaser physics bodies làm obstacles.
///   PathfindingGrid thay thế hoàn toàn cơ chế collision detection cũ
///   bằng spatial index từ GridSystem (Bước 3).
/// </summary>
public class PathfindingGrid : MonoBehaviour
{
    // =========================================================================
    // SINGLETON
    // =========================================================================

    public static PathfindingGrid Instance { get; private set; }

    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Grid Settings")]
    [Tooltip("4 hướng di chuyển (không diagonal trong isometric).")]
    [SerializeField] private bool allowDiagonalMovement = false;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    // =========================================================================
    // PATHFINDING NODES
    // =========================================================================

    /// <summary>
    /// Tập hợp PathNode map với GridSystem cells.
    /// Key: Vector2Int cell coordinate (khớp với GridSystem._grid keys)
    /// Value: PathNode với IsWalkable đã được sync từ GridNode.IsOccupied
    ///
    /// Reuse nodes qua nhiều A* queries → tránh GC.
    /// Chỉ reset G/H/Parent khi bắt đầu query mới.
    /// </summary>
    private Dictionary<Vector2Int, PathNode> _pathNodes;

    /// <summary>
    /// Danh sách 4 hướng di chuyển hợp lệ.
    /// Không diagonal vì isometric shop layout dùng 4-directional.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   Phaser physics di chuyển 8 hướng nhưng NPC thực tế đi 4 hướng
    ///   (moveStates check vx và vy riêng).
    /// </summary>
    private static readonly Vector2Int[] FourDirections =
    {
        new Vector2Int(1, 0),   // East
        new Vector2Int(-1, 0),  // West
        new Vector2Int(0, 1),   // North
        new Vector2Int(0, -1),  // South
    };

    private static readonly Vector2Int[] EightDirections =
    {
        new Vector2Int(1, 0),   new Vector2Int(-1, 0),
        new Vector2Int(0, 1),   new Vector2Int(0, -1),
        new Vector2Int(1, 1),   new Vector2Int(-1, 1),
        new Vector2Int(1, -1),  new Vector2Int(-1, -1),
    };

    // =========================================================================
    // EVENTS — Thông báo CharacterMovement khi grid thay đổi
    // =========================================================================

    /// <summary>
    /// Kích hoạt khi một hoặc nhiều node thay đổi walkability.
    /// CharacterMovement subscribe để biết khi nào cần recalculate path.
    /// Tham số: danh sách cells bị thay đổi.
    /// </summary>
    public event System.Action<List<Vector2Int>> OnGridChanged;

    // =========================================================================
    // VÒNG ĐỜI
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Delay build đến Start để GridSystem có thời gian Initialize
        _pathNodes = new Dictionary<Vector2Int, PathNode>();
    }

    private void Start()
    {
        if (GridSystem.Instance == null)
        {
            Debug.LogError("[PathfindingGrid] GridSystem.Instance là null! " +
                           "Đảm bảo GridSystem tồn tại và đã Initialize.");
            enabled = false;
            return;
        }

        BuildFromGridSystem();

        // Subscribe vào PlacementManager events để cập nhật khi furniture thay đổi
        if (PlacementManager.Instance != null)
        {
            PlacementManager.Instance.OnFurniturePlaced += HandleFurniturePlaced;
            PlacementManager.Instance.OnPlacementCancelled += HandlePlacementCancelled;
        }

        Debug.Log($"[PathfindingGrid] Initialized: {_pathNodes.Count} path nodes.");
    }

    private void OnDestroy()
    {
        if (PlacementManager.Instance != null)
        {
            PlacementManager.Instance.OnFurniturePlaced -= HandleFurniturePlaced;
            PlacementManager.Instance.OnPlacementCancelled -= HandlePlacementCancelled;
        }
    }

    // =========================================================================
    // GRID BUILDING — Đọc từ GridSystem
    // =========================================================================

    /// <summary>
    /// Build PathNode graph từ GridSystem hiện tại.
    /// Gọi một lần tại Start, sau đó chỉ update incrementally.
    ///
    /// LUỒNG:
    ///   FOR EACH (coord, gridNode) IN GridSystem._grid:
    ///     pathNode.IsWalkable = gridNode.IsWithinShopBounds AND NOT gridNode.IsOccupied
    ///     pathNode.IsWalkable = false nếu gridNode.IsOccupied (có kệ hàng)
    ///
    /// Đây là điểm kết nối chính với Bước 3:
    ///   GridNode.IsOccupied = true (kệ hàng đặt xuống)
    ///   → PathNode.IsWalkable = false (A* né kệ hàng)
    /// </summary>
    public void BuildFromGridSystem()
    {
        _pathNodes.Clear();

        var gridSystem = GridSystem.Instance;

        // Duyệt toàn bộ cells trong GridSystem bounds
        for (int x = gridSystem.ShopMinCell.x; x <= gridSystem.ShopMaxCell.x; x++)
        {
            for (int y = gridSystem.ShopMinCell.y; y <= gridSystem.ShopMaxCell.y; y++)
            {
                var coord = new Vector2Int(x, y);
                GridNode gridNode = gridSystem.GetNode(coord);

                // QUAN TRỌNG: isOccupied = true → isWalkable = false
                // Kệ hàng trở thành obstacle tự động
                bool isWalkable = gridNode.IsWithinShopBounds && !gridNode.IsOccupied;

                _pathNodes[coord] = new PathNode(coord, isWalkable);
            }
        }
    }

    // =========================================================================
    // DYNAMIC OBSTACLE UPDATES — Khi furniture được đặt/dỡ
    // =========================================================================

    /// <summary>
    /// Cập nhật PathNodes khi furniture được đặt xuống (các cells trở thành obstacle).
    /// Gọi bởi event OnFurniturePlaced từ PlacementManager.
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   Khi player đặt kệ → Phaser tạo physics static body → NPC tự né.
    ///   Ở đây: đặt kệ → các PathNode.IsWalkable = false → A* tự né.
    /// </summary>
    private void HandleFurniturePlaced(PlacedFurnitureInstance instance)
    {
        var changedCells = new List<Vector2Int>();

        // Lấy footprint từ definition
        var footprint = instance.Definition.GetFootprintCells(instance.PlacedRotation);

        foreach (var offset in footprint)
        {
            var absoluteCell = instance.OriginCell + offset;

            if (_pathNodes.TryGetValue(absoluteCell, out PathNode node))
            {
                node.IsWalkable = false;
                changedCells.Add(absoluteCell);
            }
        }

        if (changedCells.Count > 0)
        {
            // Thông báo tất cả CharacterMovement về grid change
            OnGridChanged?.Invoke(changedCells);

            if (verboseLogging)
            {
                Debug.Log($"[PathfindingGrid] Cập nhật lưới phát sinh: " +
                          $"{changedCells.Count} cells trở thành obstacles " +
                          $"do [{instance.Definition.furnitureType}].");
            }
        }
    }

    private void HandlePlacementCancelled()
    {
        // Không cần làm gì khi cancel — không có furniture được đặt
    }

    /// <summary>
    /// Cập nhật PathNodes khi furniture được dỡ (cells trở lại walkable).
    /// Gọi bởi GridSystem sau khi ReleaseCells() hoàn tất.
    /// </summary>
    public void NotifyFurnitureRemoved(List<Vector2Int> releasedCells)
    {
        var changedCells = new List<Vector2Int>();

        foreach (var cell in releasedCells)
        {
            if (_pathNodes.TryGetValue(cell, out PathNode node))
            {
                // Double-check với GridSystem (source of truth)
                GridNode gridNode = GridSystem.Instance.GetNode(cell);
                node.IsWalkable = gridNode.IsWithinShopBounds && !gridNode.IsOccupied;

                if (node.IsWalkable)
                    changedCells.Add(cell);
            }
        }

        if (changedCells.Count > 0)
        {
            OnGridChanged?.Invoke(changedCells);

            if (verboseLogging)
            {
                Debug.Log($"[PathfindingGrid] {changedCells.Count} cells " +
                          "đã được giải phóng và trở lại walkable.");
            }
        }
    }

    // =========================================================================
    // NODE ACCESS API
    // =========================================================================

    /// <summary>Lấy PathNode tại cell — O(1).</summary>
    public PathNode GetNode(Vector2Int cellCoord)
    {
        return _pathNodes.TryGetValue(cellCoord, out PathNode node) ? node : null;
    }

    /// <summary>Kiểm tra cell có walkable không — O(1).</summary>
    public bool IsWalkable(Vector2Int cellCoord)
    {
        var node = GetNode(cellCoord);
        return node != null && node.IsWalkable;
    }

    /// <summary>
    /// Lấy danh sách neighbors hợp lệ của một node.
    /// Chỉ trả về neighbors walkable và trong bounds.
    /// </summary>
    public List<PathNode> GetNeighbors(PathNode node)
    {
        var directions = allowDiagonalMovement ? EightDirections : FourDirections;
        var neighbors = new List<PathNode>(directions.Length);

        foreach (var dir in directions)
        {
            var neighborCoord = node.CellCoord + dir;
            var neighbor = GetNode(neighborCoord);

            if (neighbor != null && neighbor.IsWalkable)
                neighbors.Add(neighbor);
        }

        return neighbors;
    }

    /// <summary>Reset G/H/Parent của tất cả nodes trước mỗi A* query.</summary>
    public void ResetForNewQuery()
    {
        foreach (var node in _pathNodes.Values)
        {
            node.GCost = int.MaxValue;
            node.HCost = 0;
            node.Parent = null;
            node.HeapIndex = -1;
        }
    }
}
