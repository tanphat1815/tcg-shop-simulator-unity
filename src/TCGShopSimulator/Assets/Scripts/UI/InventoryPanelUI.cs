// Assets/Scripts/UI/InventoryPanelUI.cs

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel hiển thị kho hàng (pack inventory) của player.
/// Cung cấp Drag & Drop source để player kéo pack lên kệ.
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   Vue component quản lý shopInventory array
/// </summary>
public class InventoryPanelUI : MonoBehaviour
{
    // ========================================================================
    // UI REFERENCES
    // ========================================================================

    [Header("Panel")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Transform _packListContent;
    [SerializeField] private GameObject _packEntryPrefab;

    [Header("Pack Opening")]
    [SerializeField] private Button _btnOpenSelectedPack;
    [SerializeField] private TextMeshProUGUI _selectedPackNameText;
    [SerializeField] private TextMeshProUGUI _selectedPackCountText;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = true;

    // ========================================================================
    // STATE
    // ========================================================================

    private List<PackEntryUI> _currentEntries = new List<PackEntryUI>();
    private string _selectedPackId;

    // ========================================================================
    // VÒNG ĐỜI
    // ========================================================================

    private void OnEnable()
    {
        GameEconomyEvents.OnPackInventoryChanged += HandlePackInventoryChanged;
        GameEconomyEvents.OnPackOpeningCompleted += HandlePackOpeningCompleted;
    }

    private void OnDisable()
    {
        GameEconomyEvents.OnPackInventoryChanged -= HandlePackInventoryChanged;
        GameEconomyEvents.OnPackOpeningCompleted -= HandlePackOpeningCompleted;
    }

    private void Awake()
    {
        AutoSetup();

        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    [ContextMenu("Auto Setup UI")]
    public void AutoSetup()
    {
        Debug.Log($"[InventoryPanelUI] Running AutoSetup on {gameObject.name}...");

        if (_panelRoot == null) {
            var panelTransform = transform.Find("Panel");
            if (panelTransform != null) _panelRoot = panelTransform.gameObject;
            else _panelRoot = gameObject;
        }

        // Find List Content
        if (_packListContent == null) {
            _packListContent = transform.Find("Panel/Content") ?? transform.Find("Panel/Scroll View/Viewport/Content");
        }

        // Find Buttons & Texts
        if (_btnOpenSelectedPack == null) _btnOpenSelectedPack = FindComponentInChild<Button>("BtnOpenPack");
        if (_selectedPackNameText == null) _selectedPackNameText = FindComponentInChild<TextMeshProUGUI>("SelectedPackName");
        if (_selectedPackCountText == null) _selectedPackCountText = FindComponentInChild<TextMeshProUGUI>("SelectedPackCount");

        Debug.Log("[InventoryPanelUI] AutoSetup complete. Please check the Inspector.");
    }

    private T FindComponentInChild<T>(string name) where T : Component
    {
        Transform t = transform.Find(name);
        if (t == null) t = transform.Find("Panel/" + name);
        return t?.GetComponent<T>();
    }

    private void Start()
    {
        RefreshInventoryDisplay();

        if (_btnOpenSelectedPack != null)
            _btnOpenSelectedPack.onClick.AddListener(OnOpenSelectedPackClicked);
    }

    // ========================================================================
    // INVENTORY DISPLAY
    // ========================================================================

    private void Update()
    {
        // Nhấn phím I để đóng/mở kho hàng
        if (UnityEngine.InputSystem.Keyboard.current != null && 
            UnityEngine.InputSystem.Keyboard.current.iKey.wasPressedThisFrame)
        {
            TogglePanel();
        }
    }

    public void TogglePanel()
    {
        bool nextState = !(_panelRoot != null && _panelRoot.activeSelf);
        if (_panelRoot != null) _panelRoot.SetActive(nextState);
        
        if (nextState) RefreshInventoryDisplay();
    }

    /// <summary>
    /// Refresh toàn bộ inventory display.
    /// </summary>
    public void RefreshInventoryDisplay()
    {
        if (InventoryManager.Instance == null) return;
        if (_packListContent == null || _packEntryPrefab == null) return;

        foreach (var entry in _currentEntries)
            if (entry != null)
                Destroy(entry.gameObject);
        _currentEntries.Clear();

        var inventory = InventoryManager.Instance.GetFullPackInventory();

        foreach (var kvp in inventory)
        {
            string packId = kvp.Key;
            int count = kvp.Value;

            if (count <= 0) continue;

            GameObject entryGO = Instantiate(_packEntryPrefab, _packListContent);
            var entry = entryGO.GetComponent<PackEntryUI>();

            if (entry != null)
            {
                entry.Setup(packId, count, OnPackClicked, OnPackDragStarted);
                _currentEntries.Add(entry);
            }
        }

        if (_verboseLogging)
            Debug.Log($"[InventoryPanelUI] Refreshed. {_currentEntries.Count} pack types displayed.");
    }

    private void OnPackClicked(string packId)
    {
        _selectedPackId = packId;

        if (_selectedPackNameText != null)
            _selectedPackNameText.text = packId;

        if (_selectedPackCountText != null && InventoryManager.Instance != null)
            _selectedPackCountText.text = $"x{InventoryManager.Instance.GetPackCount(packId)}";

        if (_btnOpenSelectedPack != null)
            _btnOpenSelectedPack.interactable = InventoryManager.Instance.GetPackCount(packId) > 0;
    }

    private void OnPackDragStarted(string packId)
    {
        if (ShelfManagementUI.Instance != null && ShelfManagementUI.Instance.IsVisible)
        {
            ShelfManagementUI.Instance.OnDragEnter(packId);
        }
    }

    // ========================================================================
    // PACK OPENING
    // ========================================================================

    private void OnOpenSelectedPackClicked()
    {
        if (string.IsNullOrEmpty(_selectedPackId)) return;
        if (InventoryManager.Instance == null) return;

        if (InventoryManager.Instance.GetPackCount(_selectedPackId) <= 0)
        {
            Debug.LogWarning($"[InventoryPanelUI] No packs of '{_selectedPackId}' to open.");
            return;
        }

        PackOpeningUI.Instance?.StartPackOpening(_selectedPackId);
    }

    // ========================================================================
    // EVENT HANDLERS
    // ========================================================================

    private void HandlePackInventoryChanged(string packId, int prev, int next)
    {
        RefreshInventoryDisplay();
    }

    private void HandlePackOpeningCompleted(GachaResult result)
    {
        if (!string.IsNullOrEmpty(_selectedPackId) && _selectedPackCountText != null)
        {
            if (InventoryManager.Instance != null)
                _selectedPackCountText.text = $"x{InventoryManager.Instance.GetPackCount(_selectedPackId)}";
        }

        if (_btnOpenSelectedPack != null && InventoryManager.Instance != null)
            _btnOpenSelectedPack.interactable = InventoryManager.Instance.GetPackCount(_selectedPackId) > 0;
    }
}
