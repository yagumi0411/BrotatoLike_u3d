using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI 元素")]
    public TextMeshProUGUI TitleText;
    public Button StartButton;

    [Header("模式选择")]
    [Tooltip("三个互斥模式 Toggle（挂同一 ToggleGroup），顺序：0=单人 1=本地双人 2=联机双人")]
    public Toggle[] ModeToggles;

    [Header("地图选择（预留接口）")]
    public RectTransform MapListRoot;            // 地图列表容器
    public GameObject MapItemPrefab;             // 地图选项预制体（后续扩展用）

    private void Awake()
    {
        if (StartButton != null)
        {
            StartButton.onClick.AddListener(OnStartClicked);
        }

        if (ModeToggles != null)
        {
            for (int i = 0; i < ModeToggles.Length; i++)
            {
                int index = i;
                if (ModeToggles[i] != null)
                    ModeToggles[i].onValueChanged.AddListener(isOn => OnModeToggled(index, isOn));
            }
        }
    }

    /// <summary>
    /// 模式 Toggle 变化：互斥高亮由 ToggleGroup 组件管理（编辑器配置），
    /// 代码只负责"选中后生效"。取消选中事件不处理。
    /// </summary>
    private void OnModeToggled(int index, bool isOn)
    {
        if (!isOn || index < 0 || index > 2) return;

        GameManager.Instance?.SetGameMode((GameManager.EGameMode)index);
    }

    private void OnStartClicked()
    {
        GameManager.Instance?.StartGame();
    }
}
