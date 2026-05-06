using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Component trên mỗi pack entry trong InventoryPanelUI.
/// </summary>
public class PackEntryUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _packNameText;
    [SerializeField] private TextMeshProUGUI _countText;
    [SerializeField] private Image _packIconImage;

    private string _packId;
    private System.Action<string> _onClick;
    private System.Action<string> _onDragStart;
    
    private Canvas _mainCanvas;
    private RectTransform _rectTransform;
    private Vector2 _originalPosition;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _mainCanvas = GetComponentInParent<Canvas>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Setup(string packId, int count,
        System.Action<string> onClick, System.Action<string> onDragStart)
    {
        _packId = packId;
        _onClick = onClick;
        _onDragStart = onDragStart;

        if (_packNameText != null) _packNameText.text = packId;
        if (_countText != null) _countText.text = $"x{count}";

        GetComponent<Button>()?.onClick.AddListener(() => _onClick?.Invoke(_packId));
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _originalPosition = _rectTransform.anchoredPosition;
        _onDragStart?.Invoke(_packId);
        
        // Làm mờ khi kéo
        if (_canvasGroup != null) _canvasGroup.alpha = 0.6f;
        _canvasGroup.blocksRaycasts = false; // Cho phép raycast xuyên qua để chạm vào DropZone
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Di chuyển theo chuột
        _rectTransform.anchoredPosition += eventData.delta / (_mainCanvas != null ? _mainCanvas.scaleFactor : 1f);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Quay về vị trí cũ
        _rectTransform.anchoredPosition = _originalPosition;
        
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        if (ShelfManagementUI.Instance != null)
        {
            // Kiểm tra xem có thả trúng vùng DropZone không
            if (RectTransformUtility.RectangleContainsScreenPoint(
                ShelfManagementUI.Instance.GetComponentInChildren<RectTransform>(), 
                eventData.position, eventData.pressEventCamera))
            {
                ShelfManagementUI.Instance.OnDrop(_packId, 1); // Thả 1 gói vào kệ
            }
            
            ShelfManagementUI.Instance.OnDragExit();
        }
    }
}
