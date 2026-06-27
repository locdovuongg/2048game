using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    public bool IsGameOver { get; private set; } = false;

    [Header("References")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text adButtonText;
    [SerializeField] private Button watchAdButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button homeButton;

    private BoardManager boardManager;
    private bool adUsed = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        boardManager = FindFirstObjectByType<BoardManager>();
        panel.SetActive(false);

        if (watchAdButton != null) watchAdButton.onClick.AddListener(OnWatchAd);
        if (restartButton != null) restartButton.onClick.AddListener(OnShuffle);
    }

    public void ShowGameOver(int score, int bestScore)
    {
        IsGameOver = true;
        panel.SetActive(true);

        if (scoreText != null)     scoreText.text     = $"SCORE\n{score}";
        if (bestScoreText != null) bestScoreText.text  = $"BEST\n{bestScore}";

        if (watchAdButton != null)
        {
            watchAdButton.gameObject.SetActive(!adUsed);
            watchAdButton.interactable = true;
        }
        if (adButtonText != null) adButtonText.text = "Watch Ad\nContinue";

        Time.timeScale = 0f;
    }

    public void HideGameOver()
    {
        IsGameOver = false;
        panel.SetActive(false);
        Time.timeScale = 1f;
    }

    private void OnWatchAd()
    {
        if (watchAdButton != null) watchAdButton.interactable = false;
        if (adButtonText != null) adButtonText.text = "Loading...";

        AdsManager.Instance?.ShowRewardedAd(
            onRewarded: () =>
            {
                adUsed = true;
                HideGameOver();
                boardManager.DestroyLowestTile();
                boardManager.ResumeGame();
            },
            onFailed: () =>
            {
                if (watchAdButton != null) watchAdButton.interactable = true;
                if (adButtonText != null) adButtonText.text = "Watch Ad\nContinue";
            }
        );
    }

    private void OnRestart()
    {
        adUsed = false;
        HideGameOver();
        boardManager.ResumeGame();
        boardManager.RestartBoard();
        GameManager.Instance?.SetScore(0);
    }
    private void OnShuffle()
    {
        adUsed = false;
        HideGameOver();
        boardManager.ResumeGame();
        boardManager.ShuffleBoard();
    }
}