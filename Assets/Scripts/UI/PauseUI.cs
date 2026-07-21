using UnityEngine;
using UnityEngine.UI;

public class PauseUI : MonoBehaviour
{
    [Header("按钮")]
    public Button ContinueButton;
    public Button RestartButton;
    public Button MainMenuButton;

    private void Awake()
    {
        if (ContinueButton != null)
            ContinueButton.onClick.AddListener(OnContinueClicked);
        if (RestartButton != null)
            RestartButton.onClick.AddListener(OnRestartClicked);
        if (MainMenuButton != null)
            MainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    private void OnContinueClicked()
    {
        GameManager.Instance?.ResumeGame();
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