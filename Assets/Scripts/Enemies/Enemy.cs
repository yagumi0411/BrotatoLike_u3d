using UnityEngine;

public abstract class Enemy : MonoBehaviour
{
    [Header("配置")]
    public EnemyDefinition EnemyDef;

    [Header("运行时")]
    public float CurrentHP;
    public float StatMultiplier = 1f;

    public PlayerController OwnerPlayer { get; private set; }
    private Rigidbody _rb;
    private Collider _collider;
    private float _contactDamageTimer;

    public virtual void Initialize(EnemyDefinition def, float statMultiplier)
    {
        EnemyDef = def;
        StatMultiplier = statMultiplier;
        CurrentHP = GetEffectiveHP();
        OwnerPlayer = GameManager.Instance?.Player;
        SetupCollider();
        IgnorePlayerCollision();
        CreateVisual();

        // 注册进索敌注册表（替代 FindObjectsByType 全场景扫描）
        EnemyRegistry.Register(this);
    }

    private void IgnorePlayerCollision()
    {
        if (OwnerPlayer == null || _collider == null) return;
        var playerCollider = OwnerPlayer.GetComponent<CharacterController>();
        if (playerCollider != null)
        {
            Physics.IgnoreCollision(_collider, playerCollider, true);
        }
    }

    protected virtual void SetupCollider()
    {
        var collider = GetComponent<Collider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<CapsuleCollider>();
        }
        _collider = collider;

        if (collider is CapsuleCollider capsule)
        {
            capsule.radius = EnemyDef.CollisionRadius;
            capsule.height = EnemyDef.CollisionRadius * 2f;
        }
        collider.isTrigger = false;

        // 添加 Rigidbody 实现物理排斥，防止怪物重叠
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.mass = 1f;
        _rb.linearDamping = 5f;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX
                         | RigidbodyConstraints.FreezeRotationY
                         | RigidbodyConstraints.FreezeRotationZ;
        _rb.useGravity = false;
    }

    protected virtual void CreateVisual()
    {
        // 池化复用防御：已有视觉则跳过，防止重复创建
        if (transform.Find("Visual") != null) return;

        // 默认视觉：简单胶囊体
        VisualHelper.CreateVisual(PrimitiveType.Capsule, transform, "Visual",
            Vector3.zero, Vector3.one * EnemyDef.MeshScale, Color.white);
    }

    protected virtual void OnDestroy()
    {
        // 兜底注销（清场/场景卸载真正销毁时才触发；池化回收路径由 EnemyPool.Release 处理）
        EnemyRegistry.Unregister(this);
    }

    public virtual void ReceiveDamage(float damage, bool isCrit = false)
    {
        if (isCrit)
        {
            float critDamage = damage * OwnerPlayer.StatsComponent.GetEffectiveCritDamage();
            damage = critDamage;
        }

        // 伤害飘字
        DamagePopup.Spawn(transform.position, damage, isCrit);

        CurrentHP -= damage;
        if (CurrentHP <= 0)
        {
            OnDeath();
        }
    }

    protected virtual void OnDeath()
    {
        // 通知 GameManager 记录击杀
        GameManager.Instance?.AddKill();

        // 生成经验球
        float xp = GetEffectiveXP();
        XPOrb.Spawn(transform.position, xp);

        // 回收到对象池（替代 Destroy，消除 Instantiate/Destroy 的 GC）
        EnemyPool.Release(this);
    }

    protected virtual void Update()
    {
        MoveTowardsPlayer();
        CheckContactDamage();
    }

    protected virtual void MoveTowardsPlayer()
    {
        if (EnemyDef.MoveSpeed <= 0) return; // 不移动 (ShadowMage)

        Vector3 playerPos = GetPlayerLocation();
        Vector3 dir = (playerPos - transform.position).normalized;
        float speed = GetEffectiveMoveSpeed();

        // 使用物理速度移动，Rigidbody 碰撞自动处理重叠排斥
        _rb.linearVelocity = new Vector3(dir.x * speed, 0f, dir.z * speed);
        transform.LookAt(playerPos);
    }

    private void CheckContactDamage()
    {
        if (OwnerPlayer == null || EnemyDef.ContactDamage <= 0f) return;

        float dist = Vector3.Distance(transform.position, OwnerPlayer.transform.position);
        float contactRange = EnemyDef.CollisionRadius + 0.5f; // 玩家半径约 0.5

        if (dist <= contactRange)
        {
            _contactDamageTimer -= Time.deltaTime;
            if (_contactDamageTimer <= 0f)
            {
                OwnerPlayer.StatsComponent.TakeDamage(GetEffectiveContactDamage());
                _contactDamageTimer = 0.5f; // 0.5s 冷却，防止高频触发
            }
        }
        else
        {
            _contactDamageTimer = 0f;
        }
    }

    protected Vector3 GetPlayerLocation() =>
        GameManager.Instance?.Player?.transform?.position ?? Vector3.zero;

    protected float GetEffectiveHP() => EnemyDef.BaseHP * StatMultiplier;
    // 移速只受 30% 波次缩放，防止后期怪物速度反超玩家导致无法走位
    protected float GetEffectiveMoveSpeed() =>
        EnemyDef.MoveSpeed * (1f + (StatMultiplier - 1f) * 0.3f);
    protected float GetEffectiveContactDamage() => EnemyDef.ContactDamage * StatMultiplier;
    protected float GetEffectiveXP() => EnemyDef.BaseXP * GameManager.Instance.WaveManager.GetXPWaveMultiplier();
}
