// Assets/Scripts/World/IsometricSortingController.cs

using UnityEngine;

/// <summary>
/// Quản lý sorting order cho tất cả sprite trong không gian Isometric.
/// Trong Isometric Z-as-Y: sprite có Y lớn hơn (phía trên màn hình) được vẽ sau (dưới).
/// Sprite có Y nhỏ hơn (phía dưới màn hình) được vẽ trước (trên).
///
/// CÁCH DÙNG: Gắn vào mỗi GameObject cần sorting isometric tự động.
/// </summary>
public class IsometricSortingController : MonoBehaviour
{
    [Header("Sorting Configuration")]
    [Tooltip("Số nhân để chuyển đổi Y position sang sorting order. " +
             "Giá trị âm vì Y tăng lên trên, nhưng sorting order tăng có nghĩa 'vẽ sau' (dưới).")]
    [SerializeField] private float sortingMultiplier = -100f;

    [Tooltip("Offset sorting order, dùng để phân tầng các layer (Floor, Furniture, Character, UI).")]
    [SerializeField] private int sortingOrderOffset = 0;

    [Tooltip("Tự động cập nhật sorting order mỗi frame. " +
             "Tắt nếu object không di chuyển để tiết kiệm performance.")]
    [SerializeField] private bool dynamicSorting = true;

    // Cache reference — Không GetComponent trong Update
    private SpriteRenderer _spriteRenderer;
    private bool _isInitialized;

    private void Awake()
    {
        // Cache component — Chỉ một lần
        if (!TryGetComponent(out _spriteRenderer))
        {
            Debug.LogError($"[IsometricSortingController] {gameObject.name} thiếu SpriteRenderer! " +
                           "Component này yêu cầu SpriteRenderer.");
            enabled = false;
            return;
        }

        _isInitialized = true;
        UpdateSortingOrder();
    }

    private void Update()
    {
        // ⚠️ KHÔNG gọi GetComponent hay GameObject.Find ở đây
        if (!_isInitialized || !dynamicSorting) return;

        UpdateSortingOrder();
    }

    /// <summary>
    /// Cập nhật sorting order dựa trên vị trí Y hiện tại.
    /// Công thức: sortingOrder = round(Y × multiplier) + offset
    /// </summary>
    public void UpdateSortingOrder()
    {
        if (!_isInitialized) return;

        int newOrder = Mathf.RoundToInt(transform.position.y * sortingMultiplier) + sortingOrderOffset;
        _spriteRenderer.sortingOrder = newOrder;
    }

    /// <summary>
    /// API công khai để hệ thống bên ngoài force update sorting ngay lập tức.
    /// Dùng sau khi teleport hoặc đặt vật thể.
    /// </summary>
    public void ForceSortingUpdate()
    {
        UpdateSortingOrder();
    }
}
