using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TileUI tilePrefab;
    [SerializeField] private Transform tileParent;
    [SerializeField] private RectTransform[] cellTransforms;

    [Header("Board Size")]
    [SerializeField] private int width = 4;
    [SerializeField] private int height = 4;

    [Header("Confirm Popup")]
    [SerializeField] private GameObject confirmPopup;
    [SerializeField] private TMPro.TMP_Text confirmMessageText;
    [SerializeField] private UnityEngine.UI.Button confirmYesButton;
    [SerializeField] private UnityEngine.UI.Button confirmNoButton;

    [Header("Animation")]
    [SerializeField] private float moveDuration = 0.1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip moveClip;
    [SerializeField] private AudioClip mergeClip;
    [SerializeField] private AudioClip lightningClip;
    [SerializeField] private AudioClip nightModeClip;
    [SerializeField] private AudioClip gameOverClip;

    private int[,] grid;
    private TileUI[,] tileUIs;
    private bool isAnimating = false;

    private int[,] previousGrid;
    private TileUI[,] previousTileUIs;
    private int previousScore;
    private bool canUndo = false;

    private int difficulty;

    // Gioi han luot dung
    private bool undoFreeUsed    = false;
    private bool undoAdUsed      = false;
    private bool shuffleFreeUsed = false;
    private bool shuffleAdUsed   = false;

    private System.Action pendingConfirmAction;

    private void Start()
    {
        difficulty = PlayerPrefs.GetInt("Difficulty", 1);
        if (confirmPopup != null) confirmPopup.SetActive(false);
        StartCoroutine(InitAfterLayout());
    }

    private IEnumerator InitAfterLayout()
    {
        yield return null;
        InitializeBoard();
        LightningHazard.Instance?.Init(width, height, this);
        NightModeHazard.Instance?.Init(this);
    }

    public void Move(Vector2Int direction)
    {
        if (isAnimating) return;
        if (GameOverUI.Instance != null && GameOverUI.Instance.IsGameOver) return;
        StartCoroutine(MoveCoroutine(direction));
    }

    private void Update()
    {
        if (isAnimating) return;
        if (GameOverUI.Instance != null && GameOverUI.Instance.IsGameOver) return;

        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))  Move(Vector2Int.left);
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Move(Vector2Int.right);
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))    Move(Vector2Int.up);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))  Move(Vector2Int.down);
        if (Input.GetKeyDown(KeyCode.Z)) OnClickUndo();
    }

    public RectTransform GetCellRect(int x, int y)
    {
        int index = (height - 1 - y) * width + x;
        return cellTransforms[index];
    }

    public Vector3 GetCellWorldPos(int x, int y) => GetCellWorldPosition(x, y);

    // ✅ Sét đánh xoá tile
    public void DestroyTile(int x, int y)
    {
        if (tileUIs[x, y] != null)
        {
            Destroy(tileUIs[x, y].gameObject);
            tileUIs[x, y] = null;
        }
        grid[x, y] = 0;
        PlaySFX(lightningClip);
    }

    void InitializeBoard()
    {
        grid    = new int[width, height];
        tileUIs = new TileUI[width, height];
        SpawnRandomTile();
        SpawnRandomTile();
        RenderBoard();
    }

    Vector3 GetCellWorldPosition(int x, int y)
    {
        int index = (height - 1 - y) * width + x;
        return cellTransforms[index].position;
    }

    void RenderBoard()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int value = grid[x, y];
                TileUI existing = tileUIs[x, y];

                if (existing != null && value == 0)
                {
                    Destroy(existing.gameObject);
                    tileUIs[x, y] = null;
                    continue;
                }

                if (value == 0) continue;

                if (existing == null)
                {
                    TileUI tile = Instantiate(tilePrefab, tileParent);
        tile.transform.position = GetCellWorldPosition(x, y);
        tile.transform.localScale = Vector3.one;
        tileUIs[x, y] = tile;
        existing = tile;
                }

                existing.Setup(value);
                existing.transform.position = GetCellWorldPosition(x, y);
            }
        }
    }

    // ── MOVE WITH ANIMATION ──────────────────────────────────────────
    private IEnumerator MoveCoroutine(Vector2Int direction)
    {
        var moves = CalculateMoves(direction, out bool anyMoved);
        if (!anyMoved) yield break;

        SaveState();
        isAnimating = true;

        bool hasMerge = false;
        foreach (var move in moves)
        {
            StartCoroutine(AnimateTile(move.tileUI, move.targetWorldPos, moveDuration));
            if (move.isMerge) hasMerge = true;
        }

        PlaySFX(hasMerge ? mergeClip : moveClip);

        yield return new WaitForSeconds(moveDuration);

        ApplyMoves(moves);

        foreach (var move in moves)
            LightningHazard.Instance?.ResetTimer(move.toX, move.toY);

        foreach (var move in moves)
        {
            if (move.isMerge)
            {
                if (move.tileUI != null)
                {
                    move.tileUI.Setup(move.mergedValue);
                    StartCoroutine(BounceAnimation(move.tileUI.transform));
                }

                GameManager.Instance.AddScore(move.mergedValue);
                GameManager.Instance.CheckWin(move.mergedValue);
            }
        }

        SpawnRandomTile();
        SpawnNewTileWithAnimation();

        yield return new WaitForSeconds(0.15f);

        if (IsGameOver())
            TriggerGameOver();

        isAnimating = false;
    }

    private void SaveState()
    {
        previousGrid  = (int[,])grid.Clone();
        previousScore = GameManager.Instance.Score;
        canUndo       = true;
    }

    public void OnClickUndo()
    {
        if (!canUndo)
        {
            Debug.Log("Khong co gi de Undo");
            return;
        }

        if (!undoFreeUsed)
        {
            ShowConfirmPopup("Bạn có chắc muốn hoàn tác?", () =>
            {
                undoFreeUsed = true;
                Undo();
            });
            return;
        }

        if (!undoAdUsed)
        {
            ShowConfirmPopup("Đã hết lượt miễn phí.\nXem quảng cáo để hoàn tác?", () =>
            {
                AdsManager.Instance?.ShowRewardedAd(
                    onRewarded: () => { undoAdUsed = true; Undo(); },
                    onFailed:   () => Debug.Log("Ad thất bại")
                );
            });
            return;
        }

        Debug.Log("Đã hết lượt Undo");
    }

    public void OnClickShuffle()
    {
        if (!shuffleFreeUsed)
        {
            ShowConfirmPopup("Bạn có chắc muốn xáo trộn?", () =>
            {
                shuffleFreeUsed = true;
                ShuffleBoard();
            });
            return;
        }

        if (!shuffleAdUsed)
        {
            ShowConfirmPopup("Đã hết lượt miễn phí.\nXem quảng cáo để xáo trộn?", () =>
            {
                AdsManager.Instance?.ShowRewardedAd(
                    onRewarded: () => { shuffleAdUsed = true; ShuffleBoard(); },
                    onFailed:   () => Debug.Log("Ad thất bại")
                );
            });
            return;
        }

        Debug.Log("Đã hết lượt Shuffle");
    }

    public void OnClickRestart()
    {
        ShowConfirmPopup("Ban co chac muon Restart?", () =>
        {
            GameManager.Instance?.SetScore(0);
            RestartBoard();
        });
    }

    public void OnClickHome()
    {
        if (confirmPopup == null)
        {
            Debug.LogWarning("confirmPopup chua gan trong Inspector!");
            return;
        }

        pendingConfirmAction = () =>
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        };

        if (confirmMessageText != null)
            confirmMessageText.text = "Bạn có chắc muốn về Main Menu?";

        confirmPopup.SetActive(true);
    }

    private void ShowConfirmPopup(string message, System.Action onConfirm)
    {
        if (confirmPopup == null)
        {
            Debug.LogWarning("confirmPopup chua gan trong Inspector!");
            return; 
        }

        pendingConfirmAction = onConfirm;
        if (confirmMessageText != null) confirmMessageText.text = message;
        confirmPopup.SetActive(true);
    }

    public void OnConfirmYes()
    {
        if (confirmPopup != null) confirmPopup.SetActive(false);
        pendingConfirmAction?.Invoke();
        pendingConfirmAction = null;
    }

    public void OnConfirmNo()
    {
        if (confirmPopup != null) confirmPopup.SetActive(false);
        pendingConfirmAction = null;
    }
    

    public void Undo()
    {
        if (!canUndo) return;
        canUndo = false;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (tileUIs[x, y] != null) { Destroy(tileUIs[x, y].gameObject); tileUIs[x, y] = null; }
            }

        grid    = (int[,])previousGrid.Clone();
        tileUIs = new TileUI[width, height];

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != 0)
                {
                    TileUI tile = Instantiate(tilePrefab, tileParent);
                    tile.transform.position = GetCellWorldPosition(x, y);
                    tile.Setup(grid[x, y]);
                    tileUIs[x, y] = tile;
                    StartCoroutine(SpawnAnimation(tile.transform));
                }
            }
        GameManager.Instance.SetScore(previousScore);
    }

    bool IsGameOver()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (grid[x, y] == 0) return false;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (x + 1 < width && grid[x, y] == grid[x + 1, y]) return false;
                if (y + 1 < height && grid[x, y] == grid[x, y + 1]) return false;
            }

        return true;
    }
    public void RestartBoard()
    {
        StopAllCoroutines();
        isAnimating = false;
        canUndo     = false;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (tileUIs[x, y] != null) { Destroy(tileUIs[x, y].gameObject); tileUIs[x, y] = null; }
                grid[x, y] = 0;
            }

        InitializeBoard();
        if (confirmPopup != null) confirmPopup.SetActive(false);
    }
    private IEnumerator AnimateTile(TileUI tile, Vector3 targetPos, float duration)
    {
        if (tile == null) yield break;
        Vector3 startPos = tile.transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (tile == null) yield break;
            elapsed += Time.deltaTime;
            tile.transform.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        if (tile != null) tile.transform.position = targetPos;
    }

    private IEnumerator BounceAnimation(Transform target)
    {
        Vector3 originalScale = Vector3.one;
        float duration = 0.15f, elapsed = 0f;

        while (elapsed < duration / 2f)
        {
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(originalScale, originalScale * 1.2f, elapsed / (duration / 2f));
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < duration / 2f)
        {
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(originalScale * 1.2f, originalScale, elapsed / (duration / 2f));
            yield return null;
        }
        if (target != null) target.localScale = originalScale;
    }

    private void SpawnNewTileWithAnimation()
    {
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (grid[x, y] != 0 && tileUIs[x, y] == null)
            {
                TileUI tile = Instantiate(tilePrefab, tileParent);
                tile.transform.position   = GetCellWorldPosition(x, y);
                tile.transform.localScale = Vector3.zero;
                tile.Setup(grid[x, y]);
                tileUIs[x, y] = tile;
                StartCoroutine(SpawnAnimation(tile.transform));
            }
        }
    }

    private IEnumerator SpawnAnimation(Transform target)
    {
        if (target == null) yield break;
        float elapsed = 0f, duration = 0.15f;
        while (elapsed < duration)
        {
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        if (target != null) target.localScale = Vector3.one;
    }

    // ── CALCULATE MOVES (không thay đổi grid ngay) ──────────────────
    private struct MoveInfo
    {
        public TileUI tileUI;
        public int fromX, fromY;
        public int toX, toY;
        public Vector3 targetWorldPos;
        public bool isMerge;
        public int mergedValue;
    }

    private List<MoveInfo> CalculateMoves(Vector2Int direction, out bool anyMoved)
    {
        var moves = new List<MoveInfo>();
        anyMoved = false;

        var blocked = NightModeHazard.Instance?.BlockedCells
            ?? new System.Collections.Generic.HashSet<Vector2Int>();

        int[,] tempGrid = (int[,])grid.Clone();
        bool[,] merged = new bool[width, height];

        List<int> xRange = BuildIndexRange(width, direction.x);
        List<int> yRange = BuildIndexRange(height, direction.y);

        foreach (int y in yRange)
        {
            foreach (int x in xRange)
            {
                if (tempGrid[x, y] == 0) continue;

                if (blocked.Contains(new Vector2Int(x, y))) continue;

                int currentX = x, currentY = y;

                while (true)
                {
                    int nextX = currentX + direction.x;
                    int nextY = currentY + direction.y;

                    if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height) break;

                    if (blocked.Contains(new Vector2Int(nextX, nextY))) break;

                    if (tempGrid[nextX, nextY] == 0)
                    {
                        tempGrid[nextX, nextY] = tempGrid[currentX, currentY];
                        tempGrid[currentX, currentY] = 0;
                        currentX = nextX; currentY = nextY;
                        anyMoved = true;
                    }
                  else if (tempGrid[nextX, nextY] == tempGrid[currentX, currentY] && !merged[nextX, nextY])
                    {
                        tempGrid[nextX, nextY] *= 2;
                        tempGrid[currentX, currentY] = 0;
                        merged[nextX, nextY] = true;
                        anyMoved = true;
                        currentX = nextX; currentY = nextY;
                        break;
                    }
                    else break;
                }

                if (currentX != x || currentY != y)
                {
                    moves.Add(new MoveInfo
                    {
                        tileUI = tileUIs[x, y],
                        fromX = x, fromY = y,
                        toX = currentX, toY = currentY,
                        targetWorldPos = GetCellWorldPosition(currentX, currentY),
                        isMerge = merged[currentX, currentY],
                        mergedValue = tempGrid[currentX, currentY]
                    });
                }
            }
        }

        return moves;
    }

    private void ApplyMoves(List<MoveInfo> moves)
    {
        foreach (var move in moves)
        {
            grid[move.fromX, move.fromY] = 0;
            tileUIs[move.fromX, move.fromY] = null;
        }
        foreach (var move in moves)
        {
            grid[move.toX, move.toY] = move.mergedValue;

            if (move.isMerge)
            {
                // Destroy tile đang ở ô đích nếu khác tile đang di chuyển vào
                TileUI existingAtDest = tileUIs[move.toX, move.toY];
                if (existingAtDest != null && existingAtDest != move.tileUI)
                    Destroy(existingAtDest.gameObject);
            }

            tileUIs[move.toX, move.toY] = move.tileUI;
        }
    }

    // ── HELPERS ─────────────────────────────────────────────────────
    void SpawnRandomTile()
    {
        if (IsBoardFull()) return;
        int x, y;
        do { x = Random.Range(0, width); y = Random.Range(0, height); }
        while (grid[x, y] != 0);
        grid[x, y] = Random.value < 0.9f ? 2 : 4;
    }

    bool IsBoardFull()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (grid[x, y] == 0) return false;
        return true;
    }

    List<int> BuildIndexRange(int length, int dir)
    {
        var list = new List<int>(length);
        if (dir > 0) for (int i = length - 2; i >= 0; i--) list.Add(i);
        else if (dir < 0) for (int i = 1; i < length; i++) list.Add(i);
        else for (int i = 0; i < length; i++) list.Add(i);
        return list;
    }

    void PrintBoard() { }
    public int[,] GetGrid() => grid;
    public Transform TileParent => tileParent;

    public void ResumeGame()
    {
        isAnimating = false;
    }

    public void TriggerGameOver()
    {
        int score = GameManager.Instance.Score;
        int best = PlayerPrefs.GetInt("BestScore", 0);
        if (score > best)
        {
            best = score;
            PlayerPrefs.SetInt("BestScore", best);
        }
        PlaySFX(gameOverClip);

        isAnimating = true;
        GameOverUI.Instance?.ShowGameOver(score, best);
    }

    public void PlayNightModeSound()
    {
        PlaySFX(nightModeClip);
    }

    private void PlaySFX(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    public void DestroyLowestTile()
    {
        int minVal = int.MaxValue;
        int minX = -1, minY = -1;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (grid[x, y] > 0 && grid[x, y] < minVal)
                {
                    minVal = grid[x, y];
                    minX = x; minY = y;
                }

        if (minX == -1) return;
        DestroyTile(minX, minY);

        if (!HasValidMoves())
            TriggerGameOver();
    }

    private bool HasValidMoves()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == 0) return true;
                if (x + 1 < width  && grid[x + 1, y] == grid[x, y]) return true;
                if (y + 1 < height && grid[x, y + 1] == grid[x, y]) return true;
            }
        return false;
    }

    public void ShuffleBoard()
    {
        StopAllCoroutines();
        isAnimating = false;

        var values = new List<int>();
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (grid[x, y] > 0) values.Add(grid[x, y]);
            if (tileUIs[x, y] != null) { Destroy(tileUIs[x, y].gameObject); tileUIs[x, y] = null; }
            grid[x, y] = 0;
        }

        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        var positions = new List<Vector2Int>();
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            positions.Add(new Vector2Int(x, y));

        for (int i = positions.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (positions[i], positions[j]) = (positions[j], positions[i]);
        }

        for (int i = 0; i < values.Count; i++)
        {
            int x = positions[i].x, y = positions[i].y;
            grid[x, y] = values[i];
            SpawnTileAt(x, y, values[i]);
        }
    }
    private void SpawnTileAt(int x, int y, int value)
    {
        TileUI tile = Instantiate(tilePrefab, tileParent);
        tile.transform.position = GetCellWorldPosition(x, y);
        tile.Setup(value);
        tileUIs[x, y] = tile;
        StartCoroutine(SpawnAnimation(tile.transform));
    }
}