// Assets/Scripts/Grid/GridNode.cs

using UnityEngine;

/// <summary>
/// Đại diện cho một ô đơn trong ma trận lưới cửa hàng.
/// Dùng struct thay vì class để tránh heap allocation overhead
/// khi Dictionary chứa hàng nghìn nodes.
///
/// MAPPING TỪ HỆ THỐNG CŨ (Phaser/Vue):
///   Không có cấu trúc tương đương trực tiếp.
///   Hệ thống cũ dùng Phaser physics bodies và validatePlacement()
///   để kiểm tra va chạm. GridNode thay thế toàn bộ cơ chế đó
///   bằng spatial index O(1).
/// </summary>
public struct GridNode
{
    /// <summary>
    /// Ô này có đang bị chiếm dụng bởi một vật thể không.
    /// </summary>
    public bool IsOccupied;

    /// <summary>
    /// ID của vật thể đang chiếm dụng ô này.
    /// String.Empty nếu ô trống.
    /// </summary>
    public string OccupantId;

    /// <summary>
    /// Loại vật thể chiếm dụng ô.
    /// </summary>
    public FurnitureType OccupantType;

    /// <summary>
    /// Ô này có nằm trong shop bounds hợp lệ không.
    /// </summary>
    public bool IsWithinShopBounds;

    /// <summary>
    /// Tạo node trống hợp lệ (trong shop bounds, chưa bị chiếm).
    /// </summary>
    public static GridNode Empty => new GridNode
    {
        IsOccupied = false,
        OccupantId = string.Empty,
        OccupantType = FurnitureType.None,
        IsWithinShopBounds = true
    };

    /// <summary>
    /// Tạo node ngoài bounds (không thể đặt).
    /// </summary>
    public static GridNode OutOfBounds => new GridNode
    {
        IsOccupied = false,
        OccupantId = string.Empty,
        OccupantType = FurnitureType.None,
        IsWithinShopBounds = false
    };

    /// <summary>
    /// Ô này có thể đặt vật thể không.
    /// </summary>
    public bool IsPlaceable => IsWithinShopBounds && !IsOccupied;

    public override string ToString() =>
        $"GridNode[Occupied={IsOccupied}, InBounds={IsWithinShopBounds}, " +
        $"OccupantId={OccupantId}]";
}

/// <summary>
/// Enum phân loại vật thể nội thất.
/// Tương đương furnitureId strings trong FURNITURE_ITEMS config cũ.
/// </summary>
public enum FurnitureType
{
    None,
    ShelfSingle,   // "shelf_single"
    ShelfDouble,   // "shelf_double"
    StorageShelf,  // "storage_shelf"
    PlayTable,     // "play_table"
    CashierDesk    // "cashier_desk"
}
