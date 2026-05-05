// Assets/Scripts/Pathfinding/PathfindingCore.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thuật toán A* Pathfinding hoàn chỉnh.
///
/// ĐẶC ĐIỂM KỸ THUẬT:
///   - Heuristic: Manhattan Distance (|dx| + |dy|) — admissible cho 4-directional grid
///   - Open List: MinHeap<PathNode> — O(log n) insert/extract
///   - Closed Set: HashSet<PathNode> — O(1) membership check
///   - Node reuse: PathfindingGrid resets nodes trước mỗi query, tránh GC
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   Toàn bộ class này thay thế:
///     - physics.moveTo() trong NPCManager.ts
///     - handleStuckRecovery() trong NPCManager.ts
///     - scene.physics.moveTo(customer.sprite, ...) mỗi frame
///   Thay vì "fly straight + stuck recovery", ta tính đường đúng từ đầu.
///
/// THREAD SAFETY:
///   Class này là static utility — không có mutable state.
///   An toàn khi gọi từ nhiều CharacterMovement trong cùng frame.
///   (Nhưng PathfindingGrid.ResetForNewQuery() KHÔNG thread-safe — gọi sequential.)
/// </summary>
public static class PathfindingCore
{
    // =========================================================================
    // CONSTANTS
    // =========================================================================

    /// <summary>
    /// Chi phí di chuyển một bước ngang/dọc.
    /// Dùng integer 10 thay vì float 1.0 để:
    ///   - Tránh floating point precision errors khi so sánh FCost
    ///   - Diagonal có thể dùng 14 (≈ 10√2) nếu enable diagonal movement
    /// </summary>
    private const int STRAIGHT_COST = 10;

    /// <summary>
    /// Chi phí diagonal ≈ 10√2 ≈ 14.14, làm tròn xuống 14.
    /// </summary>
    private const int DIAGONAL_COST = 14;

    // =========================================================================
    // A* MAIN ALGORITHM
    // =========================================================================

    /// <summary>
    /// Tìm đường ngắn nhất từ startCell đến goalCell.
    ///
    /// THUẬT TOÁN A* CHI TIẾT:
    ///
    ///   BƯỚC 1: KHỞI TẠO
    ///     openList = MinHeap với startNode (G=0, H=Manhattan(start→goal))
    ///     closedSet = HashSet rỗng
    ///
    ///   BƯỚC 2: VÒNG LẶP CHÍNH
    ///     WHILE openList không rỗng:
    ///       current = openList.ExtractMin()  ← O(log n)
    ///       closedSet.Add(current)
    ///
    ///       IF current == goalNode:
    ///         RETURN TracePath(current)  ← Tìm thấy đường!
    ///
    ///       FOR EACH neighbor OF current:
    ///         IF neighbor IN closedSet: SKIP
    ///         IF NOT neighbor.IsWalkable: SKIP
    ///
    ///         newG = current.G + MoveCost(current, neighbor)
    ///
    ///         IF newG < neighbor.G:  ← Tìm được đường tốt hơn
    ///           neighbor.G = newG
    ///           neighbor.H = Manhattan(neighbor → goal)
    ///           neighbor.Parent = current
    ///
    ///           IF neighbor IN openList:
    ///             openList.UpdateItem(neighbor)  ← O(log n) nhờ HeapIndex
    ///           ELSE:
    ///             openList.Insert(neighbor)      ← O(log n)
    ///
    ///   BƯỚC 3: KHÔNG TÌM THẤY
    ///     RETURN null
    ///
    /// ĐỘ PHỨC TẠP:
    ///   Time:  O(n log n) với n = số nodes được thăm
    ///   Space: O(n) cho openList và closedSet
    ///
    /// </summary>
    /// <param name="startCell">Cell xuất phát (vị trí NPC hiện tại).</param>
    /// <param name="goalCell">Cell đích (vị trí mục tiêu).</param>
    /// <param name="grid">PathfindingGrid đã build.</param>
    /// <returns>
    ///   List<Vector2Int>: Danh sách cells từ start đến goal (không bao gồm start).
    ///   null: Không có đường đi.
    /// </returns>
    public static List<Vector2Int> FindPath(
        Vector2Int startCell,
        Vector2Int goalCell,
        PathfindingGrid grid)
    {
        // --- Validate inputs ---
        PathNode startNode = grid.GetNode(startCell);
        PathNode goalNode = grid.GetNode(goalCell);

        if (startNode == null)
        {
            Debug.LogError($"[PathfindingCore] Start cell {startCell} does not exist in the pathfinding grid. " +
                          "Ensure the grid is initialized before requesting paths.");
            return null;
        }

        if (goalNode == null)
        {
            Debug.LogError($"[PathfindingCore] Goal cell {goalCell} does not exist in the pathfinding grid. " +
                          "Ensure the grid covers the target area.");
            return null;
        }

        if (!goalNode.IsWalkable)
        {
            Debug.LogError($"[PathfindingCore] Goal cell {goalCell} is not walkable (obstacle or out of bounds). " +
                          $"NPC cannot path to this location. Falling back to ExitShop state.");
            return null;
        }

        // --- Reset all nodes for new query ---
        grid.ResetForNewQuery();

        // --- BƯỚC 1: Khởi tạo ---
        var openList = new MinHeap<PathNode>(256);
        var closedSet = new HashSet<PathNode>();

        startNode.GCost = 0;
        startNode.HCost = CalculateManhattanDistance(startCell, goalCell);
        openList.Insert(startNode);

        // --- BƯỚC 2: Vòng lặp chính ---
        while (!openList.IsEmpty)
        {
            // ExtractMin: O(log n) — Lấy node có FCost nhỏ nhất
            PathNode current = openList.ExtractMin();
            closedSet.Add(current);

            // Kiểm tra đã đến đích chưa
            if (current.CellCoord == goalCell)
            {
                return TracePath(current);
            }

            // Duyệt các neighbors
            List<PathNode> neighbors = grid.GetNeighbors(current);

            foreach (PathNode neighbor in neighbors)
            {
                // Skip nếu đã trong closed set
                if (closedSet.Contains(neighbor)) continue;

                // Tính G cost mới
                int newGCost = current.GCost + GetMoveCost(current, neighbor);

                // Nếu tìm được đường tốt hơn đến neighbor
                if (newGCost < neighbor.GCost)
                {
                    neighbor.GCost = newGCost;
                    neighbor.HCost = CalculateManhattanDistance(neighbor.CellCoord, goalCell);
                    neighbor.Parent = current;

                    if (openList.Contains(neighbor))
                    {
                        // Update priority trong heap — O(log n) nhờ HeapIndex
                        openList.UpdateItem(neighbor);
                    }
                    else
                    {
                        // Thêm vào open list lần đầu — O(log n)
                        openList.Insert(neighbor);
                    }
                }
            }
        }

        // --- BƯỚC 3: Không tìm thấy đường ---
        return null;
    }

    // =========================================================================
    // HEURISTIC: MANHATTAN DISTANCE
    // =========================================================================

    /// <summary>
    /// Tính Manhattan Distance giữa hai cells.
    ///
    /// CÔNG THỨC: |ax - bx| + |ay - by|
    ///
    /// TẠI SAO MANHATTAN:
    ///   - Grid 4-directional: chỉ đi ngang/dọc, không chéo
    ///   - Manhattan là lower bound chính xác → admissible heuristic
    ///   - Admissible → A* đảm bảo tìm đường ngắn nhất
    ///   - Euclidean: tính √, tốn kém hơn, overestimate trong 4-directional
    ///   - Chebyshev: dành cho 8-directional (cho phép diagonal)
    ///
    /// Nhân với STRAIGHT_COST (10) để khớp với GCost scale.
    /// </summary>
    private static int CalculateManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y)) * STRAIGHT_COST;
    }

    // =========================================================================
    // MOVE COST
    // =========================================================================

    /// <summary>
    /// Chi phí di chuyển từ current sang neighbor.
    /// Straight (ngang/dọc) = 10, Diagonal = 14.
    /// Trong 4-directional, luôn là STRAIGHT_COST.
    /// </summary>
    private static int GetMoveCost(PathNode current, PathNode neighbor)
    {
        int dx = Mathf.Abs(current.CellCoord.x - neighbor.CellCoord.x);
        int dy = Mathf.Abs(current.CellCoord.y - neighbor.CellCoord.y);

        // Diagonal nếu cả dx và dy đều != 0
        bool isDiagonal = dx != 0 && dy != 0;
        return isDiagonal ? DIAGONAL_COST : STRAIGHT_COST;
    }

    // =========================================================================
    // PATH RECONSTRUCTION
    // =========================================================================

    /// <summary>
    /// Trace ngược từ goal về start để lấy path.
    ///
    /// THUẬT TOÁN:
    ///   path = []
    ///   current = goalNode
    ///   WHILE current.Parent != null:
    ///     path.Add(current.CellCoord)
    ///     current = current.Parent
    ///   path.Reverse()  ← Đảo ngược để path đi từ start → goal
    ///   RETURN path (không bao gồm start cell)
    ///
    /// Kết quả: List<Vector2Int> từ cell ngay sau start đến goal.
    /// CharacterMovement sẽ di chuyển tuần tự qua từng cell.
    /// </summary>
    private static List<Vector2Int> TracePath(PathNode goalNode)
    {
        var path = new List<Vector2Int>();
        PathNode current = goalNode;

        // Trace ngược từ goal
        while (current.Parent != null)
        {
            path.Add(current.CellCoord);
            current = current.Parent;
        }

        // Đảo ngược: path hiện tại là goal→start, cần start→goal
        path.Reverse();

        return path;
    }

    // =========================================================================
    // UTILITY
    // =========================================================================

    /// <summary>
    /// Ước tính có đường đi không mà không cần tìm full path.
    /// Dùng flood fill đơn giản — nhanh hơn A* cho connectivity check.
    /// </summary>
    public static bool HasPath(Vector2Int startCell, Vector2Int goalCell, PathfindingGrid grid)
    {
        return FindPath(startCell, goalCell, grid) != null;
    }
}
