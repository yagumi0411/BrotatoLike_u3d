using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class WaveShopUI : MonoBehaviour
{
    private GameObject _root;
    private TextMeshProUGUI _status;
    private TextMeshProUGUI _purchaseText;
    private Button _purchase;
    private Image _healingIcon;
    private GameObject _tooltip;
    private TextMeshProUGUI _tooltipText;
    private bool _pointerOverHealing;
    private bool _healingSelected;
    private TMP_FontAsset _font;
    private GameManager _manager;

    public void SetVisible(bool visible)
    {
        if (visible && _root == null) Build();
        if (_root == null) return;
        _pointerOverHealing = false;
        _healingSelected = false;
        _tooltip.SetActive(false);
        _root.SetActive(visible);
        if (visible) Refresh();
    }

    private void Build()
    {
        _manager = GameManager.Instance;
        var names = _manager.LevelUpUI?.OptionNames;
        _font = names != null && names.Length > 0 && names[0] != null ? names[0].font : TMP_Settings.defaultFontAsset;
        _root = new GameObject("WaveShopCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _root.transform.SetParent(transform, false);
        var canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000;
        var scaler = _root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(_root.transform, false);
        var backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero; backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero; backdropRect.offsetMax = Vector2.zero;
        backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        var panel = Rect("ShopPanel", backdrop.transform, Vector2.zero, new Vector2(800, 450));
        panel.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.1f, 0.16f);
        Label(panel, "波次间商店", new Vector2(0, 175), new Vector2(700, 50), 34);
        _status = Label(panel, "", new Vector2(0, 115), new Vector2(700, 65), 23);

        var shelf = Rect("Shelf", panel, new Vector2(0, -50), new Vector2(700, 24));
        shelf.gameObject.AddComponent<Image>().color = new Color(0.38f, 0.27f, 0.19f);

        _purchase = Button(panel, "PurchaseHealing", new Vector2(-235, 20), new Vector2(150, 115), "", () =>
        {
            _manager.BuyHealing();
            Refresh();
        });
        _purchase.GetComponent<Image>().color = Color.white;
        var colors = _purchase.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.02f);
        colors.highlightedColor = new Color(1f, 0.85f, 0.55f, 0.3f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.02f);
        colors.pressedColor = new Color(1f, 0.85f, 0.55f, 0.5f);
        _purchase.colors = colors;
        var icon = Rect("HealingIcon", _purchase.transform, new Vector2(0, 14), new Vector2(64, 64));
        _healingIcon = icon.gameObject.AddComponent<Image>();
        _healingIcon.sprite = DungeonArt.GetSprite("flask_red");
        _healingIcon.preserveAspect = true;
        _healingIcon.raycastTarget = false;
        Label(_purchase.transform, "治疗药水", new Vector2(0, -37), new Vector2(150, 30), 22);
        _purchaseText = PriceTag(panel, -235);
        for (int slot = 0; slot < 2; slot++)
        {
            float x = slot * 235;
            Label(panel, "空", new Vector2(x, 15), new Vector2(150, 60), 30).color = new Color(0.45f, 0.4f, 0.5f);
            PriceTag(panel, x).text = "预留栏位";
        }
        Label(panel, "点击商品购买", new Vector2(-235, -175), new Vector2(210, 45), 20);
        Button(panel, "ContinueWave", new Vector2(205, -175), new Vector2(260, 50), "继续下一波", () => _manager.ContinueFromShop());

        var tooltipRect = Rect("HealingTooltip", panel, new Vector2(-25, 5), new Vector2(255, 140));
        _tooltip = tooltipRect.gameObject;
        var tooltipImage = _tooltip.AddComponent<Image>();
        tooltipImage.color = new Color(0.07f, 0.06f, 0.1f);
        tooltipImage.raycastTarget = false;
        _tooltipText = Label(tooltipRect, "", Vector2.zero, new Vector2(235, 125), 21);
        _tooltip.SetActive(false);
        var trigger = _purchase.gameObject.AddComponent<EventTrigger>();
        AddTooltipEvent(trigger, EventTriggerType.PointerEnter, () => { _pointerOverHealing = true; UpdateTooltip(); });
        AddTooltipEvent(trigger, EventTriggerType.PointerExit, () => { _pointerOverHealing = false; UpdateTooltip(); });
        AddTooltipEvent(trigger, EventTriggerType.Select, () => { _healingSelected = !_pointerOverHealing; UpdateTooltip(); });
        AddTooltipEvent(trigger, EventTriggerType.Deselect, () => { _healingSelected = false; UpdateTooltip(); });
        _manager.OnGoldChanged += OnGoldChanged;
    }

    private void OnGoldChanged(int gold) => Refresh();

    private void Refresh()
    {
        if (_status == null || _manager.Player == null) return;
        var stats = _manager.Player.StatsComponent;
        _status.text = $"第 {_manager.WaveManager.CurrentWave} 波结束    金币：{_manager.Gold}\n生命：{stats.CurrentHP:0.#} / {stats.GetEffectiveMaxHP():0.#}";
        bool full = stats.CurrentHP >= stats.GetEffectiveMaxHP();
        int price = Mathf.Max(1, _manager.HealingCost);
        _purchase.interactable = !_manager.HealingPurchased && !full && _manager.Gold >= price;
        string availability = _manager.HealingPurchased ? "本波已售出" : full ? "生命已满" :
            _manager.Gold < price ? "金币不足" : "点击购买";
        _purchaseText.text = $"{price} 金币\n{availability}";
        _healingIcon.color = _manager.HealingPurchased ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
        _tooltipText.text = $"治疗药水\n恢复 {Mathf.Max(1f, _manager.HealingAmount):0.#} 点生命\n每波限购一次\n{availability}";
    }

    private void UpdateTooltip() => _tooltip.SetActive(_pointerOverHealing || _healingSelected);

    private static void AddTooltipEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private TextMeshProUGUI PriceTag(Transform parent, float x)
    {
        var tag = Rect("PriceTag", parent, new Vector2(x, -100), new Vector2(165, 60));
        tag.gameObject.AddComponent<Image>().color = new Color(0.72f, 0.61f, 0.43f);
        var label = Label(tag, "", Vector2.zero, new Vector2(155, 55), 20);
        label.color = new Color(0.18f, 0.12f, 0.09f);
        return label;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private TextMeshProUGUI Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var label = Rect("Label", parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.fontSize = fontSize;
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.95f, 0.9f, 0.75f);
        label.raycastTarget = false;
        return label;
    }

    private Button Button(Transform parent, string name, Vector2 position, Vector2 size, string text, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(name, parent, position, size);
        rect.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.24f, 0.38f);
        var button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        Label(rect, text, Vector2.zero, size, 23);
        return button;
    }

    private void OnDestroy()
    {
        if (_manager != null) _manager.OnGoldChanged -= OnGoldChanged;
    }
}
