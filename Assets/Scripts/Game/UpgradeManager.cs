using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class UpgradeManager : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("所有可用的升级选项池，包含属性升级和武器升级")]
    public List<UpgradeOption> UpgradePool = new List<UpgradeOption>();

    [Header("UI")]
    public LevelUpUI LevelUpUI;

    [Header("选项数量")]
    [Range(1, 4)]
    public int OptionCount = 3;

    private PlayerController _player;

    private void Start()
    {
        _player = GameManager.Instance?.Player;
        LevelUpUI = LevelUpUI ?? FindAnyObjectByType<LevelUpUI>();

        if (_player != null)
        {
            _player.StatsComponent.OnLevelUp += OnPlayerLevelUp;
        }
        else
        {
            Debug.LogWarning("UpgradeManager: 未能找到玩家控制器");
        }
    }

    private void OnDestroy()
    {
        if (_player != null)
        {
            _player.StatsComponent.OnLevelUp -= OnPlayerLevelUp;
        }
    }

    private void OnPlayerLevelUp(PlayerStatsComponent stats)
    {
        var options = GenerateOptions(OptionCount);

        if (options.Count == 0)
        {
            Debug.LogWarning("UpgradeManager: 没有可用的升级选项");
            return;
        }

        LevelUpUI?.ShowOptions(_player, options);
    }

    public List<UpgradeOption> GenerateOptions(int count)
    {
        if (_player == null)
        {
            _player = GameManager.Instance?.Player;
        }

        var candidates = GetValidCandidates();

        if (candidates.Count == 0)
        {
            return CreateFallbackOptions(count);
        }

        // 随机打乱并取前 count 个
        return candidates
            .OrderBy(_ => Random.value)
            .Take(count)
            .ToList();
    }

    private List<UpgradeOption> GetValidCandidates()
    {
        var result = new List<UpgradeOption>();
        int playerLevel = _player?.StatsComponent.CurrentLevel ?? 1;

        // 记录已装备的武器，避免重复出现
        var equippedWeapons = new HashSet<WeaponDefinition>();
        if (_player != null)
        {
            foreach (var weapon in _player.EquippedWeapons)
            {
                if (weapon != null && weapon.WeaponDef != null)
                {
                    equippedWeapons.Add(weapon.WeaponDef);
                }
            }
        }

        foreach (var option in UpgradePool)
        {
            if (option == null) continue;
            if (option.MinLevelToAppear > playerLevel) continue;

            // 武器选项：槽位已满或已拥有该武器则跳过
            if (option.Type == EUpgradeType.Weapon)
            {
                if (_player != null && _player.IsWeaponSlotFull()) continue;
                if (option.WeaponDef != null && equippedWeapons.Contains(option.WeaponDef)) continue;
            }

            result.Add(option);
        }

        return result;
    }

    /// <summary>
    /// 当没有配置 UpgradePool 时，生成几个默认的属性升级选项作为后备。
    /// </summary>
    private List<UpgradeOption> CreateFallbackOptions(int count)
    {
        var options = new List<UpgradeOption>();

        var fallbacks = new (EUpgradeType type, string name, string desc, float value)[]
        {
            (EUpgradeType.MaxHP_Add, "生命强化", "最大生命值 +2", 2f),
            (EUpgradeType.Damage_All_Mul, "攻击强化", "全局伤害 +10%", 10f),
            (EUpgradeType.AttackSpeed_All_Mul, "攻速强化", "攻击速度 +10%", 10f),
            (EUpgradeType.MoveSpeed_Add, "移速强化", "移动速度 +0.5", 0.5f),
            (EUpgradeType.PickupRange_Add, "拾取强化", "拾取范围 +0.5", 0.5f),
        };

        for (int i = 0; i < count && i < fallbacks.Length; i++)
        {
            var fallback = fallbacks[i];
            var option = ScriptableObject.CreateInstance<UpgradeOption>();
            option.Type = fallback.type;
            option.Name = fallback.name;
            option.Description = fallback.desc;
            option.Value = fallback.value;
            option.MinLevelToAppear = 1;
            options.Add(option);
        }

        return options;
    }
}
