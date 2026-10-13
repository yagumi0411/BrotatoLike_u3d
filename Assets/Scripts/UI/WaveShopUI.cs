using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class WaveShopUI : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TextMeshProUGUI _status;
    [SerializeField] private TextMeshProUGUI _purchaseText;
    [SerializeField] private TextMeshProUGUI _purchaseLabel;
    [SerializeField] private Button _purchase;
    [SerializeField] private Image _healingIcon;
    [SerializeField] private GameObject _tooltip;
    [SerializeField] private TextMeshProUGUI _tooltipText;
    [SerializeField] private TextMeshProUGUI _hint;

    private bool _pointerOverHealing;
    private bool _healingSelected;
    private bool _initialized;
    private GameManager _manager;

    public void SetVisible(bool visible)
    {
        if (!_initialized) return;

        _pointerOverHealing = false;
        _healingSelected = false;
        _tooltip.SetActive(false);
        _root.SetActive(visible);

        if (visible)
            Refresh();
    }



    private void OnGoldChanged(int gold) => Refresh();

    private void Refresh()
    {
        if (!_initialized || _manager.Player == null ||
            _manager.WaveManager == null) return;

        var stats = _manager.Player.StatsComponent;
        float maxHP = stats.GetEffectiveMaxHP();
        int price = Mathf.Max(1, _manager.HealingCost);
        bool full = stats.CurrentHP >= maxHP;

        _status.text =
            $"第 {_manager.WaveManager.CurrentWave} 波结束    " +
            $"金币：{_manager.Gold}    " +
            $"生命：{Mathf.Ceil(stats.CurrentHP)} / {Mathf.Ceil(maxHP)}";

        _purchase.interactable =
            !_manager.HealingPurchased &&
            stats.CurrentHP > 0f &&
            !full &&
            _manager.Gold >= price;

        string availability =
            _manager.HealingPurchased ? "本波已售出" :
            full ? "生命已满" :
            _manager.Gold < price ? "金币不足" : "购买";

        _purchaseText.text = $"{price} 金币";
        _purchaseLabel.text = availability;

        _healingIcon.color = _manager.HealingPurchased
            ? new Color(1f, 1f, 1f, 0.3f)
            : Color.white;

        _hint.text = _purchase.interactable
            ? "点击购买按钮补充生命"
            : $"治疗药水：{availability}";

        _tooltipText.text =
            $"治疗药水\n" +
            $"恢复 {Mathf.Max(1f, _manager.HealingAmount):0.#} 点生命\n" +
            $"每波限购一次\n" +
            $"{availability}";
    }

    private void UpdateTooltip() => _tooltip.SetActive(_pointerOverHealing || _healingSelected);

    private static void AddTooltipEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private void OnDestroy()
    {
        if (_manager != null) _manager.OnGoldChanged -= OnGoldChanged;
    }


    private void Awake()
    {
        _manager = GetComponent<GameManager>();

        if (_manager == null || _root == null || _status == null ||
            _purchaseText == null || _purchaseLabel == null ||
            _purchase == null || _healingIcon == null ||
            _tooltip == null || _tooltipText == null || _hint == null)
        {
            Debug.LogError("WaveShopUI：请在 Inspector 中填写全部界面引用。", this);
            return;
        }

        _purchase.onClick.AddListener(PurchaseHealing);

        var trigger = _purchase.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = _purchase.gameObject.AddComponent<EventTrigger>();

        AddTooltipEvent(trigger, EventTriggerType.PointerEnter,
            () => { _pointerOverHealing = true; UpdateTooltip(); });

        AddTooltipEvent(trigger, EventTriggerType.PointerExit,
            () => { _pointerOverHealing = false; UpdateTooltip(); });

        AddTooltipEvent(trigger, EventTriggerType.Select,
            () => { _healingSelected = true; UpdateTooltip(); });

        AddTooltipEvent(trigger, EventTriggerType.Deselect,
            () => { _healingSelected = false; UpdateTooltip(); });

        _manager.OnGoldChanged += OnGoldChanged;
        _initialized = true;

        _tooltip.SetActive(false);
        _root.SetActive(false);
    }

    private void PurchaseHealing()
    {
        _manager.BuyHealing();
        Refresh();
    }
}
