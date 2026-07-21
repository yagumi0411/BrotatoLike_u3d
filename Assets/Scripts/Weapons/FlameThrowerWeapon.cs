using System.Threading;
using UnityEngine;
using System.Collections.Generic;

public class FlameThrowerWeapon : Weapon
{
    [Header("火焰喷射")]
    public int ParticlesPerBurst = 6;
    public float ParticleSpeed = 12f;
    public float ParticleLifetime = 0.6f;
    public float SpreadAngle = 5f;

    // 扇形伤害检测
    private float _damageTickTimer;
    private Dictionary<Enemy, float> _lastDamageTimeMap = new Dictionary<Enemy, float>();
    private Vector3 _coneDirection = Vector3.forward; // 由 Fire() 缓存的目标方向

    protected override void Update()
    {
        // 基类 Update：攻击冷却 → FindTarget → Fire() 发射视觉粒子
        base.Update();

        if (OwnerPlayer == null || WeaponDef == null) return;

        // 扇形伤害 tick（独立于发射冷却）
        _damageTickTimer -= Time.deltaTime;
        if (_damageTickTimer > 0f) return;
        _damageTickTimer = WeaponDef.AttackInterval;

        float damage = CalculateDamage();
        bool isCrit = RollCrit();
        ApplyConeDamage(damage, isCrit);
    }

    private void ApplyConeDamage(float damage, bool isCrit)
    {
        float range = GetEffectiveRange();
        float halfAngle = WeaponDef.ConeHalfAngle;

        // 用 OverlapSphere 获取半径内所有碰撞体
        Collider[] hits = Physics.OverlapSphere(transform.position, range);
        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent<Enemy>(out var enemy)) continue;

            // 扇形角度过滤
            Vector3 dirToEnemy = enemy.transform.position - transform.position;
            float dist = dirToEnemy.magnitude;
            if (dist > range) continue;

            float angle = Vector3.Angle(_coneDirection, dirToEnemy);
            if (angle > halfAngle) continue;

            // 单个敌人冷却检测
            if (_lastDamageTimeMap.TryGetValue(enemy, out float lastTime))
            {
                if (Time.time - lastTime < WeaponDef.AttackInterval)
                    continue;
            }

            enemy.ReceiveDamage(damage, isCrit);
            _lastDamageTimeMap[enemy] = Time.time;
        }

        // 清理已销毁的敌人引用
        List<Enemy> toRemove = null;
        foreach (var kvp in _lastDamageTimeMap)
        {
            if (kvp.Key == null)
            {
                toRemove ??= new List<Enemy>();
                toRemove.Add(kvp.Key);
            }
        }
        if (toRemove != null)
        {
            foreach (var key in toRemove)
                _lastDamageTimeMap.Remove(key);
        }
    }

    protected override void Fire(Enemy target)
    {
        // 缓存扇形检测方向（与粒子发射方向对齐）
        if (target != null)
        {
            Vector3 dir = (target.transform.position - transform.position).normalized;
            dir.y = 0f;
            _coneDirection = dir.normalized;
        }

        float damage = CalculateDamage();
        bool isCrit = RollCrit();

        // 发射视觉粒子（纯视觉效果，不再承担伤害检测）
        bool useVFX = WeaponDef != null && WeaponDef.ProjectileVFXPrefab != null;
        for (int i = 0; i < ParticlesPerBurst; i++)
        {
            Vector3 baseDir = (target.transform.position - transform.position).normalized;
            float spreadX = Random.Range(-SpreadAngle, SpreadAngle);
            float spreadY = Random.Range(-SpreadAngle, SpreadAngle);
            Vector3 dir = Quaternion.Euler(spreadY, spreadX, 0f) * baseDir;

            GameObject particle;
            if (useVFX)
            {
                particle = new GameObject("FlameParticle_VFX");
            }
            else
            {
                particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                particle.name = "FlameParticle";
                particle.transform.localScale = Vector3.one * 0.15f;
                if (particle.TryGetComponent<Collider>(out var col))
                    Object.Destroy(col);
            }
            particle.transform.position = transform.position + Vector3.up * 0.5f;

            var proj = particle.AddComponent<FlameProjectile>();
            proj.Initialize(dir, ParticleSpeed, ParticleLifetime, useVFX ? WeaponDef.ProjectileVFXPrefab : null);
        }
    }
}

/// <summary>
/// 火焰粒子：纯视觉效果，仅飞行 + 自销毁，不再检测伤害
/// </summary>
public class FlameProjectile : MonoBehaviour
{
    private Vector3 _direction;
    private float _speed;
    private GameObject _vfxPrefab;

    public void Initialize(Vector3 direction, float speed, float lifetime, GameObject vfxPrefab = null)
    {
        _direction = direction.normalized;
        _speed = speed;
        _vfxPrefab = vfxPrefab;

        CreateVisual();
        Destroy(gameObject, lifetime);
    }

    private void CreateVisual()
    {
        if (_vfxPrefab != null)
        {
            var vfx = Instantiate(_vfxPrefab, transform);
            vfx.transform.localPosition = Vector3.zero;
            vfx.transform.localRotation = Quaternion.identity;
            return;
        }

        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Core",
            Vector3.zero, Vector3.one * 0.1f, new Color(1f, 0.6f, 0.1f));

        VisualHelper.CreateVisual(PrimitiveType.Sphere, transform, "Outer",
            Vector3.zero, Vector3.one * 0.15f, new Color(1f, 0.85f, 0.2f));

        VisualHelper.CreateVisual(PrimitiveType.Cube, transform, "Trail",
            new Vector3(0f, 0f, -0.12f), new Vector3(0.08f, 0.08f, 0.2f),
            new Color(1f, 0.3f, 0.05f));
    }

    private void Update()
    {
        // 纯视觉飞行，不检测伤害
        transform.position += _direction * _speed * Time.deltaTime;
    }
}