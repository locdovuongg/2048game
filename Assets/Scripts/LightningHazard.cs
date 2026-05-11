using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LightningHazard : MonoBehaviour
{
    public static LightningHazard Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float warningTime = 5f;   // giây cảnh báo
    [SerializeField] private float strikeDelay = 3f;   // giây sau cảnh báo → sét đánh
    [SerializeField] private int minValueToTarget = 64; // giá trị tối thiểu để bị nhắm

    [Header("VFX")]
    [SerializeField] private GameObject lightningWarningPrefab; // icon cảnh báo ⚡
    [SerializeField] private GameObject lightningStrikePrefab;  // hiệu ứng sét

    private BoardManager boardManager;

    // tracking: grid[x,y] → thời gian không di chuyển
    private float[,] stationaryTime;
    private bool[,] isWarned;
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
        stationaryTime = new float[w, h];
        isWarned = new bool[w, h];
        initialized = true; // ✅
    }

    // Gọi sau mỗi nước đi từ BoardManager
    public void OnBoardChanged(int[,] grid)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int val = grid[x, y];
                if (val >= minValueToTarget)
                {
                    stationaryTime[x, y] += Time.deltaTime; // sẽ update trong Tick
                }
                else
                {
                    stationaryTime[x, y] = 0f;
                    isWarned[x, y] = false;
                }
            }
        }
    }

    public void Tick(int[,] grid)
    {
        if (!initialized) return; // ✅ guard

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int val = grid[x, y];
                if (val < minValueToTarget) continue;

                stationaryTime[x, y] += Time.deltaTime;

                // Bắt đầu cảnh báo
                if (stationaryTime[x, y] >= warningTime && !isWarned[x, y])
                {
                    isWarned[x, y] = true;
                    StartCoroutine(StrikeCountdown(x, y, grid));
                }
            }
        }
    }

    // Gọi khi tile di chuyển → reset timer
    public void ResetTimer(int x, int y)
    {
        if (!initialized) return; // ✅ guard
        stationaryTime[x, y] = 0f;
        isWarned[x, y] = false;
    }

    private IEnumerator StrikeCountdown(int x, int y, int[,] grid)
    {
        // Hiện warning icon tại tile
        Vector3 worldPos = boardManager.GetCellWorldPos(x, y);
        GameObject warning = null;
        if (lightningWarningPrefab != null)
            warning = Instantiate(lightningWarningPrefab, worldPos, Quaternion.identity);

        // Nhấp nháy trong strikeDelay giây
        float elapsed = 0f;
        while (elapsed < strikeDelay)
        {
            elapsed += Time.deltaTime;
            // Nếu tile đã bị move/merge → huỷ cảnh báo
            if (!isWarned[x, y])
            {
                if (warning) Destroy(warning);
                yield break;
            }
            yield return null;
        }

        // ⚡ Sét đánh!
        if (warning) Destroy(warning);
        if (lightningStrikePrefab != null)
            Instantiate(lightningStrikePrefab, worldPos, Quaternion.identity);

        boardManager.DestroyTile(x, y);
        stationaryTime[x, y] = 0f;
        isWarned[x, y] = false;
    }
}