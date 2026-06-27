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

    [Header("Difficulty Panel")]
    [SerializeField] private GameObject difficultyPanel;
    [SerializeField] private RectTransform easyButton;
    [SerializeField] private RectTransform normalButton;
    [SerializeField] private RectTransform hardButton;

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
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip   bgmClip;
    [SerializeField] private AudioClip   hoverClip;
    [SerializeField] private AudioClip   clickClip;
    [SerializeField] private float       bgmFadeDuration = 1.0f;

    private Vector3 buttonOriginalScale;
    private bool isTransitioning = false;
    private Coroutine buttonAnimCoroutine;  // ✅ track riêng

    private void Start()
    {
        if (playButton != null)
            buttonOriginalScale = playButton.localScale;
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (difficultyPanel != null)
            difficultyPanel.SetActive(false);

        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            StartCoroutine(FadeTo(0f, 0.6f));
        }

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
        AddEventTrigger(trigger, EventTriggerType.PointerUp,    (_) => OnPlayButtonClick());
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
        PlaySFX(hoverClip);
        if (buttonAnimCoroutine != null) StopCoroutine(buttonAnimCoroutine);
        buttonAnimCoroutine = StartCoroutine(ScaleTo(playButton, buttonOriginalScale * 1.12f, 0.15f));
        StartCoroutine(ColorTo(playButtonImage, hoverColor, 0.15f));
    }

    private void OnHoverExit()
    {
        if (isTransitioning) return;
        if (buttonAnimCoroutine != null) StopCoroutine(buttonAnimCoroutine);
        buttonAnimCoroutine = StartCoroutine(ScaleTo(playButton, buttonOriginalScale, 0.15f));
        StartCoroutine(ColorTo(playButtonImage, normalColor, 0.15f));
    }

    private void OnPress()
    {
        if (isTransitioning) return;
        PlaySFX(clickClip);
        if (buttonAnimCoroutine != null) StopCoroutine(buttonAnimCoroutine);
        buttonAnimCoroutine = StartCoroutine(ScaleTo(playButton, buttonOriginalScale * 0.9f, 0.08f));
        StartCoroutine(ColorTo(playButtonImage, pressedColor, 0.08f));
    }

    private void OnPlayButtonClick()
    {
        if (isTransitioning) return;
        playButton.localScale = buttonOriginalScale;
        StartCoroutine(ShowDifficultyPanel());
    }

    private IEnumerator ShowDifficultyPanel()
    {
        yield return StartCoroutine(ScaleTo(playButton, buttonOriginalScale * 1.1f, 0.1f));
        yield return StartCoroutine(ScaleTo(playButton, buttonOriginalScale, 0.1f));

        if (difficultyPanel != null)
        {
            difficultyPanel.SetActive(true);

            var cg = difficultyPanel.GetComponent<CanvasGroup>()
                  ?? difficultyPanel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var rect = difficultyPanel.GetComponent<RectTransform>();
            Vector2 originalPos = rect.anchoredPosition;
            rect.anchoredPosition = originalPos + new Vector2(0, -80f);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.3f;
                float ease = EaseOutBack(Mathf.Clamp01(t));
                cg.alpha = Mathf.Lerp(0f, 1f, t);
                rect.anchoredPosition = Vector2.Lerp(
                    originalPos + new Vector2(0, -80f),
                    originalPos, ease);
                yield return null;
            }
            cg.alpha = 1f;
            rect.anchoredPosition = originalPos;

            // Stagger animate các nút khó dễ
            yield return StartCoroutine(AnimateDiffButton(easyButton,   0f));
            yield return StartCoroutine(AnimateDiffButton(normalButton, 0.05f));
            yield return StartCoroutine(AnimateDiffButton(hardButton,   0.1f));
        }
    }

    private IEnumerator AnimateDiffButton(RectTransform btn, float delay)
    {
        if (btn == null) yield break;
        yield return new WaitForSeconds(delay);

        Vector3 original = btn.localScale;
        btn.localScale = Vector3.zero;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.25f;
            btn.localScale = Vector3.Lerp(Vector3.zero, original, EaseOutBack(Mathf.Clamp01(t)));
            yield return null;
        }
        btn.localScale = original;
    }

    public void SelectDifficulty(int level)
    {
        PlayerPrefs.SetInt("Difficulty", level);
        string[] names = { "Easy", "Normal", "Hard" };
        
        PlaySFX(clickClip);
        StartCoroutine(HidePanelAndPlay());
    }

    //  Nút Back → về lại main menu (ẩn panel)
    public void CloseDifficultyPanel()
    {
        StartCoroutine(HideDifficultyPanel());
    }

    private IEnumerator HidePanelAndPlay()
    {
        if (isTransitioning) yield break;
        isTransitioning = true;

        //  Ẩn difficulty panel
        if (difficultyPanel != null)
        {
            var cg = difficultyPanel.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / 0.2f;
                    cg.alpha = Mathf.Lerp(1f, 0f, t);
                    yield return null;
                }
            }
            difficultyPanel.SetActive(false);
        }

        yield return StartCoroutine(TransitionToGame());
    }
    private IEnumerator HideDifficultyPanel()
    {
        if (difficultyPanel == null) yield break;
        var cg = difficultyPanel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.2f;
                cg.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }
        }
        difficultyPanel.SetActive(false);
    }

    private IEnumerator TransitionToGame()
    {
        yield return StartCoroutine(ScaleTo(playButton, buttonOriginalScale * 1.2f, 0.1f));
        yield return StartCoroutine(ScaleTo(playButton, Vector3.zero, 0.15f));

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

        yield return StartCoroutine(FadeTo(1f, 0.4f));
        SceneManager.LoadScene(gameSceneName);
    }

    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

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
