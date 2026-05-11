using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NightModeHazard : MonoBehaviour
{
    public static NightModeHazard Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float nightTriggerTime = 10f; // test: 10s, production: 300f
    [SerializeField] private int handCount = 3;
    [SerializeField] private int tapsToDismiss = 15;

    [Header("UI - kéo vào đây")]
    [SerializeField] private Image nightOverlayImage;   // Image tối phủ toàn màn hình
    [SerializeField] private RectTransform handParent;  // Canvas/Panel chứa tay
    [SerializeField] private GameObject handPrefab;     // prefab cánh tay (UI Image)
    [SerializeField] private Image tapProgressBar;      // Image với ImageType = Filled

    private float gameTime = 0f;
    private bool nightModeActive = false;
    private bool nightTriggered = false;
    private int currentTaps = 0;
    private List<GameObject> activeHands = new List<GameObject>();
    public HashSet<Vector2Int> BlockedCells { get; private set; } = new HashSet<Vector2Int>();

    private BoardManager boardManager;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Init(BoardManager bm)
    {
        boardManager = bm;

        if (nightOverlayImage != null)
        {
            nightOverlayImage.color = new Color(0, 0, 0, 0);
            nightOverlayImage.gameObject.SetActive(false);
        }

        if (tapProgressBar != null)
            tapProgressBar.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (nightTriggered) return;

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
        currentTaps = 0;
        BlockedCells.Clear();
        activeHands.Clear();

        // Fade in overlay
        if (nightOverlayImage != null)
        {
            nightOverlayImage.gameObject.SetActive(true);
            nightOverlayImage.raycastTarget = false; // ✅ không block input
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 1.5f;
                nightOverlayImage.color = new Color(0, 0, 0, Mathf.Lerp(0f, 0.75f, t));
                yield return null;
            }
        }

        // Spawn tay
        SpawnHands();

        // Hiện progress bar
        if (tapProgressBar != null)
        {
            tapProgressBar.gameObject.SetActive(true);
            tapProgressBar.fillAmount = 0f;
        }

        // Chờ đủ tap
        while (currentTaps < tapsToDismiss)
            yield return null;

        yield return StartCoroutine(DismissNightMode());
    }

    private void SpawnHands()
    {
        if (handPrefab == null)
        {
            Debug.LogWarning("handPrefab chưa được gán!");
            return;
        }

        if (boardManager == null)
        {
            Debug.LogWarning("boardManager null!");
            return;
        }

        // Lấy danh sách tất cả cells rồi shuffle
        List<Vector2Int> cells = new List<Vector2Int>();
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
            Vector2Int cell = cells[i];
            BlockedCells.Add(cell);

            Vector3 worldPos = boardManager.GetCellWorldPos(cell.x, cell.y);
            GameObject hand = Instantiate(handPrefab, parent);

            // ✅ Dùng world position để đặt đúng vị trí
            RectTransform handRect = hand.GetComponent<RectTransform>();
            if (handRect != null)
                handRect.position = worldPos;

            activeHands.Add(hand);
            StartCoroutine(AnimateHandIn(hand.transform, hand.transform.position));
        }

        Debug.Log($"Spawned {activeHands.Count} hands, blocked: {BlockedCells.Count} cells");
    }

    private IEnumerator AnimateHandIn(Transform hand, Vector3 targetPos)
    {
        if (hand == null) yield break;
        Vector3 startPos = targetPos + new Vector3(0, -300f, 0);
        hand.position = startPos;

        float elapsed = 0f;
        float duration = 0.6f;

        while (elapsed < duration)
        {
            if (hand == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            hand.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        if (hand != null)
            hand.position = targetPos;
    }

    // ✅ Gọi từ BoardManager khi click/tap
    public void RegisterTap()
    {
        if (!nightModeActive) return;
        currentTaps++;
        Debug.Log($"Tap {currentTaps}/{tapsToDismiss}");

        if (tapProgressBar != null)
            tapProgressBar.fillAmount = (float)currentTaps / tapsToDismiss;
    }

    private IEnumerator DismissNightMode()
    {
        // Animate tay ra
        foreach (var hand in activeHands)
        {
            if (hand != null)
                StartCoroutine(AnimateHandOut(hand.transform));
        }

        yield return new WaitForSeconds(0.5f);

        foreach (var hand in activeHands)
            if (hand != null) Destroy(hand);

        activeHands.Clear();
        BlockedCells.Clear();

        // Fade out overlay
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
            tapProgressBar.gameObject.SetActive(false);

        nightModeActive = false;
        nightTriggered = false;
        gameTime = 0f; // reset để trigger lại sau
    }

    private IEnumerator AnimateHandOut(Transform hand)
    {
        if (hand == null) yield break;
        Vector3 startPos = hand.position;
        Vector3 endPos = startPos + new Vector3(0, -300f, 0);
        float elapsed = 0f;

        while (elapsed < 0.4f)
        {
            if (hand == null) yield break;
            elapsed += Time.deltaTime;
            hand.position = Vector3.Lerp(startPos, endPos, elapsed / 0.4f);
            yield return null;
        }
    }
}