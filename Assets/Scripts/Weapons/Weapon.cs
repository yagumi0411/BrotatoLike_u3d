using UnityEngine;

public abstract class Weapon : MonoBehaviour
{
    [Header("配置")]
    public WeaponDefinition WeaponDef;

    [HideInInspector]
    public PlayerController OwnerPlayer;

    protected float AttackCooldown;

    public virtual void Initialize(PlayerController owner, WeaponDefinition def)
    {
        OwnerPlayer = owner;
        WeaponDef = def;
    }

    protected virtual void Update()
    {
        // 玩家升级中：停火（双人"升级不暂停"，武器暂停攻击）
        if (OwnerPlayer != null && OwnerPlayer.IsChoosingUpgrade) return;

        AttackCooldown -= Time.deltaTime;
        if (CanAttack())
        {
            var target = FindTarget();
            if (target != null)
            {
                Fire(target);
                AttackCooldown = GetEffectiveAttackInterval();
            }
        }
    }

    protected virtual bool CanAttack() => AttackCooldown <= 0;

    private float _scanTimer;
    private const float SCAN_INTERVAL = 0.2f;
    private Enemy _cachedTarget;   // 缓存目标：有效期内免扫描，失效才重新索敌

    protected virtual Enemy FindTarget()
    {
        // 缓存目标仍存活且在射程内 → 直接复用，零扫描
        if (_cachedTarget != null && IsTargetValid(_cachedTarget))
            return _cachedTarget;

        // 缓存失效才重新扫描：限频 + 注册表手写循环，零 GC 分配
        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f) return null;
        _scanTimer = SCAN_INTERVAL;

        float rangeSqr = GetEffectiveRange() * GetEffectiveRange();
        Vector3 origin = transform.position;
        var enemies = EnemyRegistry.All;

        Enemy best = null;
        float bestDistSqr = float.MaxValue;
        int seen = 0;

        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy == null || enemy.CurrentHP <= 0f) continue;

            float distSqr = (enemy.transform.position - origin).sqrMagnitude;
            if (distSqr > rangeSqr) continue;

            if (WeaponDef.TargetMode == ETargetMode.Nearest)
            {
                if (distSqr < bestDistSqr)
                {
                    bestDistSqr = distSqr;
                    best = enemy;
                }
            }
            else
            {
                // Random 模式：蓄水池采样，单遍均匀随机，避免收集列表分配
                seen++;
                if (Random.Range(0, seen) == 0)
                    best = enemy;
            }
        }

        _cachedTarget = best;
        return best;
    }

    /// <summary>缓存目标有效性：仍存活且在射程内</summary>
    private bool IsTargetValid(Enemy target)
    {
        if (target.CurrentHP <= 0f) return false;
        Vector3 offset = target.transform.position - transform.position;
        return offset.sqrMagnitude <= GetEffectiveRange() * GetEffectiveRange();
    }

    protected abstract void Fire(Enemy target);

    protected float GetEffectiveAttackInterval() =>
        WeaponDef.AttackInterval / OwnerPlayer.StatsComponent.GetEffectiveAttackSpeedMultiplier();

    protected float GetEffectiveRange() => WeaponDef.Range;

    protected float CalculateDamage() =>
        WeaponDef.BaseDamage * OwnerPlayer.StatsComponent.GetEffectiveDamageMultiplier();

    protected bool RollCrit() =>
        Random.value < OwnerPlayer.StatsComponent.GetEffectiveCritChance();
}