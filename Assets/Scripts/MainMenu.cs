using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("Play Button")]
    [SerializeField] private RectTransform playButton;
    [SerializeField] private Image playButtonImage;
    [SerializeField] private TMP_Text playButtonText;

    [Header("Button Colors")]
    [SerializeField] private Color normalColor  = new Color(0.97f, 0.76f, 0.36f);
    [SerializeField] private Color hoverColor   = new Color(1f,   0.85f, 0.5f);
    [SerializeField] private Color pressedColor = new Color(0.85f, 0.60f, 0.20f);

    [Header("Camera Zoom")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float zoomDuration = 1.0f;

    [Header("Transition")]
    [SerializeField] private Image fadeOverlay;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Game";

    [Header("Audio")]
    [SerializeField] private AudioSource bgmSource;       // ✅ nhạc nền
    [SerializeField] private AudioSource sfxSource;       // ✅ sfx
    [SerializeField] private AudioClip  bgmClip;          // kéo file nhạc nền vào
    [SerializeField] private AudioClip  hoverClip;        // âm hover
    [SerializeField] private AudioClip  clickClip;        // âm bấm play
    [SerializeField] private float      bgmFadeDuration = 1.0f;

    private Vector3 buttonOriginalScale;
    private bool isTransitioning = false;

    private void Start()
    {
        if (playButton != null)
            buttonOriginalScale = playButton.localScale;

        if (mainCamera == null)
            mainCamera = Camera.main;

        // Fade in scene
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            StartCoroutine(FadeTo(0f, 0.6f));
        }

        // ✅ Bật nhạc nền fade in
        if (bgmSource != null && bgmClip != null)
        {
            bgmSource.clip   = bgmClip;
            bgmSource.loop   = true;
            bgmSource.volume = 0f;
            bgmSource.Play();
            StartCoroutine(FadeBGM(0f, 1f, bgmFadeDuration));
        }

        AddButtonEvents();
    }

    private void AddButtonEvents()
    {
        if (playButton == null) return;
        EventTrigger trigger = playButton.gameObject.GetComponent<EventTrigger>()
                            ?? playButton.gameObject.AddComponent<EventTrigger>();

        AddEventTrigger(trigger, EventTriggerType.PointerEnter, (_) => OnHoverEnter());
        AddEventTrigger(trigger, EventTriggerType.PointerExit,  (_) => OnHoverExit());
        AddEventTrigger(trigger, EventTriggerType.PointerDown,  (_) => OnPress());
        AddEventTrigger(trigger, EventTriggerType.PointerUp,    (_) => PlayGame());
    }

    private void AddEventTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

    private void OnHoverEnter()
    {
        if (isTransitioning) return;
        PlaySFX(hoverClip); // ✅
        StopAllCoroutines();
        StartCoroutine(FadeBGM(bgmSource.volume, 1f, 0f));
        StartCoroutine(ScaleTo(playButton, buttonOriginalScale * 1.12f, 0.15f));
        StartCoroutine(ColorTo(playButtonImage, hoverColor, 0.15f));
    }

    private void OnHoverExit()
    {
        if (isTransitioning) return;
        StopAllCoroutines();
        StartCoroutine(ScaleTo(playButton, buttonOriginalScale, 0.15f));
        StartCoroutine(ColorTo(playButtonImage, normalColor, 0.15f));
    }

    private void OnPress()
    {
        if (isTransitioning) return;
        PlaySFX(clickClip); // ✅
        StopAllCoroutines();
        StartCoroutine(ScaleTo(playButton, buttonOriginalScale * 0.9f, 0.08f));
        StartCoroutine(ColorTo(playButtonImage, pressedColor, 0.08f));
    }

    public void PlayGame()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        StartCoroutine(TransitionToGame());
    }

    private IEnumerator TransitionToGame()
    {
        // 1. Button bounce
        yield return StartCoroutine(ScaleTo(playButton, buttonOriginalScale * 1.2f, 0.1f));
        yield return StartCoroutine(ScaleTo(playButton, buttonOriginalScale * 0.0f, 0.15f));

        // 2. Fade out BGM + Zoom camera song song
        StartCoroutine(FadeBGM(1f, 0f, zoomDuration));

        Vector3 targetPos   = playButton.position;
        targetPos.z         = mainCamera.transform.position.z;
        Vector3 camStartPos = mainCamera.transform.position;
        float startOrtho    = mainCamera.orthographicSize;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / zoomDuration;
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            mainCamera.transform.position = Vector3.Lerp(camStartPos, targetPos, ease);
            if (mainCamera.orthographic)
                mainCamera.orthographicSize = Mathf.Lerp(startOrtho, 0.3f, ease);
            yield return null;
        }

        // 3. Fade to black
        yield return StartCoroutine(FadeTo(1f, 0.4f));

        // 4. Load scene
        SceneManager.LoadScene(gameSceneName);
    }

    // ✅ Fade BGM volume
    private IEnumerator FadeBGM(float from, float to, float duration)
    {
        if (bgmSource == null) yield break;
        if (duration <= 0f) { bgmSource.volume = to; yield break; }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            bgmSource.volume = Mathf.Lerp(from, to, t);
            yield return null;
        }
        bgmSource.volume = to;
    }

    // ✅ Play SFX one shot
    private void PlaySFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
            sfxSource.PlayOneShot(clip);
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeOverlay == null) yield break;
        fadeOverlay.gameObject.SetActive(true);
        Color c = fadeOverlay.color;
        float startAlpha = c.a;
        if (duration <= 0f)
        {
            fadeOverlay.color = new Color(c.r, c.g, c.b, targetAlpha);
            if (targetAlpha <= 0f) fadeOverlay.gameObject.SetActive(false);
            yield break;
        }
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            fadeOverlay.color = new Color(c.r, c.g, c.b, Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }
        fadeOverlay.color = new Color(c.r, c.g, c.b, targetAlpha);
        if (targetAlpha <= 0f) fadeOverlay.gameObject.SetActive(false);
    }

    private IEnumerator ScaleTo(RectTransform rect, Vector3 target, float duration)
    {
        Vector3 start = rect.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            rect.localScale = Vector3.Lerp(start, target, t);
            yield return null;
        }
        rect.localScale = target;
    }

    private IEnumerator ColorTo(Image img, Color target, float duration)
    {
        if (img == null) yield break;
        Color start = img.color;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            img.color = Color.Lerp(start, target, t);
            yield return null;
        }
        img.color = target;
    }
}
