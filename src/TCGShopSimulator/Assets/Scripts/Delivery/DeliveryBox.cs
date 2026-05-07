// Assets/Scripts/Delivery/DeliveryBox.cs

using UnityEngine;

/// <summary>
/// MonoBehaviour gắn trên DeliveryBox GameObject trong world.
///
/// VÒNG ĐỜI:
///   1. DeliveryManager.ScheduleDelivery() → Instantiate prefab tại door cell
///   2. Box nằm trong world (có SpriteRenderer)
///   3. Player raycast → [F] → Pickup → DeliveryBox.Destroy()
///
/// ISOMETRIC NOTE:
///   Box position là GridSystem.CellToWorld(doorCell)
///   SpriteRenderer.sortingOrder = GridSystem.WorldToCell(pos).y (Isometric Y-sorting)
/// </summary>
public class DeliveryBox : MonoBehaviour
{
    // ========================================================================
    // IDENTITY
    // ========================================================================

    public string BoxId { get; private set; }
    public string ItemId { get; private set; }
    public int Quantity { get; private set; }

    // ========================================================================
    // COMPONENTS
    // ========================================================================

    private SpriteRenderer _spriteRenderer;

    // ========================================================================
    // LIFETIME
    // ========================================================================

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        UpdateSortingOrder();
    }

    /// <summary>
    /// Khởi tạo box với dữ liệu delivery.
    /// Gọi bởi DeliveryManager.SpawnBox().
    /// </summary>
    public void Initialize(string boxId, string itemId, int quantity)
    {
        BoxId    = boxId;
        ItemId   = itemId;
        Quantity = quantity;
        gameObject.name = $"[DeliveryBox] {itemId} x{quantity} ({boxId})";

        // Set sprite từ PackData (hoặc dùng sprite mặc định)
        if (PackDataIconLoader.TryLoadSprite(itemId, out Sprite sprite))
        {
            _spriteRenderer.sprite = sprite;
        }
    }

    private void UpdateSortingOrder()
    {
        if (_spriteRenderer != null && GridSystem.Instance != null)
        {
            Vector2Int cell = GridSystem.Instance.WorldToCell(transform.position);
            _spriteRenderer.sortingOrder = -cell.y;
        }
    }

    /// <summary>
    /// Gọi khi Player pick up box. Hủy GameObject.
    /// </summary>
    public void OnPickedUp()
    {
        DeliveryEvents.FireBoxDestroyed(this);
        Destroy(gameObject);
    }

    /// <summary>
    /// Gọi khi box expire (quá lâu không nhặt). Hủy GameObject.
    /// </summary>
    public void OnExpired()
    {
        Debug.Log($"[DeliveryBox] Box {BoxId} expired without pickup. Destroying.");
        DeliveryEvents.FireBoxDestroyed(this);
        Destroy(gameObject);
    }
}

/// <summary>
/// Helper static class để load sprite từ PackData.
/// Thay bằng Resources.Load hoặc Addressables theo project setup.
/// </summary>
public static class PackDataIconLoader
{
    public static bool TryLoadSprite(string itemId, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrEmpty(itemId)) return false;

        if (InventoryManager.Instance?.Database != null &&
            InventoryManager.Instance.Database.TryGetPack(itemId, out PackData pack))
        {
            sprite = pack.packFrontSprite;
            return sprite != null;
        }

        return false;
    }
}
