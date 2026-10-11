using UnityEngine;
using System;

public class PlayerStatsComponent : MonoBehaviour
{
    [Header("基础属性")]
    public float BaseMaxHP = 14f;
    public float BaseMoveSpeed = 5f;
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

    private TalentBonuses _talents = new TalentBonuses();

    public void SetTalentBonuses(TalentBonuses bonuses) => _talents = bonuses ?? new TalentBonuses();
    public float GetEffectiveWeaponRangeMultiplier() => Mathf.Max(0.1f, 1f + _talents.WeaponRange);

    // 事件
    public event Action<float, float> OnHPChanged;
    public event Action<float, float, int> OnXPChanged;
    public event Action<float> OnDamageTaken;
    public event Action<float> OnXPGained;
    public event Action OnLevelUp;
    public event Action OnDeath;

    private void Start()
    {
        CurrentHP = GetEffectiveMaxHP();
        OnHPChanged?.Invoke(CurrentHP, GetEffectiveMaxHP());
    }

    public float GetEffectiveMaxHP() =>
        (BaseMaxHP + FlatHPBonus + _talents.MaxHP) * (1f + PercentHPBonus);

    public float GetEffectiveMoveSpeed() =>
        BaseMoveSpeed + FlatMoveSpeedBonus;

    public float GetEffectivePickupRadius() =>
        BasePickupRadius + FlatPickupRangeBonus + _talents.PickupRadius;

    public float GetEffectiveDamageMultiplier() =>
        1f + GlobalDamageMultiplier + _talents.Damage;

    public float GetEffectiveAttackSpeedMultiplier() =>
        1f + GlobalAttackSpeedMultiplier;

    public float GetEffectiveExpMultiplier() =>
        1f + ExpGainMultiplier;

    public float GetEffectiveCritChance() =>
        Mathf.Clamp(CritChanceBonus + _talents.CritChance, 0f, 0.6f);

    public float GetEffectiveCritDamage() =>
        1.5f + CritDamageMultiplierBonus;

    public float GetXPToNextLevel()
    {
        int growth = Mathf.Max(0, CurrentLevel - 1);
        return 24f + 14f * growth + 3f * growth * growth;
    }

    public void AddXP(float amount)
    {
        float multiplier = GetEffectiveExpMultiplier();
        amount *= multiplier;
        CurrentXP += amount;
        OnXPGained?.Invoke(amount);

        while (CurrentXP >= GetXPToNextLevel())
        {
            CurrentXP -= GetXPToNextLevel();
            CurrentLevel++;
            OnLevelUp?.Invoke();
        }

        OnXPChanged?.Invoke(CurrentXP, GetXPToNextLevel(), CurrentLevel);
    }

    public void TakeDamage(float damage)
    {
        if (CurrentHP <= 0f || (GameManager.Instance != null &&
            GameManager.Instance.CurrentState != GameManager.EGameState.Playing)) return;
        damage = Mathf.Max(0f, damage) * (1f - Mathf.Clamp(_talents.DamageReduction, 0f, 0.75f));
        CurrentHP = Mathf.Max(0f, CurrentHP - damage);
        OnDamageTaken?.Invoke(damage);
        OnHPChanged?.Invoke(CurrentHP, GetEffectiveMaxHP());

        if (CurrentHP <= 0)
        {
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (CurrentHP <= 0f || amount <= 0f) return;
        CurrentHP = Mathf.Min(GetEffectiveMaxHP(), CurrentHP + amount);
        OnHPChanged?.Invoke(CurrentHP, GetEffectiveMaxHP());
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
                CritChanceBonus += option.Value / 100f;
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
