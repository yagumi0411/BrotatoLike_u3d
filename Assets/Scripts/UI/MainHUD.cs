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

    private void Start()
    {
        _player = GameManager.Instance?.Player;
        _waveManager = GameManager.Instance?.WaveManager;

        if (_player == null || _waveManager == null)
        {
            Debug.LogWarning("MainHUD: 未能找到玩家或波次管理器");
            return;
        }

        // 绑定事件
        _player.StatsComponent.OnHPChanged += UpdateHP;
        _player.StatsComponent.OnXPChanged += UpdateXP;
        _waveManager.OnWaveChanged += UpdateWave;

        // 初始更新
        UpdateHP(_player.StatsComponent.CurrentHP, _player.StatsComponent.GetEffectiveMaxHP());
        UpdateXP(_player.StatsComponent.CurrentXP, _player.StatsComponent.GetXPToNextLevel(), _player.StatsComponent.CurrentLevel);
        UpdateWave(_waveManager.CurrentWave);
    }

    private void Update()
    {
        if (_waveManager == null) return;

        if (_waveManager.IsWaveActive)
        {
            WaveTimerText.text = $"left {Mathf.Ceil(_waveManager.WaveTimer)}s";
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
