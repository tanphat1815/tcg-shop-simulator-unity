// Assets/Scripts/Pathfinding/PathNode.cs

using UnityEngine;

/// <summary>
/// Dữ liệu của một node trong A* algorithm.
/// Dùng class (reference type) thay vì struct để:
///   - Cho phép lưu parent reference (linked list ngược về start)
///   - Tránh boxing/unboxing khi lưu trong MinHeap
///   - So sánh reference equality trong ClosedSet (HashSet)
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   Không có. Hệ thống cũ không có A* node representation.
///   NPCManager.ts dùng trực tiếp Phaser.Math.Distance.Between
///   và physics.moveTo — không có node-based pathfinding.
/// </summary>
public class PathNode : System.IComparable<PathNode>, IHeapItem
{
    // =========================================================================
    // IDENTITY
    // =========================================================================

    /// <summary>
    /// Tọa độ cell trong GridSystem. Là key để tra cứu GridNode.
    /// Tương đương Vector2Int key trong Dictionary<Vector2Int, GridNode>.
    /// </summary>
    public Vector2Int CellCoord { get; }

    // =========================================================================
    // A* COSTS
    // =========================================================================

    /// <summary>
    /// G Cost: Chi phí thực tế từ START đến node này.
    /// G(start) = 0.
    /// G(neighbor) = G(current) + stepCost (thường = 10 để tránh float precision).
    /// </summary>
    public int GCost { get; set; }

    /// <summary>
    /// H Cost: Heuristic ước tính từ node này đến GOAL.
    /// Dùng Manhattan Distance: |dx| + |dy| nhân với BASE_COST.
    /// Không bao giờ được overestimate để A* admissible.
    /// </summary>
    public int HCost { get; set; }

    /// <summary>
    /// F Cost: Tổng G + H. Node có F nhỏ nhất được xử lý trước.
    /// Đây là key của Min-Heap.
    /// </summary>
    public int FCost => GCost + HCost;

    // =========================================================================
    // GRAPH STRUCTURE
    // =========================================================================

    /// <summary>
    /// Node cha trong path. Dùng để trace ngược từ GOAL về START.
    /// null nếu đây là start node.
    /// </summary>
    public PathNode Parent { get; set; }

    /// <summary>
    /// Index trong heap array. Dùng bởi MinHeap để update priority O(log n).
    /// -1 nếu node không trong heap.
    /// </summary>
    public int HeapIndex { get; set; } = -1;

    // =========================================================================
    // WALKABILITY
    // =========================================================================

    /// <summary>
    /// Node này có thể đi qua không.
    /// FALSE nếu GridNode.IsOccupied = true (có kệ hàng) hoặc ngoài bounds.
    /// Đọc từ GridSystem tại thời điểm PathfindingGrid.BuildGraph().
    ///
    /// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
    ///   validatePlacement() check "Không được đè lên vật thể khác"
    ///   Nhưng ở đây dùng cho pathfinding thay vì placement.
    /// </summary>
    public bool IsWalkable { get; set; }

    // =========================================================================
    // CONSTRUCTOR
    // =========================================================================

    public PathNode(Vector2Int cellCoord, bool isWalkable)
    {
        CellCoord = cellCoord;
        IsWalkable = isWalkable;
        GCost = int.MaxValue; // Chưa visited → cost vô cùng
        HCost = 0;
        Parent = null;
        HeapIndex = -1;
    }

    // =========================================================================
    // COMPARISON — Dùng cho MinHeap
    // =========================================================================

    /// <summary>
    /// So sánh theo FCost để MinHeap sắp xếp đúng (nhỏ hơn = ưu tiên hơn).
    /// Tiebreak bằng HCost: nếu FCost bằng nhau, ưu tiên node gần goal hơn.
    /// </summary>
    public int CompareTo(PathNode other)
    {
        int compare = FCost.CompareTo(other.FCost);
        if (compare == 0)
            compare = HCost.CompareTo(other.HCost);
        return compare;
    }

    public override string ToString() =>
        $"PathNode[{CellCoord}|G={GCost}|H={HCost}|F={FCost}|Walk={IsWalkable}]";
}
