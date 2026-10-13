using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TalentGraphUI : MonoBehaviour
{
    private class NodeView
    {
        public TalentNodeDefinition Definition;
        public TalentNodeGraphic Graphic;
        public TextMeshProUGUI Rank;
        public TextMeshProUGUI Name;
    }

    private class LinkView
    {
        public TalentLinkDefinition Definition;
        public Image Graphic;
    }

    private readonly Dictionary<string, NodeView> _nodes = new Dictionary<string, NodeView>();
    private readonly List<LinkView> _links = new List<LinkView>();
    private GameManager _manager;
    private MetaProgression _progression;
    private TMP_FontAsset _font;
    private GameObject _root;
    private TextMeshProUGUI _menuStatus;
    private TextMeshProUGUI _status;
    private TextMeshProUGUI _detail;
    private TextMeshProUGUI _bonuses;
    private Button _allocate;
    private Button _reset;
    private TalentGraphViewport _viewport;
    private RectTransform _content;
    private string _selected;
    private string _hovered;

    private void Start()
    {
        _manager = GameManager.Instance;
        _progression = _manager.Progression;
        var names = _manager.LevelUpUI?.OptionNames;
        _font = names != null && names.Length > 0 && names[0] != null ? names[0].font : TMP_Settings.defaultFontAsset;
        if (_manager.MainMenuUI != null)
        {
            _menuStatus = _manager.MainMenuUI.MetaProgressionStatus;
        }
        _progression.OnChanged += Refresh;
        Refresh();
    }

    public void SetVisible(bool visible)
    {
        if (visible && (_manager == null || _manager.CurrentState != GameManager.EGameState.MainMenu)) return;
        if (visible && _root == null) Build();
        if (_root == null) return;
        _root.SetActive(visible);
        if (visible) { _hovered = null; _selected = _progression.Graph.StartNodeId; ResetView(); Refresh(); }
    }

    private void Update()
    {
        if (_root != null && _root.activeSelf && UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) SetVisible(false);
    }

    private void Build()
    {
        _root = new GameObject("TalentGraphCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _root.transform.SetParent(transform, false);
        var canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 3100;
        var scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var backdrop = Rect("Backdrop", _root.transform, Vector2.zero, Vector2.zero);
        backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one;
        backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
        backdrop.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.03f, 0.055f, 0.97f);
        var panel = Rect("TalentPanel", backdrop, Vector2.zero, new Vector2(1200, 650));
        var title = Label(panel, "Title", new Vector2(-250, 285), new Vector2(660, 55), 30);
        title.text = _progression.Graph.Name; title.alignment = TextAlignmentOptions.Left;
        _status = Label(panel, "ProgressionStatus", new Vector2(-230, 240), new Vector2(700, 45), 20);
        _status.alignment = TextAlignmentOptions.Left;
        MakeButton(panel, "CloseTalents", new Vector2(510, 285), new Vector2(160, 45), "返回菜单", () => SetVisible(false));

        var view = Rect("GraphViewport", panel, new Vector2(-150, -20), new Vector2(850, 475));
        view.gameObject.AddComponent<Image>().color = new Color(0.055f, 0.07f, 0.1f);
        view.gameObject.AddComponent<RectMask2D>();
        _content = Rect("GraphContent", view, Vector2.zero, new Vector2(1600, 1300));
        _viewport = view.gameObject.AddComponent<TalentGraphViewport>();
        _viewport.Content = _content;
        foreach (var link in _progression.Graph.Links)
        {
            Vector2 from = _progression.Graph.GetNode(link.From).Position;
            Vector2 to = _progression.Graph.GetNode(link.To).Position;
            var line = Rect("Link", _content, (from + to) * 0.5f, new Vector2(Vector2.Distance(from, to), 3));
            line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
            var image = line.gameObject.AddComponent<Image>(); image.raycastTarget = false;
            _links.Add(new LinkView { Definition = link, Graphic = image });
        }
        foreach (var node in _progression.Graph.Nodes) BuildNode(node);

        var details = Rect("NodeDetails", panel, new Vector2(445, -20), new Vector2(280, 475));
        details.gameObject.AddComponent<Image>().color = new Color(0.075f, 0.085f, 0.13f);
        _detail = Label(details, "DetailsText", new Vector2(0, 112), new Vector2(245, 230), 21);
        _detail.alignment = TextAlignmentOptions.TopLeft;
        _allocate = MakeButton(details, "AllocateTalent", new Vector2(0, -30), new Vector2(245, 45), "投入 1 点", AllocateSelected);
        _bonuses = Label(details, "TotalBonuses", new Vector2(0, -137), new Vector2(245, 145), 18);
        _bonuses.alignment = TextAlignmentOptions.TopLeft;
        var help = Label(panel, "Help", new Vector2(-145, -285), new Vector2(850, 55), 17);
        help.text = "拖拽移动 · 滚轮缩放 · 悬停查看 · 点击选择\n相邻节点投入第 1 点即可延伸，全部等级均可通过游玩获得。";
        MakeButton(panel, "ResetView", new Vector2(380, -285), new Vector2(120, 45), "回到起点", ResetView);
        _reset = MakeButton(panel, "ResetTalents", new Vector2(520, -285), new Vector2(140, 45), "免费重置", () =>
        {
            if (_manager.CurrentState == GameManager.EGameState.MainMenu) _progression.ResetTalents();
        });
    }

    private void BuildNode(TalentNodeDefinition node)
    {
        float size = node.Notable ? 68f : 52f;
        var rect = Rect(node.Id, _content, node.Position, new Vector2(size, size));
        var graphic = rect.gameObject.AddComponent<TalentNodeGraphic>();
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = graphic;
        button.transition = Selectable.Transition.None;
        string id = node.Id;
        button.onClick.AddListener(() => { _selected = id; RefreshDetail(); });
        var rank = Label(rect, "Rank", Vector2.zero, new Vector2(size, size), node.Notable ? 20 : 17);
        var name = Label(rect, "NodeName", new Vector2(0, -size * 0.5f - 15), new Vector2(105, 25), 15);
        name.text = node.Name;
        var pointer = rect.gameObject.AddComponent<TalentNodePointer>();
        pointer.Enter = () => { _hovered = id; RefreshDetail(); };
        pointer.Exit = () => { _hovered = null; RefreshDetail(); };
        pointer.Select = () => { _selected = id; RefreshDetail(); };
        _nodes.Add(id, new NodeView { Definition = node, Graphic = graphic, Rank = rank, Name = name });
    }

    private void AllocateSelected()
    {
        if (_manager.CurrentState == GameManager.EGameState.MainMenu) _progression.TryAllocate(_selected);
    }

    private void ResetView()
    {
        if (_content == null) return;
        _viewport.ResetView(new Vector2(0, -15));
    }

    private void Refresh()
    {
        if (_progression == null) return;
        if (_root != null && (_nodes.Count != _progression.Graph.Nodes.Count || _links.Count != _progression.Graph.Links.Count))
        {
            bool visible = _root.activeSelf;
            _root.SetActive(false); Destroy(_root);
            _nodes.Clear(); _links.Clear(); _root = null;
            _hovered = null;
            Build(); _root.SetActive(visible); ResetView();
        }
        string next = _progression.AllPointsEarned ? "本片区天赋点已全部获得" : $"下一点经验：{_progression.UnspentXP} / {_progression.NextPointXP}";
        string summary = $"可用天赋点：{_progression.AvailablePoints}    已分配：{_progression.SpentPoints} / {_progression.Graph.TotalRanks}\n{next}";
        var reward = _progression.LastReward;
        if (_menuStatus != null)
            _menuStatus.text = summary + (reward == null ? "" : $"\n上局天赋经验 +{reward.XP}，获得天赋点 +{reward.Points}") +
                (string.IsNullOrEmpty(_progression.StorageError) ? "" : "\n" + _progression.StorageError);
        if (_status == null) return;
        _status.text = summary;
        foreach (var pair in _nodes)
        {
            var view = pair.Value;
            int rank = _progression.GetRank(pair.Key);
            bool origin = pair.Key == _progression.Graph.StartNodeId;
            bool connected = _progression.IsConnected(pair.Key);
            Color branch = BranchColor(view.Definition.Branch);
            view.Graphic.color = origin || rank > 0 ? branch : connected ? Color.Lerp(branch, Color.white, 0.35f) : new Color(0.24f, 0.27f, 0.34f);
            view.Rank.text = origin ? "法师" : $"{rank}/{view.Definition.MaxRank}";
            view.Rank.color = origin || rank > 0 || connected ? new Color(0.96f, 0.92f, 0.82f) : new Color(0.55f, 0.57f, 0.65f);
            view.Name.color = view.Rank.color;
        }
        foreach (var link in _links)
        {
            bool from = link.Definition.From == _progression.Graph.StartNodeId || _progression.GetRank(link.Definition.From) > 0;
            bool to = link.Definition.To == _progression.Graph.StartNodeId || _progression.GetRank(link.Definition.To) > 0;
            link.Graphic.color = from && to ? new Color(0.9f, 0.72f, 0.38f) : from || to ? new Color(0.46f, 0.48f, 0.5f) : new Color(0.2f, 0.24f, 0.3f);
        }
        _reset.interactable = !_progression.IsReadOnly && _progression.SpentPoints > 0;
        var bonus = _progression.GetBonuses();
        _bonuses.text = $"当前天赋加成 · 下一局生效\n伤害 +{bonus.Damage * 100:0.#}%  暴击 +{bonus.CritChance * 100:0.#}%\n武器距离 +{bonus.WeaponRange * 100:0.#}%\n拾取半径 +{bonus.PickupRadius:0.#}\n生命 +{bonus.MaxHP:0.#}  减伤 {bonus.DamageReduction * 100:0.#}%";
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        if (_detail == null) return;
        string id = _hovered ?? _selected;
        var node = _progression.Graph.GetNode(id);
        if (node == null) return;
        bool origin = id == _progression.Graph.StartNodeId;
        int rank = _progression.GetRank(id);
        string availability = origin ? "起点 · 无需投入点数" : rank >= node.MaxRank ? "已达到最高等级" :
            !_progression.IsConnected(id) ? "需要先点亮相邻节点" : _progression.AvailablePoints <= 0 ? "天赋点不足，完成对局积累经验" : "可投入 1 点";
        if (!string.IsNullOrEmpty(_progression.StorageError)) availability = _progression.StorageError;
        _detail.text = $"<b>{node.Name}</b>\n{(origin ? "法师起点" : $"等级 {rank} / {node.MaxRank}")}\n\n{node.Description}\n\n{availability}";
        // Hover only previews; the button always applies to the explicitly selected node.
        _allocate.interactable = _hovered == null && _progression.CanAllocate(_selected);
        foreach (var view in _nodes.Values)
        {
            view.Graphic.Selected = view.Definition.Id == (_hovered ?? _selected);
            view.Graphic.SetVerticesDirty();
        }
    }

    private static Color BranchColor(string branch)
    {
        switch (branch)
        {
            case "damage": return new Color(0.88f, 0.47f, 0.33f);
            case "range": return new Color(0.3f, 0.68f, 0.85f);
            case "survival": return new Color(0.42f, 0.72f, 0.5f);
            default: return new Color(0.82f, 0.69f, 0.4f);
        }
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    private TextMeshProUGUI Label(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        var label = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.95f, 0.9f, 0.8f); label.raycastTarget = false;
        return label;
    }

    private Button MakeButton(Transform parent, string name, Vector2 position, Vector2 size, string text, UnityAction action)
    {
        var rect = Rect(name, parent, position, size);
        rect.gameObject.AddComponent<Image>().color = new Color(0.24f, 0.29f, 0.38f);
        var button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
        Label(rect, "Label", Vector2.zero, size, 20).text = text;
        return button;
    }

    private void OnDestroy()
    {
        if (_progression != null) _progression.OnChanged -= Refresh;
    }
}

public class TalentNodePointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
{
    public UnityAction Enter;
    public UnityAction Exit;
    public UnityAction Select;
    public void OnPointerEnter(PointerEventData eventData) => Enter?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => Exit?.Invoke();
    public void OnSelect(BaseEventData eventData) => Select?.Invoke();
}

// Lightweight circle mesh avoids runtime texture assets for data-defined nodes.
[RequireComponent(typeof(CanvasRenderer))]
public class TalentNodeGraphic : MaskableGraphic
{
    public bool Selected;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        const int segments = 40;
        var rect = rectTransform.rect;
        Vector2 center = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
        Color fill = new Color(color.r * 0.16f, color.g * 0.16f, color.b * 0.16f, 1f);
        Color ring = Selected ? new Color(1f, 0.9f, 0.6f) : color;
        mesh.AddVert(center, fill, Vector2.zero);
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            mesh.AddVert(center + direction * (radius - 3), fill, Vector2.zero);
            mesh.AddVert(center + direction * (radius - 3), ring, Vector2.zero);
            mesh.AddVert(center + direction * radius, ring, Vector2.zero);
            if (i == 0) continue;
            int index = 1 + i * 3;
            mesh.AddTriangle(0, index - 3, index);
            mesh.AddTriangle(index - 2, index - 1, index + 2);
            mesh.AddTriangle(index - 2, index + 2, index + 1);
        }
    }
}

public class TalentGraphViewport : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
{
    public RectTransform Content;
    private Vector2 _previousPointer;

    public void ResetView(Vector2 position)
    {
        Content.localScale = Vector3.one * 0.9f;
        Content.anchoredPosition = position;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out _previousPointer);
    }

    public void OnDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out var position);
        Content.anchoredPosition += position - _previousPointer; _previousPointer = position;
        ClampPosition();
    }

    public void OnScroll(PointerEventData eventData)
    {
        float oldScale = Content.localScale.x;
        float scale = Mathf.Clamp(oldScale * (1f + eventData.scrollDelta.y * 0.12f), 0.6f, 1.8f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, eventData.position, eventData.pressEventCamera, out var pointer);
        Content.anchoredPosition = pointer - (pointer - Content.anchoredPosition) * (scale / oldScale);
        Content.localScale = Vector3.one * scale;
        ClampPosition();
    }

    private void ClampPosition()
    {
        Vector2 limit = Content.sizeDelta * Content.localScale.x * 0.5f;
        Content.anchoredPosition = new Vector2(Mathf.Clamp(Content.anchoredPosition.x, -limit.x, limit.x),
            Mathf.Clamp(Content.anchoredPosition.y, -limit.y, limit.y));
    }
}
