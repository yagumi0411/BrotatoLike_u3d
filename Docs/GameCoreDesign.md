# 游戏核心开发文档 — BrotatoLike

> **面向受众**: 程序员 & AI 实现者
> **用途**: 面试 Demo 实现规范
> **主题**: 魔法风格 Top-Down 无限波次生存游戏
> **引擎**: Unity 2022.3+ (C# 实现)

---

## 1. 项目概述

### 1.1 游戏类型

Top-down 2.5D 动作生存游戏。玩家控制一名法师，在封闭竞技场内对抗无限波次的怪物。武器自动瞄准攻击，击杀怪物掉落经验球，升级时暂停游戏并从 3 个选项中选 1 个强化。

### 1.2 核心循环

```
波次推进 → 怪物生成 → 武器自动攻击 → 击杀掉落经验 → 拾取经验升级 → 暂停选强化 → 继续
```

直到玩家死亡，记录存活波次和等级。

### 1.3 技术架构

| 层级 | 技术 |
|------|------|
| 引擎 | Unity 2022.3+ |
| 语言 | C# |
| 数据驱动 | ScriptableObject 驱动武器/敌人/升级配置 |
| 架构模式 | Component-based ECS + OOP 混合 |

---

## 2. 核心数值公式

### 2.1 伤害计算

```csharp
RawDamage = Weapon.BaseDamage * (1.0f + Stats.GlobalDamageMultiplier)

if Random.value < EffectiveCritChance:
    FinalDamage = RawDamage * (1.5f + Stats.CritDamageMultiplierBonus)
else:
    FinalDamage = RawDamage
```

- `Weapon.BaseDamage`: 武器基础伤害，每个武器类型独立配置
- `Stats.GlobalDamageMultiplier`: 由 `Damage_All_Mul` 类升级累加（如 0.0 → 0.1 → 0.2）
- `EffectiveCritChance`: 暴击率 = Mathf.Clamp(Stats.CritChanceBonus, 0.0f, 0.6f)
- `Stats.CritDamageMultiplierBonus`: 由 `CritDamage_Mul` 类升级累加（如 0.0 → 0.15 → 0.35）
- 暴击基础倍率 1.5，即暴击时至少造成 150% 伤害

### 2.2 经验值与升级

```csharp
XP_ToNextLevel(CurrentLevel) = 10 * (CurrentLevel + 1)

EffectiveXP = Enemy.BaseXP * (1.0f + Stats.ExpGainMultiplier) * WaveXPMultiplier
```

| 当前等级 | 升到下一级所需 XP |
|----------|-------------------|
| 1 | 20 |
| 2 | 30 |
| 3 | 40 |
| 5 | 60 |
| 10 | 110 |
| N | 10 × (N + 1) |

```csharp
WaveXPMultiplier = 1.0f + (WaveNumber - 1) * 0.1f
```

### 2.3 波次缩放

```csharp
WaveInterval = 30 秒

EnemyStatMultiplier = 1.0f + (WaveNumber - 1) * 0.15f
SpawnRate = 2.0f + WaveNumber * 0.5f  // 每秒生成怪物数
WaveXPMultiplier = 1.0f + (WaveNumber - 1) * 0.1f
```

波次 1 = 基准，波次越高敌人越强、越多、经验也越多（但经验增速慢于血量增速 → 后期更难）。

### 2.4 玩家有效属性（运行时计算）

```csharp
EffectiveMaxHP       = (BaseMaxHP + FlatHPBonus) * (1.0f + PercentHPBonus)
EffectiveMoveSpeed   = BaseMoveSpeed + FlatMoveSpeedBonus
EffectivePickupRadius = BasePickupRadius + FlatPickupRangeBonus
EffectiveDamageMultiplier = 1.0f + GlobalDamageMultiplier
EffectiveAttackSpeedMultiplier = 1.0f + GlobalAttackSpeedMultiplier
EffectiveExpMultiplier = 1.0f + ExpGainMultiplier
EffectiveCritChance  = Mathf.Clamp(CritChanceBonus, 0.0f, 0.6f)
EffectiveCritDamage  = 1.5f + CritDamageMultiplierBonus
```

---

## 3. 核心枚举定义

```csharp
// 升级选项类型
public enum EUpgradeType
{
    Weapon,              // 新武器
    MaxHP_Add,           // 最大生命值固定提升
    MaxHP_Mul,           // 最大生命值百分比提升
    Damage_All_Mul,      // 全局伤害百分比提升
    AttackSpeed_All_Mul, // 全局攻速百分比提升
    MoveSpeed_Add,       // 移速固定提升
    ExpRate_Mul,         // 经验获取倍率
    PickupRange_Add,     // 拾取范围固定提升
    CritChance_Add,      // 暴击率
    CritDamage_Mul       // 暴击伤害倍率
}

// 武器类型
public enum EWeaponType
{
    MagicBullet,    // 魔法弹 (初始武器)
    FlameThrower,   // 火焰喷射
    SpellOrbit,     // 飞弹环绕
    // 后续扩展:
    // IceSpike,     // 冰锥穿透
    // LightningChain, // 闪电链
    // PoisonCloud  // 毒雾
}

// 敌人类型
public enum EEnemyType
{
    Slime,       // 史莱姆 - 慢速高血量近战
    Skeleton,    // 骷髅 - 中速中血量近战
    Bat,         // 蝙蝠 - 快速低血量近战
    ShadowMage,  // 暗影法师 - 远程站桩
    Ghost        // 幽灵 - 中速冲刺
}

// 武器索敌模式
public enum ETargetMode
{
    Nearest,   // 范围内最近的敌人
    Random     // 范围内随机敌人
}
```

---

## 4. 核心数据结构

### 4.1 WeaponDefinition — 武器配置 (ScriptableObject)

```csharp
[CreateAssetMenu(fileName = "Weapon_", menuName = "Game/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("基础信息")]
    public EWeaponType Type;
    public string Name;                // 显示名称
    [TextArea] public string Description;  // 升级界面的简介文本

    [Header("战斗属性")]
    public float BaseDamage;          // 基础伤害
    public float AttackInterval;      // 攻击间隔 (秒)
    public float Range;               // 索敌范围
    public ETargetMode TargetMode;    // 索敌模式

    [Header("投射物属性")]
    public float ProjectileSpeed;     // 弹丸速度
    public float ProjectileLifetime;  // 弹丸存活时间 (秒)
    public int ProjectileCount;       // 每次攻击弹丸数

    [Header("扇形武器属性")]
    public float ConeHalfAngle;       // 扇形半角 (度), 火焰喷射用

    [Header("环绕武器属性")]
    public int OrbitCount;            // 环绕飞弹数量
    public float OrbitRadius;         // 环绕半径
    public float OrbitSpeed;          // 环绕速度 (度/秒)

    [Header("解锁条件")]
    public int MinWaveToAppear;       // 最低出现波次 (0 = 无限制)
}
```

### 4.2 EnemyDefinition — 敌人配置 (ScriptableObject)

```csharp
[CreateAssetMenu(fileName = "Enemy_", menuName = "Game/Enemy Definition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("基础信息")]
    public EEnemyType Type;
    public string Name;

    [Header("战斗属性")]
    public float BaseHP;
    public float MoveSpeed;
    public float ContactDamage;       // 接触碰撞时对玩家造成的伤害
    public float BaseXP;              // 击杀后掉落的经验值
    public float CollisionRadius;     // 碰撞半径
    public float MeshScale;           // 模型缩放

    [Header("远程特性")]
    public bool bIsRanged;            // 是否远程敌人
    public float RangedAttackInterval; // 远程攻击间隔
    public float ProjectileDamage;    // 远程弹幕伤害
    public float ProjectileSpeed;     // 远程弹幕速度

    [Header("冲刺特性")]
    public bool bCanDash;             // 是否有冲刺技能
    public float DashCooldown;        // 冲刺冷却
    public float DashSpeed;           // 冲刺速度
    public float DashDuration;        // 冲刺持续时间

    [Header("解锁条件")]
    public int MinWaveToSpawn;        // 最早出现的波次
}
```

### 4.3 UpgradeOption — 升级选项 (ScriptableObject)

```csharp
[CreateAssetMenu(fileName = "Upgrade_", menuName = "Game/Upgrade Option")]
public class UpgradeOption : ScriptableObject
{
    public EUpgradeType Type;
    public string Name;                // 选项显示名
    [TextArea] public string Description; // 选项描述
    public float Value;                // 数值 (非武器类用)

    [Header("武器类专用")]
    public WeaponDefinition WeaponDef; // 武器定义 (武器类用)

    [Header("解锁条件")]
    public int MinLevelToAppear;       // 最低出现等级 (0 = 无限制)
}
```

### 4.4 PlayerStats — 玩家属性快照

```csharp
[System.Serializable]
public struct PlayerStats
{
    // 基础值 (角色决定)
    public float BaseMaxHP;
    public float BaseMoveSpeed;
    public float BasePickupRadius;

    // 升级累加值
    public float FlatHPBonus;         // MaxHP_Add 累加
    public float PercentHPBonus;      // MaxHP_Mul 累加 (0.0~)
    public float GlobalDamageMultiplier;   // Damage_All_Mul 累加
    public float GlobalAttackSpeedMultiplier; // AttackSpeed_All_Mul 累加
    public float FlatMoveSpeedBonus;  // MoveSpeed_Add 累加
    public float ExpGainMultiplier;   // ExpRate_Mul 累加
    public float FlatPickupRangeBonus; // PickupRange_Add 累加
    public float CritChanceBonus;     // CritChance_Add 累加
    public float CritDamageMultiplierBonus; // CritDamage_Mul 累加

    // 运行时状态
    public int CurrentLevel;
    public int WeaponSlotCount;       // 已填充武器槽数
}
```

---

## 5. 核心类设计

### 5.1 PlayerController — 玩家控制器

继承自 `MonoBehaviour`，使用 CharacterController 移动。

```csharp
public class PlayerController : MonoBehaviour
{
    // 组件
    [Header("组件")]
    public CharacterController CharacterController;
    public PlayerStatsComponent StatsComponent;
    public Transform CameraTransform;

    // 武器槽
    [Header("武器槽")]
    public List<Weapon> EquippedWeapons = new List<Weapon>();
    public const int MaxWeaponSlots = 6;

    // 移动输入
    private Vector2 _moveInput;

    // 方法
    public bool AddWeapon(Weapon weapon);
    public bool IsWeaponSlotFull();
    public int GetRemainingWeaponSlots();
    public void ApplyUpgrade(UpgradeOption option);

    // 受伤
    public void TakeDamage(float damage);

    // 初始化武器
    private void SpawnStartingWeapon();

    // 死亡
    private void OnDeath();
}
```

### 5.2 PlayerStatsComponent — 玩家属性组件

继承自 `MonoBehaviour`，挂载在玩家上。

```csharp
public class PlayerStatsComponent : MonoBehaviour
{
    [Header("基础属性 (可在 Inspector 覆写)")]
    public float BaseMaxHP = 10f;
    public float BaseMoveSpeed = 6f;
    public float BasePickupRadius = 3f;
    public WeaponDefinition StartingWeapon;

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

    // === 计算有效值 ===
    public float GetEffectiveMaxHP();
    public float GetEffectiveMoveSpeed();
    public float GetEffectivePickupRadius();
    public float GetEffectiveDamageMultiplier();
    public float GetEffectiveAttackSpeedMultiplier();
    public float GetEffectiveExpMultiplier();
    public float GetEffectiveCritChance();
    public float GetEffectiveCritDamage();

    // === 升级 ===
    public void ApplyUpgrade(UpgradeOption option);
    public float GetXPToNextLevel();
    public void AddXP(float amount);

    // === 事件 ===
    public System.Action<float, float> OnHPChanged;
    public System.Action<float, float, int> OnXPChanged;
    public System.Action OnLevelUp;
    public System.Action OnDeath;

    private void LevelUp();
}
```

### 5.3 Weapon — 武器基类

```csharp
public abstract class Weapon : MonoBehaviour
{
    // 配置
    [Header("配置")]
    public WeaponDefinition WeaponDef;

    // 所属玩家
    [HideInInspector]
    public PlayerController OwnerPlayer;

    // 攻击冷却
    protected float AttackCooldown;

    // 初始化
    public virtual void Initialize(PlayerController owner);

    // 每帧调用
    protected virtual void Update();

    // 获取实际攻击间隔
    protected float GetEffectiveAttackInterval();

    // === 子类重写 ===
    // 索敌
    protected virtual Enemy FindTarget();

    // 执行攻击
    protected virtual void Fire(Enemy target);

    // 判断是否可以攻击
    protected virtual bool CanAttack();

    // 计算本次伤害
    protected float CalculateDamage();

    // 判定暴击
    protected bool RollCrit();

    // 获取范围
    protected float GetEffectiveRange();
}
```

**Weapon.CalculateDamage 实现:**

```csharp
float CalculateDamage()
{
    if (OwnerPlayer == null || OwnerPlayer.StatsComponent == null)
        return WeaponDef.BaseDamage;

    float multiplier = OwnerPlayer.StatsComponent.GetEffectiveDamageMultiplier();
    return WeaponDef.BaseDamage * multiplier;
}

bool RollCrit()
{
    if (OwnerPlayer == null || OwnerPlayer.StatsComponent == null)
        return false;

    float critChance = OwnerPlayer.StatsComponent.GetEffectiveCritChance();
    return Random.value < critChance;
}

float GetEffectiveAttackInterval()
{
    if (OwnerPlayer == null || OwnerPlayer.StatsComponent == null)
        return WeaponDef.AttackInterval;

    return WeaponDef.AttackInterval /
        OwnerPlayer.StatsComponent.GetEffectiveAttackSpeedMultiplier();
}
```

### 5.4 MagicBulletWeapon — 魔法弹

继承 `Weapon`，单目标跟踪弹丸。

```csharp
public class MagicBulletWeapon : Weapon
{
    protected override void Fire(Enemy target);
    protected override Enemy FindTarget();
    private void SpawnBullet(Vector3 targetPos, float damage, bool isCrit);
}
```

**FindTarget 逻辑:**

1. 从所有者位置获取范围内所有 `Enemy`
2. 筛选距离 ≤ `WeaponDef.Range` 的敌人
3. 根据 `WeaponDef.TargetMode` 选择目标
4. Nearest: 返回距离最近的
5. 无有效目标返回 `null`

**Fire 逻辑:**

1. 获取目标当前位置
2. 调用 `SpawnBullet` 生成投射物
3. 投射物飞向目标位置（非追踪，发射即确定方向）

**配置数据 (ScriptableObject 预设):**

| 属性 | 值 |
|------|-----|
| Type | MagicBullet |
| Name | 魔法弹 |
| Description | 向最近的敌人发射一枚魔法弹丸 |
| BaseDamage | 5 |
| AttackInterval | 1.0s |
| Range | 15 |
| TargetMode | Nearest |
| ProjectileSpeed | 8 |
| ProjectileLifetime | 2.0s |
| ProjectileCount | 1 |
| MinWaveToAppear | 0 (初始可用) |

### 5.5 FlameThrowerWeapon — 火焰喷射

继承 `Weapon`，前方扇形持续伤害。

```csharp
public class FlameThrowerWeapon : Weapon
{
    protected override void Update();
    protected override void Fire(Enemy target);
    protected override Enemy FindTarget();

    // 获取扇形范围内所有敌人
    private List<Enemy> GetEnemiesInCone();

    // 对每个锥形内敌人应用伤害
    private float DamageTickTimer;
    private Dictionary<Enemy, float> LastDamageTimeMap = new Dictionary<Enemy, float>();
}
```

**Update 逻辑:**

1. 每帧递减 `DamageTickTimer`
2. `DamageTickTimer` ≤ 0 时，获取锥形内敌人
3. 对每个敌人检查距离上次受伤害时间 ≥ `AttackInterval`
4. 满足条件 → 计算伤害 (含暴击判定) → 应用到敌人
5. 重置 `DamageTickTimer` = `AttackInterval`

**配置数据:**

| 属性 | 值 |
|------|-----|
| Type | FlameThrower |
| Name | 火焰喷射 |
| Description | 向前方扇形范围持续喷射火焰 |
| BaseDamage | 2 (每跳) |
| AttackInterval | 0.15s |
| Range | 4 |
| TargetMode | Nearest |
| ConeHalfAngle | 30° |
| MinWaveToAppear | 0 (初始可用) |

### 5.6 SpellOrbitWeapon — 飞弹环绕

继承 `Weapon`，在玩家周围生成环绕飞弹，被动碰撞伤害。

```csharp
public class SpellOrbitWeapon : Weapon
{
    protected override void Update();

    // 环绕飞弹 GameObject 列表
    private List<OrbitProjectile> OrbitProjectiles = new List<OrbitProjectile>();

    // 生成环绕飞弹
    private void SpawnOrbitProjectiles();

    // 更新飞弹位置
    private void UpdateOrbitPositions(float deltaTime);
}
```

**BeginPlay 逻辑:**

1. 调用 `SpawnOrbitProjectiles` 生成 `WeaponDef.OrbitCount` 个环绕飞弹
2. 均匀分布在圆周上 (360° / OrbitCount 间隔)

**Update 逻辑:**

1. 调用 `UpdateOrbitPositions` 更新每个飞弹的环绕角度
2. 飞弹自身检测与敌人的碰撞

**配置数据:**

| 属性 | 值 |
|------|-----|
| Type | SpellOrbit |
| Name | 飞弹环绕 |
| Description | 召唤魔法飞弹环绕自身，碰触敌人造成伤害 |
| BaseDamage | 3 |
| AttackInterval | 0.3s |
| Range | 1.5 (环绕半径) |
| OrbitCount | 3 |
| OrbitRadius | 1.5 |
| OrbitSpeed | 180 (度/秒，即 2 秒一圈) |
| MinWaveToAppear | 0 (初始可用) |

### 5.7 Projectile / MagicBulletProjectile / OrbitProjectile — 投射物

```csharp
// 投射物基类
public class Projectile : MonoBehaviour
{
    public float Damage;
    public bool IsCrit;
    public PlayerController OwnerPlayer;
    public Vector3 Direction;
    public float Speed;
    public float Lifetime;

    protected virtual void Update();
    protected virtual void OnTriggerEnter(Collider other);
    protected virtual void OnLifetimeExpired();
}

// 魔法弹投射物: 直线飞行，命中敌人或到达存活时间后销毁
public class MagicBulletProjectile : Projectile
{
    protected override void OnTriggerEnter(Collider other);
}

// 环绕飞弹: 绕玩家旋转，碰撞敌人造成伤害，有冷却
public class OrbitProjectile : Projectile
{
    public float Angle;
    public float Radius;
    public Transform CenterTarget;
    private Dictionary<Enemy, float> EnemyHitCooldowns = new Dictionary<Enemy, float>();
    private float HitCooldownDuration;

    public void UpdateOrbitPosition(float angle, float radius, Vector3 center);
    protected override void OnTriggerEnter(Collider other);
}
```

### 5.8 Enemy — 敌人基类

```csharp
public abstract class Enemy : MonoBehaviour
{
    [Header("配置")]
    public EnemyDefinition EnemyDef;

    // 运行时
    [Header("运行时")]
    public float CurrentHP;
    public float StatMultiplier;  // 波次缩放倍率，由 MonsterSpawner 设置

    // 组件
    [Header("组件")]
    public Collider Collider;
    public Renderer MeshRenderer;

    // 移动
    protected Vector3 MoveDirection;
    protected bool IsDashing;
    protected float DashCooldownRemaining;
    protected float DashDurationRemaining;
    protected Vector3 DashDirection;

    // 初始化
    public virtual void Initialize(EnemyDefinition def, float inStatMultiplier);

    // 受伤害
    public virtual void ReceiveDamage(float damage, bool isCrit = false);

    // 死亡
    public virtual void OnDeath();

    // 碰撞逻辑
    protected virtual void OnTriggerEnter(Collider other);

    // 移动逻辑
    protected virtual void Update();
    protected virtual void MoveTowardsPlayer(float deltaTime);

    // 获取玩家位置
    protected Vector3 GetPlayerLocation();

    // 获取实际属性
    protected float GetEffectiveHP();
    protected float GetEffectiveMoveSpeed();
    protected float GetEffectiveContactDamage();
    protected float GetEffectiveXP();
}
```

**具体敌人子类:**

```csharp
// SlimeEnemy: 追踪移动，无特殊技能
public class SlimeEnemy : Enemy { }

// SkeletonEnemy: 追踪移动，速度中等
public class SkeletonEnemy : Enemy { }

// BatEnemy: 追踪移动，快速低血量
public class BatEnemy : Enemy { }

// ShadowMageEnemy: 站桩，远程攻击
public class ShadowMageEnemy : Enemy
{
    protected override void Update();
    private void FireProjectile();
    private float RangedAttackCooldown;
}

// GhostEnemy: 追踪移动 + 周期冲刺
public class GhostEnemy : Enemy
{
    protected override void Update();
}
```

### 5.9 EnemyProjectile — 敌人弹幕

```csharp
public class EnemyProjectile : MonoBehaviour
{
    public float Damage;
    public Vector3 FlyDirection;
    public float Speed;
    public PlayerController TargetPlayer;

    private void Update();
    private void OnTriggerEnter(Collider other);
}
```

### 5.10 XPOrb — 经验球

```csharp
public class XPOrb : MonoBehaviour
{
    [Header("属性")]
    public float ExpValue;

    [Header("组件")]
    private Collider _collider;
    private Renderer _meshRenderer;

    [Header("状态")]
    private bool _isMagnetizing;
    private const float MagnetSpeed = 6f;

    private void Update();
    private void TryMagnet();

    private void OnTriggerEnter(Collider other);
}
```

**Update 逻辑:**

```
if _isMagnetizing:
    向玩家位置移动
else:
    计算与玩家距离
    if 距离 <= 玩家有效拾取范围:
        _isMagnetizing = true
```

### 5.11 MonsterSpawner — 怪物生成器

```csharp
public class MonsterSpawner : MonoBehaviour
{
    [Header("生成区域")]
    public float ArenaRadius = 30f;
    public LayerMask SpawnAreaMask;

    [Header("配置")]
    private WaveManager _waveManager;
    private float _spawnTimer;

    // 敌人预制体列表 (通过 Inspector 绑定)
    public List<Enemy> EnemyPrefabs;

    private void Update();
    private void SpawnEnemy();
    private Enemy PickEnemyType();
    private Vector3 GetSpawnPosition();
    private float GetSpawnInterval();
}
```

### 5.12 WaveManager — 波次管理器

```csharp
public class WaveManager : MonoBehaviour
{
    [Header("配置")]
    public float WaveInterval = 30f;      // 每波持续时间
    public float RestBetweenWaves = 3f;   // 波次间休息时间

    [Header("状态")]
    public int CurrentWave;
    public float WaveTimer;
    public bool IsWaveActive;
    public float RestTimer;

    // 事件
    public System.Action<int> OnWaveChanged;

    public void StartGame();

    public float GetEnemyStatMultiplier();
    public float GetXPWaveMultiplier();
    public bool IsWaveActiveFunc();

    private void Update();
    private void StartNextWave();
    private void EndWave();
}
```

**波次时间线示例:**

| 时间 | 事件 |
|------|------|
| 0:00 | 游戏开始，波次 1 开始 |
| 0:30 | 波次 1 结束，休息 3 秒 |
| 0:33 | 波次 2 开始 |
| 1:03 | 波次 2 结束，休息 3 秒 |
| 1:06 | 波次 3 开始 (解锁骷髅) |
| ... | ... |

### 5.13 GameManager — 游戏管理器

```csharp
public class GameManager : MonoBehaviour
{
    [Header("预制体")]
    public PlayerController PlayerPrefab;
    public GameObject LevelUpUIPrefab;
    public GameObject MainHUDUIPrefab;

    [Header("管理器")]
    public WaveManager WaveManager;
    public MonsterSpawner MonsterSpawner;

    [Header("UI")]
    private GameObject _levelUpUIInstance;
    private GameObject _mainHUDInstance;
    private GameObject _gameOverUIInstance;

    // 数据表
    private List<UpgradeOption> _upgradeOptions;
    private List<WeaponDefinition> _weapons;

    // 暂停/恢复
    public void PauseGame();
    public void ResumeGame();

    // 游戏结束
    public void GameOver();

    // 获取升级选项池
    public List<UpgradeOption> GenerateUpgradeOptions(int count, int remainingWeaponSlots);

    private void Start();
    private void BindPlayerEvents();
}
```

### 5.14 LevelUpUI — 升级选择界面

```csharp
public class LevelUpUI : MonoBehaviour
{
    // 三个选项按钮
    public Button[] OptionButtons;
    public Text[] OptionNames;
    public Text[] OptionDescriptions;
    public Image[] OptionIcons;

    // 当前选项数据
    private List<UpgradeOption> _currentOptions;

    // 显示升级界面
    public void ShowOptions(List<UpgradeOption> options);

    // 选项选中回调
    private void OnOptionSelected(int index);

    // 关闭
    public void Close();
}
```

**升级选项生成算法:**

```
输入: Count (恒为3), RemainingWeaponSlots

1. 从 UpgradeOptions 加载所有可用的 UpgradeOption
2. 筛选 MinLevelToAppear <= 玩家当前等级
3. 加载所有可用武器
4. 筛选 MinWaveToAppear <= 当前波次 且 玩家尚未持有
5. 武器选项数量 = Clamp(Random(1, Count), 1, RemainingWeaponSlots)
6. 数值选项数量 = Count - 武器选项数量
7. 从武器池随机选武器选项数量个
8. 从数值池随机选数值选项数量个
9. 随机打乱顺序返回
```

### 5.15 MainHUD — 游戏 HUD

```csharp
public class MainHUD : MonoBehaviour
{
    [Header("HP")]
    public Slider HPBar;
    public Text HPText;

    [Header("XP")]
    public Slider XPBar;
    public Text LevelText;

    [Header("波次")]
    public Text WaveText;
    public Text WaveTimerText;

    [Header("武器槽")]
    public Image[] WeaponSlots;

    // 每帧更新
    public void UpdateHP(float current, float max);
    public void UpdateXP(float current, float toNext, int level);
    public void UpdateWave(int waveNumber, float timeRemaining);
    public void UpdateWeaponSlots(List<Weapon> weapons);
}
```

---

## 6. 敌人详细数值

基础数值 × `EnemyStatMultiplier` = 最终属性

| 敌人 | 波次解锁 | BaseHP | MoveSpeed | ContactDamage | BaseXP | 特点 |
|------|---------|--------|-----------|---------------|--------|------|
| 史莱姆 Slime | 1 | 12 | 2 | 1 | 3 | 慢速肉盾 |
| 骷髅 Skeleton | 3 | 20 | 3.5 | 2 | 5 | 标准近战 |
| 蝙蝠 Bat | 5 | 8 | 5 | 1 | 2 | 快速脆皮群怪 |
| 暗影法师 ShadowMage | 7 | 15 | 0 | 0 | 8 | 远程站桩, 发射弹幕 2s/次, 弹幕伤害 3 |
| 幽灵 Ghost | 9 | 10 | 3 | 2 | 6 | 3s 冲刺一次, 冲刺速度 12, 持续 0.3s |

---

## 7. 升级选项详细数值

### 7.1 数值类选项

| UpgradeType | 显示名 | 描述模板 | Value 区间 | 随机逻辑 |
|-------------|--------|---------|-----------|----------|
| MaxHP_Add | 生命强化 | +{Value} 最大生命值 | {2, 3, 4, 5} | 范围内随机取一 |
| MaxHP_Mul | 生命增幅 | +{Value}% 最大生命值 | {10, 15, 20, 25} | 范围内随机取一 |
| Damage_All_Mul | 伤害增幅 | +{Value}% 所有伤害 | {5, 8, 10, 15} | 范围内随机取一 |
| AttackSpeed_All_Mul | 急速 | +{Value}% 攻击速度 | {5, 8, 10, 15} | 范围内随机取一 |
| MoveSpeed_Add | 敏捷 | +{Value} 移动速度 | {0.3, 0.5, 0.6, 0.8} | 范围内随机取一 |
| ExpRate_Mul | 领悟 | +{Value}% 经验获取 | {10, 15, 20, 30} | 范围内随机取一 |
| PickupRange_Add | 磁力 | +{Value} 拾取范围 | {0.5, 0.8, 1.0, 1.5} | 范围内随机取一 |
| CritChance_Add | 精准 | +{Value}% 暴击率 | {3, 5, 8} | 范围内随机取一 |
| CritDamage_Mul | 致命 | +{Value}% 暴击伤害 | {15, 20, 30} | 范围内随机取一 |

### 7.2 武器选项显示

每个武器选项显示:

| 显示项 | 示例 |
|--------|------|
| 名称 | 火焰喷射 |
| 简介 | 向前方扇形范围持续喷射火焰 |
| 伤害 | 伤害: 2/跳 |
| 频率 | 频率: 0.15s |
| 特殊 | [扇形 30° / 射程 4] |

---

## 8. 游戏完整流程

```
GameManager.Start()
├── 创建 WaveManager
├── 创建 MonsterSpawner (绑定 WaveManager)
├── 创建 MainHUD
├── 生成玩家 PlayerController
│   ├── StatsComponent 初始化 (BaseMaxHP=10, ...)
│   ├── Camera 初始化
│   ├── 生成初始武器 MagicBulletWeapon
│   └── 绑定输入
└── WaveManager.StartGame()
    └── StartNextWave() → 波次 1 开始

游戏循环 (每帧 Update):
├── WaveManager.Update()
│   └── 波次倒计时 / 休息倒计时 → 波次切换 → 广播事件
├── MonsterSpawner.Update()
│   └── 波次活跃时按 SpawnRate 生成怪物
├── PlayerController.Update()
│   └── 输入处理 → 移动
├── 每个 Weapon.Update()
│   ├── 冷却递减
│   ├── FindTarget()
│   └── 冷却就绪 → Fire()
├── 每个 Enemy.Update()
│   ├── MoveTowardsPlayer()
│   └── 特殊技能
├── 每个 Projectile.Update()
│   └── 飞行 → 命中判定
├── XPOrb 检查磁吸
└── HUD 更新

怪物死亡:
├── Enemy.OnDeath()
├── 生成 XPOrb (ExpValue = Enemy.GetEffectiveXP())
└── 销毁 Enemy

经验球被拾取:
├── XPOrb.OnTriggerEnter(Player)
├── StatsComponent.AddXP(ExpValue * ExpMultiplier)
├── 累积 XP >= XP_ToNextLevel?
│   ├── 是 → LevelUp()
│   │   ├── 增加 CurrentLevel
│   │   ├── 重置 CurrentXP (溢出保留)
│   │   └── GameManager.PauseGame() + 弹出升级界面
│   └── 否 → 继续
└── 销毁 XPOrb

升级选择:
├── GameManager.GenerateUpgradeOptions(3, RemainingSlots)
├── LevelUpUI.ShowOptions()
├── 玩家点击某选项
├── OnOptionSelected()
│   ├── 武器: PlayerController.AddWeapon()
│   └── 数值: StatsComponent.ApplyUpgrade()
├── GameManager.ResumeGame()
└── 隐藏 LevelUpUI

玩家死亡:
├── PlayerController.OnDeath()
├── GameManager.GameOver()
│   ├── 显示结算 (存活波次、等级)
│   └── 停止所有生成
```

---

## 9. ScriptableObject 资产清单

需在编辑器中创建的 ScriptableObject:

| 资产类型 | 说明 |
|---------|------|
| WeaponDefinition | 所有武器配置 (Create > Game > Weapon Definition) |
| EnemyDefinition | 所有敌人配置 (Create > Game > Enemy Definition) |
| UpgradeOption | 所有数值升级选项 (Create > Game > Upgrade Option) |

---

## 10. 竞技场规格

| 属性 | 值 |
|------|-----|
| 形状 | 圆形 |
| 半径 | 30 Unity 单位 |
| 边界 | 不可通过 (碰撞墙) |
| 怪物生成位置 | 边界外随机点 |
| 玩家起始位置 | 圆心 (0, 0, 0) |

---

## 11. 后续扩展规划

### 11.1 新武器

| 武器 | 类型 | 特点 |
|------|------|------|
| 冰锥 | 穿透弹丸 | 直线穿透所有敌人，单次伤害递减 |
| 闪电链 | 连锁弹丸 | 命中后连锁至附近 N 个敌人 |
| 毒雾 | 地面 AoE | 在命中点生成持续伤害区域 |

### 11.2 新敌人类型

| 敌人 | 特点 |
|------|------|
| 精英怪 | 大体积/高血量/低概率出现/掉落大量 XP |
| 召唤师 | 周期性召唤小怪 |
| 自爆怪 | 靠近玩家后爆炸造成高伤害 |

### 11.3 新系统

| 系统 | 说明 |
|------|------|
| 武器进化 | 多个武器组合进化为更强武器 |
| 多角色 | 不同角色有不同初始武器和基础属性 |
| 精英/Boss 波次 | 每 N 波出现 Boss/精英波 |
| 武器专属升级 | 某个武器自身的伤害/攻速/范围加成选项 |

---

## 12. 关键设计原则

- **从简优先**: 所有系统第一版保持最小可行设计，保留扩展接口 (virtual / ScriptableObject / 枚举扩展)
- **数据驱动**: 武器、敌人、升级数值全部走 ScriptableObject，方便编辑器内调数值
- **Component 分离**: 玩家属性独立为 `PlayerStatsComponent`，武器和敌人逻辑各自内聚
- **无魔法数字**: 所有可调数值暴露为 `[Header]` 或 Inspector 可编辑字段
- **C# 为主**: 所有游戏逻辑在 C# 层实现，Editor 脚本仅用于编辑器扩展