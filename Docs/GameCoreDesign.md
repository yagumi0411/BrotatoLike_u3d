# 游戏核心开发文档 — BrotatoLike

> **面向受众**: 程序员 & AI 实现者
> **用途**: 当前实现说明与后续扩展参考
> **更新日期**: 2026-10-12；数值以现有场景和配置资产为准。
> **主题**: 魔法风格 Top-Down 无限波次生存游戏
> **引擎**: Unity 6.5 (6000.5.4f1) (C# 实现)

---

## 1. 项目概述

### 1.1 游戏类型

Top-down 2.5D 动作生存游戏。玩家控制一名法师，在竞技场内对抗无限波次的怪物。武器自动瞄准攻击，击杀怪物掉落经验球，升级时暂停游戏并从 3 个选项中选 1 个强化。

### 1.2 核心循环

```
主菜单分配天赋 → 开始对局 → 战斗 / 升级 → 波末商店 → 手动下一波
死亡 / 重开 / 返回菜单 → 结算局外经验 → 跨局保存 → 主菜单继续成长
```

击杀直接获得金币，经验球仍需拾取。结束对局记录波次、等级、击杀和战斗时间，并按有效对局规则结算独立天赋经验；即时退出不产生最低奖励。

### 1.3 技术架构

| 层级 | 技术 |
|------|------|
| 引擎 | Unity 6.5 (6000.5.4f1) |
| 语言 | C# |
| 数据驱动 | ScriptableObject 驱动武器/敌人/升级配置 |
| 架构模式 | MonoBehaviour 组件化 + OOP（未采用 Unity Entities ECS） |

### 1.4 后续玩法方向（规划，尚未实现）

后续以发现、搭配和收集 combo 为核心：保留原材料并增加联动效果，
通过升级选择激活组合，首次选择后永久点亮图鉴。
图鉴初始显示黑色剪影，鼠标悬停只显示组合所需材料，不展示效果。
首批组合名单、配方与效果尚未确定。
完整规则及待定内容见 [武器联动与组合图鉴设计](ComboAndCollectionDesign.md)。

### 1.5 金币与波次间商店（第一版）

击杀怪物直接获得金币，波次结束暂停战斗并打开商店。
当前仅支持购买治疗或继续下一波；素材商品与跨局限额存款暂缓。
详细规则、默认数值及验证方式见 [金币与波次间商店](GoldAndWaveShopDesign.md)。

### 1.6 局外成长与法师天赋图

有效对局结束获得独立的天赋经验，逐步换取可分配天赋点。主菜单的天赋图以相邻连通规则分配，
首片区包含伤害、范围、生存路线及交汇节点，部分节点多级，支持免费重置并最终全部点满。
死亡、重开、返回菜单均按本局表现结算一次，分配结果跨局保存，下一局开始时生效。
节点、连线、经验规则和效果 ID 使用独立配置，为后续片区及 mod 内容注册预留入口。
完整规则和验证记录见 [局外成长与法师天赋图](MetaProgressionDesign.md)。

---

## 2. 核心数值公式

### 2.1 伤害计算

```csharp
RawDamage = Weapon.BaseDamage * Stats.GetEffectiveDamageMultiplier()

if Random.value < EffectiveCritChance:
    FinalDamage = RawDamage * (1.5f + Stats.CritDamageMultiplierBonus)
else:
    FinalDamage = RawDamage
```

- `Weapon.BaseDamage`: 武器基础伤害，每个武器类型独立配置
- `Stats.GetEffectiveDamageMultiplier()`: 1 + 局内全局伤害加成 + 本局天赋伤害快照；不重复修改武器资产。
- `EffectiveCritChance`: 局内与天赋暴击加成之和，限制在 0 至 60%；配置 5 表示 5%，累加值为 0.05。
- `Stats.CritDamageMultiplierBonus`: 由 `CritDamage_Mul` 类升级累加（如 0.0 → 0.15 → 0.35）
- 暴击基础倍率 1.5，即暴击时至少造成 150% 伤害

### 2.2 经验值与升级

```csharp
g = Mathf.Max(0, CurrentLevel - 1)
XP_ToNextLevel(CurrentLevel) = 24 + 14 * g + 3 * g * g

EffectiveXP = Enemy.BaseXP * (1.0f + Stats.ExpGainMultiplier) * WaveXPMultiplier
```

| 当前等级 | 升到下一级所需 XP |
|----------|-------------------|
| 1 | 24 |
| 2 | 41 |
| 3 | 64 |
| 5 | 128 |
| 10 | 393 |
| N | 24 + 14 × (N - 1) + 3 × (N - 1)² |

```csharp
WaveXPMultiplier = 1.0f + Mathf.Min((WaveNumber - 1) * 0.04f, 0.4f)
```

### 2.3 波次缩放

```csharp
WaveInterval = 45 秒（波末商店暂停，手动继续下一波）

EnemyStatMultiplier = 1.0f + (WaveNumber - 1) * 0.035f
SpawnRate = Mathf.Min(1.0f + (WaveNumber - 1) * 0.12f, 4.0f)
WaveXPMultiplier = 1.0f + Mathf.Min((WaveNumber - 1) * 0.04f, 0.4f)
```

波次 1 = 基准，刷怪率上限是基础值；启用自适应难度时另乘其刷怪倍率。经验倍率最高 1.4。
玩家初始生命 14、移速 5；骷髅、蝙蝠、暗影法师、幽灵分别从第 3、4、6、8 波出现。

### 2.4 玩家有效属性（运行时计算）

以下为计算表达式。Talent 表示开始对局时传入的 TalentBonuses 快照，局内升级不覆盖该快照。

```text
EffectiveMaxHP = (BaseMaxHP + FlatHPBonus + Talent.MaxHP) * (1 + PercentHPBonus)
EffectiveMoveSpeed = BaseMoveSpeed + FlatMoveSpeedBonus
EffectivePickupRadius = BasePickupRadius + FlatPickupRangeBonus + Talent.PickupRadius
EffectiveDamageMultiplier = 1 + GlobalDamageMultiplier + Talent.Damage
EffectiveAttackSpeedMultiplier = 1 + GlobalAttackSpeedMultiplier
EffectiveExpMultiplier = 1 + ExpGainMultiplier
EffectiveCritChance = Clamp(CritChanceBonus + Talent.CritChance, 0, 0.6)
EffectiveCritDamage = 1.5 + CritDamageMultiplierBonus
EffectiveWeaponRangeMultiplier = Max(0.1, 1 + Talent.WeaponRange)
DamageTaken = Max(0, IncomingDamage) * (1 - Clamp(Talent.DamageReduction, 0, 0.75))
```

基础生命 14、移速 5、拾取半径 3。免费重置与重新分配只改变局外进度，下一局重新生成属性快照。

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

### 4.4 TalentBonuses — 本局天赋快照

`PlayerStatsComponent` 管理局内升级；`MetaProgression.GetBonuses` 汇总天赋后，在重置玩家时通过 `SetTalentBonuses` 应用一次。

```csharp
public class TalentBonuses
{
    public float Damage;
    public float WeaponRange;
    public float MaxHP;
    public float DamageReduction;
    public float PickupRadius;
    public float CritChance;
}
```

节点、连线及成长规则定义在 `TalentGraph.cs`，实例内容在 `MageTalentGraph.json`；不以临时 PlayerStats 结构替代当前玩家组件。

---

## 5. 核心类设计

本节为职责与接口摘要，省略完整实现；具体成员、签名与生命周期以 Assets/Scripts 源码为准。

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
    public bool AddWeapon(WeaponDefinition def);
    public bool IsWeaponSlotFull();
    public int GetRemainingWeaponSlots();
    public void ApplyUpgrade(UpgradeOption option);

    // 受伤
    // 伤害与生命由 StatsComponent.TakeDamage 处理

    // 初始化武器
    public void SpawnStartingWeapon();

    // 死亡
    public void OnPlayerDeath();
}
```

### 5.2 PlayerStatsComponent — 玩家属性组件

继承自 `MonoBehaviour`，挂载在玩家上。

```csharp
public class PlayerStatsComponent : MonoBehaviour
{
    [Header("基础属性 (可在 Inspector 覆写)")]
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

    // === 计算有效值 ===
    public float GetEffectiveMaxHP();
    public float GetEffectiveMoveSpeed();
    public float GetEffectivePickupRadius();
    public float GetEffectiveDamageMultiplier();
    public float GetEffectiveAttackSpeedMultiplier();
    public float GetEffectiveExpMultiplier();
    public float GetEffectiveCritChance();
    public float GetEffectiveCritDamage();
    public float GetEffectiveWeaponRangeMultiplier();
    public void SetTalentBonuses(TalentBonuses bonuses);

    // === 升级 ===
    public void ApplyUpgrade(UpgradeOption option);
    public float GetXPToNextLevel();
    public void AddXP(float amount);

    // === 事件 ===
    public System.Action<float, float> OnHPChanged;
    public System.Action<float, float, int> OnXPChanged;
    public System.Action OnLevelUp;
    public System.Action OnDeath;

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

    // 执行攻击
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
    protected override void Fire(Enemy target);
    protected override Enemy FindTarget();
    private void SpawnBullet(Vector3 targetPos, float damage, bool isCrit);
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
2. 调用 `SpawnBullet` 生成投射物
3. 投射物飞向目标位置（非追踪，发射即确定方向）

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
| BaseDamage | 2.5 |
| AttackInterval | 0.4s |
| Range | 1.5 (环绕半径) |
| OrbitCount | 3 |
| OrbitRadius | 1.5 |
| OrbitSpeed | 180 (度/秒，即 2 秒一圈) |
| MinWaveToAppear | 0 (初始可用) |

### 5.7 投射物 — 现有独立组件

当前没有统一的 Projectile 抽象基类，投射物分别继承 MonoBehaviour：

| 组件 | 当前行为 |
|---|---|
| MagicBulletProjectile | 初始化伤害、暴击、方向、速度和寿命；飞行与触发命中，寿命结束或命中后回池 |
| OrbitProjectile | 按武器初始化环绕半径与速度；触发碰撞造成伤害，内部冷却 0.5 秒 |
| FlameProjectile | 定义在 FlameThrowerWeapon.cs，承担纯视觉飞行；真实火焰伤害由武器扇形检测执行 |

三维 Collider / Rigidbody 机制保留，命中须满足 Playing 状态。魔法弹寿命和环绕半径在创建时应用本局天赋范围倍率。

环绕伤害和暴击仍是初始化快照，因此本局之后选择的伤害升级不会动态更新已经生成的环绕弹；局外天赋在武器生成前应用，能正确进入该快照。这是当前实现限制，不是本轮新增功能。

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
    protected float GetEffectiveMoveSpeed();  // 移速仅受 30% 波次缩放，防止后期速度反超玩家
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
    public float ArenaRadius = 28f; // SampleScene 序列化覆盖为 40
    public float MinSpawnDistance = 13f;
    public float SpawnMargin = 3f;
    public List<EnemyDefinition> EnemyPrefabs;
    public float BaseSpawnRate = 1f;
    public float SpawnRateGrowthPerWave = 0.12f;
    public float MaxSpawnRate = 4f;

    public float GetCurrentSpawnInterval();
    public void ClearAllEnemies();
    public void ResetSpawner();
}
```

仅在 Playing 且波次活跃时刷怪，按配置解锁敌人，通过 EnemyPool 创建或复用。
生成点按随机方向计算相机视野外下限与地图圆边界上限，最多尝试 8 次；边缘无合法方向时有兜底点，不等同于固定 [13, 20] 环带。

### 5.12 WaveManager — 波次管理器

```csharp
public class WaveManager : MonoBehaviour
{
    public float WaveInterval = 45f;
    public float RestBetweenWaves = 3f; // 兼容原字段；SampleScene 为 1，商店期间不计时
    public int CurrentWave { get; private set; }
    public float WaveTimer { get; private set; }
    public bool IsWaveActive { get; private set; }
    public float RestTimer { get; private set; }
    public event System.Action<int> OnWaveChanged;

    public void StartGame();
    public void StartNextWave();
    public void EndWave();
    public void ResetGame();
    public float GetEnemyStatMultiplier();
    public float GetXPWaveMultiplier();
}
```

| 累计战斗时间 | 事件 |
|---|---|
| 0 秒 | 第 1 波，史莱姆可出现 |
| 45 秒 | 第 1 波完成，暂停进入商店 |
| 点击继续 | 第 2 波，下一轮 45 秒 |
| 90 秒及之后点击继续 | 第 3 波，骷髅可出现 |
| 第 4/6/8 波 | 蝙蝠、暗影法师、幽灵依次可出现 |

时间不含商店、暂停与升级选择，不能据此给出固定墙钟解锁时间。

### 5.13 GameManager — 对局协调

状态为 MainMenu、Playing、Paused、LevelUp、GameOver、Shop。Awake 查找场景引用及载入局外存档，Start 进入主菜单；不在 Start 自动开局。

主要接口：

| 接口 | 职责 |
|---|---|
| StartGame / RestartGame / ReturnToMainMenu | 开局、重开、返回；重开与返回先结算，再清理局内状态 |
| PauseGame / ResumeGame | 状态切换，有待选升级时先处理队列 |
| GameOver | 结算天赋奖励、清空待选升级、展示结果 |
| AddKill / AddGold | 记录击杀与直接发放金币 |
| OpenWaveShop / BuyHealing / ContinueFromShop | 波末商店、资格校验、下一波 |
| OnPlayerLevelUp / OnUpgradeSelected | 连续升级排队，拒绝过期选择 |
| Progression / TalentUI / ShopUI | 局外服务及运行时 UI 引用 |

当一局开始时生成唯一 ID，结束时通过 MetaProgression 记录并去重。ResetPlayer 分别重置局内加成并应用天赋快照，不修改基础资产。

### 5.14 LevelUpUI — 升级选择

使用 Panel、Button[]、TextMeshProUGUI[]。ShowOptions 展示候选，Hide 关闭；点击后向玩家应用强化并通知 GameManager 处理下一项待选升级。

生成候选的实际规则：

1. 从 UpgradeOptionPool 筛选满足 MinLevelToAppear 的选项。
2. 武器类还需有剩余槽、WeaponDef 非空、达到 MinWaveToAppear，且尚未持有同类型。
3. 对统一候选列表洗牌，返回最多 3 项；不强制“至少一个武器”或固定武器比例。
4. 连续升级保留当前候选，选择完成后再生成下一组；死亡时清空待选队列。

### 5.15 MainHUD — 游戏 HUD

HPBar、HPText、XPBar、LevelText、WaveText、WaveTimerText 展示局内属性。
Rebind 在初次启用和重开时解绑旧引用再绑定玩家/波次事件，避免重复订阅。
金币文字在运行时创建；金币变化和波次倒计时在 Update 中更新，不能把全部 HUD 行为描述为仅由事件驱动。

### 5.16 WaveShopUI — 像素装备面板商店

固定布局在 SampleScene 的现有 Canvas 下通过编辑器搭建，三个商品卡片复用 HealingCard Prefab；不再运行时创建布局。
WaveShopUI 通过 SerializeField 绑定界面，Awake 绑定事件，Refresh 更新价格、生命、金币和购买状态；规则仍来自 GameManager。
购买按钮支持悬停及键盘选中提示，点击调用 BuyHealing；继续按钮在 Inspector 中绑定 ContinueFromShop。
另两个栏位为禁用的预留卡片，隐藏药水图标，未实现素材或存款商品。配置说明见 [商店 UI 编辑器维护指南](UIEditorGuide.md)。

### 5.17 局外成长 — 天赋图与存档

TalentGraph 管理连接与配置校验，TalentEffectRegistry 注册效果处理器，
MetaProgression 管理经验换点、资格校验、重置及存档，TalentGraphUI 仅展示与操作。

法师首片区为 18 个可分配节点、33 级，伤害/范围/生存路线通过交汇节点连通。
相邻节点 1 级即可解锁后续，不要求点满；支持免费重置并最终全部点满。
配置、存档兼容与 mod 接入边界见 [天赋文档](MetaProgressionDesign.md)。

---

## 6. 敌人详细数值

生命与接触伤害受 EnemyStatMultiplier 缩放，Adaptive 模式再应用有限修正；移速只承受 30% 的波次增量；经验使用独立的波次经验倍率。下表为基础配置。

| 敌人 | 波次解锁 | BaseHP | MoveSpeed | ContactDamage | BaseXP | 特点 |
|------|---------|--------|-----------|---------------|--------|------|
| 史莱姆 Slime | 1 | 7 | 1.5 | 1 | 2 | 基础慢速近战 |
| 骷髅 Skeleton | 3 | 12 | 2.7 | 1.5 | 4 | 标准近战 |
| 蝙蝠 Bat | 4 | 5 | 3.6 | 1 | 2 | 快速低血量近战 |
| 暗影法师 ShadowMage | 6 | 12 | 0 | 0 | 6 | 站桩远程，弹幕 3 秒/次、伤害 3、速度 6 |
| 幽灵 Ghost | 8 | 7 | 2.2 | 2 | 5 | 冲刺冷却 4 秒、速度 9、持续 0.3 秒 |

---

## 7. 升级选项详细数值

### 7.1 数值类选项

| UpgradeType | 显示名 | 描述模板 | Value (固定) | 说明 |
|-------------|--------|---------|-----------|----------|
| MaxHP_Add | 生命强化 | +{Value} 最大生命值 | 3 | 每次升级固定 +3 最大生命 |
| MaxHP_Mul | 生命增幅 | +{Value}% 最大生命值 | 15 | 每次升级固定 +15% 最大生命 |
| Damage_All_Mul | 伤害增幅 | +{Value}% 所有伤害 | 10 | 每次升级固定 +10% 伤害 |
| AttackSpeed_All_Mul | 急速 | +{Value}% 攻击速度 | 10 | 每次升级固定 +10% 攻速 |
| MoveSpeed_Add | 敏捷 | +{Value} 移动速度 | 0.3 | 每次升级固定 +0.3 移速 |
| ExpRate_Mul | 领悟 | +{Value}% 经验获取 | 10 | 每次升级固定 +10% 经验 |
| PickupRange_Add | 磁力 | +{Value} 拾取范围 | 0.8 | 每次升级固定 +0.8 拾取范围 |
| CritChance_Add | 精准 | +{Value}% 暴击率 | 5 | 每次升级固定 +5% 暴击率 (上限 60%) |
| CritDamage_Mul | 致命 | +{Value}% 暴击伤害 | 20 | 每次升级固定 +20% 暴击伤害 |

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

```text
启动
├── 初始化场景引用、局外存档、对象池与事件
└── MainMenu（暂停，尚未开波）

点击开始
├── 重置局内等级、经验、金币、属性与玩家位置
├── 应用天赋快照，创建初始武器
└── Playing → 第 1 波

Playing
├── 战斗计时 / 波次倒计时
├── 刷怪 → 自动攻击 → 命中与伤害
├── 击杀：记录击杀、生成经验球、直接加金币、敌人回池
└── 经验吸收一次后回池 → 连续升级进入待选队列

LevelUp
├── 保留当前选项，逐个选择并应用
└── 队列清空 → Playing

波次结束
├── 记录一波完成 → Shop，冻结战斗对象
├── 买治疗或跳过
└── 点击继续 → 下一波

死亡 / 重开 / 返回菜单
├── 按对局 ID 结算一次天赋经验，保存并换取天赋点
├── 死亡：GameOver 显示统计与奖励，清空升级队列
└── 重开 / 返回：清理战斗对象并重置局内状态

MainMenu 天赋图
├── 连通节点投入点数 / 多级强化 / 免费重置
├── 立即保存经验、点数和节点稳定 ID
└── 下一局生效
```

实际战斗时间仅在 Playing 累计。暂停中的拾取、伤害及投射物碰撞不能继续产生战斗结果。天赋经验与局内经验、金币相互独立。

---

## 9. ScriptableObject 资产清单

现有 ScriptableObject 位于 Assets/Data；新增内容沿用以下资产类型：

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
| 刷怪地图圆半径 | 40 Unity 单位（SampleScene；代码默认 28） |
| 边界 | 暂无物理边界（规划中） |
| 怪物生成位置 | 按方向计算视野外下限（兜底 13、边距 3）与地图圆边界上限；8 次尝试后使用兜底点 |
| 玩家起始位置 | 启动时记录场景玩家位置，重开时恢复 |
| 表现 | 正交相机、像素精灵、石砖网格与装饰柱；保留 Ground 碰撞 |

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
| 组合联动与图鉴 | 保留材料并增加联动，首次选择永久点亮；图鉴悬停只显示材料，具体组合待定 |
| 外围天赋与角色 | 法师首片区已实现，武器特色节点与其他角色待扩展 |
| 素材与存款 | 波次商店的材料商品与限额跨局存款，未实现 |
| mod / 创意工坊 | 已有天赋内容注册入口，外部文件发现与平台 SDK 未接入 |
| 多角色 | 不同角色有不同初始武器和基础属性 |
| 精英/Boss 波次 | 每 N 波出现 Boss/精英波 |
| 武器专属升级 | 某个武器自身的伤害/攻速/范围加成选项 |

---

## 12. 关键设计原则

- **从简优先**: 所有系统第一版保持最小可行设计，保留扩展接口 (virtual / ScriptableObject / 枚举扩展)
- **数据驱动**: 武器、敌人、局内升级使用 ScriptableObject；天赋节点、连线和成长规则使用 JSON 配置。
- **Component 分离**: 玩家属性独立为 `PlayerStatsComponent`，武器和敌人逻辑各自内聚
- **扩展边界**: 节点、连通校验、效果处理和存档分离；内容使用稳定 ID 与命名空间，兼容缺失内容。
- **C# 为主**: 所有游戏逻辑在 C# 层实现，Editor 脚本仅用于编辑器扩展
