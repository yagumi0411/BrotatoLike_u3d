# 变更日志

> 记录求职 Demo 开发过程中的结构性变更，供面试叙述与回归参考。
> 每次会话的结构性改动追加一节；纯文档润色不重复记录。

---

## 2026-08-04 — 数值重设计 + 演示节奏 + 性能重构

### 背景

1. 演示时随意修改数值导致失衡：魔法弹 BaseDamage 20（原设计 5）造成"开局秒杀、中期暴毙"，武器 DPS、怪物成长、玩家成长三条曲线互不相符。
2. 为录制演示视频，需要 2-3 分钟一局的快节奏。
3. README 性能声称（OverlapSphereNonAlloc 零分配）与代码实现不符，需要真优化或改措辞。

### 一、数值体系重设计（平衡重建）

**波次缩放：线性 → 温和指数**
```csharp
// WaveManager.GetEnemyStatMultiplier()
1f + (Wave - 1) * 0.15f   →   Mathf.Pow(1.06f, Wave - 1)
```
- 前期平缓后期陡峭，与 README"指数增长"表述一致；目标死亡波次 8-9（约 2.5-3 分钟）。

**怪物移速缩放：满缩放 → 30%**
```csharp
// Enemy.GetEffectiveMoveSpeed()
MoveSpeed * StatMultiplier   →   MoveSpeed * (1f + (StatMultiplier - 1f) * 0.3f)
```
- 修复原实现的致命平衡 bug：波 12+ 蝙蝠速度 13 远超玩家 6，后期不是打不过而是跑不掉。

**武器最终数值**

| 武器 | 伤害 | 间隔 | 射程/范围 | DPS(单目标) | 定位 |
|------|------|------|----------|-------------|------|
| 魔法弹 | 7 | 0.6s | 15 | 11.7 | 单体主力（2 发杀史莱姆/骷髅/法师，1 发杀蝙蝠/幽灵） |
| 火焰喷射 | 2.5/tick | 0.15s | 4m / 扇形 30° | 16.7（AoE 按目标数倍乘） | 中期清场 |
| 飞弹环绕 | 2.5 | 0.4s/颗 | 半径 1.5 | 理论 18.75 | 近身防护（3 颗独立冷却） |

**敌人最终数值**

| 敌人 | 解锁波次 | HP | 移速 | 接触伤害 | XP |
|------|---------|-----|------|---------|-----|
| 史莱姆 | 1 | 8 | 2 | 1 | 3 |
| 骷髅 | 2 | 14 | 3.5 | 2 | 5 |
| 蝙蝠 | 3 | 5 | 4.5 | 1 | 2 |
| 暗影法师 | 5 | 12 | 0（站桩） | 弹幕 3/2s | 8 |
| 幽灵 | 7 | 7 | 2.5 | 2 | 6 |

**升级数值**：保留原值（3 / 15% / 10% / 10% / 0.5 / 15% / 0.8 / 5% / 20%），设计文档从"区间随机"修正为实际实现的固定值。

### 二、演示快节奏调整（一局 2-3 分钟）

| 参数 | 旧值 | 新值 | 位置 |
|------|------|------|------|
| 波次时长 | 30s | **20s** | 场景 WaveManager |
| 波间休息 | 3s | **1s** | 场景 WaveManager |
| 竞技场半径 | 50（场景覆盖值） | **20** | 场景 MonsterSpawner（代码默认 28） |
| 生成速率 | `1.5 + 0.35n` | **`2.0 + 0.4n`**（/秒） | MonsterSpawner |
| XP 需求 | `10×(L+1)` | **`8×(L+1)`** | PlayerStatsComponent |
| 武器 DPS | 基准 | **+20%** | 3 个武器 asset |
| 敌人解锁 | 1/3/5/7/9 | **1/2/3/5/7** | 敌人 asset |

- 时间轴：0:00 开局 → 0:21 骷髅 → 0:42 蝙蝠 → 1:24 暗影法师 → 2:06 幽灵 → 2:28-2:49 死亡结算。

### 三、性能重构：敌人注册表 + 对象池

**新增文件**
| 文件 | 职责 |
|------|------|
| `Game/EnemyRegistry.cs` | 活敌人注册表：O(1) 注册/注销（swap-remove），查询零分配 |
| `Game/ObjectPool.cs` | 通用对象池：SetActive 复用，重复回收防御，支持临时工厂 |
| `Game/EnemyPool.cs` | 敌人按 EEnemyType 分池，回收时同步注销注册表 |

**修改文件**
| 文件 | 改动 |
|------|------|
| `Enemy.cs` | Initialize 注册 / OnDeath 回池 / OnDestroy 兜底注销 / CreateVisual 防重 |
| `Weapon.cs` | FindTarget 重写：缓存目标（有效即免扫描）+ 注册表手写循环 + 蓄水池采样（Random 模式）+ sqrMagnitude |
| `MonsterSpawner.cs` | 生成走池，清场走注册表 |
| `MagicBulletWeapon.cs` / `MagicBulletProjectile.cs` | 静态子弹池；寿命由 Update 管理、命中回池 |
| `XPOrb.cs` | 静态池；OnEnable 重置磁吸状态 |
| `FlameThrowerWeapon.cs` | 火焰粒子池（约 40 颗/秒）+ `_lastDamageTimeMap` 字典泄漏修复 |
| `GameManager.cs` | Awake 清空注册表/池（防热重载残留） |

**过程中修复的隐藏问题**
1. 火焰粒子 40 次/秒 `CreatePrimitive`——全项目最大 GC 源（比子弹高 20 倍），已池化。
2. 火焰伤害冷却字典泄漏：对象池回收后引用不为 null，原"null 清理"永不触发 → 改为 `isActiveAndEnabled` 检查。
3. `EnemyPool` 传入 null 工厂导致首次生成 NRE（ObjectPool.Get 未接收调用方临时工厂）→ `Get(factory = null)` 支持外部临时工厂。

**验证**：`FindObjectsByType<Enemy>`（原每秒 15 次）清零，剩余调用均为低频一次性（结算清理/初始化）。

### 四、怪物生成地点重构

```csharp
// MonsterSpawner.GetSpawnPosition()
// 旧：以世界原点为圆心、半径 ArenaRadius 的单圆环（玩家走位后贴脸刷怪）
// 新：以玩家为中心的环形带 [MinSpawnDistance=13, ArenaRadius=20]
playerPos + Random.insideUnitCircle.normalized * Random.Range(13f, 20f)
```
- 修复"玩家离开圆心后怪物在 5 单位内凭空刷出"问题；生成点恒在玩家视野外（垂直方向）。
- 副作用：接敌时间从 10s 缩至 6-7s，节奏更快（符合演示诉求）。

### 五、文档同步

| 文档 | 同步内容 |
|------|---------|
| `README.md` | Unity 版本统一、暂停菜单截图补齐、波次 20s/1s、性能表与面试亮点重写为真实实现 |
| `Docs/GameCoreDesign.md` | 版本号、数值公式（指数缩放/生成速率/XP）、武器/敌人/升级表、FindTarget 逻辑、竞技场规格 |
| `Docs/CHANGELOG.md` | 本文档 |

### 遗留项（未处理）

- 环绕飞弹伤害为生成时快照，后期伤害升级对环绕武器无效（`SpellOrbitWeapon.cs`）
- 伤害飘字（`DamagePopup`）未池化，波 8+ 每秒 30+ 实例
- 竞技场无物理边界墙（README"竞技场"名不副实，录视频时玩家可走出画面）
- 玩家朝向依赖 Ground 层射线，场景无碰撞体导致模型朝向不更新（不影响核心玩法）

---
