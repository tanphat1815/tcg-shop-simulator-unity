// Assets/Scripts/Inventory/InventoryManager.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton quản lý toàn bộ kho đồ của người chơi.
///
/// MAPPING TỪ HỆ THỐNG CŨ (inventoryStore.ts):
///   shopInventory: Record<itemId, quantity>   → _packInventory: Dictionary<string, int>
///   personalBinder: Record<cardId, quantity>  → _cardBinder: Dictionary<string, int>
///   shopItems: Record<itemId, StockItemInfo> → cardDatabase (CardDatabase reference)
///
/// TẠI SAO DÙNG DICTIONARY:
///   Dictionary<string, int> cho phép lookup O(1) — bất kể có 10 hay 10,000 items.
///   List<T>.Find() là O(n) — chậm hơn khi inventory lớn.
///
/// LƯU Ý: InventoryManager quản lý SỐ LƯỢNG.
///        Metadata (tên, giá, sprite) lấy từ CardDatabase.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    // =========================================================================
    // SINGLETON
    // =========================================================================

    public static InventoryManager Instance { get; private set; }

    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    [Header("Database Reference")]
    [Tooltip("CardDatabase ScriptableObject chứa toàn bộ catalogue. " +
             "Kéo thả CardDatabase_Main.asset vào đây.")]
    [SerializeField] private CardDatabase cardDatabase;

    // =========================================================================
    // INVENTORY STATE — Dictionary<string, int> để O(1) lookup
    // =========================================================================

    private Dictionary<string, int> _packInventory = new Dictionary<string, int>();
    private Dictionary<string, int> _cardBinder = new Dictionary<string, int>();

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

        if (cardDatabase == null)
        {
            Debug.LogError("[InventoryManager] cardDatabase chưa được assign trong Inspector! " +
                           "Kéo thả CardDatabase_Main.asset vào field 'Card Database'.");
            enabled = false;
            return;
        }

        cardDatabase.Initialize();
        Debug.Log("[InventoryManager] Initialized.");
    }

    // =========================================================================
    // PACK INVENTORY API
    // =========================================================================

    /// <summary>
    /// Thêm pack vào kho. O(1).
    /// </summary>
    public void AddPack(string packId, int amount = 1)
    {
        if (string.IsNullOrEmpty(packId) || amount <= 0) return;

        if (!_packInventory.ContainsKey(packId))
            _packInventory[packId] = 0;

        _packInventory[packId] += amount;
        Debug.Log($"[InventoryManager] Added {amount}x '{packId}'. Total: {_packInventory[packId]}");
    }

    /// <summary>
    /// Kiểm tra số lượng pack trong kho. O(1).
    /// </summary>
    public int GetPackCount(string packId)
    {
        return _packInventory.TryGetValue(packId, out int count) ? count : 0;
    }

    /// <summary>
    /// Mở một pack: Giảm kho, chạy Gacha, thêm thẻ vào binder.
    /// Trả về null nếu không đủ pack trong kho.
    /// </summary>
    public GachaResult OpenPack(string packId)
    {
        if (GetPackCount(packId) <= 0)
        {
            Debug.LogWarning($"[InventoryManager] Không đủ pack '{packId}' trong kho!");
            return null;
        }

        if (!cardDatabase.TryGetPack(packId, out PackData pack))
        {
            Debug.LogError($"[InventoryManager] Không tìm thấy PackData cho packId '{packId}'!");
            return null;
        }

        _packInventory[packId]--;
        if (_packInventory[packId] <= 0)
            _packInventory.Remove(packId);

        GachaResult result = GachaEngine.OpenPack(pack);

        foreach (var card in result.DroppedCards)
        {
            if (card != null)
                AddCardToBinder(card.cardId);
        }

        Debug.Log($"[InventoryManager] Opened pack '{packId}'. " +
                  $"Got {result.DroppedCards.Count} cards, +{result.TotalXpGained} XP.");
        Debug.Log(result.ToDebugString());

        return result;
    }

    // =========================================================================
    // CARD BINDER API
    // =========================================================================

    /// <summary>
    /// Thêm thẻ vào binder. O(1).
    /// </summary>
    public void AddCardToBinder(string cardId, int amount = 1)
    {
        if (string.IsNullOrEmpty(cardId) || amount <= 0) return;

        if (!_cardBinder.ContainsKey(cardId))
            _cardBinder[cardId] = 0;

        _cardBinder[cardId] += amount;
    }

    /// <summary>
    /// Số lượng một thẻ trong binder. O(1).
    /// </summary>
    public int GetCardCount(string cardId)
    {
        return _cardBinder.TryGetValue(cardId, out int count) ? count : 0;
    }

    /// <summary>
    /// Toàn bộ binder dưới dạng read-only.
    /// </summary>
    public IReadOnlyDictionary<string, int> GetFullBinder()
    {
        return _cardBinder;
    }

    /// <summary>
    /// Toàn bộ pack inventory dưới dạng read-only.
    /// </summary>
    public IReadOnlyDictionary<string, int> GetFullPackInventory()
    {
        return _packInventory;
    }

    // =========================================================================
    // DATABASE ACCESS
    // =========================================================================

    public CardDatabase Database => cardDatabase;
}
