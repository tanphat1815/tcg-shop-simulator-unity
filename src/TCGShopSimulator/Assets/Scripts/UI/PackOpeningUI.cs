// Assets/Scripts/UI/PackOpeningUI.cs

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel UI hiển thị khi player mở pack.
/// Quản lý PlayPackOpeningRoutine() — coroutine chính cho toàn bộ animation.
///
/// ANIMATION FLOW (PlayPackOpeningRoutine):
///   PHASE 0: Setup        — Dim screen, spawn pack sprite, 0.5s
///   PHASE 1: Pack Open    — Particle burst, pack "splits", 0.8s
///   PHASE 2: Card Reveals — Sequential card flips (5 cards), 0.4s/card = 2.0s total
///   PHASE 3: Summary      — Show XP gained, cards added, 1.5s
///   PHASE 4: Close        — Fade out panel, resume game
///
/// TƯƠNG ĐƯƠNG HỆ THỐNG CŨ:
///   packPhase = 'pack_visible' → SQL query → sortedCards →
///   'cards_visible' → gainExp()
///
/// NOTE: Pack opening animation dùng real-time (Time.timeScale không bị thay đổi).
/// Pause game chỉ được dùng trong ShelfManagementUI.
/// </summary>
public class PackOpeningUI : MonoBehaviour
{
    // ========================================================================
    // SINGLETON
    // ========================================================================

    public static PackOpeningUI Instance { get; private set; }

    // ========================================================================
    // UI REFERENCES
    // ========================================================================

    [Header("Panel")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private CanvasGroup _panelCanvasGroup;
    [SerializeField] private RectTransform _panelRect;

    [Header("Pack Display")]
    [SerializeField] private Image _packImage;
    [SerializeField] private TextMeshProUGUI _packNameText;
    [SerializeField] private Sprite _defaultPackSprite;

    [Header("Card Slots")]
    [Tooltip("5 Image components cho 5 lá bài. Kéo từ prefab hoặc tạo runtime.")]
    [SerializeField] private List<Image> _cardSlotImages = new List<Image>();

    [Header("Card Back Sprite (placeholder).")]
    [SerializeField] private Sprite _cardBackSprite;
    [SerializeField] private Sprite _cardFrontPlaceholderSprite;

    [Header("FX")]
    [Tooltip("Particle system khi pack mở. Instantiate nếu có prefab.")]
    [SerializeField] private GameObject _packOpenParticlePrefab;

    [Tooltip("Image overlay làm tối màn hình khi mở pack.")]
    [SerializeField] private Image _screenDimOverlay;

    [Header("Summary Panel")]
    [SerializeField] private GameObject _summaryContent;
    [SerializeField] private TextMeshProUGUI _xpGainedText;
    [SerializeField] private TextMeshProUGUI _cardsRevealedText;
    [SerializeField] private Button _btnContinue;

    [Header("Animation Config")]
    [SerializeField] private float _dimDuration = 0.4f;
    [SerializeField] private float _packOpenDuration = 0.6f;
    [SerializeField] private float _cardFlipDuration = 0.4f;
    [SerializeField] private float _cardRevealDelay = 0.2f;

    [Header("Audio")]
    [SerializeField] private AudioClip _packOpenSound;
    [SerializeField] private AudioClip _cardFlipSound;
    [SerializeField] private AudioClip _cardRevealSound;
    [SerializeField] private AudioClip _packCloseSound;

    [Header("Debug")]
    [SerializeField] private bool _verboseLogging = true;

    // ========================================================================
    // STATE
    // ========================================================================

    private bool _isOpening = false;
    private GachaResult _currentResult;
    private string _currentPackId;
    private Coroutine _openingCoroutine;

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

        SetupPanelResponsive();

        AutoSetup();

        if (_panelRoot != null)
            _panelRoot.SetActive(false);
    }

    [ContextMenu("Auto Setup UI")]
    public void AutoSetup()
    {
        Debug.Log($"[PackOpeningUI] Running AutoSetup on {gameObject.name}...");

        if (_panelRoot == null) {
            var panelTransform = transform.Find("Panel");
            if (panelTransform != null) _panelRoot = panelTransform.gameObject;
            else _panelRoot = gameObject;
        }

        if (_panelRect == null) _panelRect = _panelRoot?.GetComponent<RectTransform>();
        if (_panelCanvasGroup == null) _panelCanvasGroup = _panelRoot?.GetComponent<CanvasGroup>();

        // Find Pack UI
        if (_packImage == null) _packImage = FindComponentInChild<Image>("PackImage");
        if (_packNameText == null) _packNameText = FindComponentInChild<TextMeshProUGUI>("PackName");
        if (_screenDimOverlay == null) _screenDimOverlay = FindComponentInChild<Image>("DimOverlay");

        // Find Summary UI
        if (_summaryContent == null) _summaryContent = transform.Find("Summary")?.gameObject ?? transform.Find("Panel/Summary")?.gameObject;
        if (_xpGainedText == null) _xpGainedText = FindComponentInChild<TextMeshProUGUI>("XpText");
        if (_cardsRevealedText == null) _cardsRevealedText = FindComponentInChild<TextMeshProUGUI>("CardsText");
        if (_btnContinue == null) _btnContinue = FindComponentInChild<Button>("BtnContinue");

        // Find Card Slots (CardSlot0 to CardSlot4)
        if (_cardSlotImages == null || _cardSlotImages.Count == 0)
        {
            _cardSlotImages = new List<Image>();
            for (int i = 0; i < 5; i++)
            {
                Image slot = FindComponentInChild<Image>("CardSlot" + i);
                if (slot != null) _cardSlotImages.Add(slot);
            }
        }

        Debug.Log("[PackOpeningUI] AutoSetup complete. Please check the Inspector.");
    }

    private T FindComponentInChild<T>(string name) where T : Component
    {
        Transform t = transform.Find(name);
        if (t == null) t = transform.Find("Panel/" + name);
        return t?.GetComponent<T>();
    }

    private void Start()
    {
        if (_btnContinue != null)
            _btnContinue.onClick.AddListener(OnContinueClicked);

        GameEconomyEvents.OnPackOpeningStarted += HandlePackOpeningStarted;
    }

    private void OnDestroy()
    {
        GameEconomyEvents.OnPackOpeningStarted -= HandlePackOpeningStarted;
    }

    // ========================================================================
    // PUBLIC API
    // ========================================================================

    /// <summary>
    /// Bắt đầu quy trình mở pack với animation đầy đủ.
    /// Đây là entry point CHÍNH của pack opening.
    ///
    /// GỌI TỪ:
    ///   InventoryPanelUI khi player click "Open Pack"
    /// </summary>
    public void StartPackOpening(string packId)
    {
        if (_isOpening)
        {
            Debug.LogWarning("[PackOpeningUI] Already opening a pack!");
            return;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[PackOpeningUI] InventoryManager.Instance is null!");
            return;
        }

        GachaResult result = InventoryManager.Instance.OpenPack(packId);
        if (result == null)
        {
            Debug.LogWarning($"[PackOpeningUI] Failed to open pack '{packId}'.");
            return;
        }

        _currentPackId = packId;
        _currentResult = result;

        GameEconomyEvents.FirePackOpeningStarted(packId, result);

        _openingCoroutine = StartCoroutine(PlayPackOpeningRoutine(result, packId));
    }

    // ========================================================================
    // CORE COROUTINE
    // ========================================================================

    private IEnumerator PlayPackOpeningRoutine(GachaResult result, string packId)
    {
        _isOpening = true;

        if (_verboseLogging)
            Debug.Log($"[PackOpeningUI] === PACK OPENING START: {packId} ===");

        yield return Phase0_Setup(result, packId);
        yield return Phase1_PackOpen();
        yield return Phase2_CardReveals(result);
        yield return Phase3_Summary(result);
        yield return Phase4_WaitForContinue();

        ClosePanel();

        _isOpening = false;

        if (_verboseLogging)
            Debug.Log($"[PackOpeningUI] === PACK OPENING END ===");
    }

    // ========================================================================
    // PHASE IMPLEMENTATIONS
    // ========================================================================

    private IEnumerator Phase0_Setup(GachaResult result, string packId)
    {
        if (_verboseLogging)
            Debug.Log($"[PackOpeningUI] Phase 0: Setup ({_dimDuration}s)");

        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        // Ensure screen dim overlay stretches to cover the entire screen
        if (_screenDimOverlay != null)
        {
            _screenDimOverlay.gameObject.SetActive(true);
            RectTransform overlayRect = _screenDimOverlay.GetComponent<RectTransform>();
            if (overlayRect != null)
            {
                // Stretch to full screen so dim covers everything
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
            }

            Color c = _screenDimOverlay.color;
            c.a = 0f;
            _screenDimOverlay.color = c;

            float elapsed = 0f;
            while (elapsed < _dimDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / _dimDuration) * 0.85f;
                _screenDimOverlay.color = c;
                yield return null;
            }
            c.a = 0.85f;
            _screenDimOverlay.color = c;
        }

        if (_packNameText != null)
            _packNameText.text = packId;

        if (_packImage != null && _defaultPackSprite != null)
            _packImage.sprite = _defaultPackSprite;

        SetupCardSlots(result.DroppedCards.Count);

        if (_summaryContent != null)
            _summaryContent.SetActive(false);

        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator Phase1_PackOpen()
    {
        if (_verboseLogging)
            Debug.Log($"[PackOpeningUI] Phase 1: Pack Open ({_packOpenDuration}s)");

        PlaySound(_packOpenSound);

        if (_packImage != null)
        {
            Vector3 originalScale = _packImage.transform.localScale;
            float elapsed = 0f;

            while (elapsed < _packOpenDuration * 0.4f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (_packOpenDuration * 0.4f);
                float eased = EaseOutBack(t);
                _packImage.transform.localScale = originalScale * (1f + eased * 0.3f);
                yield return null;
            }

            SpawnPackOpenParticles();

            Color c = _packImage.color;
            elapsed = 0f;
            while (elapsed < _packOpenDuration * 0.6f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (_packOpenDuration * 0.6f);
                c.a = 1f - t;
                _packImage.color = c;
                yield return null;
            }

            _packImage.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator Phase2_CardReveals(GachaResult result)
    {
        if (_verboseLogging)
            Debug.Log($"[PackOpeningUI] Phase 2: Card Reveals");

        for (int i = 0; i < result.DroppedCards.Count; i++)
        {
            CardData card = result.DroppedCards[i];

            if (i < _cardSlotImages.Count && _cardSlotImages[i] != null)
            {
                Image cardImage = _cardSlotImages[i];
                cardImage.gameObject.SetActive(true);

                if (_cardBackSprite != null)
                    cardImage.sprite = _cardBackSprite;
                cardImage.transform.localScale = Vector3.zero;
                cardImage.color = Color.white;

                yield return StartCoroutine(AnimateCardPopIn(cardImage));

                yield return StartCoroutine(AnimateCardFlip(cardImage, card));

                GameEconomyEvents.FireCardRevealed(i, card);

                yield return new WaitForSeconds(_cardRevealDelay);
            }
        }

        yield return new WaitForSeconds(0.3f);
    }

    private IEnumerator Phase3_Summary(GachaResult result)
    {
        if (_verboseLogging)
            Debug.Log($"[PackOpeningUI] Phase 3: Summary");

        if (_summaryContent != null)
            _summaryContent.SetActive(true);

        if (_xpGainedText != null)
            _xpGainedText.text = $"+{result.TotalXpGained} XP";

        if (_cardsRevealedText != null)
        {
            int rareCount = 0;
            foreach (var card in result.DroppedCards)
                if (card != null && card.RarityRank >= 3) rareCount++;

            _cardsRevealedText.text = rareCount > 0
                ? $"{result.DroppedCards.Count} cards ({rareCount} Rare+)"
                : $"{result.DroppedCards.Count} cards";
        }

        for (int i = 0; i < result.DroppedCards.Count; i++)
        {
            CardData card = result.DroppedCards[i];
            if (card != null && card.RarityRank >= 3 && i < _cardSlotImages.Count)
            {
                yield return StartCoroutine(AnimateRareHighlight(_cardSlotImages[i]));
            }
        }

        yield return new WaitForSeconds(0.5f);
    }

    private IEnumerator Phase4_WaitForContinue()
    {
        if (_verboseLogging)
            Debug.Log($"[PackOpeningUI] Phase 4: Waiting for Continue");

        float timeout = 10f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_verboseLogging)
            Debug.Log($"[PackOpeningUI] Auto-closing after timeout.");
    }

    // ========================================================================
    // CARD ANIMATION HELPERS
    // ========================================================================

    private IEnumerator AnimateCardPopIn(Image cardImage)
    {
        float duration = 0.25f;
        float elapsed = 0f;
        Vector3 originalScale = Vector3.one * 1.1f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float eased = EaseOutBack(Mathf.Clamp01(t));
            cardImage.transform.localScale = Vector3.one * eased * 1.1f;
            yield return null;
        }

        cardImage.transform.localScale = originalScale;
    }

    private IEnumerator AnimateCardFlip(Image cardImage, CardData card)
    {
        PlaySound(_cardFlipSound);

        float duration = _cardFlipDuration;
        float elapsed = 0f;
        Vector3 originalScale = cardImage.transform.localScale;
        bool spriteSwapped = false;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float scaleX;
            if (t < 0.5f)
            {
                scaleX = 1f - (t / 0.5f);
            }
            else
            {
                scaleX = (t - 0.5f) / 0.5f;

                if (!spriteSwapped)
                {
                    spriteSwapped = true;
                    if (_cardFrontPlaceholderSprite != null)
                        cardImage.sprite = _cardFrontPlaceholderSprite;
                    else
                    {
                        cardImage.color = CardRevealEffect.RarityColor(card?.RarityRank ?? 0);
                    }

                    PlaySound(_cardRevealSound);

                    if (_verboseLogging && card != null)
                    {
                        Debug.Log($"[PackOpeningUI] Revealed: {card.cardName} " +
                                 $"({card.rarity?.displayName ?? "Unknown"})");
                    }
                }
            }

            cardImage.transform.localScale = new Vector3(scaleX, originalScale.y, originalScale.z);
            yield return null;
        }

        cardImage.transform.localScale = originalScale;
    }

    private IEnumerator AnimateRareHighlight(Image cardImage)
    {
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float scale = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.05f;
            cardImage.transform.localScale = Vector3.one * scale;
            yield return null;
        }

        cardImage.transform.localScale = Vector3.one;
    }

    // ========================================================================
    // SETUP & UTILITY
    // ========================================================================

    private void SetupCardSlots(int count)
    {
        for (int i = 0; i < 5; i++)
        {
            if (i < _cardSlotImages.Count && _cardSlotImages[i] != null)
            {
                _cardSlotImages[i].gameObject.SetActive(false);
                _cardSlotImages[i].transform.localScale = Vector3.one;
                _cardSlotImages[i].color = Color.white;
            }
        }
    }

    private void SpawnPackOpenParticles()
    {
        if (_packOpenParticlePrefab == null)
        {
            if (_verboseLogging)
                Debug.Log("[PackOpeningUI] No particle prefab — skipping particle effect.");
            return;
        }

        Vector3 spawnPos = _packImage != null
            ? _packImage.transform.position
            : (_panelRect != null ? _panelRect.position : Vector3.zero);

        GameObject particles = Instantiate(_packOpenParticlePrefab, spawnPos, Quaternion.identity);
        Destroy(particles, 3f);
    }

    private void ClosePanel()
    {
        PlaySound(_packCloseSound);

        if (_currentResult != null)
            GameEconomyEvents.FirePackOpeningCompleted(_currentResult);

        if (_panelCanvasGroup != null)
        {
            StartCoroutine(FadeOutPanel());
        }
        else
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(false);

            if (_screenDimOverlay != null)
                _screenDimOverlay.gameObject.SetActive(false);
        }

        _currentResult = null;
        _currentPackId = null;
    }

    private IEnumerator FadeOutPanel()
    {
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _panelCanvasGroup.alpha = 1f - (elapsed / duration);
            yield return null;
        }

        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        _panelCanvasGroup.alpha = 1f;

        if (_screenDimOverlay != null)
            _screenDimOverlay.gameObject.SetActive(false);
    }

    private void OnContinueClicked()
    {
        if (_verboseLogging)
            Debug.Log("[PackOpeningUI] Continue clicked.");
    }

    private void HandlePackOpeningStarted(string packId, GachaResult result)
    {
        if (_verboseLogging)
            Debug.Log($"[PackOpeningUI] Received OnPackOpeningStarted: {packId}");
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, Vector3.zero);
    }

    private static float EaseOutBack(float t) =>
        1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);

    // ─────────────────────────────────────────────────────────────────
    // Responsive
    // ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Configures the panel RectTransform for responsive behavior.
    /// PackOpeningUI uses a fullscreen overlay, so it stretches to
    /// fill the entire canvas regardless of resolution.
    /// </summary>
    private void SetupPanelResponsive()
    {
        if (_panelRect == null)
            _panelRect = _panelRoot?.GetComponent<RectTransform>();

        if (_panelRect == null) return;

        // Fullscreen stretch: anchors fill the entire canvas
        _panelRect.anchorMin = Vector2.zero;
        _panelRect.anchorMax = Vector2.one;
        _panelRect.pivot = new Vector2(0.5f, 0.5f);
        _panelRect.offsetMin = Vector2.zero;
        _panelRect.offsetMax = Vector2.zero;
    }
}
