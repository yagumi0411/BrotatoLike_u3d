# 游戏核心开发文档 — BrotatoLike

> **面向受众**: 程序员 & AI 实现者  
> **用途**: 面试 Demo 实现规范  
> **主题**: 魔法风格 Top-Down 无限波次生存游戏  
> **引擎**: Unreal Engine 5.7 (C++ 实现)

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
| 引擎 | UE 5.7 |
| 语言 | C++ (原 Blueprint 逻辑全部迁移) |
| 数据驱动 | UDataTable 驱动武器/敌人/升级配置 |
| 架构模式 | Actor + Component 分离 |

---

## 2. 核心数值公式

### 2.1 伤害计算

```
RawDamage = Weapon.BaseDamage × (1.0 + Stats.GlobalDamageMultiplier)

if Random() < EffectiveCritChance:
    FinalDamage = RawDamage × (1.5 + Stats.CritDamageMultiplierBonus)
else:
    FinalDamage = RawDamage
```

- `Weapon.BaseDamage`: 武器基础伤害，每个武器类型独立配置
- `Stats.GlobalDamageMultiplier`: 由 `Damage_All_Mul` 类升级累加（如 0.0 → 0.1 → 0.2）
- `EffectiveCritChance`: 暴击率 = Clamp(Stats.CritChanceBonus, 0.0, 0.6)
- `Stats.CritDamageMultiplierBonus`: 由 `CritDamage_Mul` 类升级累加（如 0.0 → 0.15 → 0.35）
- 暴击基础倍率 1.5，即暴击时至少造成 150% 伤害

### 2.2 经验值与升级

```
XP_ToNextLevel(CurrentLevel) = 10 × (CurrentLevel + 1)

EffectiveXP = Enemy.BaseXP × (1.0 + Stats.ExpGainMultiplier) × WaveXPMultiplier
```

| 当前等级 | 升到下一级所需 XP |
|----------|-------------------|
| 1 | 20 |
| 2 | 30 |
| 3 | 40 |
| 5 | 60 |
| 10 | 110 |
| N | 10 × (N + 1) |

```
WaveXPMultiplier = 1.0 + (WaveNumber - 1) × 0.1
```

### 2.3 波次缩放

```
WaveInterval = 30 秒

EnemyStatMultiplier = 1.0 + (WaveNumber - 1) × 0.15
SpawnRate = 2.0 + WaveNumber × 0.5  (每秒生成怪物数)
WaveXPMultiplier = 1.0 + (WaveNumber - 1) × 0.1
```

波次 1 = 基准，波次越高敌人越强、越多、经验也越多（但经验增速慢于血量增速 → 后期更难）。

### 2.4 玩家有效属性（运行时计算）

```
EffectiveMaxHP       = (BaseMaxHP + FlatHPBonus) × (1.0 + PercentHPBonus)
EffectiveMoveSpeed   = BaseMoveSpeed + FlatMoveSpeedBonus
EffectivePickupRadius = BasePickupRadius + FlatPickupRangeBonus
EffectiveDamageMultiplier = 1.0 + GlobalDamageMultiplier
EffectiveAttackSpeedMultiplier = 1.0 + GlobalAttackSpeedMultiplier
EffectiveExpMultiplier = 1.0 + ExpGainMultiplier
EffectiveCritChance  = Clamp(CritChanceBonus, 0.0, 0.6)
EffectiveCritDamage  = 1.5 + CritDamageMultiplierBonus
```

---

## 3. 核心枚举定义

```cpp
// 升级选项类型
UENUM(BlueprintType)
enum class EUpgradeType : uint8
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
};

// 武器类型
UENUM(BlueprintType)
enum class EWeaponType : uint8
{
    MagicBullet,    // 魔法弹 (初始武器)
    FlameThrower,   // 火焰喷射
    SpellOrbit,     // 飞弹环绕
    // 后续扩展:
    // IceSpike,    // 冰锥穿透
    // LightningChain, // 闪电链
    // PoisonCloud  // 毒雾
};

// 敌人类型
UENUM(BlueprintType)
enum class EEnemyType : uint8
{
    Slime,       // 史莱姆 - 慢速高血量近战
    Skeleton,    // 骷髅 - 中速中血量近战
    Bat,         // 蝙蝠 - 快速低血量近战
    ShadowMage,  // 暗影法师 - 远程站桩
    Ghost        // 幽灵 - 中速冲刺
};

// 武器索敌模式
UENUM(BlueprintType)
enum class ETargetMode : uint8
{
    Nearest,   // 范围内最近的敌人
    Random     // 范围内随机敌人
};
```

---

## 4. 核心数据结构

### 4.1 FWeaponDefinition — 武器配置

```cpp
USTRUCT(BlueprintType)
struct FWeaponDefinition
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    EWeaponType Type;

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    FText Name;                // 显示名称

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    FText Description;         // 升级界面的简介文本

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float BaseDamage;          // 基础伤害

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float AttackInterval;      // 攻击间隔 (秒)

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float Range;               // 索敌范围 (UE 单位)

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    ETargetMode TargetMode;    // 索敌模式

    // --- 投射物武器专用 ---
    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float ProjectileSpeed;     // 弹丸速度

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float ProjectileLifetime;  // 弹丸存活时间 (秒)

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    int32 ProjectileCount;     // 每次攻击弹丸数

    // --- 区域/扇形武器专用 ---
    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float ConeHalfAngle;       // 扇形半角 (度), 火焰喷射用

    // --- 环绕武器专用 ---
    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    int32 OrbitCount;          // 环绕飞弹数量

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float OrbitRadius;         // 环绕半径

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float OrbitSpeed;          // 环绕速度 (度/秒)

    // --- 解锁条件 ---
    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    int32 MinWaveToAppear;     // 最低出现波次 (0 = 无限制)
};
```

### 4.2 FEnemyDefinition — 敌人配置

```cpp
USTRUCT(BlueprintType)
struct FEnemyDefinition
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    EEnemyType Type;

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    FText Name;

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float BaseHP;

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float MoveSpeed;

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float ContactDamage;       // 接触碰撞时对玩家造成的伤害

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float BaseXP;              // 击杀后掉落的经验值

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float CollisionRadius;     // 碰撞半径

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float MeshScale;           // 模型缩放

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    bool bIsRanged;            // 是否远程敌人

    UPROPERTY(EditAnywhere, BlueprintReadOnly, meta=(EditCondition="bIsRanged"))
    float RangedAttackInterval; // 远程攻击间隔

    UPROPERTY(EditAnywhere, BlueprintReadOnly, meta=(EditCondition="bIsRanged"))
    float ProjectileDamage;    // 远程弹幕伤害

    UPROPERTY(EditAnywhere, BlueprintReadOnly, meta=(EditCondition="bIsRanged"))
    float ProjectileSpeed;     // 远程弹幕速度

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    bool bCanDash;             // 是否有冲刺技能

    UPROPERTY(EditAnywhere, BlueprintReadOnly, meta=(EditCondition="bCanDash"))
    float DashCooldown;        // 冲刺冷却

    UPROPERTY(EditAnywhere, BlueprintReadOnly, meta=(EditCondition="bCanDash"))
    float DashSpeed;           // 冲刺速度

    UPROPERTY(EditAnywhere, BlueprintReadOnly, meta=(EditCondition="bCanDash"))
    float DashDuration;        // 冲刺持续时间

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    int32 MinWaveToSpawn;      // 最早出现的波次
};
```

### 4.3 FUpgradeOption — 升级选项

```cpp
USTRUCT(BlueprintType)
struct FUpgradeOption
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    EUpgradeType Type;

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    FText Name;                // 选项显示名

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    FText Description;         // 选项描述

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    float Value;               // 数值 (非武器类用)

    UPROPERTY(EditAnywhere, BlueprintReadOnly, meta=(EditCondition="Type==EUpgradeType::Weapon"))
    TSubclassOf<class AWeapon> WeaponClass;  // 武器类 (武器类用)

    UPROPERTY(EditAnywhere, BlueprintReadOnly)
    int32 MinLevelToAppear;    // 最低出现等级 (0 = 无限制)
};
```

### 4.4 FPlayerStats — 玩家属性快照（存档/UI 读取用）

```cpp
USTRUCT(BlueprintType)
struct FPlayerStats
{
    GENERATED_BODY()

    // 基础值 (角色决定)
    UPROPERTY(BlueprintReadOnly)
    float BaseMaxHP;

    UPROPERTY(BlueprintReadOnly)
    float BaseMoveSpeed;

    UPROPERTY(BlueprintReadOnly)
    float BasePickupRadius;

    // 升级累加值
    UPROPERTY(BlueprintReadOnly)
    float FlatHPBonus;         // MaxHP_Add 累加

    UPROPERTY(BlueprintReadOnly)
    float PercentHPBonus;      // MaxHP_Mul 累加 (0.0~)

    UPROPERTY(BlueprintReadOnly)
    float GlobalDamageMultiplier;   // Damage_All_Mul 累加

    UPROPERTY(BlueprintReadOnly)
    float GlobalAttackSpeedMultiplier; // AttackSpeed_All_Mul 累加

    UPROPERTY(BlueprintReadOnly)
    float FlatMoveSpeedBonus;  // MoveSpeed_Add 累加

    UPROPERTY(BlueprintReadOnly)
    float ExpGainMultiplier;   // ExpRate_Mul 累加

    UPROPERTY(BlueprintReadOnly)
    float FlatPickupRangeBonus; // PickupRange_Add 累加

    UPROPERTY(BlueprintReadOnly)
    float CritChanceBonus;     // CritChance_Add 累加

    UPROPERTY(BlueprintReadOnly)
    float CritDamageMultiplierBonus; // CritDamage_Mul 累加

    UPROPERTY(BlueprintReadOnly)
    int32 CurrentLevel;

    UPROPERTY(BlueprintReadOnly)
    int32 WeaponSlotCount;     // 已填充武器槽数
};
```

---

## 5. 核心类设计

### 5.1 AGameCharacter — 玩家角色

继承自 `ACharacter`。

```cpp
UCLASS()
class AGameCharacter : public ACharacter
{
    GENERATED_BODY()

public:
    AGameCharacter();

    // 组件
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly)
    class UPlayerStatsComponent* StatsComponent;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly)
    class USpringArmComponent* CameraBoom;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly)
    class UCameraComponent* FollowCamera;

    // 武器槽
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly)
    TArray<class AWeapon*> EquippedWeapons;

    static constexpr int32 MaxWeaponSlots = 6;

    // 方法
    UFUNCTION(BlueprintCallable)
    bool AddWeapon(TSubclassOf<AWeapon> WeaponClass);

    UFUNCTION(BlueprintCallable)
    bool IsWeaponSlotFull() const;

    UFUNCTION(BlueprintCallable)
    int32 GetRemainingWeaponSlots() const;

    UFUNCTION(BlueprintCallable)
    void ApplyUpgrade(const FUpgradeOption& Option);

    virtual float TakeDamage(float Damage, const FDamageEvent& DamageEvent,
        AController* EventInstigator, AActor* DamageCauser) override;

protected:
    virtual void BeginPlay() override;
    virtual void SetupPlayerInputComponent(UInputComponent* Input) override;

    // 移动输入
    void OnMoveForward(float Value);
    void OnMoveRight(float Value);

    // 死亡
    UFUNCTION()
    void OnDeath();

    // 初始化武器
    void SpawnStartingWeapon();
};
```

### 5.2 UPlayerStatsComponent — 玩家属性组件

`UActorComponent`，挂载在 `AGameCharacter` 上。

```cpp
UCLASS(ClassGroup=(Custom), meta=(BlueprintSpawnableComponent))
class UPlayerStatsComponent : public UActorComponent
{
    GENERATED_BODY()

public:
    // === 基础属性 (可在角色蓝图上覆写) ===
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Stats|Base")
    float BaseMaxHP = 10.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Stats|Base")
    float BaseMoveSpeed = 600.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Stats|Base")
    float BasePickupRadius = 300.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Stats|Base")
    TSubclassOf<AWeapon> StartingWeaponClass;

    // === 升级累加值 (运行时存储) ===
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Stats|Bonuses")
    float FlatHPBonus = 0.0f;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Stats|Bonuses")
    float PercentHPBonus = 0.0f;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Stats|Bonuses")
    float GlobalDamageMultiplier = 0.0f;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Stats|Bonuses")
    float GlobalAttackSpeedMultiplier = 0.0f;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Stats|Bonuses")
    float FlatMoveSpeedBonus = 0.0f;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Stats|Bonuses")
    float ExpGainMultiplier = 0.0f;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Stats|Bonuses")
    float FlatPickupRangeBonus = 0.0f;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Stats|Bonuses")
    float CritChanceBonus = 0.0f;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Stats|Bonuses")
    float CritDamageMultiplierBonus = 0.0f;

    // === 运行时状态 ===
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Stats|Runtime")
    float CurrentHP;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Stats|Runtime")
    float CurrentXP = 0.0f;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Stats|Runtime")
    int32 CurrentLevel = 1;

    // === 计算有效值 ===
    UFUNCTION(BlueprintCallable, Category="Stats")
    float GetEffectiveMaxHP() const;

    UFUNCTION(BlueprintCallable, Category="Stats")
    float GetEffectiveMoveSpeed() const;

    UFUNCTION(BlueprintCallable, Category="Stats")
    float GetEffectivePickupRadius() const;

    UFUNCTION(BlueprintCallable, Category="Stats")
    float GetEffectiveDamageMultiplier() const;

    UFUNCTION(BlueprintCallable, Category="Stats")
    float GetEffectiveAttackSpeedMultiplier() const;

    UFUNCTION(BlueprintCallable, Category="Stats")
    float GetEffectiveExpMultiplier() const;

    UFUNCTION(BlueprintCallable, Category="Stats")
    float GetEffectiveCritChance() const;

    UFUNCTION(BlueprintCallable, Category="Stats")
    float GetEffectiveCritDamage() const;

    // === 升级 ===
    UFUNCTION(BlueprintCallable, Category="Stats")
    void ApplyUpgrade(const FUpgradeOption& Option);

    UFUNCTION(BlueprintCallable, Category="Stats")
    float GetXPToNextLevel() const;

    UFUNCTION(BlueprintCallable, Category="Stats")
    void AddXP(float Amount);

    UFUNCTION(BlueprintCallable, Category="Stats")
    FPlayerStats GetStatsSnapshot() const;

protected:
    virtual void BeginPlay() override;

private:
    void LevelUp();
};
```

### 5.3 AWeapon — 武器基类

```cpp
UCLASS(Abstract)
class AWeapon : public AActor
{
    GENERATED_BODY()

public:
    AWeapon();

    // 配置
    UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Weapon")
    FWeaponDefinition WeaponDef;

    // 所属玩家
    UPROPERTY(BlueprintReadOnly, Category="Weapon")
    AGameCharacter* OwnerCharacter;

    // 初始化
    UFUNCTION(BlueprintCallable, Category="Weapon")
    void Initialize(AGameCharacter* InOwner);

    // 每帧调用 (由武器自行 Tick)
    virtual void Tick(float DeltaTime) override;

protected:
    // 攻击冷却
    float AttackCooldown = 0.0f;

    // 获取实际攻击间隔 (除以攻速加成)
    float GetEffectiveAttackInterval() const;

    // === 子类重写 ===
    // 索敌: 返回当前最佳目标
    UFUNCTION(BlueprintCallable, Category="Weapon|Targeting")
    virtual class AEnemy* FindTarget();

    // 执行攻击 (冷却归零时调用)
    virtual void Fire(AEnemy* Target);

    // 判断是否可以攻击
    virtual bool CanAttack() const;

    // 计算本次伤害
    float CalculateDamage() const;

    // 判定暴击
    bool RollCrit() const;

    // 获取范围
    float GetEffectiveRange() const;
};
```

**AWeapon::CalculateDamage 实现:**

```cpp
float AWeapon::CalculateDamage() const
{
    if (!OwnerCharacter || !OwnerCharacter->StatsComponent)
        return WeaponDef.BaseDamage;

    float Multiplier = OwnerCharacter->StatsComponent->GetEffectiveDamageMultiplier();
    return WeaponDef.BaseDamage * Multiplier;
}

bool AWeapon::RollCrit() const
{
    if (!OwnerCharacter || !OwnerCharacter->StatsComponent)
        return false;

    float CritChance = OwnerCharacter->StatsComponent->GetEffectiveCritChance();
    return FMath::FRand() < CritChance;
}

float AWeapon::GetEffectiveAttackInterval() const
{
    if (!OwnerCharacter || !OwnerCharacter->StatsComponent)
        return WeaponDef.AttackInterval;

    return WeaponDef.AttackInterval /
        OwnerCharacter->StatsComponent->GetEffectiveAttackSpeedMultiplier();
}

float AWeapon::GetEffectiveRange() const
{
    // 后续可扩展为: 武器 Range × 全局范围加成
    return WeaponDef.Range;
}
```

### 5.4 AMagicBulletWeapon — 魔法弹

继承 `AWeapon`，单目标跟踪弹丸。

```cpp
UCLASS()
class AMagicBulletWeapon : public AWeapon
{
    GENERATED_BODY()

public:
    virtual void Fire(AEnemy* Target) override;

protected:
    virtual AEnemy* FindTarget() override;

private:
    // 向目标发射一个 AMagicBulletProjectile
    void SpawnBullet(FVector TargetLocation, float Damage, bool bIsCrit);
};
```

**FindTarget 逻辑:**

1. 从 `OwnerCharacter` 位置获取范围内所有 `AEnemy`
2. 筛选距离 ≤ `WeaponDef.Range` 的敌人
3. 根据 `WeaponDef.TargetMode` 选择目标
4. Nearest: 返回距离最近的
5. 无有效目标返回 `nullptr`

**Fire 逻辑:**

1. 获取目标当前位置
2. 调用 `SpawnBullet` 生成 `AMagicBulletProjectile`
3. 弹丸飞向目标位置（非追踪，发射即确定方向）

**配置数据 (DataTable 预设):**

| 属性 | 值 |
|------|-----|
| Type | MagicBullet |
| Name | 魔法弹 |
| Description | 向最近的敌人发射一枚魔法弹丸 |
| BaseDamage | 5 |
| AttackInterval | 1.0s |
| Range | 1500 |
| TargetMode | Nearest |
| ProjectileSpeed | 800 |
| ProjectileLifetime | 2.0s |
| ProjectileCount | 1 |
| MinWaveToAppear | 0 (初始可用) |

### 5.5 AFlameThrowerWeapon — 火焰喷射

继承 `AWeapon`，前方扇形持续伤害。

```cpp
UCLASS()
class AFlameThrowerWeapon : public AWeapon
{
    GENERATED_BODY()

public:
    virtual void Tick(float DeltaTime) override;
    virtual void Fire(AEnemy* Target) override;

protected:
    virtual AEnemy* FindTarget() override;

private:
    // 获取扇形范围内所有敌人
    TArray<AEnemy*> GetEnemiesInCone() const;

    // 对每个锥形内敌人应用伤害 (每 DamageTick 间隔)
    float DamageTickTimer = 0.0f;

    // 上次受伤害的敌人记录 (避免同一敌人被重复伤害过快)
    TMap<AEnemy*, float> LastDamageTimeMap;
};
```

**Tick 逻辑:**

1. 每帧递减 `DamageTickTimer`
2. `DamageTickTimer` ≤ 0 时，获取锥形内敌人
3. 对每个敌人检查距离上次受伤害时间 ≥ `AttackInterval`
4. 满足条件 → 计算伤害 (含暴击判定) → 应用到敌人
5. 重置 `DamageTickTimer` = `AttackInterval`

**FindTarget:** 返回锥形内最近的敌人用于视觉朝向参考。

**配置数据 (DataTable 预设):**

| 属性 | 值 |
|------|-----|
| Type | FlameThrower |
| Name | 火焰喷射 |
| Description | 向前方扇形范围持续喷射火焰 |
| BaseDamage | 2 (每跳) |
| AttackInterval | 0.15s |
| Range | 400 |
| TargetMode | Nearest |
| ConeHalfAngle | 30° |
| ProjectileSpeed | 0 (不发射弹丸) |
| ProjectileLifetime | 0 |
| ProjectileCount | 0 |
| MinWaveToAppear | 0 (初始可用) |

### 5.6 ASpellOrbitWeapon — 飞弹环绕

继承 `AWeapon`，在玩家周围生成环绕飞弹，被动碰撞伤害。

```cpp
UCLASS()
class ASpellOrbitWeapon : public AWeapon
{
    GENERATED_BODY()

public:
    virtual void Tick(float DeltaTime) override;

    // 此武器无主动 Fire，环绕飞弹碰撞即伤害
    virtual void Fire(AEnemy* Target) override {} // 空实现

protected:
    virtual void BeginPlay() override;

private:
    // 环绕飞弹 Actor 数组
    UPROPERTY()
    TArray<class AOrbitProjectile*> OrbitProjectiles;

    // 生成环绕飞弹
    void SpawnOrbitProjectiles();

    // 更新飞弹位置 (绕玩家旋转)
    void UpdateOrbitPositions(float DeltaTime);
};
```

**BeginPlay 逻辑:**

1. 调用 `SpawnOrbitProjectiles` 生成 `WeaponDef.OrbitCount` 个 `AOrbitProjectile`
2. 均匀分布在圆周上 (360° / OrbitCount 间隔)

**Tick 逻辑:**

1. 调用 `UpdateOrbitPositions` 更新每个飞弹的环绕角度
2. 飞弹自身 Tick 检测与敌人的碰撞 (由 `AOrbitProjectile` 处理)

**配置数据 (DataTable 预设):**

| 属性 | 值 |
|------|-----|
| Type | SpellOrbit |
| Name | 飞弹环绕 |
| Description | 召唤魔法飞弹环绕自身，碰触敌人造成伤害 |
| BaseDamage | 3 |
| AttackInterval | 0.3s (同一敌人被同一飞弹击中冷却) |
| Range | 150 (环绕半径, 非索敌范围) |
| TargetMode | Nearest (不使用) |
| OrbitCount | 3 |
| OrbitRadius | 150 |
| OrbitSpeed | 180 (度/秒, 即 2 秒一圈) |
| MinWaveToAppear | 0 (初始可用) |

### 5.7 AProjectile / AMagicBulletProjectile / AOrbitProjectile — 弹丸

```cpp
// 弹丸基类
UCLASS()
class AProjectile : public AActor
{
    GENERATED_BODY()

public:
    AProjectile();

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly)
    class UProjectileMovementComponent* Movement;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly)
    class USphereComponent* Collision;

    UPROPERTY(BlueprintReadOnly)
    float Damage;

    UPROPERTY(BlueprintReadOnly)
    bool bIsCrit;

    UPROPERTY(BlueprintReadOnly)
    AGameCharacter* OwnerCharacter;

    // 初始化弹丸
    void Initialize(float InDamage, bool bCrit, AGameCharacter* InOwner,
        FVector Direction, float Speed, float Lifetime);

protected:
    UFUNCTION()
    virtual void OnProjectileHit(UPrimitiveComponent* HitComp, AActor* Other,
        UPrimitiveComponent* OtherComp, FVector NormalImpulse,
        const FHitResult& Hit);

    virtual void BeginPlay() override;

    float LifespanRemaining;
};

// 魔法弹弹丸: 直线飞行，命中敌人或到达存活时间后销毁
UCLASS()
class AMagicBulletProjectile : public AProjectile
{
    GENERATED_BODY()

protected:
    virtual void OnProjectileHit(...) override;
    // 命中敌人造成伤害，然后销毁
    // 命中其他 (墙壁等) 直接销毁
};

// 环绕飞弹: 绕玩家旋转，碰撞敌人造成伤害，有冷却
UCLASS()
class AOrbitProjectile : public AProjectile
{
    GENERATED_BODY()

public:
    void UpdateOrbitPosition(float Angle, float Radius, FVector Center);

protected:
    virtual void OnProjectileHit(...) override;

    // 每个敌人伤害冷却
    TMap<AEnemy*, float> EnemyHitCooldowns;

    float HitCooldownDuration;  // = OwnerWeapon.WeaponDef.AttackInterval
};
```

### 5.8 AEnemy — 敌人基类

```cpp
UCLASS(Abstract)
class AEnemy : public AActor
{
    GENERATED_BODY()

public:
    AEnemy();

    // 配置
    UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Enemy")
    FEnemyDefinition EnemyDef;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly)
    class UStaticMeshComponent* Mesh;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly)
    class UCapsuleComponent* Collision;

    // 运行时
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly)
    float CurrentHP;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly)
    float StatMultiplier;  // 波次缩放倍率，由 MonsterSpawner 设置

    // 初始化 (生成时调用)
    UFUNCTION(BlueprintCallable)
    void Initialize(const FEnemyDefinition& Def, float InStatMultiplier);

    // 受伤害
    UFUNCTION(BlueprintCallable)
    virtual void ReceiveDamage(float Damage, bool bIsCrit = false);

    // 死亡
    UFUNCTION(BlueprintCallable)
    virtual void OnDeath();

    // 对玩家造成接触伤害
    UFUNCTION()
    virtual void OnBeginOverlap(UPrimitiveComponent* Overlapped,
        AActor* Other, UPrimitiveComponent* OtherComp,
        int32 OtherBodyIndex, bool bFromSweep, const FHitResult& SweepResult);

protected:
    virtual void BeginPlay() override;
    virtual void Tick(float DeltaTime) override;

    // === 子类重写 ===
    // 移动逻辑
    virtual void MoveTowardsPlayer(float DeltaTime);

    // 获得玩家位置
    FVector GetPlayerLocation() const;

    // 是否在接触伤害冷却中
    bool bContactDamageOnCooldown = false;
    float ContactDamageCooldown = 0.5f;  // 防止每帧重复伤害
    FTimerHandle ContactCooldownTimer;

    // 获取实际属性 (基础 × 波次缩放)
    float GetEffectiveHP() const;
    float GetEffectiveMoveSpeed() const;
    float GetEffectiveContactDamage() const;
    float GetEffectiveXP() const;
};
```

**具体敌人子类:**

```cpp
// ASlimeEnemy: 追踪移动，无特殊技能
UCLASS()
class ASlimeEnemy : public AEnemy { };

// ASkeletonEnemy: 追踪移动，速度中等
UCLASS()
class ASkeletonEnemy : public AEnemy { };

// ABatEnemy: 追踪移动，快速低血量
UCLASS()
class ABatEnemy : public AEnemy { };

// AShadowMageEnemy: 站桩，远程攻击
UCLASS()
class AShadowMageEnemy : public AEnemy
{
    virtual void MoveTowardsPlayer(float DeltaTime) override; // 空实现，不移动
    virtual void Tick(float DeltaTime) override;
    // Tick: 距离玩家 ≤ Range 时，按 RangedAttackInterval 发射弹幕

private:
    float RangedAttackCooldown = 0.0f;
    void FireProjectile();
};

// AGhostEnemy: 追踪移动 + 周期冲刺
UCLASS()
class AGhostEnemy : public AEnemy
{
    virtual void Tick(float DeltaTime) override;
    // Tick: 追踪阶段 → DashCooldown 到 → 冲刺阶段 → 回到追踪

private:
    float DashCooldownRemaining;
    float DashDurationRemaining;
    FVector DashDirection;
    bool bIsDashing = false;
};
```

### 5.9 AEnemyProjectile — 敌人弹幕

```cpp
UCLASS()
class AEnemyProjectile : public AActor
{
    GENERATED_BODY()

public:
    UPROPERTY(VisibleAnywhere)
    class UProjectileMovementComponent* Movement;

    UPROPERTY(VisibleAnywhere)
    class USphereComponent* Collision;

    float Damage;
    FVector FlyDirection;

    void Initialize(float InDamage, FVector Direction, float Speed);

protected:
    UFUNCTION()
    void OnHit(UPrimitiveComponent* HitComp, AActor* Other,
        UPrimitiveComponent* OtherComp, FVector NormalImpulse,
        const FHitResult& Hit);
};
```

### 5.10 AXPOrb — 经验球

```cpp
UCLASS()
class AXPOrb : public AActor
{
    GENERATED_BODY()

public:
    AXPOrb();

    UPROPERTY(VisibleAnywhere)
    class USphereComponent* Collision;

    UPROPERTY(VisibleAnywhere)
    class UStaticMeshComponent* Mesh;

    UPROPERTY(BlueprintReadOnly)
    float ExpValue;

    void Initialize(float InExpValue);

protected:
    virtual void Tick(float DeltaTime) override;

    // 检查磁吸: 距离玩家 ≤ 拾取范围时向玩家移动
    void TryMagnet(float DeltaTime);

    UFUNCTION()
    void OnPlayerOverlap(UPrimitiveComponent* Overlapped, AActor* Other,
        UPrimitiveComponent* OtherComp, int32 OtherBodyIndex,
        bool bFromSweep, const FHitResult& SweepResult);

private:
    bool bIsMagnetizing = false;
    float MagnetSpeed = 600.0f;  // 磁吸飞行速度
};
```

**Tick 逻辑:**

```
if bIsMagnetizing:
    向玩家位置移动
else:
    计算与玩家距离
    if 距离 ≤ 玩家有效拾取范围:
        bIsMagnetizing = true
```

### 5.11 AMonsterSpawner — 怪物生成器

```cpp
UCLASS()
class AMonsterSpawner : public AActor
{
    GENERATED_BODY()

public:
    AMonsterSpawner();

    // 生成区域 (竞技场边界)
    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Spawn")
    float ArenaRadius = 3000.0f;

protected:
    virtual void BeginPlay() override;
    virtual void Tick(float DeltaTime) override;

private:
    // 波次管理器引用
    UPROPERTY()
    class AWaveManager* WaveManager;

    // 生成计时器
    float SpawnTimer = 0.0f;

    // 当前波的怪物生成已持续
    float WaveSpawnElapsed = 0.0f;

    // 生成一个怪物
    void SpawnEnemy();

    // 选择生成哪个怪物类型 (从可用池中随机)
    TSubclassOf<AEnemy> PickEnemyType();

    // 获取生成位置 (竞技场边缘随机点)
    FVector GetSpawnPosition();

    // 当前可用敌人类型
    TArray<TSubclassOf<AEnemy>> GetAvailableEnemyTypes() const;

    // 当前生成间隔
    float GetSpawnInterval() const;
};

// 每帧逻辑:
void AMonsterSpawner::Tick(float DeltaTime)
{
    if (!WaveManager || !WaveManager->IsWaveActive())
        return; // 波次间隙不生成

    SpawnTimer -= DeltaTime;
    if (SpawnTimer <= 0.0f)
    {
        SpawnEnemy();
        SpawnTimer = GetSpawnInterval();
    }
}

float AMonsterSpawner::GetSpawnInterval() const
{
    float SpawnRate = 2.0f + WaveManager->GetCurrentWave() * 0.5f;
    return 1.0f / SpawnRate; // 转换为间隔
}
```

### 5.12 AWaveManager — 波次管理器

```cpp
UCLASS()
class AWaveManager : public AActor
{
    GENERATED_BODY()

public:
    AWaveManager();

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Wave")
    float WaveInterval = 30.0f;  // 每波持续时间

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Wave")
    float RestBetweenWaves = 3.0f;  // 波次间休息时间

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Wave")
    int32 CurrentWave = 0;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Wave")
    float WaveTimer = 0.0f;

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Wave")
    bool bIsWaveActive = false;

    // 波次间休息
    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Wave")
    float RestTimer = 0.0f;

    UFUNCTION(BlueprintCallable)
    void StartGame();

    UFUNCTION(BlueprintCallable)
    float GetEnemyStatMultiplier() const;

    UFUNCTION(BlueprintCallable)
    float GetXPWaveMultiplier() const;

    UFUNCTION(BlueprintCallable)
    bool IsWaveActive() const { return bIsWaveActive; }

protected:
    virtual void Tick(float DeltaTime) override;

private:
    void StartNextWave();
    void EndWave();

    // 波次变更事件 (可广播给 UI 等)
    DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnWaveChanged, int32, NewWave);
    UPROPERTY(BlueprintAssignable)
    FOnWaveChanged OnWaveChanged;
};

// 每帧逻辑:
void AWaveManager::Tick(float DeltaTime)
{
    if (!bIsWaveActive)
    {
        // 波次休息阶段
        RestTimer -= DeltaTime;
        if (RestTimer <= 0.0f)
        {
            StartNextWave();
        }
    }
    else
    {
        // 波次进行中
        WaveTimer -= DeltaTime;
        if (WaveTimer <= 0.0f)
        {
            EndWave();
        }
    }
}

void AWaveManager::StartNextWave()
{
    CurrentWave++;
    WaveTimer = WaveInterval;
    bIsWaveActive = true;
    OnWaveChanged.Broadcast(CurrentWave);
}

void AWaveManager::EndWave()
{
    bIsWaveActive = false;
    RestTimer = RestBetweenWaves;
    // 可在此处销毁场上的剩余怪物 (可选)
}

float AWaveManager::GetEnemyStatMultiplier() const
{
    return 1.0f + (CurrentWave - 1) * 0.15f;
}

float AWaveManager::GetXPWaveMultiplier() const
{
    return 1.0f + (CurrentWave - 1) * 0.1f;
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

### 5.13 AGameMode — 游戏模式

```cpp
UCLASS()
class ABrotatoLikeGameMode : public AGameModeBase
{
    GENERATED_BODY()

public:
    ABrotatoLikeGameMode();

    UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Classes")
    TSubclassOf<ULevelUpWidget> LevelUpWidgetClass;

    UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Classes")
    TSubclassOf<UMainHUDWidget> MainHUDClass;

    // 暂停/恢复 (升级时)
    UFUNCTION(BlueprintCallable)
    void PauseGame();

    UFUNCTION(BlueprintCallable)
    void ResumeGame();

    // 游戏结束
    UFUNCTION(BlueprintCallable)
    void GameOver();

    // 获取升级选项池 (从 DataTable 读取)
    UFUNCTION(BlueprintCallable)
    TArray<FUpgradeOption> GenerateUpgradeOptions(int32 Count, int32 RemainingWeaponSlots);

protected:
    virtual void BeginPlay() override;

private:
    UPROPERTY()
    class UDataTable* UpgradeDataTable;

    UPROPERTY()
    class UDataTable* WeaponDataTable;

    UPROPERTY()
    class UDataTable* EnemyDataTable;

    UPROPERTY()
    ULevelUpWidget* LevelUpWidgetInstance;

    UPROPERTY()
    UMainHUDWidget* MainHUDInstance;
};
```

### 5.14 ULevelUpWidget — 升级选择界面

```cpp
UCLASS()
class ULevelUpWidget : public UUserWidget
{
    GENERATED_BODY()

public:
    // 3 个选项按钮
    UPROPERTY(meta=(BindWidget))
    class UUpgradeOptionWidget* Option1;

    UPROPERTY(meta=(BindWidget))
    class UUpgradeOptionWidget* Option2;

    UPROPERTY(meta=(BindWidget))
    class UUpgradeOptionWidget* Option3;

    // 显示升级界面 (由 GameMode 调用)
    UFUNCTION(BlueprintCallable)
    void ShowOptions(const TArray<FUpgradeOption>& Options);

protected:
    UFUNCTION()
    void OnOptionSelected(const FUpgradeOption& Option);
    // 1. 通知 GameMode 应用升级
    // 2. 通知 GameMode 恢复游戏
    // 3. 隐藏自身
};

// 单个升级选项组件
UCLASS()
class UUpgradeOptionWidget : public UUserWidget
{
    GENERATED_BODY()

public:
    UPROPERTY(meta=(BindWidget))
    class UTextBlock* NameText;

    UPROPERTY(meta=(BindWidget))
    class UTextBlock* DescriptionText;

    UPROPERTY(meta=(BindWidget))
    class UButton* SelectButton;

    void Setup(const FUpgradeOption& Option);
    // 武器选项: 显示名称 + 简介 + BaseDamage / AttackInterval / 特殊说明
    // 数值选项: 显示名称 + "+X 属性名"

private:
    FUpgradeOption CachedOption;
    UFUNCTION()
    void OnClicked();
};
```

**升级选项生成算法 (AGameMode::GenerateUpgradeOptions):**

```
输入: Count (恒为3), RemainingWeaponSlots

1. 从 UpgradeDataTable 加载所有可用的 FUpgradeOption
2. 筛选 MinLevelToAppear <= 玩家当前等级
3. 从 WeaponDataTable 加载所有可用武器
4. 筛选 MinWaveToAppear <= 当前波次 且 玩家尚未持有
5. 武器选项数量 = Clamp(Random(1, Count), 1, RemainingWeaponSlots)
   // 至少 1 个武器 (如果还有槽位)，至多不超出剩余槽位
6. 数值选项数量 = Count - 武器选项数量
7. 从武器池随机选武器选项数量个
8. 从数值池随机选数值选项数量个
9. 随机打乱顺序返回
```

### 5.15 UMainHUDWidget — 游戏 HUD

```cpp
UCLASS()
class UMainHUDWidget : public UUserWidget
{
    GENERATED_BODY()

public:
    // HP 条
    UPROPERTY(meta=(BindWidget))
    class UProgressBar* HPBar;

    UPROPERTY(meta=(BindWidget))
    class UTextBlock* HPText;      // "5 / 10"

    // XP 条
    UPROPERTY(meta=(BindWidget))
    class UProgressBar* XPBar;

    UPROPERTY(meta=(BindWidget))
    class UTextBlock* LevelText;   // "Lv.3"

    // 波次
    UPROPERTY(meta=(BindWidget))
    class UTextBlock* WaveText;    // "波次 5"

    // 波次计时
    UPROPERTY(meta=(BindWidget))
    class UTextBlock* WaveTimerText; // "剩余 12s"

    // 武器槽 (6 个图标)
    UPROPERTY(meta=(BindWidget))
    class UUniformGridPanel* WeaponSlotGrid;

    // 每帧更新 (由 Tick 或事件驱动)
    void UpdateHP(float Current, float Max);
    void UpdateXP(float Current, float ToNext, int32 Level);
    void UpdateWave(int32 WaveNumber, float TimeRemaining);
    void UpdateWeaponSlots(const TArray<AWeapon*>& Weapons);
};
```

---

## 6. 敌人详细数值

基础数值 × `EnemyStatMultiplier` = 最终属性

| 敌人 | 波次解锁 | BaseHP | MoveSpeed | ContactDamage | BaseXP | 特点 |
|------|---------|--------|-----------|---------------|--------|------|
| 史莱姆 Slime | 1 | 12 | 200 | 1 | 3 | 慢速肉盾 |
| 骷髅 Skeleton | 3 | 20 | 350 | 2 | 5 | 标准近战 |
| 蝙蝠 Bat | 5 | 8 | 500 | 1 | 2 | 快速脆皮群怪 |
| 暗影法师 ShadowMage | 7 | 15 | 0 | 0 | 8 | 远程站桩, 发射弹幕 2s/次, 弹幕伤害 3 |
| 幽灵 Ghost | 9 | 10 | 300 | 2 | 6 | 3s 冲刺一次, 冲刺速度 1200, 持续 0.3s |

---

## 7. 升级选项详细数值

### 7.1 数值类选项

| UpgradeType | 显示名 | 描述模板 | Value 区间 | 随机逻辑 |
|-------------|--------|---------|-----------|----------|
| MaxHP_Add | 生命强化 | +{Value} 最大生命值 | {2, 3, 4, 5} | 范围内随机取一 |
| MaxHP_Mul | 生命增幅 | +{Value%} 最大生命值 | {10, 15, 20, 25} | 范围内随机取一 |
| Damage_All_Mul | 伤害增幅 | +{Value%} 所有伤害 | {5, 8, 10, 15} | 范围内随机取一 |
| AttackSpeed_All_Mul | 急速 | +{Value%} 攻击速度 | {5, 8, 10, 15} | 范围内随机取一 |
| MoveSpeed_Add | 敏捷 | +{Value} 移动速度 | {30, 50, 60, 80} | 范围内随机取一 |
| ExpRate_Mul | 领悟 | +{Value%} 经验获取 | {10, 15, 20, 30} | 范围内随机取一 |
| PickupRange_Add | 磁力 | +{Value} 拾取范围 | {50, 80, 100, 150} | 范围内随机取一 |
| CritChance_Add | 精准 | +{Value%} 暴击率 | {3, 5, 8} | 范围内随机取一 |
| CritDamage_Mul | 致命 | +{Value%} 暴击伤害 | {15, 20, 30} | 范围内随机取一 |

### 7.2 武器选项显示

每个武器选项显示:

| 显示项 | 示例 |
|--------|------|
| 名称 | 火焰喷射 |
| 简介 | 向前方扇形范围持续喷射火焰 |
| 伤害 | 伤害: 2/跳 |
| 频率 | 频率: 0.15s |
| 特殊 | [扇形 30° / 射程 400] |

---

## 8. 游戏完整流程

```
GameMode::BeginPlay()
├── 创建 WaveManager
├── 创建 MonsterSpawner (绑定 WaveManager)
├── 创建 MainHUD
├── 生成玩家 AGameCharacter
│   ├── StatsComponent 初始化 (BaseMaxHP=10, ...)
│   ├── 生成初始武器 AMagicBulletWeapon
│   └── 绑定输入
└── WaveManager::StartGame()
    └── StartNextWave() → 波次 1 开始

游戏循环 (每帧):
├── WaveManager::Tick()
│   └── 波次倒计时 / 休息倒计时 → 波次切换 → 广播事件
├── MonsterSpawner::Tick()
│   └── 波次活跃时按 SpawnRate 生成怪物
├── Player::Tick() → 移动
├── 每个 Weapon::Tick()
│   ├── 冷却递减
│   ├── FindTarget()
│   └── 冷却就绪 → Fire()
├── 每个 Enemy::Tick()
│   ├── MoveTowardsPlayer()
│   └── 特殊技能
├── 每个 Projectile::Tick()
│   └── 飞行 → 命中判定
├── XPMultiplier 检查磁吸
└── HUD 更新

怪物死亡:
├── Enemy::OnDeath()
├── 生成 AXPOrb (ExpValue = Enemy.GetEffectiveXP())
└── 销毁 Enemy

经验球被拾取:
├── AXPOrb::OnPlayerOverlap()
├── StatsComponent::AddXP(ExpValue * ExpMultiplier)
├── 累积 XP ≥ XP_ToNextLevel?
│   ├── 是 → LevelUp()
│   │   ├── 增加 CurrentLevel
│   │   ├── 重置 CurrentXP (溢出保留)
│   │   └── GameMode::PauseGame() + 弹出升级界面
│   └── 否 → 继续
└── 销毁 XPOrb

升级选择:
├── GameMode::GenerateUpgradeOptions(3, RemainingSlots)
├── LevelUpWidget::ShowOptions()
├── 玩家点击某选项
├── OnOptionSelected()
│   ├── 武器: Character::AddWeapon()
│   └── 数值: StatsComponent::ApplyUpgrade()
├── GameMode::ResumeGame()
└── 隐藏 LevelUpWidget

玩家死亡:
├── Character::OnDeath()
├── GameMode::GameOver()
│   ├── 显示结算 (存活波次、等级)
│   └── 停止所有生成
```

---

## 9. DataTable 资产清单

需在编辑器中创建的 DataTable:

| DataTable | 行结构 | 说明 |
|-----------|--------|------|
| DT_Weapons | FWeaponDefinition | 所有武器配置 |
| DT_Enemies | FEnemyDefinition | 所有敌人配置 |
| DT_Upgrades | FUpgradeOption | 所有数值升级选项 |

---

## 10. 竞技场规格

| 属性 | 值 |
|------|-----|
| 形状 | 圆形 |
| 半径 | 3000 UE 单位 |
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

- **从简优先**: 所有系统第一版保持最小可行设计，保留扩展接口 (virtual / DataAsset / 枚举扩展)
- **数据驱动**: 武器、敌人、升级数值全部走 DataTable，方便编辑器内调数值
- **Component 分离**: 玩家属性独立为 `UPlayerStatsComponent`，武器和敌人逻辑各自内聚
- **无魔法数字**: 所有可调数值暴露为 `UPROPERTY(EditAnywhere)`
- **C++ 为主**: 所有游戏逻辑在 C++ 层实现，Blueprint 仅用于资源引用和简单连线