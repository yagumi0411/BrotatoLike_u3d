# BrotatoLike Unity 实现指南

> 基于用户选择：
> - 控制方式：WASD 移动 + 鼠标瞄准
> - 美术风格：3D 低多边形
> - 目标平台：PC

---

## 阶段一：项目基础搭建 (预计 1-2 天)

### 1.1 创建 Unity 项目
```
Unity Hub → 新建项目 → 3D Core → 命名为 BrotatoLike
```

### 1.2 项目设置
- **目标平台**：Switch to PC (Standalone)
- **帧率**：60 FPS
- **输入系统**：建议使用新版 Input System（可选，先用 Legacy 快速上手）

### 1.3 场景搭建
```
Hierarchy:
├── Plane (地面) - Scale (30, 1, 30) - 灰色材质
├── Wall (圆柱) - Scale (30, 2, 30) - 透明碰撞
├── Directional Light
├── Main Camera - Position (0, 15, -10), Rotation (60, 0, 0)
└── GameManager (空物体)
```

### 1.4 第一个脚本：GameManager

```csharp
// Assets/Scripts/Game/GameManager.cs
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public WaveManager WaveManager { get; private set; }
    public MonsterSpawner MonsterSpawner { get; private set; }
    public PlayerController Player { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // 初始化系统
        WaveManager = FindObjectOfType<WaveManager>();
        MonsterSpawner = FindObjectOfType<MonsterSpawner>();

        // 开始游戏
        WaveManager?.StartGame();
    }
}
```

---

## 阶段二：枚举和数据结构 (预计 0.5 天)

### 2.1 GameTypes.cs

```csharp
// Assets/Scripts/GameTypes.cs
using System;

public enum EUpgradeType
{
    Weapon,
    MaxHP_Add,
    MaxHP_Mul,
    Damage_All_Mul,
    AttackSpeed_All_Mul,
    MoveSpeed_Add,
    ExpRate_Mul,
    PickupRange_Add,
    CritChance_Add,
    CritDamage_Mul
}

public enum EWeaponType
{
    MagicBullet,
    FlameThrower,
    SpellOrbit
}

public enum EEnemyType
{
    Slime,
    Skeleton,
    Bat,
    ShadowMage,
    Ghost
}

public enum ETargetMode
{
    Nearest,
    Random
}
```

### 2.2 核心数据结构 (待 ScriptableObject 实现)

---

## 阶段三：玩家系统 (预计 1-2 天)

### 3.1 PlayerStatsComponent

```csharp
// Assets/Scripts/Player/PlayerStatsComponent.cs
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

    // 事件
    public event Action<float, float> OnHPChanged;
    public event Action<float, float, int> OnXPChanged;
    public event Action OnLevelUp;
    public event Action OnDeath;

    private void Start()
    {
        CurrentHP = GetEffectiveMaxHP();
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

    public float GetXPToNextLevel() => 10 * (CurrentLevel + 1);

    public void AddXP(float amount)
    {
        float multiplier = GetEffectiveExpMultiplier();
        amount *= multiplier;
        CurrentXP += amount;

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
        CurrentHP -= damage;
        OnHPChanged?.Invoke(CurrentHP, GetEffectiveMaxHP());

        if (CurrentHP <= 0)
        {
            OnDeath?.Invoke();
        }
    }

    public void ApplyUpgrade(UpgradeOption option)
    {
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

        // 同步最大生命值变化
        float maxHP = GetEffectiveMaxHP();
        if (CurrentHP < maxHP)
        {
            CurrentHP = maxHP;
        }
        OnHPChanged?.Invoke(CurrentHP, maxHP);
    }
}
```

### 3.2 PlayerController

```csharp
// Assets/Scripts/Player/PlayerController.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("组件")]
    public CharacterController CharacterController;
    public PlayerStatsComponent StatsComponent;
    public Transform CameraTransform;

    [Header("武器")]
    public List<Weapon> EquippedWeapons = new List<Weapon>();
    public const int MaxWeaponSlots = 6;

    [Header("初始武器")]
    public WeaponDefinition StartingWeaponPrefab;

    private Vector2 _moveInput;
    private Vector3 _aimDirection;

    private void Awake()
    {
        CharacterController = GetComponent<CharacterController>();
        StatsComponent = GetComponent<PlayerStatsComponent>();
    }

    private void Start()
    {
        // 相机
        var cam = Camera.main;
        if (cam != null) CameraTransform = cam.transform;

        // 初始化初始武器
        SpawnStartingWeapon();

        // 事件绑定
        StatsComponent.OnDeath += OnPlayerDeath;
    }

    private void Update()
    {
        HandleInput();
        HandleMovement();
    }

    private void HandleInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        _moveInput = new Vector2(h, v).normalized;

        // 鼠标瞄准方向
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("Ground")))
        {
            Vector3 targetPoint = hit.point;
            targetPoint.y = transform.position.y;
            _aimDirection = (targetPoint - transform.position).normalized;
        }
    }

    private void HandleMovement()
    {
        if (_moveInput.sqrMagnitude > 0.01f)
        {
            // 转换到相机空间
            Vector3 forward = CameraTransform.forward;
            forward.y = 0;
            forward.Normalize();
            Vector3 right = CameraTransform.right;
            right.y = 0;
            right.Normalize();

            Vector3 moveDir = (forward * _moveInput.y + right * _moveInput.x).normalized;
            float speed = StatsComponent.GetEffectiveMoveSpeed();

            CharacterController.Move(moveDir * speed * Time.deltaTime);

            // 更新朝向
            if (_aimDirection.sqrMagnitude > 0.01f)
            {
                transform.forward = _aimDirection;
            }
        }
    }

    private void SpawnStartingWeapon()
    {
        if (StartingWeaponPrefab != null)
        {
            AddWeapon(StartingWeaponPrefab);
        }
    }

    public bool AddWeapon(WeaponDefinition weaponDef)
    {
        if (IsWeaponSlotFull()) return false;

        var weaponObj = new GameObject(weaponDef.Name);
        weaponObj.transform.SetParent(transform);
        weaponObj.transform.localPosition = Vector3.zero;

        Weapon weapon = weaponObj.AddComponent(weaponDef.WeaponType switch
        {
            EWeaponType.MagicBullet => typeof(MagicBulletWeapon),
            EWeaponType.FlameThrower => typeof(FlameThrowerWeapon),
            EWeaponType.SpellOrbit => typeof(SpellOrbitWeapon),
            _ => typeof(MagicBulletWeapon)
        }) as Weapon;

        weapon.Initialize(this, weaponDef);
        EquippedWeapons.Add(weapon);
        return true;
    }

    public bool IsWeaponSlotFull() => EquippedWeapons.Count >= MaxWeaponSlots;
    public int GetRemainingWeaponSlots() => MaxWeaponSlots - EquippedWeapons.Count;

    public void ApplyUpgrade(UpgradeOption option)
    {
        if (option.Type == EUpgradeType.Weapon && option.WeaponDef != null)
        {
            AddWeapon(option.WeaponDef);
        }
        else
        {
            StatsComponent.ApplyUpgrade(option);
        }
    }

    public void OnPlayerDeath()
    {
        Debug.Log($"游戏结束! 存活波次: {GameManager.Instance?.WaveManager.CurrentWave}, 等级: {StatsComponent.CurrentLevel}");
        // 这里可以弹出结算界面
        Time.timeScale = 0;
    }
}
```

### 3.3 玩家预制体
```
创建 Player 空物体:
├── CharacterController (Radius: 0.4, Height: 1.8)
├── PlayerStatsComponent
├── PlayerController
└── Collider (Sphere, Radius: 0.4) - 用于被敌人检测
```

---

## 阶段四：ScriptableObject 数据定义 (预计 0.5 天)

### 4.1 WeaponDefinition

```csharp
// Assets/Scripts/Definitions/WeaponDefinition.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Weapon_", menuName = "Game/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("基础信息")]
    public EWeaponType Type;
    public string Name;
    [TextArea] public string Description;

    [Header("战斗属性")]
    public float BaseDamage = 5f;
    public float AttackInterval = 1f;
    public float Range = 15f;
    public ETargetMode TargetMode = ETargetMode.Nearest;

    [Header("投射物")]
    public float ProjectileSpeed = 8f;
    public float ProjectileLifetime = 2f;
    public int ProjectileCount = 1;

    [Header("扇形")]
    public float ConeHalfAngle = 30f;

    [Header("环绕")]
    public int OrbitCount = 3;
    public float OrbitRadius = 1.5f;
    public float OrbitSpeed = 180f;

    [Header("解锁")]
    public int MinWaveToAppear;
}
```

### 4.2 EnemyDefinition

```csharp
// Assets/Scripts/Definitions/EnemyDefinition.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Enemy_", menuName = "Game/Enemy Definition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("基础信息")]
    public EEnemyType Type;
    public string Name;

    [Header("属性")]
    public float BaseHP = 10f;
    public float MoveSpeed = 3f;
    public float ContactDamage = 1f;
    public float BaseXP = 3f;
    public float CollisionRadius = 0.5f;
    public float MeshScale = 1f;

    [Header("远程")]
    public bool bIsRanged;
    public float RangedAttackInterval = 2f;
    public float ProjectileDamage = 3f;
    public float ProjectileSpeed = 8f;

    [Header("冲刺")]
    public bool bCanDash;
    public float DashCooldown = 3f;
    public float DashSpeed = 12f;
    public float DashDuration = 0.3f;

    [Header("解锁")]
    public int MinWaveToSpawn;
}
```

### 4.3 UpgradeOption

```csharp
// Assets/Scripts/Definitions/UpgradeOption.cs
using UnityEngine;

[CreateAssetMenu(fileName = "Upgrade_", menuName = "Game/Upgrade Option")]
public class UpgradeOption : ScriptableObject
{
    public EUpgradeType Type;
    public string Name;
    [TextArea] public string Description;
    public float Value;

    [Header("武器专用")]
    public WeaponDefinition WeaponDef;

    [Header("解锁")]
    public int MinLevelToAppear;
}
```

---

## 阶段五：武器系统 (预计 1-2 天)

### 5.1 武器基类

```csharp
// Assets/Scripts/Weapons/Weapon.cs
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

    protected virtual Enemy FindTarget()
    {
        var enemies = FindObjectsOfType<Enemy>();
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
```

### 5.2 MagicBulletWeapon

```csharp
// Assets/Scripts/Weapons/MagicBulletWeapon.cs
using UnityEngine;

public class MagicBulletWeapon : Weapon
{
    protected override void Fire(Enemy target)
    {
        Vector3 targetPos = target.transform.position;
        float damage = CalculateDamage();
        bool isCrit = RollCrit();

        // 生成投射物
        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "MagicBullet";
        bullet.transform.position = transform.position + Vector3.up * 0.5f;
        bullet.transform.localScale = Vector3.one * 0.3f;

        // 设置投射物
        var projectile = bullet.AddComponent<MagicBulletProjectile>();
        projectile.Initialize(damage, isCrit, OwnerPlayer,
            (targetPos - transform.position).normalized,
            WeaponDef.ProjectileSpeed,
            WeaponDef.ProjectileLifetime);
    }
}

// MagicBulletProjectile.cs
public class MagicBulletProjectile : MonoBehaviour
{
    public float Damage;
    public bool IsCrit;
    public PlayerController OwnerPlayer;
    public Vector3 Direction;
    public float Speed;
    public float Lifetime;

    private void Start()
    {
        Destroy(gameObject, Lifetime);
    }

    private void Update()
    {
        transform.position += Direction * Speed * Time.deltaTime;
    }

    public void Initialize(float damage, bool isCrit, PlayerController owner,
        Vector3 direction, float speed, float lifetime)
    {
        Damage = damage;
        IsCrit = isCrit;
        OwnerPlayer = owner;
        Direction = direction.normalized;
        Speed = speed;
        Lifetime = lifetime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<Enemy>(out var enemy))
        {
            enemy.ReceiveDamage(Damage, IsCrit);
            Destroy(gameObject);
        }
    }
}
```

---

## 阶段六：敌人系统 (预计 1-2 天)

### 6.1 Enemy 基类

```csharp
// Assets/Scripts/Enemies/Enemy.cs
using UnityEngine;

public abstract class Enemy : MonoBehaviour
{
    [Header("配置")]
    public EnemyDefinition EnemyDef;

    [Header("运行时")]
    public float CurrentHP;
    public float StatMultiplier = 1f;

    protected virtual void Initialize(EnemyDefinition def, float statMultiplier)
    {
        EnemyDef = def;
        StatMultiplier = statMultiplier;
        CurrentHP = GetEffectiveHP();
    }

    public virtual void ReceiveDamage(float damage, bool isCrit = false)
    {
        if (isCrit)
        {
            float critDamage = damage * OwnerPlayer.StatsComponent.GetEffectiveCritDamage();
            damage = critDamage;
        }

        CurrentHP -= damage;
        if (CurrentHP <= 0)
        {
            OnDeath();
        }
    }

    protected virtual void OnDeath()
    {
        // 生成经验球
        float xp = GetEffectiveXP();
        XPOrb.Spawn(transform.position, xp);

        Destroy(gameObject);
    }

    protected virtual void Update()
    {
        MoveTowardsPlayer();
    }

    protected virtual void MoveTowardsPlayer()
    {
        if (EnemyDef.MoveSpeed <= 0) return; // 不移动 (ShadowMage)

        Vector3 playerPos = GetPlayerLocation();
        Vector3 dir = (playerPos - transform.position).normalized;
        float speed = GetEffectiveMoveSpeed();

        transform.position += dir * speed * Time.deltaTime;
        transform.LookAt(playerPos);
    }

    protected Vector3 GetPlayerLocation() =>
        GameManager.Instance?.Player?.transform?.position ?? Vector3.zero;

    protected float GetEffectiveHP() => EnemyDef.BaseHP * StatMultiplier;
    protected float GetEffectiveMoveSpeed() => EnemyDef.MoveSpeed * StatMultiplier;
    protected float GetEffectiveContactDamage() => EnemyDef.ContactDamage * StatMultiplier;
    protected float GetEffectiveXP() => EnemyDef.BaseXP * GameManager.Instance.WaveManager.GetXPWaveMultiplier();

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var player))
        {
            player.StatsComponent.TakeDamage(GetEffectiveContactDamage());
        }
    }
}
```

### 6.2 具体敌人

```csharp
// SlimeEnemy.cs - 慢速肉盾
public class SlimeEnemy : Enemy
{
    // 使用基类默认行为
}

// GhostEnemy.cs - 会冲刺
public class GhostEnemy : Enemy
{
    private bool _isDashing;
    private float _dashCooldown;
    private float _dashDuration;
    private Vector3 _dashDirection;

    protected override void Update()
    {
        base.Update();

        if (EnemyDef.bCanDash)
        {
            _dashCooldown -= Time.deltaTime;
            if (!_isDashing && _dashCooldown <= 0)
            {
                StartDash();
            }
            else if (_isDashing)
            {
                _dashDuration -= Time.deltaTime;
                transform.position += _dashDirection * EnemyDef.DashSpeed * StatMultiplier * Time.deltaTime;
                if (_dashDuration <= 0)
                {
                    _isDashing = false;
                    _dashCooldown = EnemyDef.DashCooldown;
                }
            }
        }
    }

    private void StartDash()
    {
        _isDashing = true;
        _dashDuration = EnemyDef.DashDuration;
        _dashDirection = (GetPlayerLocation() - transform.position).normalized;
    }
}
```

---

## 阶段七：经验球 (预计 0.5 天)

```csharp
// Assets/Scripts/XPOrb.cs
using UnityEngine;

public class XPOrb : MonoBehaviour
{
    public float ExpValue;
    private bool _isMagnetizing;
    private const float MagnetSpeed = 6f;
    private Renderer _renderer;
    private Collider _collider;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
    }

    private void Update()
    {
        // 检查磁吸
        var player = GameManager.Instance?.Player;
        if (player != null)
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            float pickupRadius = player.StatsComponent.GetEffectivePickupRadius();

            if (dist <= pickupRadius || _isMagnetizing)
            {
                _isMagnetizing = true;
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    player.transform.position,
                    MagnetSpeed * Time.deltaTime);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerController>(out var player))
        {
            player.StatsComponent.AddXP(ExpValue);
            Destroy(gameObject);
        }
    }

    public static void Spawn(Vector3 position, float xpValue)
    {
        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "XPOrb";
        orb.transform.position = position + Vector3.up * 0.5f;
        orb.transform.localScale = Vector3.one * 0.3f;
        orb.GetComponent<Renderer>().material.color = Color.blue;

        var orbComp = orb.AddComponent<XPOrb>();
        orbComp.ExpValue = xpValue;
    }
}
```

---

## 阶段八：波次和怪物生成 (预计 1 天)

### 8.1 WaveManager

```csharp
// Assets/Scripts/Game/WaveManager.cs
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public float WaveInterval = 30f;
    public float RestBetweenWaves = 3f;

    public int CurrentWave { get; private set; }
    public float WaveTimer { get; private set; }
    public bool IsWaveActive { get; private set; }
    public float RestTimer { get; private set; }

    public event System.Action<int> OnWaveChanged;

    public void StartGame()
    {
        CurrentWave = 0;
        StartNextWave();
    }

    private void Update()
    {
        if (IsWaveActive)
        {
            WaveTimer -= Time.deltaTime;
            if (WaveTimer <= 0)
            {
                EndWave();
            }
        }
        else
        {
            RestTimer -= Time.deltaTime;
            if (RestTimer <= 0)
            {
                StartNextWave();
            }
        }
    }

    public void StartNextWave()
    {
        CurrentWave++;
        WaveTimer = WaveInterval;
        IsWaveActive = true;
        OnWaveChanged?.Invoke(CurrentWave);
    }

    public void EndWave()
    {
        IsWaveActive = false;
        RestTimer = RestBetweenWaves;
    }

    public float GetEnemyStatMultiplier() => 1f + (CurrentWave - 1) * 0.15f;
    public float GetXPWaveMultiplier() => 1f + (CurrentWave - 1) * 0.1f;
}
```

### 8.2 MonsterSpawner

```csharp
// Assets/Scripts/Game/MonsterSpawner.cs
using UnityEngine;
using System.Linq;

public class MonsterSpawner : MonoBehaviour
{
    public float ArenaRadius = 28f;
    public List<EnemyDefinition> EnemyPrefabs;

    private WaveManager _waveManager;
    private float _spawnTimer;

    private void Awake()
    {
        _waveManager = GetComponent<WaveManager>();
    }

    private void Update()
    {
        if (_waveManager == null || !_waveManager.IsWaveActive)
            return;

        float spawnInterval = GetSpawnInterval();
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0)
        {
            SpawnEnemy();
            _spawnTimer = spawnInterval;
        }
    }

    private float GetSpawnInterval() =>
        1f / (2f + _waveManager.CurrentWave * 0.5f);

    private void SpawnEnemy()
    {
        var available = EnemyPrefabs
            .Where(e => e.MinWaveToSpawn <= _waveManager.CurrentWave)
            .ToList();

        if (available.Count == 0) return;

        var def = available[Random.Range(0, available.Count)];
        Vector3 pos = GetSpawnPosition();

        GameObject enemyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemyObj.name = def.Name;
        enemyObj.transform.position = pos;
        enemyObj.transform.localScale = Vector3.one * def.MeshScale * 0.5f;

        Enemy enemy = enemyObj.AddComponent(def.Type switch
        {
            EEnemyType.Slime => typeof(SlimeEnemy),
            EEnemyType.Skeleton => typeof(SkeletonEnemy),
            EEnemyType.Bat => typeof(BatEnemy),
            EEnemyType.ShadowMage => typeof(ShadowMageEnemy),
            EEnemyType.Ghost => typeof(GhostEnemy),
            _ => typeof(SlimeEnemy)
        }) as Enemy;

        enemy.Initialize(def, _waveManager.GetEnemyStatMultiplier());
    }

    private Vector3 GetSpawnPosition()
    {
        Vector2 random = Random.insideUnitCircle.normalized * ArenaRadius;
        return new Vector3(random.x, 1f, random.y);
    }
}
```

---

## 阶段九：UI 系统 (预计 1 天)

### 9.1 MainHUD

```csharp
// Assets/Scripts/UI/MainHUD.cs
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
        _player = GameManager.Instance.Player;
        _waveManager = GameManager.Instance.WaveManager;

        // 绑定事件
        _player.StatsComponent.OnHPChanged += UpdateHP;
        _player.StatsComponent.OnXPChanged += UpdateXP;
        _waveManager.OnWaveChanged += UpdateWave;

        // 初始更新
        UpdateHP(_player.StatsComponent.CurrentHP, _player.StatsComponent.GetEffectiveMaxHP());
        UpdateXP(_player.StatsComponent.CurrentXP, _player.StatsComponent.GetXPToNextLevel(), _player.StatsComponent.CurrentLevel);
    }

    private void Update()
    {
        if (_waveManager.IsWaveActive)
        {
            WaveTimerText.text = $"剩余 {Mathf.Ceil(_waveManager.WaveTimer)}s";
        }
        else
        {
            WaveTimerText.text = "休息中";
        }
    }

    private void UpdateHP(float current, float max)
    {
        HPBar.maxValue = max;
        HPBar.value = current;
        HPText.text = $"{Mathf.Ceil(current)} / {Mathf.Ceil(max)}";
    }

    private void UpdateXP(float current, float toNext, int level)
    {
        XPBar.maxValue = toNext;
        XPBar.value = current;
        LevelText.text = $"Lv.{level}";
    }

    private void UpdateWave(int wave)
    {
        WaveText.text = $"波次 {wave}";
    }
}
```

### 9.2 LevelUpUI

```csharp
// Assets/Scripts/UI/LevelUpUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LevelUpUI : MonoBehaviour
{
    public GameObject Panel;
    public Button[] OptionButtons;
    public TextMeshProUGUI[] OptionNames;
    public TextMeshProUGUI[] OptionDescriptions;

    private List<UpgradeOption> _currentOptions;
    private GameManager _gameManager;

    private void Awake()
    {
        Panel.SetActive(false);
        _gameManager = GameManager.Instance;

        for (int i = 0; i < OptionButtons.Length; i++)
        {
            int index = i;
            OptionButtons[i].onClick.AddListener(() => OnOptionSelected(index));
        }
    }

    public void ShowOptions(List<UpgradeOption> options)
    {
        _currentOptions = options;
        Panel.SetActive(true);
        Time.timeScale = 0;

        for (int i = 0; i < 3; i++)
        {
            if (i < options.Count)
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
        if (opt.Type == EUpgradeType.Weapon && opt.WeaponDef != null)
        {
            return $"{opt.WeaponDef.Description}\n伤害: {opt.WeaponDef.BaseDamage}\n攻速: {opt.WeaponDef.AttackInterval}s";
        }
        return opt.Description;
    }

    private void OnOptionSelected(int index)
    {
        var option = _currentOptions[index];
        _gameManager.Player.ApplyUpgrade(option);

        Panel.SetActive(false);
        Time.timeScale = 1;
    }
}
```

---

## 开发顺序总结

| 顺序 | 阶段 | 关键文件 | 预计时间 |
|------|------|----------|---------|
| 1 | 项目基础 | GameManager, 场景搭建 | 0.5天 |
| 2 | 数据结构 | GameTypes | 0.5天 |
| 3 | 玩家系统 | PlayerStatsComponent, PlayerController | 1天 |
| 4 | ScriptableObject | Weapon/Enemy/Upgrade Definition | 0.5天 |
| 5 | 武器系统 | Weapon 基类, MagicBulletWeapon | 1天 |
| 6 | 敌人系统 | Enemy 基类, 具体敌人 | 1天 |
| 7 | 经验球 | XPOrb | 0.5天 |
| 8 | 波次系统 | WaveManager, MonsterSpawner | 1天 |
| 9 | UI 系统 | MainHUD, LevelUpUI | 1天 |

**总计约 7 天完成核心 Demo**

---

## 后续可选扩展

1. **新武器**: FlameThrower, SpellOrbit
2. **新敌人**: Skeleton, Bat, ShadowMage, Ghost
3. **武器进化系统**
4. **多角色系统**
5. **音效和特效**