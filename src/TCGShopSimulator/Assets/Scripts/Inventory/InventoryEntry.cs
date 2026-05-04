// Assets/Scripts/Inventory/InventoryEntry.cs

/// <summary>
/// Data class cho một slot trong inventory.
/// Tương đương một entry trong shopInventory / personalBinder của hệ thống cũ.
/// 
/// THIẾT KẾ: Struct thuần — không kế thừa MonoBehaviour hay ScriptableObject.
/// Immutable sau khi tạo (tạo bản mới khi thay đổi).
/// </summary>
public struct InventoryEntry
{
    /// <summary>
    /// ID của item (packId hoặc cardId).
    /// </summary>
    public readonly string itemId;

    /// <summary>
    /// Số lượng hiện có.
    /// </summary>
    public readonly int quantity;

    /// <summary>
    /// Giá bán hiện tại (USD). Chỉ dùng cho pack.
    /// </summary>
    public readonly float currentPrice;

    /// <summary>
    /// Loại item.
    /// </summary>
    public readonly InventoryEntryType entryType;

    public InventoryEntry(string itemId, int quantity, float currentPrice, InventoryEntryType entryType)
    {
        this.itemId = itemId;
        this.quantity = quantity;
        this.currentPrice = currentPrice;
        this.entryType = entryType;
    }

    /// <summary>
    /// Tạo entry mới với số lượng thay đổi (immutable pattern).
    /// </summary>
    public InventoryEntry WithQuantity(int newQuantity) =>
        new InventoryEntry(itemId, newQuantity, currentPrice, entryType);

    /// <summary>
    /// Tạo entry mới với giá thay đổi (immutable pattern).
    /// </summary>
    public InventoryEntry WithPrice(float newPrice) =>
        new InventoryEntry(itemId, quantity, newPrice, entryType);

    public override string ToString() =>
        $"InventoryEntry[{entryType}|{itemId}|qty={quantity}|${currentPrice:F2}]";
}

public enum InventoryEntryType
{
    Pack,
    Card
}
