using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("统计文字")]
    public TextMeshProUGUI WaveResultText;
    public TextMeshProUGUI LevelResultText;
    public TextMeshProUGUI KillResultText;
    public TextMeshProUGUI TimeResultText;

    [Header("按钮")]
    public Button RestartButton;
    public Button MainMenuButton;

    private void Awake()
    {
        if (RestartButton != null)
            RestartButton.onClick.AddListener(OnRestartClicked);
        if (MainMenuButton != null)
            MainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    public void ShowResult(int wave, int level, int kills, float playTimeSeconds)
    {
        if (WaveResultText != null)
            WaveResultText.text = $"存活波次: {wave}";

        if (LevelResultText != null)
            LevelResultText.text = $"最终等级: Lv.{level}";

        if (KillResultText != null)
            KillResultText.text = $"击杀数: {kills}";

        if (TimeResultText != null)
        {
            int minutes = Mathf.FloorToInt(playTimeSeconds / 60f);
            int seconds = Mathf.FloorToInt(playTimeSeconds % 60f);
            TimeResultText.text = $"存活时间: {minutes}分{seconds}秒";
        }
    }

    private void OnRestartClicked()
    {
        GameManager.Instance?.RestartGame();
    }

    private void OnMainMenuClicked()
    {
        GameManager.Instance?.ReturnToMainMenu();
    }
}