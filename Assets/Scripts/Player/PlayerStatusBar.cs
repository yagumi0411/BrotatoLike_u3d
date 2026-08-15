using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家 2 头顶血条（世界空间 UI，运行时创建，零场景配置）。
/// 玩家 1 使用主 HUD，双人模式第二个玩家没有 HUD 面板，用头顶血条补足感知。
/// </summary>
public class PlayerStatusBar : MonoBehaviour
{
    private Image _fill;
    private PlayerStatsComponent _stats;

    private void Start()
    {
        _stats = GetComponent<PlayerStatsComponent>();
        Build();

        if (_stats != null)
        {
            _stats.OnHPChanged += OnHPChanged;
            OnHPChanged(_stats.CurrentHP, _stats.GetEffectiveMaxHP());
        }
    }

    private void OnDestroy()
    {
        if (_stats != null)
            _stats.OnHPChanged -= OnHPChanged;
    }

    private void Build()
    {
        var canvasGo = new GameObject("PlayerStatusCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = new Vector3(0f, 2.4f, 0f);
        canvasGo.transform.localScale = Vector3.one * 0.01f;

        // 背景
        Image bg = CreateBar("HP_BG", new Color(0f, 0f, 0f, 0.6f));
        bg.transform.SetParent(canvasGo.transform, false);
        bg.rectTransform.sizeDelta = new Vector2(2.4f, 0.3f);

        // 血量填充（Filled 水平填充，随血量变化）
        _fill = CreateBar("HP_Fill", new Color(0.2f, 0.9f, 0.3f));
        _fill.transform.SetParent(bg.transform, false);
        _fill.rectTransform.sizeDelta = new Vector2(2.4f, 0.3f);
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = 0;
    }

    private static Image CreateBar(string name, Color color)
    {
        var go = new GameObject(name);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private void OnHPChanged(float hp, float maxHp)
    {
        if (_fill != null)
            _fill.fillAmount = Mathf.Clamp01(hp / Mathf.Max(maxHp, 0.001f));
    }
}
