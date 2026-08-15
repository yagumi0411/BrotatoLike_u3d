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

    // 联机面板（仅联机模式出现，代码动态创建；静态布局的 UI 应走编辑器，这里是"运行时才需要"的例外）
    private GameObject _onlinePanel;
    private TMP_InputField _ipInput;

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

    // === 联机面板（创建房间 / 加入房间） ===

    /// <summary>显示联机面板（首次显示时动态创建）</summary>
    public void ShowOnlinePanel()
    {
        if (_onlinePanel == null)
            CreateOnlinePanel();
        _onlinePanel?.SetActive(true);
    }

    private void CreateOnlinePanel()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        _onlinePanel = new GameObject("OnlinePanel");
        var panelRt = _onlinePanel.AddComponent<RectTransform>();
        _onlinePanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
        _onlinePanel.transform.SetParent(canvas.transform, false);
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(440f, 320f);

        var layout = _onlinePanel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.padding = new RectOffset(28, 28, 28, 28);
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateLabel("联机双人", 30f, Color.white, true);

        // IP 输入框
        var inputGo = new GameObject("IPInput");
        inputGo.transform.SetParent(_onlinePanel.transform, false);
        var inputRt = inputGo.AddComponent<RectTransform>();
        inputRt.sizeDelta = new Vector2(0f, 48f);
        inputGo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 1f);

        _ipInput = inputGo.AddComponent<TMP_InputField>();
        var text = CreateTextComponent("", 20f, Color.white, inputGo.transform);
        var placeholder = CreateTextComponent("输入主机 IP（默认 127.0.0.1）", 20f, Color.gray, inputGo.transform);
        _ipInput.textComponent = text;
        _ipInput.placeholder = placeholder;
        _ipInput.textViewport = inputRt;
        _ipInput.text = "127.0.0.1";
        SetFullStretch(text.rectTransform);
        SetFullStretch(placeholder.rectTransform);
        text.raycastTarget = false;
        placeholder.raycastTarget = false;

        CreateButton("创建房间（主机）", () => StartNetwork(true));
        CreateButton("加入房间（客户端）", () => StartNetwork(false));
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
            if (transport != null && _ipInput != null && !string.IsNullOrEmpty(_ipInput.text))
                transport.ConnectionData.Address = _ipInput.text.Trim();
        }

        _onlinePanel?.SetActive(false);

        if (asHost)
            nm.StartHost();
        else
            nm.StartClient();
    }

    // === 动态 UI 工具 ===

    private TextMeshProUGUI CreateLabel(string content, float fontSize, Color color, bool bold = false)
    {
        var tmp = CreateTextComponent(content, fontSize, color, _onlinePanel.transform);
        if (bold)
            tmp.fontStyle = FontStyles.Bold;
        return tmp;
    }

    private TextMeshProUGUI CreateTextComponent(string content, float fontSize, Color color, Transform parent)
    {
        var go = new GameObject("Text");
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        go.transform.SetParent(parent, false);
        return tmp;
    }

    private Button CreateButton(string label, System.Action onClick)
    {
        var go = new GameObject(label);
        go.transform.SetParent(_onlinePanel.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 52f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.45f, 0.8f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());

        var text = CreateTextComponent(label, 22f, Color.white, go.transform);
        SetFullStretch(text.rectTransform);
        text.raycastTarget = false;
        return btn;
    }

    private static void SetFullStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(12f, 4f);
        rt.offsetMax = new Vector2(-12f, -4f);
    }
}
