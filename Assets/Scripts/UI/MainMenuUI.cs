using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

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

    [Header("联机面板（编辑器搭建，默认隐藏）")]
    public GameObject OnlinePanel;
    public TMP_InputField IPInput;
    public Button CreateRoomButton;
    public Button JoinRoomButton;

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

        // 联机面板按钮（结构在编辑器搭建，事件绑定走代码）
        if (CreateRoomButton != null)
            CreateRoomButton.onClick.AddListener(() => StartNetwork(true));
        if (JoinRoomButton != null)
            JoinRoomButton.onClick.AddListener(() => StartNetwork(false));
    }

    private void Start()
    {
        // 以主菜单 Toggle 的勾选状态为准，同步 GameManager 模式。
        // 避免编辑器里勾选与 GameMode 默认值（LocalCoop）不一致：
        // UI 显示"单人模式"但开局却是双人、P2 可操控。
        if (ModeToggles != null)
        {
            for (int i = 0; i < ModeToggles.Length; i++)
            {
                if (ModeToggles[i] != null && ModeToggles[i].isOn)
                {
                    GameManager.Instance?.SetGameMode((GameManager.EGameMode)i);
                    break;
                }
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

    // === 联机面板（创建房间 / 加入房间） ===

    /// <summary>显示联机面板（结构在编辑器搭建，默认隐藏）</summary>
    public void ShowOnlinePanel()
    {
        if (OnlinePanel != null)
            OnlinePanel.SetActive(true);
    }

    private void StartNetwork(bool asHost)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[MainMenuUI] 场景中未找到 NetworkManager（需放置 NetworkManager 对象并配置 Player Prefab）");
            return;
        }

        // 重复启动保护：先断开旧连接（幂等）
        if (nm.IsServer || nm.IsConnectedClient || nm.IsListening)
            nm.Shutdown();

        if (!asHost)
        {
            var transport = nm.GetComponent<UnityTransport>();
            if (transport != null && IPInput != null && !string.IsNullOrEmpty(IPInput.text))
                transport.ConnectionData.Address = IPInput.text.Trim();
        }

        OnlinePanel?.SetActive(false);

        if (asHost)
            nm.StartHost();
        else
            nm.StartClient();
    }
}
