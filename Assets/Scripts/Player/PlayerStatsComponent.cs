using UnityEngine;
using System;

public class PlayerStatsComponent : MonoBehaviour
{
    [Header("基础属性")]
    public float BaseMaxHP = 10f;
    public float BaseMoveSpeed = 6f;
    public float BasePickupRadius = 3f;

    [Header("升级累加值")]
    public float FlatHPBonus;
    public float PercentHPBonus;
    public float GlobalDamageMultiplier;
    public float GlobalAttackSpeedMultiplier;
    public float FlatMoveSpeedBonus;
    public float ExpGainMultiplier;
    public float FlatPickupRangeBonus;
    public float CritChanceBonus;
    public float CritDamageMultiplierBonus;

    [Header("运行时状态")]
    public float CurrentHP;
    public float CurrentXP;
    public int CurrentLevel = 1;

    [Header("升级无敌（双人升级不暂停玩法）")]
    public bool IsInvincible { get; private set; }
    private float _invincibleRemaining;
    public const float LevelUpInvincibleDuration = 3f;

    // 事件
    public event Action<float, float> OnHPChanged;
    public event Action<float, float, int> OnXPChanged;
    public event Action<PlayerStatsComponent> OnLevelUp;
    public event Action OnDeath;

    private void Start()
    {
        CurrentHP = GetEffectiveMaxHP();
        OnHPChanged?.Invoke(CurrentHP, GetEffectiveMaxHP());
    }

    private void Update()
    {
        // 无敌计时（升级触发 3 秒，到期自动解除）
        if (_invincibleRemaining > 0f)
        {
            _invincibleRemaining -= Time.deltaTime;
            if (_invincibleRemaining <= 0f)
            {
                _invincibleRemaining = 0f;
                IsInvincible = false;
            }
        }
    }

    public float GetEffectiveMaxHP() =>
        (BaseMaxHP + FlatHPBonus) * (1f + PercentHPBonus);

    public float GetEffectiveMoveSpeed() =>
        BaseMoveSpeed + FlatMoveSpeedBonus;

    public float GetEffectivePickupRadius() =>
        BasePickupRadius + FlatPickupRangeBonus;

    public float GetEffectiveDamageMultiplier() =>
        1f + GlobalDamageMultiplier;

    public float GetEffectiveAttackSpeedMultiplier() =>
        1f + GlobalAttackSpeedMultiplier;

    public float GetEffectiveExpMultiplier() =>
        1f + ExpGainMultiplier;

    public float GetEffectiveCritChance() =>
        Mathf.Clamp(CritChanceBonus, 0f, 0.6f);

    public float GetEffectiveCritDamage() =>
        1.5f + CritDamageMultiplierBonus;

    public float GetXPToNextLevel() => 8 * (CurrentLevel + 1);

    public void AddXP(float amount)
    {
        float multiplier = GetEffectiveExpMultiplier();
        amount *= multiplier;
        CurrentXP += amount;

        while (CurrentXP >= GetXPToNextLevel())
        {
            CurrentXP -= GetXPToNextLevel();
            CurrentLevel++;
            OnLevelUp?.Invoke(this);
        }

        OnXPChanged?.Invoke(CurrentXP, GetXPToNextLevel(), CurrentLevel);
    }

    /// <summary>进入升级状态：短暂无敌（供双人"升级不暂停"玩法）</summary>
    public void BeginLevelUpState()
    {
        IsInvincible = true;
        _invincibleRemaining = LevelUpInvincibleDuration;
    }

    public void TakeDamage(float damage)
    {
        if (IsInvincible) return;

        CurrentHP -= damage;
        OnHPChanged?.Invoke(CurrentHP, GetEffectiveMaxHP());

        if (CurrentHP <= 0)
        {
            OnDeath?.Invoke();
        }
    }

    public void ApplyUpgrade(UpgradeOption option)
    {
        float oldMaxHP = GetEffectiveMaxHP();

        switch (option.Type)
        {
            case EUpgradeType.MaxHP_Add:
                FlatHPBonus += option.Value;
                break;
            case EUpgradeType.MaxHP_Mul:
                PercentHPBonus += option.Value / 100f;
                break;
            case EUpgradeType.Damage_All_Mul:
                GlobalDamageMultiplier += option.Value / 100f;
                break;
            case EUpgradeType.AttackSpeed_All_Mul:
                GlobalAttackSpeedMultiplier += option.Value / 100f;
                break;
            case EUpgradeType.MoveSpeed_Add:
                FlatMoveSpeedBonus += option.Value;
                break;
            case EUpgradeType.ExpRate_Mul:
                ExpGainMultiplier += option.Value / 100f;
                break;
            case EUpgradeType.PickupRange_Add:
                FlatPickupRangeBonus += option.Value;
                break;
            case EUpgradeType.CritChance_Add:
                CritChanceBonus += option.Value;
                break;
            case EUpgradeType.CritDamage_Mul:
                CritDamageMultiplierBonus += option.Value / 100f;
                break;
        }

        float newMaxHP = GetEffectiveMaxHP();
        float delta = newMaxHP - oldMaxHP;

        // 生命值升级时，当前血量同步增加
        if (delta > 0f)
        {
            CurrentHP += delta;
        }

        // 确保当前血量不超过上限
        if (CurrentHP > newMaxHP)
        {
            CurrentHP = newMaxHP;
        }

        OnHPChanged?.Invoke(CurrentHP, newMaxHP);
    }
}