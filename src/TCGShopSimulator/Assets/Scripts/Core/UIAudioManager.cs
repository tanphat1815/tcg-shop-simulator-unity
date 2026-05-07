// Assets/Scripts/Core/UIAudioManager.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Centralized audio feedback for UI interactions (clicks, hovers, etc.).
/// Attach to a persistent GameObject in the scene.
/// </summary>
public class UIAudioManager : MonoBehaviour
{
    public static UIAudioManager Instance { get; private set; }

    [Header("Audio Clips")]
    [SerializeField] private AudioClip _clickClip;
    [SerializeField] private AudioClip _hoverClip;
    [SerializeField] private AudioClip _openPanelClip;
    [SerializeField] private AudioClip _closePanelClip;
    [SerializeField] private AudioClip _errorClip;
    [SerializeField] private AudioClip _successClip;

    [Header("Settings")]
    [SerializeField] [Range(0f, 1f)] private float _volume = 1f;
    [SerializeField] private bool _muteOnMobile = false;

    private AudioSource _audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.volume = _volume;
    }

    public void PlayClick()
    {
        if (_clickClip == null || IsMuted()) return;
        _audioSource.PlayOneShot(_clickClip);
    }

    public void PlayHover()
    {
        if (_hoverClip == null || IsMuted()) return;
        _audioSource.PlayOneShot(_hoverClip);
    }

    public void PlayOpenPanel()
    {
        if (_openPanelClip == null || IsMuted()) return;
        _audioSource.PlayOneShot(_openPanelClip);
    }

    public void PlayClosePanel()
    {
        if (_closePanelClip == null || IsMuted()) return;
        _audioSource.PlayOneShot(_closePanelClip);
    }

    public void PlayError()
    {
        if (_errorClip == null || IsMuted()) return;
        _audioSource.PlayOneShot(_errorClip);
    }

    public void PlaySuccess()
    {
        if (_successClip == null || IsMuted()) return;
        _audioSource.PlayOneShot(_successClip);
    }

    private bool IsMuted()
    {
#if UNITY_ANDROID || UNITY_IOS
        return _muteOnMobile;
#else
        return false;
#endif
    }

    /// <summary>
    /// Call this from a UI button's OnClick to play click sound.
    /// Hook up via Inspector or via AddListener in code.
    /// </summary>
    public void OnButtonClicked()
    {
        PlayClick();
    }
}
