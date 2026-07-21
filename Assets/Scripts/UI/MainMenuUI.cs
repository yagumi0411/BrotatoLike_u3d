using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI 元素")]
    public TextMeshProUGUI TitleText;
    public Button StartButton;

    [Header("地图选择（预留接口）")]
    public RectTransform MapListRoot;            // 地图列表容器
    public GameObject MapItemPrefab;             // 地图选项预制体（后续扩展用）

    private void Awake()
    {
        if (StartButton != null)
        {
            StartButton.onClick.AddListener(OnStartClicked);
        }
    }

    private void OnStartClicked()
    {
        GameManager.Instance?.StartGame();
    }
}