// Assets/Scripts/Events/GameEconomyEvents.cs

using System;

/// <summary>
/// Static class chứa tất cả economy events của game.
/// Dùng Observer Pattern: Publishers fire events → Subscribers tự cập nhật.
///
/// NGUỒN SỰ THẬT DUY NHẤT cho tất cả cross-system communication.
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   Vue reactive stores: statsStore, inventoryStore, furnitureStore
///   Ở đây dùng C# events thay thế reactive data binding.
/// </summary>
public static class GameEconomyEvents
{
    // ========================================================================
    // MONEY & REVENUE
    // ========================================================================

    /// <summary>
    /// Kích hoạt khi tiền trong cửa hàng thay đổi.
    /// Subscriber: MoneyDisplay UI, StatsPopup, SoundEffectPlayer
    /// </summary>
    public static event Action<float, float> OnMoneyChanged;

    public static void FireMoneyChanged(float previousAmount, float newAmount)
    {
        OnMoneyChanged?.Invoke(previousAmount, newAmount);
    }

    // ========================================================================
    // TRANSACTIONS
    // ========================================================================

    /// <summary>
    /// Kích hoạt khi một giao dịch hoàn thành (NPC mua pack).
    /// Subscriber: StatsPopup, AchievementChecker, DailyStatsTracker
    /// </summary>
    public static event Action<TransactionReceipt> OnTransactionCompleted;

    public static void FireTransactionCompleted(TransactionReceipt receipt)
    {
        OnTransactionCompleted?.Invoke(receipt);
    }

    // ========================================================================
    // SHELF INVENTORY
    // ========================================================================

    /// <summary>
    /// Kích hoạt khi stock trên kệ thay đổi (đặt hàng, lấy hàng).
    /// Subscriber: ShelfSlotUI, ShelfManagementPanel
    /// </summary>
    public static event Action<ShelfStockChange> OnShelfStockChanged;

    public static void FireShelfStockChanged(ShelfStockChange change)
    {
        OnShelfStockChanged?.Invoke(change);
    }

    // ========================================================================
    // PACK OPENING
    // ========================================================================

    /// <summary>
    /// Kích hoạt khi pack được mở (trước khi animation bắt đầu).
    /// Subscriber: PackOpeningUI, SoundEffectPlayer
    /// </summary>
    public static event Action<string, GachaResult> OnPackOpeningStarted;

    public static void FirePackOpeningStarted(string packId, GachaResult result)
    {
        OnPackOpeningStarted?.Invoke(packId, result);
    }

    /// <summary>
    /// Kích hoạt khi animation mở pack kết thúc.
    /// Subscriber: InventoryManager (thêm cards vào binder), AchievementChecker
    /// </summary>
    public static event Action<GachaResult> OnPackOpeningCompleted;

    public static void FirePackOpeningCompleted(GachaResult result)
    {
        OnPackOpeningCompleted?.Invoke(result);
    }

    /// <summary>
    /// Kích hoạt khi một lá bài được reveal trong animation.
    /// Subscriber: CardRevealEffect (sync timing), SFXPlayer
    /// </summary>
    public static event Action<int, CardData> OnCardRevealed;

    public static void FireCardRevealed(int cardIndex, CardData cardData)
    {
        OnCardRevealed?.Invoke(cardIndex, cardData);
    }

    // ========================================================================
    // PLAYER INVENTORY
    // ========================================================================

    /// <summary>
    /// Kích hoạt khi pack inventory thay đổi (thêm/bớt pack).
    /// Subscriber: InventoryPanelUI
    /// </summary>
    public static event Action<string, int, int> OnPackInventoryChanged;

    public static void FirePackInventoryChanged(string packId, int prev, int next)
    {
        OnPackInventoryChanged?.Invoke(packId, prev, next);
    }

    /// <summary>
    /// Kích hoạt khi card binder thay đổi.
    /// Subscriber: BinderUI, CollectionStatsUI
    /// </summary>
    public static event Action<string, int, int> OnBinderChanged;

    public static void FireBinderChanged(string cardId, int prev, int next)
    {
        OnBinderChanged?.Invoke(cardId, prev, next);
    }

    // ========================================================================
    // XP & LEVEL
    // ========================================================================

    /// <summary>
    /// Kích hoạt khi player nhận XP.
    /// Subscriber: XpBarUI, LevelUpPopup
    /// </summary>
    public static event Action<int, int, int> OnXpGained;

    public static void FireXpGained(int xpGained, int currentXp, int level)
    {
        OnXpGained?.Invoke(xpGained, currentXp, level);
    }

    /// <summary>
    /// Kích hoạt khi player level up.
    /// </summary>
    public static event Action<int> OnLevelUp;

    public static void FireLevelUp(int newLevel)
    {
        OnLevelUp?.Invoke(newLevel);
    }

    /// <summary>
    /// Kích hoạt khi toàn bộ game state được load xong từ save.
    /// Subscriber: MoneyDisplay, InventoryPanelUI, any UI cần refresh toàn bộ.
    /// </summary>
    public static event Action<GameData> OnGameDataLoaded;

    public static void FireGameDataLoaded(GameData data)
    {
        OnGameDataLoaded?.Invoke(data);
    }

    /// <summary>
    /// Kích hoạt khi player mua expansion.
    /// </summary>
    public static event Action<int, float> OnExpansionBought;

    public static void FireExpansionBought(int newLevel, float cost) =>
        OnExpansionBought?.Invoke(newLevel, cost);

    /// <summary>
    /// Kích hoạt khi XP thay đổi.
    /// </summary>
    public static event Action<int, int> OnXpChanged;

    public static void FireXpChanged(int currentExp, int requiredExp) =>
        OnXpChanged?.Invoke(currentExp, requiredExp);

    // ========================================================================
    // MATCH / PLAY TABLE
    // ========================================================================

    /// <summary>
    /// Kích hoạt khi một match tại bàn chơi kết thúc (sau 12 giây).
    /// </summary>
    public static event Action<int, string> OnMatchFinished;

    public static void FireMatchFinished(int xpAmount, string source)
    {
        OnMatchFinished?.Invoke(xpAmount, source);
    }
}

// ========================================================================
// DATA CLASSES — Immutable Records cho Event Payloads
// ========================================================================

/// <summary>
/// Receipt của một giao dịch mua hàng.
/// </summary>
public readonly struct TransactionReceipt
{
    public string CustomerId { get; }
    public string ItemId { get; }
    public float Price { get; }
    public DateTime Timestamp { get; }

    public TransactionReceipt(string customerId, string itemId, float price)
    {
        CustomerId = customerId;
        ItemId = itemId;
        Price = price;
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// Thông tin thay đổi stock trên kệ.
/// </summary>
public readonly struct ShelfStockChange
{
    public ShelfInstance Shelf { get; }
    public string ItemId { get; }
    public int PreviousCount { get; }
    public int NewCount { get; }
    public float Price { get; }
    public ShelfStockChangeReason Reason { get; }

    public ShelfStockChange(
        ShelfInstance shelf, string itemId,
        int prevCount, int newCount, float price,
        ShelfStockChangeReason reason)
    {
        Shelf = shelf;
        ItemId = itemId;
        PreviousCount = prevCount;
        NewCount = newCount;
        Price = price;
        Reason = reason;
    }
}

public enum ShelfStockChangeReason
{
    PlayerRestocked,
    NpcPurchased,
    PlayerCleared
}
