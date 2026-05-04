// Assets/Scripts/Data/FurnitureDefinition.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject khai báo metadata của một loại nội thất.
/// Là nguồn dữ liệu duy nhất (Single Source of Truth) cho mọi thông tin
/// liên quan đến một loại furniture.
///
/// CÁCH TẠO ASSET:
///   Right-click > Create > TCGShop > Data > Furniture Definition
/// </summary>
[CreateAssetMenu(
    fileName = "Furniture_New",
    menuName = "TCGShop/Data/Furniture Definition",
    order = 4
)]
public class FurnitureDefinition : ScriptableObject
{
    // =========================================================================
    // IDENTITY
    // =========================================================================

    [Header("Identity")]
    [Tooltip("Loại nội thất. Dùng làm key để lookup trong code.")]
    public FurnitureType furnitureType = FurnitureType.ShelfSingle;

    [Tooltip("Tên hiển thị trong UI Shop. Vd: 'Single Sided Shelf'.")]
    public string displayName = "New Furniture";

    [Tooltip("Mô tả ngắn hiển thị trong Build Menu.")]
    [TextArea(2, 3)]
    public string description;

    // =========================================================================
    // VISUAL
    // =========================================================================

    [Header("Visual")]
    [Tooltip("Prefab được Instantiate khi người chơi đặt nội thất xuống lưới. " +
             "Prefab này PHẢI có PlacedFurnitureInstance component.")]
    public GameObject furniturePrefab;

    [Tooltip("Prefab Ghost (bóng ma preview). Nếu null, tự tạo từ furniturePrefab.")]
    public GameObject ghostPrefab;

    [Tooltip("Sprite icon hiển thị trong Build Menu UI.")]
    public Sprite menuIcon;

    // =========================================================================
    // GRID FOOTPRINT
    // =========================================================================

    [Header("Grid Footprint")]
    [Tooltip("Số cell chiếm theo trục X (ngang). " +
             "ShelfSingle=1, ShelfDouble=2, PlayTable=2.")]
    [Range(1, 4)]
    public int footprintWidth = 1;

    [Tooltip("Số cell chiếm theo trục Y (dọc/sâu trong isometric). " +
             "ShelfSingle=1, PlayTable=2.")]
    [Range(1, 4)]
    public int footprintHeight = 1;

    [Tooltip("Nếu true, vật thể có thể xoay bằng phím R. " +
             "false cho những vật thể đối xứng (1x1).")]
    public bool canRotate = false;

    // =========================================================================
    // ECONOMY
    // =========================================================================

    [Header("Economy")]
    [Tooltip("Giá mua nội thất từ Online Shop.")]
    [Min(0f)]
    public float buyCost = 300f;

    [Tooltip("Level shop tối thiểu để mở khóa.")]
    [Range(1, 80)]
    public int requiredShopLevel = 1;

    // =========================================================================
    // SHELF CONFIG
    // =========================================================================

    [Header("Shelf Config (Shelf types only)")]
    [Tooltip("Số tầng kệ. 0 nếu không phải shelf.")]
    [Range(0, 6)]
    public int numberOfTiers = 0;

    [Tooltip("Số slot tối đa mỗi tầng (cho pack).")]
    [Range(0, 64)]
    public int slotsPerTier = 0;

    [Tooltip("Vai trò của kệ: Selling (NPC mua được) hay Storage (chỉ lưu trữ).")]
    public ShelfRole shelfRole = ShelfRole.Selling;

    // =========================================================================
    // COMPUTED PROPERTIES
    // =========================================================================

    public int TotalCells => footprintWidth * footprintHeight;

    public List<Vector2Int> GetFootprintCells(int rotationDegrees = 0)
    {
        var cells = new List<Vector2Int>();
        for (int x = 0; x < footprintWidth; x++)
        {
            for (int y = 0; y < footprintHeight; y++)
            {
                cells.Add(new Vector2Int(x, y));
            }
        }

        int normalizedRotation = ((rotationDegrees % 360) + 360) % 360;
        int steps = normalizedRotation / 90;

        for (int step = 0; step < steps; step++)
            cells = RotateFootprint90CW(cells);

        return cells;
    }

    private List<Vector2Int> RotateFootprint90CW(List<Vector2Int> cells)
    {
        var rotated = new List<Vector2Int>(cells.Count);
        foreach (var cell in cells)
            rotated.Add(new Vector2Int(cell.y, -cell.x));
        return rotated;
    }

    public (Vector2Int min, Vector2Int max) GetFootprintBounds(int rotationDegrees = 0)
    {
        var cells = GetFootprintCells(rotationDegrees);
        if (cells.Count == 0) return (Vector2Int.zero, Vector2Int.zero);

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var cell in cells)
        {
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxX = Mathf.Max(maxX, cell.x);
            maxY = Mathf.Max(maxY, cell.y);
        }

        return (new Vector2Int(minX, minY), new Vector2Int(maxX, maxY));
    }

    public bool IsValid()
    {
        if (furniturePrefab == null) return false;
        if (string.IsNullOrEmpty(displayName)) return false;
        if (footprintWidth <= 0 || footprintHeight <= 0) return false;
        return true;
    }

    public override string ToString() =>
        $"FurnitureDef[{furnitureType}|{footprintWidth}x{footprintHeight}|${buyCost}]";
}

/// <summary>
/// Vai trò của kệ hàng.
/// </summary>
public enum ShelfRole
{
    Selling,  // NPC có thể mua
    Storage   // Chỉ lưu trữ, NPC không mua
}
