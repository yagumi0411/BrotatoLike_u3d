# 游戏核心开发文档 — BrotatoLike

> **面向受众**: 程序员 & AI 实现者
> **用途**: 面试 Demo 实现规范
> **主题**: 魔法风格 Top-Down 无限波次生存游戏
> **引擎**: Unity 6.5 (6000.5.4f1) (C# 实现)

---

## 1. 项目概述

### 1.1 游戏类型

Top-down 2.5D 动作生存游戏。玩家控制一名法师，在封闭竞技场内对抗无限波次的怪物。武器自动瞄准攻击，击杀怪物掉落经验球，升级时从 3 个选项中选 1 个强化（单人模式暂停选择；双人模式升级不暂停，升级者本人短暂无敌）。

### 1.2 核心循环

```
波次推进 → 怪物生成 → 武器自动攻击 → 击杀掉落经验 → 拾取经验升级 → 选强化（单人暂停 / 双人升级者短暂无敌，游戏继续）
```

直到玩家死亡，记录存活波次和等级。

### 1.3 技术架构

| 层级 | 技术 |
|------|------|
| 引擎 | Unity 6.5 (6000.5.4f1) |
| 语言 | C# |
| 数据驱动 | ScriptableObject 驱动武器/敌人/升级配置 |
| 架构模式 | 组件化 OOP + 数据驱动（无 ECS） |

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
XP_ToNextLevel(CurrentLevel) = 8 * (CurrentLevel + 1)

EffectiveXP = Enemy.BaseXP * (1.0f + Stats.ExpGainMultiplier) * WaveXPMultiplier
```

（示例：Lv.1→2 需 16 XP，Lv.5→6 需 48 XP，Lv.10→11 需 88 XP）

```csharp
WaveXPMultiplier = 1.0f + (WaveNumber - 1) * 0.1f
```

### 2.3 波次缩放

```csharp
WaveInterval = 20 秒（波次间休息 1 秒）  // 代码默认 30s/3s，场景覆盖为 20s/1s

EnemyStatMultiplier = 1.06^(WaveNumber - 1)  // 温和指数增长，前期平缓后期陡峭
SpawnRate = 2.0f + WaveNumber * 0.4f  // 每秒生成怪物数
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

### 4.4 玩家属性

玩家属性由 `PlayerStatsComponent`（挂载在玩家 GameObject 上的 MonoBehaviour）持有，字段与 §2.4 公式一一对应：基础值（BaseMaxHP/BaseMoveSpeed/BasePickupRadius）+ 升级累加值 + 运行时状态（CurrentHP/CurrentXP/CurrentLevel）。详见 §5.2。

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
    public bool AddWeapon(WeaponDefinition weaponDef);   // 按类型运行时挂载武器组件
    public bool IsWeaponSlotFull();
    public int GetRemainingWeaponSlots();
    public void ApplyUpgrade(UpgradeOption option);
    public void SpawnStartingWeapon();   // 初始武器（public，供 GameManager 重置调用）

    // 死亡（由 StatsComponent.OnDeath 事件触发）
    public void OnPlayerDeath();

    // 本地控制权（联机）：仅 Owner 客户端读输入，远端由 NetworkTransform 驱动
    public bool IsLocallyControlled = true;
    // 升级中：暂停自身输入与武器攻击（双人"升级不暂停"）
    public bool IsChoosingUpgrade { get; set; }
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
    // 初始武器定义在 PlayerController.StartingWeaponPrefab（不在本组件）

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
    public System.Action<PlayerStatsComponent> OnLevelUp;  // 带 sender，双人模式定位升级者
    public System.Action OnDeath;

    // === 升级无敌（双人"升级不暂停"玩法） ===
    public bool IsInvincible { get; private set; }
    public void BeginLevelUpState();   // 进入升级：3 秒无敌（LevelUpInvincibleDuration）
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
    public virtual void Initialize(PlayerController owner, WeaponDefinition def);

    // 每帧调用
    protected virtual void Update();

    // 获取实际攻击间隔
    protected float GetEffectiveAttackInterval();

    // === 子类重写 ===
    // 索敌
    protected virtual Enemy FindTarget();

    // 执行攻击（子类必须实现）
    protected abstract void Fire(Enemy target);

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
    // 索敌由基类 Weapon.FindTarget 统一实现（注册表 + 缓存目标 + 蓄水池采样）
    protected override void Fire(Enemy target);   // 从静态 ObjectPool 取子弹并 Initialize
}
```

**FindTarget 逻辑:**

1. 优先复用缓存目标（存活且在射程内则直接返回，零扫描）
2. 缓存失效时按 0.2s 限频扫描 `EnemyRegistry`（出生/死亡 O(1) 维护的活敌人表，零 GC 分配）
3. 手写循环 + `sqrMagnitude` 筛选距离 ≤ `WeaponDef.Range` 的敌人
4. Nearest: 返回距离最近的；Random: 蓄水池采样单遍均匀随机
5. 无有效目标返回 `null`

**Fire 逻辑:**

1. 获取目标当前位置
2. 从静态 `ObjectPool` 取子弹（消除运行时 Instantiate/Destroy）
3. 子弹飞向目标位置（非追踪，发射即确定方向），寿命到期/命中回池

**配置数据 (ScriptableObject 预设):**

| 属性 | 值 |
|------|-----|
| Type | MagicBullet |
| Name | 魔法弹 |
| Description | 向最近的敌人发射一枚魔法弹丸 |
| BaseDamage | 7 |
| AttackInterval | 0.6s |
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

    // 伤害检测：Physics.OverlapSphere + 扇形角度过滤（非索敌，直接对范围内敌人结算）
    // 伤害冷却：_lastDamageTimeMap 字典（isActiveAndEnabled 检查防池化泄漏）
    // 火焰粒子：对象池复用（约 40 颗/秒），纯视觉不再检测伤害
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
| BaseDamage | 2.5 (每跳) |
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

    // 环绕飞弹 GameObject 列表（静态池复用）
    private List<OrbitProjectile> OrbitProjectiles = new List<OrbitProjectile>();

    // 生成环绕飞弹（初始化/重置时调用）
    // 注意：命中冷却为 OrbitProjectile 硬编码 0.5s，WeaponDef.AttackInterval 对环绕武器不生效（遗留项）
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
| BaseDamage | 2.5 |
| AttackInterval | 0.4s |
| Range | 1.5 (环绕半径) |
| OrbitCount | 3 |
| OrbitRadius | 1.5 |
| OrbitSpeed | 180 (度/秒，即 2 秒一圈) |
| MinWaveToAppear | 0 (初始可用) |

### 5.7 MagicBulletProjectile / OrbitProjectile — 投射物（两个独立类，无共同基类）

```csharp
// 魔法弹投射物：直线飞行，命中敌人或寿命到期回池
public class MagicBulletProjectile : MonoBehaviour
{
    private float _damage;
    private bool _isCrit;
    private Vector3 _direction;
    private float _speed;
    private float _lifetimeRemaining;

    public void Initialize(float damage, bool isCrit, Vector3 dir, float speed, float lifetime);
    private void Update();                // 飞行 + 寿命倒计时，到期回池
    private void OnTriggerEnter(Collider other);   // 命中敌人 → Enemy.ReceiveDamage → 回池
}

// 环绕飞弹：绕玩家旋转，碰撞敌人造成伤害（命中冷却硬编码 0.5s）
public class OrbitProjectile : MonoBehaviour
{
    private float _damage;
    private float _radius;
    private float _currentAngle;

    public void SetOrbit(float damage, float radius, float angle);
    private void Update();                // 按中心玩家位置计算环绕坐标
    private void OnTriggerEnter(Collider other);
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

    // 组件（私有，SetupCollider 运行时创建：CapsuleCollider + Kinematic Rigidbody）
    private Collider _collider;
    private Rigidbody _rb;

    // 移动（冲刺状态在 GhostEnemy 子类）
    protected Vector3 MoveDirection;

    // 初始化（public；生成时锁定最近玩家为目标 OwnerPlayer，双人各打各的）
    public virtual void Initialize(EnemyDefinition def, float statMultiplier);

    // 受伤害（暴击倍率取 OwnerPlayer 属性）
    public virtual void ReceiveDamage(float damage, bool isCrit = false);

    // 死亡（通知击杀 + 生成经验球 + 回池 EnemyPool.Release）
    public virtual void OnDeath();

    // 移动逻辑（Rigidbody.linearVelocity 物理移动，防重叠排斥）
    protected virtual void Update();
    protected virtual void MoveTowardsPlayer();
    private void CheckContactDamage();   // 距离检测 + 0.5s 冷却（非碰撞事件）

    // 获取锁定目标位置
    protected Vector3 GetTargetLocation();

    // 获取实际属性
    protected float GetEffectiveHP();
    protected float GetEffectiveMoveSpeed();  // 移速仅受 30% 波次缩放，防止后期速度反超玩家
    protected float GetEffectiveContactDamage();
    protected float GetEffectiveXP();
}
```

**具体敌人子类:**

```csharp
// SlimeEnemy: 追踪移动，无特殊技能（重写 CreateVisual 用 VisualHelper 拼装模型）
// SkeletonEnemy: 追踪移动，速度中等
// BatEnemy: 追踪移动，快速低血量
// ShadowMageEnemy: 站桩，远程攻击（TryRangedAttack 发射 EnemyProjectile 弹幕）
// GhostEnemy: 追踪移动 + 周期冲刺（Initialize 预置冲刺冷却 + StartDash 冲刺）
```

### 5.9 EnemyProjectile — 敌人弹幕

```csharp
public class EnemyProjectile : MonoBehaviour
{
    public float Damage;
    public Vector3 Direction;   // 发射即确定方向
    public float Speed;

    public void Initialize(float damage, Vector3 dir, float speed, float lifetime);
    private void Update();      // 直线飞行 + 寿命销毁
    private void OnTriggerEnter(Collider other);   // 命中任一玩家造成伤害
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

    private void Update();      // 磁吸逻辑内联：找最近玩家（GetNearestPlayer），距离 ≤ 拾取范围进入磁吸
    private void OnTriggerEnter(Collider other);

    // 静态对象池（Pool.Release 回收替代 Destroy；OnEnable 重置磁吸状态）
    private static ObjectPool Pool;
    public static void Spawn(Vector3 position, float xpValue);
    public static void ClearAllOrbs();
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
    public float ArenaRadius = 28f;      // 代码默认 28，场景覆盖为 40
    public float MinSpawnDistance = 13f; // 兜底最小生成距离
    public float SpawnMargin = 3f;       // 视野外余量

    [Header("配置")]
    private WaveManager _waveManager;
    private float _spawnTimer;

    // 敌人定义列表（Inspector 绑定）
    public List<EnemyDefinition> EnemyPrefabs;

    private void Update();
    private void SpawnEnemy();           // EnemyPool.Get 按类型取（分池复用）
    private Vector3 GetSpawnPosition();  // 视口四边形方向相关环带（见下）
    private float GetSpawnInterval();    // 1 / (2.0 + Wave * 0.4)
    public void ClearAllEnemies();       // 走注册表清场
    public void ResetSpawner();

    // 刷怪环带：方向均匀随机 360°；下限 = 该方向穿出屏幕视口四边形距离 + SpawnMargin；
    // 上限 = 地图圆边界（射线-圆求交正根）；空间不足换方向（最多 8 次）+ 两级兜底
    private Vector2[] GetViewQuad();     // 屏幕四角视线与玩家水平面求交（相机缺失回退 MinSpawnDistance）
    private float GetViewDistance(Vector2 pos2D, Vector2 dir, Vector2[] quad);  // 射线-线段求交（克拉默法则）
    private float GetMaxSpawnRadius(Vector3 playerPos, Vector3 dir);
}
```

### 5.12 WaveManager — 波次管理器

```csharp
public class WaveManager : MonoBehaviour
{
    [Header("配置")]
    public float WaveInterval = 20f;      // 每波持续时间
    public float RestBetweenWaves = 1f;   // 波次间休息时间

    [Header("状态")]
    public int CurrentWave;
    public float WaveTimer;
    public bool IsWaveActive;
    public float RestTimer;

    // 事件
    public System.Action<int> OnWaveChanged;

    public void StartGame();
    public void ResetGame();

    public float GetEnemyStatMultiplier();  // 1.06^(Wave-1)
    public float GetXPWaveMultiplier();
    public bool IsWaveActive { get; }       // 属性（原设计 IsWaveActiveFunc 未实现）

    private void Update();
    private void StartNextWave();
    private void EndWave();
}
```

**波次时间线示例:**

| 时间 | 事件 |
|------|------|
| 0:00 | 游戏开始，波次 1 开始 |
| 0:20 | 波次 1 结束，休息 1 秒 |
| 0:21 | 波次 2 开始 (解锁骷髅) |
| 0:41 | 波次 2 结束，休息 1 秒 |
| 0:42 | 波次 3 开始 (解锁蝙蝠) |
| ... | ... |

### 5.13 GameManager — 游戏管理器（全局单例 + 状态机）

```csharp
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 游戏状态机
    public enum EGameState { MainMenu, Playing, Paused, LevelUp, GameOver }
    public EGameState CurrentState { get; private set; }

    // 游戏模式（主菜单 Toggle 选择）
    public enum EGameMode { Single, LocalCoop, Online }
    public EGameMode GameMode = EGameMode.LocalCoop;

    // 玩家（多玩家支持）
    public List<PlayerController> Players;          // 单机=1，同屏双人=2，联机=按连接数
    public PlayerController Player { get; }         // 计算属性：首个激活玩家（兼容旧引用）
    public void RegisterPlayer(PlayerController player);    // 幂等，绑定升级事件
    public PlayerController GetNearestPlayer(Vector3 pos);  // 最近玩家（索敌/磁吸用）

    // UI 引用（场景 Inspector 拖拽，非运行时创建）
    public MainMenuUI MainMenuUI; public PauseUI PauseUI; public GameOverUI GameOverUI;
    public MainHUD MainHUD; public LevelUpUI LevelUpUI;

    // 暂停/恢复/重开/回主菜单（SetState 统一管理 UI 显隐 + timeScale）
    public void StartGame();
    public void PauseGame();
    public void ResumeGame();
    public void RestartGame();
    public void ReturnToMainMenu();
    public void GameOver();

    // 升级（双人"升级不暂停"：仅升级者停操作 + 3 秒无敌）
    public void OnPlayerLevelUp(PlayerStatsComponent stats);
    public List<UpgradeOption> GenerateUpgradeOptions(PlayerController player, int count, int remainingWeaponSlots);

    // 同屏双人：克隆场景玩家生成玩家 2（方向键 + 头顶血条 + 材质区分）
    private void EnsureLocalPlayerTwo();
    // 联机：隐藏场景玩家 → 联机面板 → 网络玩家生成后 OnLocalPlayerReady 开战
    private void StartOnlineGame();
    public void OnLocalPlayerReady(PlayerController player);
}
```

### 5.14 LevelUpUI — 升级选择界面

```csharp
public class LevelUpUI : MonoBehaviour
{
    // 三个选项按钮（TextMeshProUGUI 文字）
    public Button[] OptionButtons;
    public TextMeshProUGUI[] OptionNames;
    public TextMeshProUGUI[] OptionDescriptions;

    // 当前选项数据 + 升级者（双人面板复用）
    private List<UpgradeOption> _currentOptions;
    private PlayerController _upgradingPlayer;

    // 显示升级界面（绑定升级者本人）
    public void ShowOptions(PlayerController player, List<UpgradeOption> options);

    // 选项选中回调 → 应用升级 + 结束升级
    private void OnOptionSelected(int index);

    // 结束升级：恢复玩家操作并隐藏面板（GameManager 面板复用时也调用）
    public void EndLevelUp();
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

    // 事件驱动更新（非轮询），绑定 Players[0]（主玩家）
    public void UpdateHP(float current, float max);
    public void UpdateXP(float current, float toNext, int level);
    public void UpdateWave(int wave);
    public void Rebind();   // 重新绑定（重开/联机玩家生成后，订阅 GameManager.OnPlayerRegistered）
}
```

---

## 6. 敌人详细数值

基础数值 × `EnemyStatMultiplier` = 最终属性

| 敌人 | 波次解锁 | BaseHP | MoveSpeed | ContactDamage | BaseXP | 特点 |
|------|---------|--------|-----------|---------------|--------|------|
| 史莱姆 Slime | 1 | 8 | 2 | 1 | 3 | 慢速肉盾, 2 发魔法弹击杀 |
| 骷髅 Skeleton | 2 | 14 | 3.5 | 2 | 5 | 标准近战, 2 发击杀 |
| 蝙蝠 Bat | 3 | 5 | 4.5 | 1 | 2 | 快速脆皮群怪, 1 发击杀 |
| 暗影法师 ShadowMage | 5 | 12 | 0 | 0 | 8 | 远程站桩, 发射弹幕 2s/次, 弹幕伤害 3 |
| 幽灵 Ghost | 7 | 7 | 2.5 | 2 | 6 | 3s 冲刺一次, 冲刺速度 12, 持续 0.3s |

---

## 7. 升级选项详细数值

### 7.1 数值类选项

| UpgradeType | 显示名 | 描述模板 | Value (固定) | 说明 |
|-------------|--------|---------|-----------|----------|
| MaxHP_Add | 生命强化 | +{Value} 最大生命值 | 3 | 每次升级固定 +3 最大生命 |
| MaxHP_Mul | 生命增幅 | +{Value}% 最大生命值 | 15 | 每次升级固定 +15% 最大生命 |
| Damage_All_Mul | 伤害增幅 | +{Value}% 所有伤害 | 10 | 每次升级固定 +10% 伤害 |
| AttackSpeed_All_Mul | 急速 | +{Value}% 攻击速度 | 10 | 每次升级固定 +10% 攻速 |
| MoveSpeed_Add | 敏捷 | +{Value} 移动速度 | 0.5 | 每次升级固定 +0.5 移速 |
| ExpRate_Mul | 领悟 | +{Value}% 经验获取 | 15 | 每次升级固定 +15% 经验 |
| PickupRange_Add | 磁力 | +{Value} 拾取范围 | 0.8 | 每次升级固定 +0.8 拾取范围 |
| CritChance_Add | 精准 | +{Value}% 暴击率 | 5 | 每次升级固定 +5% 暴击率 (上限 60%) |
| CritDamage_Mul | 致命 | +{Value}% 暴击伤害 | 20 | 每次升级固定 +20% 暴击伤害 |

### 7.2 武器选项显示

每个武器选项显示（`LevelUpUI.GetDescription`）:

| 显示项 | 说明 |
|--------|------|
| 名称 | 升级选项资产名称（武器 asset 的 Name/Description 为空，显示文案来自 UpgradeOption 资产） |
| 简介 | 升级选项资产 Description |
| 伤害 | 伤害: {BaseDamage}（魔法弹 7 / 火焰 2.5 / 飞弹 2.5） |
| 攻速 | 攻速: {AttackInterval}s |

---

## 8. 游戏完整流程

```
GameManager.Awake()
├── 查找场景中已有的 WaveManager / MonsterSpawner / Player（场景预置，非运行时创建）
├── 注册场景玩家 → EnsureLocalPlayerTwo()（双人模式克隆玩家 2）
└── 查找 UI 引用（MainHUD / LevelUpUI）

主菜单点击"开始游戏" → GameManager.StartGame()
├── 按模式调整玩家（ApplyGameMode：单人停用 P2 / 本地双人激活 / 联机走网络流程）
├── ResetPlayer()（所有玩家回出生点 + 重置属性 + 重新生成初始武器）
├── SetState(Playing) → MainHUD.Rebind() → WaveManager.StartGame()
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
├── GameManager.AddKill()（击杀计数）
├── 生成 XPOrb (ExpValue = Enemy.GetEffectiveXP())
└── EnemyPool.Release（回池复用，非 Destroy）

经验球被拾取:
├── XPOrb 磁吸最近玩家 → Absorb()
├── StatsComponent.AddXP(ExpValue * ExpMultiplier)
├── 累积 XP >= XP_ToNextLevel?
│   ├── 是 → 升级循环（CurrentLevel++ → OnLevelUp(stats)）
│   │   ├── GameManager.OnPlayerLevelUp(升级者)
│   │   ├── 升级者 IsChoosingUpgrade=true + 3 秒无敌（游戏不暂停）
│   │   └── LevelUpUI.ShowOptions(升级者, 3 选项)
│   └── 否 → 继续
└── XPOrb.Pool.Release（回池）

升级选择:
├── GameManager.GenerateUpgradeOptions(升级者, 3, RemainingSlots)
├── 玩家点击某选项 → OnOptionSelected()
│   ├── 武器: PlayerController.AddWeapon()
│   └── 数值: StatsComponent.ApplyUpgrade()
└── LevelUpUI.EndLevelUp()（恢复操作 + 隐藏面板）

玩家死亡:
├── PlayerController.OnPlayerDeath()（由 StatsComponent.OnDeath 事件触发）
├── GameManager.GameOver() → SetState(GameOver)
│   ├── 显示结算 (存活波次、等级、击杀数、存活时间)
│   └── 关闭升级面板（防双人模式另一玩家升级中时残留 UI）
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
| 半径 | 40 Unity 单位（场景配置，代码默认 28） |
| 边界 | 暂无物理边界（规划中） |
| 怪物生成位置 | 视口四边形方向相关环带（下限 = 该方向穿出屏幕距离 + SpawnMargin 3，上限 = 地图圆边界，360° 均匀来怪） |
| 玩家起始位置 | 圆心 (0, 1.15, 0) |

---

## 11. 后续扩展规划

> 更新（2026-08-17）：以下规划中，**同屏双人与联机框架已实现**（见 §13）。本节其余项仍为规划。

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

## 12. 多玩家与联机（2026-08 新增）

### 12.1 同屏双人（LocalCoop）

- 入口：主菜单 3 个互斥 Toggle 选"本地双人"，点"开始游戏"后生效（ToggleGroup 管互斥与高亮，编辑器配置）
- 实现：`GameManager.Awake` 克隆场景玩家生成玩家 2——方向键输入（`ArrowsInputProvider`）、头顶血条（`PlayerStatusBar`，世界空间 UI）、独立材质
- 输入抽象：`IInputProvider`（MoveInput + AimDirection）三源复用——键盘鼠标 / 方向键 / 网络
- 敌人：生成时锁定最近玩家为目标（各打各的）；经验球磁吸最近玩家
- 升级不暂停：升级者 `IsChoosingUpgrade`（停操作/停武器）+ 3 秒无敌，另一玩家照常战斗

### 12.2 联机双人（Online，骨架）

- 技术栈：NGO 2.13.1（Unity 6.5 兼容版本，2.3.0 存在 CS0619 废弃 API 报错）+ UnityTransport（局域网）
- `PlayerNetworkBehaviour`：OnNetworkSpawn 按 `IsOwner` 区分本地/远端——本地读输入可战斗并触发开战（`GameManager.OnLocalPlayerReady`），远端由 `NetworkTransform`（AuthorityMode=Owner）驱动位置、武器停火、不参与本地索敌
- 联机面板：`MainMenuUI` 运行时动态创建（IP 输入 + 创建房间/加入房间）
- 权威模型：移动=客户端权威；伤害/击杀/升级=服务器权威（阶段 3 实施）；升级选项服务器生成 + RPC 广播（规避随机种子不同步）
- 阶段 3 未开工：玩法同步（波次/刷怪/敌人/伤害统一）待实施

---

## 13. 关键设计原则

- **从简优先**: 所有系统第一版保持最小可行设计，保留扩展接口 (virtual / ScriptableObject / 枚举扩展)
- **数据驱动**: 武器、敌人、升级数值全部走 ScriptableObject，方便编辑器内调数值
- **Component 分离**: 玩家属性独立为 `PlayerStatsComponent`，武器和敌人逻辑各自内聚
- **无魔法数字**: 所有可调数值暴露为 `[Header]` 或 Inspector 可编辑字段
- **C# 为主**: 所有游戏逻辑在 C# 层实现，Editor 脚本仅用于编辑器扩展