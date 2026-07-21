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

    public void ShowOptions(List<UpgradeOption> options)
    {
        if (options == null || options.Count == 0)
        {
            Debug.LogWarning("LevelUpUI: 没有可显示的升级选项");
            return;
        }

        _currentOptions = options;

        if (Panel != null)
        {
            Panel.SetActive(true);
        }

        // Time.timeScale 由 GameManager 管理

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
        GameManager.Instance?.Player?.ApplyUpgrade(option);

        if (Panel != null)
        {
            Panel.SetActive(false);
        }

        // 通知 GameManager 升级已选择，恢复游戏
        GameManager.Instance?.OnUpgradeSelected();
    }
}
