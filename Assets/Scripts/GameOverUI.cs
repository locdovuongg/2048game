using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Button watchAdButton;
    [SerializeField] private Button restartButton;

    private BoardManager boardManager;
    private bool adUsed = false; // chỉ cho xem ad 1 lần

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        boardManager = FindFirstObjectByType<BoardManager>();
        panel.SetActive(false);

        watchAdButton.onClick.AddListener(OnWatchAd);
        restartButton.onClick.AddListener(OnRestart);
    }

    public void ShowGameOver(int score, int bestScore)
    {
        panel.SetActive(true);
        scoreText.text = $"SCORE\n{score}";

        // ✅ Chỉ cho xem ad 1 lần
        watchAdButton.gameObject.SetActive(!adUsed);

        Time.timeScale = 0f; // dừng game
    }

    private void OnWatchAd()
    {
        // ✅ Giả lập xem ad xong
        StartCoroutine(SimulateAd());
    }

    private IEnumerator SimulateAd()
    {
        watchAdButton.interactable = false;

        yield return new WaitForSecondsRealtime(2f); // ✅ dùng Realtime vì timeScale = 0

        adUsed = true;

        HideGameOver(); // ✅ restore timeScale = 1f TRƯỚC

        yield return null; // đợi 1 frame

        boardManager.DestroyLowestTile(); // ✅ destroy SAU khi resume
        boardManager.ResumeGame();        // ✅ reset trạng thái board
    }

    private void OnRestart()
    {
        adUsed = false;
        HideGameOver();
        boardManager.ResumeGame();
        boardManager.RestartGame();
    }

    public void HideGameOver()
    {
        panel.SetActive(false);
        Time.timeScale = 1f; // ✅ resume time
    }
}