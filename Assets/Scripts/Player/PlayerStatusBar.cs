using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家头顶血条（世界空间 UI）。结构在 PlayerStatusBar.prefab 中搭建（编辑器配置），
/// 本脚本只负责绑定血量事件与填充更新。
/// </summary>
public class PlayerStatusBar : MonoBehaviour
{
    [Tooltip("血量填充条（prefab 中配置，Filled 水平填充）")]
    public Image Fill;
    [Tooltip("经验填充条（prefab 中配置，位于血条下方）")]
    public Image XPFill;

    private PlayerStatsComponent _stats;

    private void Start()
    {
        // 血条实例化在玩家下，属性组件在父级玩家上
        _stats = GetComponentInParent<PlayerStatsComponent>();
        if (Fill == null || XPFill == null)
        {
            Debug.LogWarning("PlayerStatusBar: Fill/XPFill 未完整引用（请在 prefab 中拖入）");
            return;
        }

        if (_stats != null)
        {
            _stats.OnHPChanged += OnHPChanged;
            _stats.OnXPChanged += OnXPChanged;
            OnHPChanged(_stats.CurrentHP, _stats.GetEffectiveMaxHP());
            OnXPChanged(_stats.CurrentXP, _stats.GetXPToNextLevel(), _stats.CurrentLevel);
        }
    }

    private void OnDestroy()
    {
        if (_stats != null)
        {
            _stats.OnHPChanged -= OnHPChanged;
            _stats.OnXPChanged -= OnXPChanged;
        }
    }

    private void OnHPChanged(float hp, float maxHp)
    {
        if (Fill != null)
            Fill.fillAmount = Mathf.Clamp01(hp / Mathf.Max(maxHp, 0.001f));
    }

    private void OnXPChanged(float current, float toNext, int level)
    {
        if (XPFill != null)
            XPFill.fillAmount = Mathf.Clamp01(current / Mathf.Max(toNext, 0.001f));
    }
}
