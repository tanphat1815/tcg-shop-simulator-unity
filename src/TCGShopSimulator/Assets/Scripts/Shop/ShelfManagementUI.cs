// Assets/Scripts/Shop/ShelfManagementUI.cs

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel UI quản lý stock trên kệ hàng.
/// Hiển thị khi Player raycast vào ShelfInstance.
///
/// LUỒNG:
///   1. Player raycast vào ShelfInstance → ShelfInstance.OnShelfInteracted
///   2. ShelfManagementUI.Show(shelf) → Pause game, hiện panel
///   3. Player chọn pack từ InventoryPanel → Drag vào slot trên kệ
///   4. Player set giá → shelfInstance.SetPrice()
///   5. Player đóng panel → Unpause game
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   ShelfManagementMenu.vue + SetPriceModal.vue + TierSelectionModal.vue
/// </summary>
public class ShelfManagementUI : MonoBehaviour
{
    // ========================================================================
    // SINGLETON
    // ========================================================================

    public static ShelfManagementUI Instance { get; private set; }

    // ========================================================================
    // UI REFERENCES
    // ========================================================================

    [Header("Panel")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private RectTransform _panelRect;

    [Header("Shelf Info")]
    [SerializeField] private TextMeshProUGUI _shelfNameText;
    [SerializeField] private TextMeshProUGUI _shelfRoleText;

    [Header("Stock Display")]
    [SerializeField] private TextMeshProUGUI _currentItemNameText;
    [SerializeField] private TextMeshProUGUI _currentStockCountText;
    [SerializeField] private TextMeshProUGUI _currentPriceText;
    [SerializeField] private Image _itemIconImage;

    [Header("Price Editor")]
    [SerializeField] private TMP_InputField _priceInputField;
    [SerializeField] private Button _btnSetMarketPrice;
    [SerializeField] private Button _btnIncrease10;
    [SerializeField] private Button _btnDecrease10;

    [Header("Stock Actions")]
    [SerializeField] private Button _btnAddStock;
    [SerializeField] private Button _btnClearStock;
    [SerializeField] private Button _btnClose;

    [Header("Inventory Drop Zone")]
    [SerializeField] private RectTransform _dropZoneRect;

    [Header("Audio")]
    [SerializeField] private AudioClip _openSound;
    [SerializeField] private AudioClip _closeSound;
    [SerializeField] private AudioClip _placeItemSound;

    // ========================================================================
    // STATE
    // ========================================================================

    private ShelfInstance _activeShelf;
    private bool _isVisible = false;
    private float _previousTimeScale;

    // ========================================================================
    // VÒNG ĐỜI
    // ========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        AutoSetup();

        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    [ContextMenu("Auto Setup UI")]
    public void AutoSetup()
    {
        Debug.Log($"[ShelfManagementUI] Running AutoSetup on {gameObject.name}...");
        
        // Find Panel Root (if not assigned)
        if (_panelRoot == null) {
            var panelTransform = transform.Find("Panel");
            if (panelTransform != null) _panelRoot = panelTransform.gameObject;
            else _panelRoot = gameObject;
        }
        
        if (_panelRect == null) _panelRect = _panelRoot?.GetComponent<RectTransform>();
        if (_dropZoneRect == null) _dropZoneRect = transform.Find("DropZone")?.GetComponent<RectTransform>() ?? _panelRect;

        // Find Texts
        if (_shelfNameText == null) _shelfNameText = FindComponentInChild<TextMeshProUGUI>("ShelfName");
        if (_shelfRoleText == null) _shelfRoleText = FindComponentInChild<TextMeshProUGUI>("ShelfRole");
        if (_currentItemNameText == null) _currentItemNameText = FindComponentInChild<TextMeshProUGUI>("ItemName");
        if (_currentStockCountText == null) _currentStockCountText = FindComponentInChild<TextMeshProUGUI>("StockCount");
        if (_currentPriceText == null) _currentPriceText = FindComponentInChild<TextMeshProUGUI>("PriceText");

        // Find Images
        if (_itemIconImage == null) _itemIconImage = FindComponentInChild<Image>("ItemIcon");

        // Find Input
        if (_priceInputField == null) _priceInputField = FindComponentInChild<TMP_InputField>("PriceInput");

        // Find Buttons
        if (_btnSetMarketPrice == null) _btnSetMarketPrice = FindComponentInChild<Button>("BtnMarket");
        if (_btnIncrease10 == null) _btnIncrease10 = FindComponentInChild<Button>("BtnPlus");
        if (_btnDecrease10 == null) _btnDecrease10 = FindComponentInChild<Button>("BtnMinus");
        if (_btnClearStock == null) _btnClearStock = FindComponentInChild<Button>("BtnClear");
        if (_btnClose == null) _btnClose = FindComponentInChild<Button>("BtnClose");
        
        Debug.Log("[ShelfManagementUI] AutoSetup complete. Please check the Inspector.");
    }

    private T FindComponentInChild<T>(string name) where T : Component
    {
        Transform t = transform.Find(name);
        if (t == null) t = transform.Find("Panel/" + name); // Search inside Panel too
        return t?.GetComponent<T>();
    }

    private void Start()
    {
        SetupEventListeners();
        SetupButtonListeners();
    }

    private void OnDestroy()
    {
        RemoveEventListeners();
    }

    // ========================================================================
    // PUBLIC API
    // ========================================================================

    /// <summary>
    /// Hiển thị panel quản lý cho kệ hàng được chỉ định.
    /// Pause game khi mở.
    ///
    /// GỌI TỪ:
    ///   ShelfInstance.OnShelfInteracted event (via raycast)
    /// </summary>
    public void Show(ShelfInstance shelf)
    {
        if (shelf == null) return;

        _activeShelf = shelf;
        _isVisible = true;

        _previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        UpdateShelfInfo();
        UpdateStockDisplay();
        UpdatePriceEditor();

        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        PlaySound(_openSound);

        Debug.Log($"[ShelfManagementUI] Opened for shelf: {shelf}");
    }

    /// <summary>
    /// Đóng panel và tiếp tục game.
    /// </summary>
    public void Hide()
    {
        if (!_isVisible) return;

        _isVisible = false;
        _activeShelf = null;

        Time.timeScale = _previousTimeScale;

        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        PlaySound(_closeSound);

        Debug.Log("[ShelfManagementUI] Closed.");
    }

    // ========================================================================
    // DRAG & DROP
    // ========================================================================

    /// <summary>
    /// Gọi từ InventoryPanelUI khi player bắt đầu drag một pack.
    /// </summary>
    public void OnDragEnter(string packId)
    {
        if (_dropZoneRect == null) return;

        var image = _dropZoneRect.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.3f, 0.8f, 0.3f, 0.3f);
    }

    /// <summary>
    /// Gọi khi player thả pack vào drop zone.
    /// </summary>
    public void OnDrop(string packId, int quantity)
    {
        if (_activeShelf == null || string.IsNullOrEmpty(packId)) return;

        float marketPrice = GetMarketPrice(packId);

        _activeShelf.SetStock(packId, quantity, marketPrice, marketPrice);

        UpdateStockDisplay();

        PlaySound(_placeItemSound);

        Debug.Log($"[ShelfManagementUI] Placed {quantity}x {packId} on shelf. Price: ${marketPrice:F2}");

        GameEconomyEvents.FireShelfStockChanged(new ShelfStockChange(
            _activeShelf, packId, 0, quantity, marketPrice,
            ShelfStockChangeReason.PlayerRestocked));
    }

    /// <summary>
    /// Gọi khi player kéo pack ra khỏi drop zone.
    /// </summary>
    public void OnDragExit()
    {
        if (_dropZoneRect == null) return;

        var image = _dropZoneRect.GetComponent<Image>();
        if (image != null)
            image.color = Color.white;
    }

    // ========================================================================
    // PRICE EDITING
    // ========================================================================

    private void SetupButtonListeners()
    {
        _btnClose?.GetComponent<Button>()?.onClick.AddListener(Hide);

        _btnSetMarketPrice?.GetComponent<Button>()?.onClick.AddListener(SetMarketPrice);
        _btnIncrease10?.GetComponent<Button>()?.onClick.AddListener(() => AdjustPrice(1.1f));
        _btnDecrease10?.GetComponent<Button>()?.onClick.AddListener(() => AdjustPrice(0.9f));

        _btnClearStock?.GetComponent<Button>()?.onClick.AddListener(ClearStock);

        if (_priceInputField != null)
        {
            _priceInputField.onEndEdit.AddListener(OnPriceInputChanged);
        }
    }

    private void SetMarketPrice()
    {
        if (_activeShelf == null) return;

        float marketPrice = GetMarketPrice(_activeShelf.DisplayedItemId);
        _activeShelf.SetPrice(marketPrice);
        UpdatePriceEditor();

        Debug.Log($"[ShelfManagementUI] Price set to market: ${marketPrice:F2}");
    }

    private void AdjustPrice(float multiplier)
    {
        if (_activeShelf == null) return;

        float newPrice = _activeShelf.CurrentSellPrice * multiplier;
        newPrice = Mathf.Round(newPrice * 100f) / 100f;
        newPrice = Mathf.Max(0.01f, newPrice);

        _activeShelf.SetPrice(newPrice);
        UpdatePriceEditor();

        Debug.Log($"[ShelfManagementUI] Price adjusted x{multiplier:F2}: ${newPrice:F2}");
    }

    private void OnPriceInputChanged(string inputValue)
    {
        if (_activeShelf == null) return;

        if (float.TryParse(inputValue, out float newPrice))
        {
            newPrice = Mathf.Max(0.01f, newPrice);
            _activeShelf.SetPrice(newPrice);
            UpdatePriceEditor();
        }
    }

    private void ClearStock()
    {
        if (_activeShelf == null) return;

        string itemId = _activeShelf.DisplayedItemId;
        int qty = _activeShelf.StockCount;

        if (!string.IsNullOrEmpty(itemId) && qty > 0)
        {
            InventoryManager.Instance?.AddPack(itemId, qty);

            GameEconomyEvents.FireShelfStockChanged(new ShelfStockChange(
                _activeShelf, itemId, qty, 0, _activeShelf.CurrentSellPrice,
                ShelfStockChangeReason.PlayerCleared));
        }

        _activeShelf.SetStock(string.Empty, 0, 0f, 0f);
        UpdateStockDisplay();
        UpdatePriceEditor();
    }

    // ========================================================================
    // UI UPDATE
    // ========================================================================

    private void UpdateShelfInfo()
    {
        if (_activeShelf == null) return;

        if (_shelfNameText != null)
            _shelfNameText.text = _activeShelf.name;

        if (_shelfRoleText != null)
            _shelfRoleText.text = _activeShelf.IsSellingShelf ? "SELLING" : "STORAGE";
    }

    private void UpdateStockDisplay()
    {
        if (_activeShelf == null) return;

        if (_currentItemNameText != null)
        {
            _currentItemNameText.text = string.IsNullOrEmpty(_activeShelf.DisplayedItemId)
                ? "(Empty)"
                : _activeShelf.DisplayedItemId;
        }

        if (_currentStockCountText != null)
            _currentStockCountText.text = $"x{_activeShelf.StockCount}";

        if (_currentPriceText != null)
        {
            _currentPriceText.text = _activeShelf.StockCount > 0
                ? $"${_activeShelf.CurrentSellPrice:F2}"
                : "—";
        }
    }

    private void UpdatePriceEditor()
    {
        if (_activeShelf == null) return;

        if (_priceInputField != null)
            _priceInputField.text = _activeShelf.CurrentSellPrice.ToString("F2");
    }

    // ========================================================================
    // EVENT SUBSCRIPTION
    // ========================================================================

    private void SetupEventListeners()
    {
        GameEconomyEvents.OnShelfStockChanged += HandleShelfStockChanged;
        GameEconomyEvents.OnMoneyChanged += HandleMoneyChanged;
    }

    private void RemoveEventListeners()
    {
        GameEconomyEvents.OnShelfStockChanged -= HandleShelfStockChanged;
        GameEconomyEvents.OnMoneyChanged -= HandleMoneyChanged;
    }

    private void HandleShelfStockChanged(ShelfStockChange change)
    {
        if (_activeShelf != null && change.Shelf == _activeShelf)
            UpdateStockDisplay();
    }

    private void HandleMoneyChanged(float prev, float next) { }

    // ========================================================================
    // UTILITY
    // ========================================================================

    private float GetMarketPrice(string packId)
    {
        if (InventoryManager.Instance?.Database != null &&
            InventoryManager.Instance.Database.TryGetPack(packId, out var packData))
        {
            return packData.defaultSellPrice;
        }
        return 5f;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, Vector3.zero);
    }

    // ========================================================================
    // PROPERTIES
    // ========================================================================

    public bool IsVisible => _isVisible;
    public ShelfInstance ActiveShelf => _activeShelf;
}
