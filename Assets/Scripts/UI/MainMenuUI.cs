using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI 元素")]
    public TextMeshProUGUI TitleText;
    public Button StartButton;

    [Header("天赋入口")]
    public Button OpenTalentsButton;
    public TextMeshProUGUI MetaProgressionStatus;

    [Header("地图选择（预留接口）")]
    public RectTransform MapListRoot;            // 地图列表容器
    public GameObject MapItemPrefab;             // 地图选项预制体（后续扩展用）

    private void Awake()
    {
        if (StartButton != null)
        {
            StartButton.onClick.AddListener(OnStartClicked);
        }
        if (OpenTalentsButton != null)
        {
            OpenTalentsButton.onClick.AddListener(OnOpenTalentsClicked);
        }
        if (OpenTalentsButton == null || MetaProgressionStatus == null)
        {
            Debug.LogError("MainMenuUI：请在 Inspector 中绑定 Open Talents Button 和 Meta Progression Status。", this);
        }
    }

    private void OnStartClicked()
    {
        GameManager.Instance?.StartGame();
    }

    private void OnOpenTalentsClicked()
    {
        GameManager.Instance?.TalentUI?.SetVisible(true);
    }

    private void OnDestroy()
    {
        if (StartButton != null) StartButton.onClick.RemoveListener(OnStartClicked);
        if (OpenTalentsButton != null) OpenTalentsButton.onClick.RemoveListener(OnOpenTalentsClicked);
    }
}
