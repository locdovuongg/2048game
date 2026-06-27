using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NightModeHazard : MonoBehaviour
{
    public static NightModeHazard Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float nightTriggerTime        = 120f;
    [SerializeField] private int   handCount               = 3;
    [SerializeField] private int   tapsToDismiss           = 15;
    [SerializeField] private float tapTimeLimit            = 5f;
    [SerializeField] private int   tapsRequiredPerInterval = 5;

    [Header("UI")]
    [SerializeField] private Image         nightOverlayImage;
    [SerializeField] private RectTransform handParent;
    [SerializeField] private GameObject    handPrefab;
    [SerializeField] private Image         tapProgressBar;
    [SerializeField] private GameObject    blockedCellOverlayPrefab;

    [Header("Fog")]
    [SerializeField] private FogController fogController;

    private float   gameTime        = 0f;
    private bool    nightModeActive = false;
    private bool    nightTriggered  = false;
    private bool    canTap          = false;
    private int     currentTaps     = 0;

    private List<GameObject>                   activeHands     = new List<GameObject>();
    private Dictionary<Vector2Int, GameObject> blockedOverlays = new Dictionary<Vector2Int, GameObject>();

    public HashSet<Vector2Int> BlockedCells      { get; private set; } = new HashSet<Vector2Int>();
    public bool                IsNightModeActive => nightModeActive;

    private BoardManager boardManager;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideUI();
    }

    private void HideUI()
    {
        if (tapProgressBar != null)
        {
            tapProgressBar.fillAmount = 0f;
            tapProgressBar.gameObject.SetActive(false);
            Debug.Log("TapProgressBar hidden");
        }
        else
        {
            Debug.LogWarning("tapProgressBar chua gan Inspector!");
        }

        if (nightOverlayImage != null)
        {
            nightOverlayImage.color = new Color(0, 0, 0, 0);
            nightOverlayImage.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        int difficulty = PlayerPrefs.GetInt("Difficulty", 1);
        switch (difficulty)
        {
            case 0: gameObject.SetActive(false); return;
            case 1: nightTriggerTime = 120f; break;
            case 2: nightTriggerTime = 60f;  break;
        }
    }

    public void Init(BoardManager bm)
    {
        boardManager = bm;
        HideUI();
        fogController?.Hide();
    }

    private void Update()
    {
        if (nightTriggered) return;
        if (GameOverUI.Instance != null && GameOverUI.Instance.IsGameOver) return;

        gameTime += Time.deltaTime;
        if (gameTime >= nightTriggerTime)
        {
            nightTriggered = true;
            StartCoroutine(TriggerNightMode());
        }
    }

    private IEnumerator TriggerNightMode()
    {
        nightModeActive = true;
        canTap          = false;
        currentTaps     = 0;
        BlockedCells.Clear();
        activeHands.Clear();

        boardManager?.PlayNightModeSound();

        if (nightOverlayImage != null)
        {
            nightOverlayImage.gameObject.SetActive(true);
            nightOverlayImage.raycastTarget = false;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 1.5f;
                nightOverlayImage.color = new Color(0, 0, 0, Mathf.Lerp(0f, 0.4f, t));
                yield return null;
            }
        }

        if (fogController != null)
        {
            fogController.Show();
            fogController.SetSpeed(0.03f, 0.01f);
            fogController.SetSize(3f);
            fogController.SetColor(new Color(0.6f, 0.8f, 1f, 1f));
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 2f;
                fogController.SetOpacity(Mathf.Lerp(0f, 0.5f, t));
                yield return null;
            }
        }

        SpawnHands();

        if (tapProgressBar != null)
        {
            tapProgressBar.gameObject.SetActive(true);
            tapProgressBar.fillAmount = 0f;
        }

        yield return new WaitForSeconds(0.7f);
        canTap = true;

        StartCoroutine(PenaltyLoop());

        while (currentTaps < tapsToDismiss)
            yield return null;

        yield return StartCoroutine(DismissNightMode());
    }

    private IEnumerator DismissNightMode()
    {
        canTap = false;

        foreach (var hand in activeHands)
            if (hand != null) StartCoroutine(AnimateHandOut(hand.transform));

        foreach (var overlay in blockedOverlays.Values)
            if (overlay != null) Destroy(overlay);
        blockedOverlays.Clear();

        if (fogController != null)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                fogController.SetOpacity(Mathf.Lerp(0.5f, 0f, t));
                yield return null;
            }
            fogController.Hide();
        }

        yield return new WaitForSeconds(0.5f);

        foreach (var hand in activeHands)
            if (hand != null) Destroy(hand);
        activeHands.Clear();
        BlockedCells.Clear();

        if (nightOverlayImage != null)
        {
            float t = 0f;
            Color startColor = nightOverlayImage.color;
            while (t < 1f)
            {
                t += Time.deltaTime;
                nightOverlayImage.color = Color.Lerp(startColor, new Color(0, 0, 0, 0), t);
                yield return null;
            }
            nightOverlayImage.gameObject.SetActive(false);
        }

        if (tapProgressBar != null)
        {
            tapProgressBar.fillAmount = 0f;
            tapProgressBar.gameObject.SetActive(false);
        }

        nightModeActive = false;
        nightTriggered  = false;
        gameTime        = 0f;
    }

    public void RegisterTap()
    {
        if (!nightModeActive || !canTap) return;
        currentTaps++;
        if (tapProgressBar != null)
            tapProgressBar.fillAmount = (float)currentTaps / tapsToDismiss;
    }

    private IEnumerator PenaltyLoop()
    {
        while (nightModeActive)
        {
            int tapsBefore = currentTaps;
            yield return new WaitForSeconds(tapTimeLimit);
            if (!nightModeActive) yield break;

            if (currentTaps - tapsBefore < tapsRequiredPerInterval)
                BlockRandomCell();
        }
    }

    private void BlockRandomCell()
    {
        if (boardManager == null) return;

        var available = new List<Vector2Int>();
        for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!BlockedCells.Contains(cell))
                    available.Add(cell);
            }

        if (available.Count == 0) return;

        Vector2Int blocked  = available[Random.Range(0, available.Count)];
        Transform  parent   = handParent != null ? handParent : transform;
        Vector3    worldPos = boardManager.GetCellWorldPos(blocked.x, blocked.y);

        BlockedCells.Add(blocked);

        if (blockedCellOverlayPrefab != null)
        {
            GameObject overlay = Instantiate(blockedCellOverlayPrefab, parent);
            var rect = overlay.GetComponent<RectTransform>();
            if (rect != null) rect.position = worldPos;
            blockedOverlays[blocked] = overlay;
        }
        else
        {
            GameObject overlay = new GameObject($"BlockedCell_{blocked.x}_{blocked.y}");
            overlay.transform.SetParent(parent, false);

            var canvas = overlay.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder    = 50;

            var img = overlay.AddComponent<Image>();
            img.color         = new Color(1f, 0f, 0f, 0.45f);
            img.raycastTarget = false;

            var rect = overlay.GetComponent<RectTransform>();
            rect.position  = worldPos;
            rect.sizeDelta = new Vector2(160f, 160f);

            blockedOverlays[blocked] = overlay;
        }
    }

    private void SpawnHands()
    {
        if (handPrefab == null || boardManager == null) return;

        var cells = new List<Vector2Int>();
        for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
                cells.Add(new Vector2Int(x, y));

        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }

        Transform parent = handParent != null ? handParent : transform;

        for (int i = 0; i < Mathf.Min(handCount, cells.Count); i++)
        {
            Vector2Int cell     = cells[i];
            Vector3    worldPos = boardManager.GetCellWorldPos(cell.x, cell.y);

            BlockedCells.Add(cell);

            GameObject hand     = Instantiate(handPrefab, parent);
            var        handRect = hand.GetComponent<RectTransform>();
            if (handRect != null) handRect.position = worldPos;

            activeHands.Add(hand);
            StartCoroutine(AnimateHandIn(hand.transform, hand.transform.position));
        }
    }

    private IEnumerator AnimateHandIn(Transform hand, Vector3 targetPos)
    {
        if (hand == null) yield break;
        Vector3 startPos = targetPos + new Vector3(0, -300f, 0);
        hand.position = startPos;

        float elapsed  = 0f;
        float duration = 0.6f;
        while (elapsed < duration)
        {
            if (hand == null) yield break;
            elapsed += Time.deltaTime;
            hand.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        if (hand != null) hand.position = targetPos;
    }

    private IEnumerator AnimateHandOut(Transform hand)
    {
        if (hand == null) yield break;
        Vector3 startPos = hand.position;
        Vector3 endPos   = startPos + new Vector3(0, -300f, 0);
        float elapsed  = 0f;
        float duration = 0.4f;
        while (elapsed < duration)
        {
            if (hand == null) yield break;
            elapsed += Time.deltaTime;
            hand.position = Vector3.Lerp(startPos, endPos, elapsed / duration);
            yield return null;
        }
    }
}