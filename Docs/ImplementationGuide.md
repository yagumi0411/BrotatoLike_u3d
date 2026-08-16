# BrotatoLike 架构速览（2026-08 重写）

> **本文档原为 7/31 的分阶段实施教程，8/4 大规模重构后 90% 内容过时，已重写为当前架构速览。**
> 详细设计（数值公式/类设计/流程）见 `GameCoreDesign.md`；变更历史见 `CHANGELOG.md`；会话总结见 `SessionSummary.md`。

---

## 1. 架构总览

```
数据层 (ScriptableObject: Weapons / Enemies / Upgrades)
    ↓
逻辑层 (MonoBehaviour Components)
    ├── Game/    GameManager(状态机) · WaveManager · MonsterSpawner · ObjectPool · EnemyPool · EnemyRegistry
    ├── Player/  PlayerController · PlayerStatsComponent · IInputProvider 三实现 · PlayerStatusBar · PlayerNetworkBehaviour
    ├── Weapons/ Weapon 基类 + 3 实现（魔法弹/火焰/环绕）
    ├── Enemies/ Enemy 基类 + 5 实现
    ├── Projectiles/ MagicBulletProjectile · OrbitProjectile · EnemyProjectile
    └── UI/      MainMenuUI · MainHUD · LevelUpUI · PauseUI · GameOverUI · DamagePopup
    ↓
表现层 (UI / 伤害飘字 / 对象池 VFX)
```

## 2. 核心系统速览

| 系统 | 关键类 | 一句话说明 |
|------|--------|-----------|
| 状态机 | `GameManager` | `EGameState`：MainMenu/Playing/Paused/LevelUp/GameOver；`SetState` 统一管 UI 显隐 + timeScale |
| 游戏模式 | `GameManager.EGameMode` | Single / LocalCoop（同屏双人）/ Online（联机骨架），主菜单 Toggle 选择 |
| 玩家 | `PlayerController` + `PlayerStatsComponent` | CharacterController 移动；属性公式全部在 StatsComponent；输入走 `IInputProvider` |
| 武器 | `Weapon` 基类 | 自动索敌（注册表 + 缓存目标 + 蓄水池采样，零 GC）→ 子类实现 `Fire()` |
| 敌人 | `Enemy` 基类 | 生成时锁定最近玩家；Rigidbody 物理移动；死亡回池 |
| 波次 | `WaveManager` | 20s/波 + 1s 休息（场景覆盖）；敌属性 `1.06^(n-1)` 温和指数 |
| 刷怪 | `MonsterSpawner` | 视口四边形方向相关环带：屏幕外刷出 + 地图内约束，360° 均匀来怪 |
| 对象池 | `ObjectPool` / `EnemyPool` / 静态池 | 敌人/子弹/经验球/火焰粒子四类高频对象池化，零运行时 Instantiate |
| 索敌 | `EnemyRegistry` | 出生/死亡 O(1) 注册注销（swap-remove），查询零分配 |
| 升级 | `GameManager` + `LevelUpUI` | 双人"升级不暂停"：升级者停操作 + 3 秒无敌；选项生成算法在 GameManager |
| 双人/联机 | `PlayerNetworkBehaviour` 等 | 本地双人=克隆玩家 2（方向键）；联机=NGO 2.13.1，`IsOwner` 区分本地/远端 |

## 3. 关键设计决策（面试要点）

- **数据驱动**：所有数值走 ScriptableObject，调参不改代码
- **零分配性能**：索敌/拾取高频路径无 GC（注册表 + sqrMagnitude + 蓄水池采样）
- **输入抽象**：`IInputProvider` 统一键鼠/方向键/网络三源
- **权威模型**（联机）：移动=客户端权威（NetworkTransform Owner），战斗=服务器权威（阶段 3）
- **升级不暂停**：双人玩法设计，无敌 3 秒替代全局暂停（也规避了 timeScale 跨端同步问题）

## 4. 快速启动

```
1. Unity Hub 打开项目（6000.5.4f1）
2. 打开 Assets/Scenes/SampleScene.unity → Play
3. 主菜单选模式：单人 / 本地双人（方向键=玩家2）/ 联机双人（骨架）
```

## 5. 文档索引

| 文档 | 内容 |
|------|------|
| `GameCoreDesign.md` | 数值公式、类设计、完整流程（唯一设计规范） |
| `CHANGELOG.md` | 结构性变更历史（8/4 数值/性能重构、8/15-17 双人/联机） |
| `SessionSummary.md` | 全部会话总结、遗留待办、可复用素材 |
| `ProjectReview_求职评估.md` | 7/19 求职评估 + 8/17 当前状态对照 |
| `readme.md` | 对外宣传口径（含截图） |
