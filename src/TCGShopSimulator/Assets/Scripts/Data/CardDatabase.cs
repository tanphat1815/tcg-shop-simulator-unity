// Assets/Scripts/Data/CardDatabase.cs

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject tổng hợp toàn bộ catalogue thẻ bài và pack.
/// Là điểm truy cập trung tâm thay thế apiStore trong hệ thống cũ.
///
/// MAPPING TỪ HỆ THỐNG CŨ:
///   apiStore.shopItems     → allPacks (list) + packLookup (dictionary)
///   apiStore.setCardsCache → cardsBySetId (dictionary)
///   dbService.query(...)   → Không còn cần thiết — data đã load sẵn
///
/// CÁCH TẠO ASSET:
///   Right-click > Create > TCGShop > Data > Card Database
///   Chỉ cần MỘT database duy nhất: CardDatabase_Main.asset
/// </summary>
[CreateAssetMenu(
    fileName = "CardDatabase_Main",
    menuName = "TCGShop/Data/Card Database",
    order = 3
)]
public class CardDatabase : ScriptableObject
{
    // =========================================================================
    // DATA LISTS
    // =========================================================================

    [Header("Content")]
    [Tooltip("Tất cả PackData assets. Kéo thả từ ScriptableObjects/Packs/.")]
    public List<PackData> allPacks = new List<PackData>();

    [Tooltip("Tất cả RarityDefinition assets. Kéo thả từ ScriptableObjects/Rarities/.")]
    public List<RarityDefinition> allRarities = new List<RarityDefinition>();

    // =========================================================================
    // RUNTIME LOOKUP DICTIONARIES — Build khi Initialize() được gọi
    // =========================================================================

    private Dictionary<string, PackData> _packLookup;
    private Dictionary<string, CardData> _cardLookup;
    private Dictionary<string, List<CardData>> _cardsBySetId;
    private bool _isInitialized = false;

    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    /// <summary>
    /// Build tất cả lookup dictionaries từ lists.
    /// Phải gọi một lần trước khi dùng các hàm lookup.
    /// Gọi từ InventoryManager.Awake().
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized) return;

        _packLookup = new Dictionary<string, PackData>();
        _cardLookup = new Dictionary<string, CardData>();
        _cardsBySetId = new Dictionary<string, List<CardData>>();

        foreach (var pack in allPacks)
        {
            if (pack == null || string.IsNullOrEmpty(pack.packId))
            {
                Debug.LogWarning("[CardDatabase] Pack null hoặc thiếu packId. Bỏ qua.");
                continue;
            }

            if (_packLookup.ContainsKey(pack.packId))
            {
                Debug.LogWarning($"[CardDatabase] Duplicate packId: '{pack.packId}'. Giữ lại entry đầu tiên.");
                continue;
            }

            _packLookup[pack.packId] = pack;

            foreach (var card in pack.availableCards)
            {
                if (card == null || string.IsNullOrEmpty(card.cardId)) continue;

                if (!_cardLookup.ContainsKey(card.cardId))
                    _cardLookup[card.cardId] = card;

                if (!_cardsBySetId.ContainsKey(card.setId))
                    _cardsBySetId[card.setId] = new List<CardData>();

                if (!_cardsBySetId[card.setId].Contains(card))
                    _cardsBySetId[card.setId].Add(card);
            }
        }

        _isInitialized = true;
        Debug.Log($"[CardDatabase] Initialized: {_packLookup.Count} packs, " +
                  $"{_cardLookup.Count} unique cards, " +
                  $"{_cardsBySetId.Count} sets.");
    }

    // =========================================================================
    // LOOKUP API — O(1) complexity
    // =========================================================================

    /// <summary>
    /// Tìm PackData theo packId. O(1).
    /// </summary>
    public bool TryGetPack(string packId, out PackData pack)
    {
        EnsureInitialized();
        return _packLookup.TryGetValue(packId, out pack);
    }

    /// <summary>
    /// Tìm CardData theo cardId. O(1).
    /// </summary>
    public bool TryGetCard(string cardId, out CardData card)
    {
        EnsureInitialized();
        return _cardLookup.TryGetValue(cardId, out card);
    }

    /// <summary>
    /// Lấy tất cả cards thuộc một set. O(1).
    /// </summary>
    public List<CardData> GetCardsBySet(string setId)
    {
        EnsureInitialized();
        return _cardsBySetId.TryGetValue(setId, out var cards)
            ? cards
            : new List<CardData>();
    }

    /// <summary>
    /// Lấy tất cả packs người chơi có thể mua ở shop level hiện tại.
    /// </summary>
    public List<PackData> GetAvailablePacks(int currentShopLevel)
    {
        EnsureInitialized();
        var result = new List<PackData>();
        foreach (var pack in allPacks)
        {
            if (pack != null && pack.requiredShopLevel <= currentShopLevel)
                result.Add(pack);
        }
        return result;
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            Debug.LogWarning("[CardDatabase] Chưa được Initialize(). Gọi Initialize() trước.");
            Initialize();
        }
    }
}
