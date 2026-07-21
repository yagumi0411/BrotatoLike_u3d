using UnityEngine;
using System.Linq;

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

    protected virtual Enemy FindTarget()
    {
        // 限频扫描，避免每帧 FindObjectsByType 产生 GC 卡顿
        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f) return null;
        _scanTimer = SCAN_INTERVAL;

        //var enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        var inRange = enemies.Where(e =>
            Vector3.Distance(transform.position, e.transform.position) <= GetEffectiveRange()
        ).ToList();

        if (inRange.Count == 0) return null;

        if (WeaponDef.TargetMode == ETargetMode.Nearest)
        {
            return inRange.OrderBy(e =>
                Vector3.Distance(transform.position, e.transform.position)
            ).First();
        }
        else
        {
            return inRange[Random.Range(0, inRange.Count)];
        }
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