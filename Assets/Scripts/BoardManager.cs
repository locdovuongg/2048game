using System.Collections;
using System.Collections.Generic;
using System.Text;
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

    [Header("Animation")]
    [SerializeField] private float moveDuration = 0.1f;

    private int[,] grid;
    private TileUI[,] tileUIs;
    private bool isAnimating = false;

    private int[,] previousGrid;
    private TileUI[,] previousTileUIs;
    private int previousScore;
    private bool canUndo = false;

    private void Start()
    {
        if (cellTransforms == null || cellTransforms.Length != width * height)
            Debug.LogWarning("cellTransforms length does not match width*height");

        StartCoroutine(InitAfterLayout());
    }

    private IEnumerator InitAfterLayout()
    {
        yield return null;
        InitializeBoard();
        LightningHazard.Instance?.Init(width, height, this);
        NightModeHazard.Instance?.Init(this);
    }

    private void Update()
    {
        if (isAnimating) return;
        if (Input.GetKeyDown(KeyCode.A)) StartCoroutine(MoveCoroutine(Vector2Int.left));
        if (Input.GetKeyDown(KeyCode.D)) StartCoroutine(MoveCoroutine(Vector2Int.right));
        if (Input.GetKeyDown(KeyCode.W)) StartCoroutine(MoveCoroutine(Vector2Int.up));
        if (Input.GetKeyDown(KeyCode.S)) StartCoroutine(MoveCoroutine(Vector2Int.down));
        if (Input.GetKeyDown(KeyCode.Z)) Undo();

        if (Input.GetMouseButtonDown(0))
            NightModeHazard.Instance?.RegisterTap();
    }

    // ✅ Thêm GetCellRect để LightningHazard dùng
    public RectTransform GetCellRect(int x, int y)
    {
        int index = (height - 1 - y) * width + x;
        return cellTransforms[index];
    }

    // ✅ Expose để Hazard scripts dùng
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
        PrintBoard();
    }

    void InitializeBoard()
    {
        grid = new int[width, height];
        tileUIs = new TileUI[width, height];
        SpawnRandomTile();
        SpawnRandomTile();
        RenderBoard();
        PrintBoard();
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
                    tile.transform.localScale = Vector3.one; // ✅ reset scale
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

        foreach (var move in moves)
            StartCoroutine(AnimateTile(move.tileUI, move.targetWorldPos, moveDuration));

        yield return new WaitForSeconds(moveDuration);

        ApplyMoves(moves);

        // Reset lightning timer cho tile đã di chuyển
        foreach (var move in moves)
            LightningHazard.Instance?.ResetTimer(move.toX, move.toY);

        // 4. Bounce animation cho các tile vừa merge
         foreach (var move in moves)
    {
        if (move.isMerge)
        {
            // ✅ Kiểm tra tile còn tồn tại trước khi gọi Setup
            if (move.tileUI != null)
            {
                move.tileUI.Setup(move.mergedValue);
                StartCoroutine(BounceAnimation(move.tileUI.transform));
            }

            GameManager.Instance.AddScore(move.mergedValue);
            GameManager.Instance.CheckWin(move.mergedValue);
        }
    }

        // 5. Spawn tile mới với spawn animation
        SpawnRandomTile();
        SpawnNewTileWithAnimation();
        PrintBoard();

        yield return new WaitForSeconds(0.15f);

        // ✅ Thêm game over check
        if (IsGameOver())
            GameManager.Instance.TriggerGameOver();

        isAnimating = false;
    }

    private void SaveState()
    {
        previousGrid = (int[,])grid.Clone();
        previousTileUIs = (TileUI[,])tileUIs.Clone();
        previousScore = GameManager.Instance.Score; // cần expose Score
        canUndo = true;
    }

    public void Undo()
    {
        if (!canUndo) return;
        canUndo = false;

        // Xoá tất cả tiles hiện tại
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (tileUIs[x, y] != null)
                {
                    Destroy(tileUIs[x, y].gameObject);
                    tileUIs[x, y] = null;
                }
            }

        // Khôi phục grid
        grid = (int[,])previousGrid.Clone();
        tileUIs = new TileUI[width, height];

        // Tạo lại tiles từ state cũ
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

        // Khôi phục score
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
        isAnimating = false; // ✅ reset nếu đang animate khi restart
        StopAllCoroutines();

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (tileUIs[x, y] != null)
                {
                    Destroy(tileUIs[x, y].gameObject);
                    tileUIs[x, y] = null;
                }
            }
        InitializeBoard();
    }
    private IEnumerator AnimateTile(TileUI tile, Vector3 targetPos, float duration)
    {
        if (tile == null) yield break; // ✅
        Vector3 startPos = tile.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (tile == null) yield break; // ✅
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            tile.transform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        if (tile != null)
            tile.transform.position = targetPos;
    }

    private IEnumerator BounceAnimation(Transform target)
    {
        Vector3 originalScale = Vector3.one;
        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration / 2f)
        {
            if (target == null) yield break; // ✅
            elapsed += Time.deltaTime;
            float t = elapsed / (duration / 2f);
            target.localScale = Vector3.Lerp(originalScale, originalScale * 1.2f, t);
            yield return null;
        }

        elapsed = 0f;

        while (elapsed < duration / 2f)
        {
            if (target == null) yield break; // ✅
            elapsed += Time.deltaTime;
            float t = elapsed / (duration / 2f);
            target.localScale = Vector3.Lerp(originalScale * 1.2f, originalScale, t);
            yield return null;
        }

        if (target != null)
            target.localScale = originalScale;
    }

    private void SpawnNewTileWithAnimation()
    {
        // RenderBoard nhưng chỉ tạo tile mới (chưa có tileUI) và chạy spawn animation
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] != 0 && tileUIs[x, y] == null)
                {
                    TileUI tile = Instantiate(tilePrefab, tileParent);
                    tile.transform.position = GetCellWorldPosition(x, y);
                    tile.transform.localScale = Vector3.zero;
                    tile.Setup(grid[x, y]);
                    tileUIs[x, y] = tile;
                    StartCoroutine(SpawnAnimation(tile.transform));
                }
            }
        }
    }

    private IEnumerator SpawnAnimation(Transform target)
    {
        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // ✅ Kiểm tra null trước khi truy cập
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            target.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);
            yield return null;
        }

        if (target != null)
            target.localScale = Vector3.one;
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

        // ✅ Lấy ô bị block từ NightMode
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

                // ✅ Tile ở ô bị block không được di chuyển
                if (blocked.Contains(new Vector2Int(x, y))) continue;

                int currentX = x, currentY = y;

                while (true)
                {
                    int nextX = currentX + direction.x;
                    int nextY = currentY + direction.y;

                    if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height) break;

                    // ✅ Không di chuyển vào ô bị block
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

    void PrintBoard()
    {
        var sb = new StringBuilder();
        for (int y = height - 1; y >= 0; y--)
        {
            for (int x = 0; x < width; x++)
                sb.Append(grid[x, y].ToString().PadRight(5));
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }

    // ✅ Expose grid để LightningHazard đọc
    public int[,] GetGrid() => grid;

    // ✅ Expose TileParent
    public Transform TileParent => tileParent;

    // ✅ Gọi khi hết nước đi
    // ✅ Thêm hàm resume để reset trạng thái
    public void ResumeGame()
    {
        isAnimating = false;
        StopAllCoroutines(); // clear coroutine cũ nếu bị kẹt
        Debug.Log("✅ Board resumed");
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

        // ✅ Dừng nhận input trước khi show GameOver
        isAnimating = true;
        GameOverUI.Instance?.ShowGameOver(score, best);
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
                if (x + 1 < width && grid[x + 1, y] == grid[x, y]) return true;
                if (y + 1 < height && grid[x, y + 1] == grid[x, y]) return true;
            }
        return false;
    }

    public void RestartGame()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (tileUIs[x, y] != null)
                {
                    Destroy(tileUIs[x, y].gameObject);
                    tileUIs[x, y] = null;
                }
                grid[x, y] = 0;
            }

        GameManager.Instance.SetScore(0); // ✅ reset score qua GameManager
        SpawnRandomTile();
        SpawnRandomTile();
        RenderBoard();
    }
}