using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainHUD : MonoBehaviour
{
    [Header("HP")]
    public Slider HPBar;
    public TextMeshProUGUI HPText;

    [Header("XP")]
    public Slider XPBar;
    public TextMeshProUGUI LevelText;

    [Header("波次")]
    public TextMeshProUGUI WaveText;
    public TextMeshProUGUI WaveTimerText;

    private PlayerController _player;
    private WaveManager _waveManager;
    private TextMeshProUGUI _goldText;
    private int _lastGold = -1;

    private void Start()
    {
        Rebind();
        var goldObject = new GameObject("GoldText", typeof(RectTransform));
        goldObject.transform.SetParent(transform, false);
        var rect = goldObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-25f, -25f);
        rect.sizeDelta = new Vector2(220f, 45f);
        _goldText = goldObject.AddComponent<TextMeshProUGUI>();
        _goldText.font = HPText != null ? HPText.font : TMP_Settings.defaultFontAsset;
        _goldText.fontSize = 28;
        _goldText.alignment = TextAlignmentOptions.Right;
        _goldText.color = new Color(1f, 0.8f, 0.25f);
        _goldText.raycastTarget = false;
    }

    private void Update()
    {
        int gold = GameManager.Instance?.Gold ?? 0;
        if (_goldText != null && gold != _lastGold)
        {
            _goldText.text = $"金币：{gold}";
            _lastGold = gold;
        }
        if (_waveManager == null) return;

        if (_waveManager.IsWaveActive)
        {
            WaveTimerText.text = $"剩余 {Mathf.Max(0, Mathf.Ceil(_waveManager.WaveTimer))} 秒";
        }
        else
        {
            WaveTimerText.text = "休息中";
        }
    }

    /// <summary>
    /// 重新绑定事件（重新开始游戏后调用）
    /// </summary>
    public void Rebind()
    {
        // 解除旧绑定
        if (_player != null)
        {
            _player.StatsComponent.OnHPChanged -= UpdateHP;
            _player.StatsComponent.OnXPChanged -= UpdateXP;
        }
        if (_waveManager != null)
        {
            _waveManager.OnWaveChanged -= UpdateWave;
        }

        _player = GameManager.Instance?.Player;
        _waveManager = GameManager.Instance?.WaveManager;

        if (_player == null || _waveManager == null)
        {
            Debug.LogWarning("MainHUD.Rebind: 玩家或波次管理器不可用");
            return;
        }

        // 重新绑定事件
        _player.StatsComponent.OnHPChanged += UpdateHP;
        _player.StatsComponent.OnXPChanged += UpdateXP;
        _waveManager.OnWaveChanged += UpdateWave;

        // 初始更新
        UpdateHP(_player.StatsComponent.CurrentHP, _player.StatsComponent.GetEffectiveMaxHP());
        UpdateXP(_player.StatsComponent.CurrentXP, _player.StatsComponent.GetXPToNextLevel(), _player.StatsComponent.CurrentLevel);
        UpdateWave(_waveManager.CurrentWave);
    }

    private void OnDestroy()
    {
        if (_player != null)
        {
            _player.StatsComponent.OnHPChanged -= UpdateHP;
            _player.StatsComponent.OnXPChanged -= UpdateXP;
        }

        if (_waveManager != null)
        {
            _waveManager.OnWaveChanged -= UpdateWave;
        }
    }

    private void UpdateHP(float current, float max)
    {
        if (HPBar != null)
        {
            HPBar.maxValue = max;
            HPBar.value = current;
        }

        if (HPText != null)
        {
            HPText.text = $"{Mathf.Ceil(current)} / {Mathf.Ceil(max)}";
        }
    }

    private void UpdateXP(float current, float toNext, int level)
    {
        if (XPBar != null)
        {
            XPBar.maxValue = toNext;
            XPBar.value = current;
        }

        if (LevelText != null)
        {
            LevelText.text = $"Lv.{level}";
        }
    }

    private void UpdateWave(int wave)
    {
        if (WaveText != null)
        {
            WaveText.text = $"wave: {wave}";
        }
    }
}
