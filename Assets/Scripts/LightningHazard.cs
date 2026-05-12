using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LightningHazard : MonoBehaviour
{
    public static LightningHazard Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float intervalBetweenStrikes = 20f; // test: 20s
    [SerializeField] private float warningDuration = 5f;

    [Header("References - Kéo vào Inspector")]
    [SerializeField] private RectTransform[] cellTransforms; // ✅ kéo 16 cells từ Board vào
    [SerializeField] private Transform warningParent;        // ✅ kéo TileParent vào

    private BoardManager boardManager;
    private int width = 4, height = 4;
    private bool initialized = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Init(int w, int h, BoardManager bm)
    {
        width = w;
        height = h;
        boardManager = bm;
        initialized = true;

        // ✅ Debug in ra tất cả cell positions để kiểm tra
        for (int i = 0; i < cellTransforms.Length; i++)
        {
            if (cellTransforms[i] != null)
                Debug.Log($"Cell[{i}] name={cellTransforms[i].name} pos={cellTransforms[i].position}");
        }

        StartCoroutine(StrikeLoop());
    }

    public void ResetTimer(int x, int y) { }

    private IEnumerator StrikeLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(intervalBetweenStrikes);
            if (!initialized) continue;

            int[,] grid = boardManager.GetGrid();
            var candidates = new List<Vector2Int>();

            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (grid[x, y] > 0)
                        candidates.Add(new Vector2Int(x, y));

            if (candidates.Count == 0)
            {
                Debug.Log("⚠️ Không có tile nào để đánh!");
                continue;
            }

            Vector2Int target = candidates[Random.Range(0, candidates.Count)];
            Debug.Log($"⚡ Nhắm ô [{target.x},{target.y}] giá trị={grid[target.x, target.y]}");
            StartCoroutine(StrikeCountdown(target.x, target.y));
        }
    }

    private GameObject CreateWarningUI(RectTransform cellRect)
    {
        GameObject obj = new GameObject("LightningWarning");
        obj.transform.SetParent(cellRect, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Canvas canvas = obj.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 999;
        obj.AddComponent<GraphicRaycaster>();

        Image bg = obj.AddComponent<Image>();
        bg.color = new Color(1f, 0.1f, 0f, 0.85f);
        bg.raycastTarget = false;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);

        TMP_Text txt = textObj.AddComponent<TextMeshProUGUI>();
        // ✅ Bỏ emoji ⚡, dùng text thường
        txt.text = $"!\n{Mathf.CeilToInt(warningDuration)}";
        txt.fontSize = 60;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.yellow;
        txt.raycastTarget = false;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return obj;
    }

    private IEnumerator StrikeCountdown(int x, int y)
    {
        RectTransform cellRect = boardManager.GetCellRect(x, y);
        if (cellRect == null) { Debug.LogError($"❌ GetCellRect({x},{y}) null!"); yield break; }

        GameObject warning = CreateWarningUI(cellRect);
        TMP_Text countdownText = warning.GetComponentInChildren<TMP_Text>();

        float elapsed = 0f;
        float blinkInterval = 0.25f;
        float nextBlink = 0f;
        bool visible = true;

        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            nextBlink += Time.deltaTime;

            if (nextBlink >= blinkInterval)
            {
                nextBlink = 0f;
                visible = !visible;
                if (warning) warning.SetActive(visible);
            }

            // ✅ Không dùng emoji
            if (countdownText != null)
                countdownText.text = $"!\n{Mathf.CeilToInt(warningDuration - elapsed)}";

            yield return null;
        }

        if (warning) Destroy(warning);
        StartCoroutine(FlashEffect(cellRect));
        boardManager.DestroyTile(x, y);
        Debug.Log($"Destroy grid({x},{y})");
    }

    private IEnumerator FlashEffect(RectTransform cellRect)
    {
        GameObject flash = new GameObject("Flash");
        flash.transform.SetParent(cellRect, false);

        RectTransform rect = flash.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = flash.AddComponent<Image>();
        img.color = new Color(1f, 0.9f, 0f, 1f);
        img.raycastTarget = false;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.5f;
            img.color = new Color(1f, 0.9f, 0f, Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        Destroy(flash);
    }
}