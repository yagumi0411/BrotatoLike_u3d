using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LevelUpUI : MonoBehaviour
{
    [Header("面板")]
    public GameObject Panel;

    [Header("选项按钮")]
    public Button[] OptionButtons;
    public TextMeshProUGUI[] OptionNames;
    public TextMeshProUGUI[] OptionDescriptions;

    private List<UpgradeOption> _currentOptions;
    private PlayerController _upgradingPlayer;

    private void Awake()
    {
        if (Panel != null)
        {
            Panel.SetActive(false);
        }

        for (int i = 0; i < OptionButtons.Length; i++)
        {
            int index = i;
            OptionButtons[i].onClick.AddListener(() => OnOptionSelected(index));
        }
    }

    public void ShowOptions(PlayerController player, List<UpgradeOption> options)
    {
        if (player == null || options == null || options.Count == 0)
        {
            Debug.LogWarning("LevelUpUI: 没有可显示的升级选项");
            return;
        }

        _upgradingPlayer = player;
        _currentOptions = options;

        if (Panel != null)
        {
            Panel.SetActive(true);
        }

        // 双人"升级不暂停"：时间缩放由 GameManager 管理，游戏世界继续运行

        int count = Mathf.Min(OptionButtons.Length, options.Count);
        for (int i = 0; i < OptionButtons.Length; i++)
        {
            if (i < count)
            {
                OptionButtons[i].gameObject.SetActive(true);
                OptionNames[i].text = options[i].Name;
                OptionDescriptions[i].text = GetDescription(options[i]);
            }
            else
            {
                OptionButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private string GetDescription(UpgradeOption opt)
    {
        if (opt == null) return string.Empty;

        if (opt.Type == EUpgradeType.Weapon && opt.WeaponDef != null)
        {
            return $"{opt.WeaponDef.Description}\n伤害: {opt.WeaponDef.BaseDamage}\n攻速: {opt.WeaponDef.AttackInterval}s";
        }

        return opt.Description;
    }

    private void OnOptionSelected(int index)
    {
        if (_currentOptions == null || index < 0 || index >= _currentOptions.Count)
        {
            return;
        }

        var option = _currentOptions[index];
        _upgradingPlayer?.ApplyUpgrade(option);
        EndLevelUp();
    }

    /// <summary>
    /// 结束升级：恢复玩家操作并隐藏面板（GameManager 面板复用时也会调用）
    /// </summary>
    public void EndLevelUp()
    {
        if (_upgradingPlayer != null)
        {
            _upgradingPlayer.IsChoosingUpgrade = false;
            _upgradingPlayer = null;
        }

        if (Panel != null)
        {
            Panel.SetActive(false);
        }
    }
}
