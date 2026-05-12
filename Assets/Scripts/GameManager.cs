using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI bestText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private Button restartButton;

    private int bestScore = 0;
    private bool hasWon = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [System.Obsolete]
    private void Start()
    {
        bestScore = PlayerPrefs.GetInt("BestScore", 0);
        UpdateScoreUI();

        gameOverPanel?.SetActive(false);
        winPanel?.SetActive(false);

        restartButton?.onClick.AddListener(RestartGame);
    }

    public int Score { get; private set; }

    public void AddScore(int value)
    {
        Score += value;
        if (Score > bestScore)
        {
            bestScore = Score;
            PlayerPrefs.SetInt("BestScore", bestScore);
        }
        UpdateScoreUI();
    }

    public void CheckWin(int mergedValue)
    {
        if (mergedValue == 2048 && !hasWon)
        {
            hasWon = true;
            winPanel?.SetActive(true);
        }
    }

    public void TriggerGameOver()
    {
        gameOverPanel?.SetActive(true);
    }

    [System.Obsolete]
    public void RestartGame()
    {
        Score = 0;
        hasWon = false;
        UpdateScoreUI();
        gameOverPanel?.SetActive(false);
        winPanel?.SetActive(false);

        FindObjectOfType<BoardManager>().RestartBoard();
    }

    private void UpdateScoreUI()
    {
        if (scoreText) scoreText.text = Score.ToString();
        if (bestText) bestText.text = bestScore.ToString();
    }

    public void SetScore(int value)
    {
        Score = value;
        UpdateScoreUI();
    }
}