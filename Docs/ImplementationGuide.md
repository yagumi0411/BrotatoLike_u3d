# BrotatoLike 当前实现与扩展指南

更新日期：2026-10-12。本指南对应现有工程，替代初版从零搭建的示例代码。具体类签名以源码为准，数值以场景和配置资产为准。

## 1. 打开与运行

1. 安装 Unity 6000.5.4f1 与 Git LFS，克隆后执行 `git lfs pull`。
2. 在 Unity Hub 打开工程，确认现有 Packages 导入完成，不另行升级依赖。
3. 打开 `Assets/Scenes/SampleScene.unity`，进入 Play，从主菜单开始游戏。
4. 按 WASD / 方向键移动、ESC 暂停；武器自动攻击。
5. 波末购买治疗或手动继续；结束对局后回菜单分配天赋。

表现为 2D 像素精灵，底层仍使用 CharacterController、Rigidbody 与三维碰撞。不要把战斗组件改成 2D 物理组件，也不要删除 Ground 的碰撞。

## 2. 模块与启动顺序

| 模块 | 源码 | 当前职责 |
|---|---|---|
| 游戏状态 | `Assets/Scripts/Game/GameManager.cs` | 6 种状态、UI 显隐、金币、升级队列、局外成长协调 |
| 波次 | `Assets/Scripts/Game/WaveManager.cs` | 45 秒战斗倒计时、波末商店、下一波与基础成长 |
| 生成与难度 | `MonsterSpawner.cs`、`DifficultyManager.cs` | 屏幕外刷怪、敌人权重与 Fixed / Adaptive 调节 |
| 玩家 | `Assets/Scripts/Player` | 输入、移动、武器槽、局内属性及天赋快照 |
| 战斗 | `Weapons`、`Enemies`、`Projectiles` | 自动索敌、攻击、命中、敌人移动和技能 |
| 美术 | `Assets/Scripts/Visuals` | 图集切片、精灵动画、billboard、排序和地面 |
| 局外成长 | `Assets/Scripts/Progression` | 天赋图、效果注册、经验换点、结算、存档 |
| 界面 | `Assets/Scripts/UI` | 菜单、HUD、升级、商店与天赋图 |

`GameManager.Awake` 查找场景引用、载入局外进度并挂载运行时商店/天赋 UI 组件；`Start` 进入主菜单并绑定事件。点击开始后重置局内属性、应用天赋、创建初始武器并开始第 1 波。

已有场景提供基础 Canvas 和菜单。商店复用现有 Canvas，固定布局在编辑器搭建，通过 HealingCard Prefab 维护商品卡片；WaveShopUI 挂在 GameManager 所在对象并在 Inspector 中绑定界面引用，不再按需生成布局。天赋图仍由代码按需构建。不要重复添加 UI 控制器。

## 3. 状态与对局边界

| 状态 | 战斗与计时 | 界面 |
|---|---|---|
| MainMenu | 停止 | 主菜单、可打开天赋图 |
| Playing | 执行 | HUD |
| Paused | 冻结 | 暂停菜单 |
| LevelUp | 冻结 | 待选强化 |
| Shop | 冻结 | 波次间像素装备面板商店 |
| GameOver | 停止 | 对局及天赋奖励结算 |

新增战斗逻辑应沿用 Playing 状态判断，尤其是伤害、拾取、刷怪和投射物碰撞。不要只依赖 timeScale：暂停期间碰撞回调和按钮回调仍可能到达。

连续升级使用待选队列，保留已展示的选项；死亡清空队列并忽略过期选择。重开及返回菜单清理战斗对象、归零局内经验/金币/升级，保留局外进度。

## 4. 修改数值

| 内容 | 配置入口 |
|---|---|
| 武器伤害、间隔、射程 | `Assets/Data/Weapons/*.asset` |
| 敌人基础属性、出现波次与技能 | `Assets/Data/Enemies/*.asset` |
| 局内升级数值、描述、出现条件 | `Assets/Data/Upgrades/*.asset` |
| 波次时长 | 场景 WaveManager.WaveInterval，当前 45 |
| 基础刷怪率、每波增长与上限 | MonsterSpawner.BaseSpawnRate / SpawnRateGrowthPerWave / MaxSpawnRate，默认 1 / 0.12 / 4 |
| 金币和治疗 | 场景 GameManager.GoldPerKill / HealingCost / HealingAmount，当前 1 / 8 / 4 |
| 难度模式 | 场景 GameManager.DifficultyMode，当前 Fixed |
| 局内经验需求 | PlayerStatsComponent.GetXPToNextLevel，当前 `24 + 14g + 3g²` |
| 天赋、连线、经验换点 | `Assets/Resources/Progression/MageTalentGraph.json` |

场景序列化值会覆盖脚本默认值。不要仅改字段默认值就假设 SampleScene 已更新；百分比升级配置 5 表示 5%，属性累加时转换为 0.05。

## 5. 金币与商店

击杀路径为 `Enemy.OnDeath → AddKill / AddGold / XPOrb.Spawn → EnemyPool.Release`。金币直接入账，经验仍生成经验球；当前没有运行时金币掉落物。

波次结束打开 Shop，保留敌人、经验与投射物并冻结。购买通过 `GameManager.BuyHealing` 校验状态、金币、生命与限购，不由按钮状态决定合法性。`ContinueFromShop` 手动开始下一波。

`GoldPickup.cs` 是旧版拾取机制保留脚本，当前没有金币生成或波末收集调用。素材和跨局存款仍是规划。详细规则见 [商店文档](GoldAndWaveShopDesign.md)。

商店布局、素材导入、Prefab 实例覆盖和九个 Inspector 引用见 [商店 UI 编辑器维护指南](UIEditorGuide.md)。购买按钮由 WaveShopUI.Awake 绑定，继续按钮在 Inspector 中绑定 GameManager.ContinueFromShop。修改外观时在非 Play Mode 下编辑场景或 Prefab；修改数值与购买规则时调整 GameManager。

## 6. 天赋图与局外成长

### 配置与路径

当前法师图有 1 个免费起点、18 个可分配节点、33 级。节点以稳定 ID 关联，图连接双向；任一相邻节点投入第 1 点即可继续，多级节点不需要点满后再走下一步。

主要字段：

| 字段 | 用途 |
|---|---|
| Version、Id、Name、StartNodeId | 内容版本、标识、名称与起点 |
| Nodes[].Id / Name / Description / Branch | 稳定标识与显示内容 |
| Nodes[].Position / MaxRank / Notable | 图布局、等级上限、较大节点样式 |
| Nodes[].Effects[].Id / Value | 效果注册标识与每级数值 |
| Links[].From / To | 节点连接关系 |
| Rules | 有效对局门槛、结算奖励与经验换点费用 |

伤害、范围、生存和交汇效果均为通用属性。范围影响索敌、魔法弹寿命、火焰检测长度和环绕半径，不扩大魔法弹碰撞体。完整节点与费用见 [天赋文档](MetaProgressionDesign.md)。

### 职责分离

- `TalentGraph`：验证配置唯一 ID、效果合法性和起点可达性；维护节点与连接。
- `TalentEffectRegistry`：注册效果处理器，汇总为 `TalentBonuses`。
- `MetaProgression`：校验加点、换点、免费重置、结算去重和磁盘保存。
- `TalentGraphUI`：根据图数据构建界面并展示结果。
- `GameManager`：创建对局 ID，在死亡、重开、返回菜单时结算；下一局应用天赋快照。
- `PlayerStatsComponent`：将局外加成与局内升级一起计算有效属性，保持基础资产不变。

### 保存与扩展

存档位于 `Application.persistentDataPath/meta_progression_v1.json`。写临时文件、备份上次正式文件，再原子替换；不能读取或不支持的版本不会以空进度覆盖。缺失内容的 ID 与点数保留，但不应用其效果。

新片区使用独立命名空间，连接到已有节点，调用 `MetaProgression.TryRegisterContent`。新效果需先注册处理器，再载入引用它的图配置。重复 ID、未注册效果与不可达节点会被拒绝，失败不会部分修改图。

当前未实现外部目录扫描、依赖解析、任意脚本加载或创意工坊 SDK。不要把内容注册接口视为完整 mod 发布系统。

## 7. 美术与新增内容

图集、坐标和材质在 `Assets/Resources/DungeonArt`。精灵由 `DungeonArt` 按作者坐标切片；动画在 `DungeonSpriteVisual` 处理。敌人按 Rigidbody 速度判断运动并保持短暂移动状态，避免渲染帧与物理帧不同步导致动画闪烁。

需要重新应用导入/场景配置时使用 `Tools/Dungeon Art/Configure SampleScene`。保留原碰撞组件，避免重复创建表现对象。来源许可见 [美术文档](ArtAdaptation.md)。

新增武器沿用 Weapon 与 WeaponDefinition；新增敌人沿用 Enemy 与 EnemyDefinition，并接入对应创建分支。现有武器可能重写 Update / 初始化流程，不能假设所有新行为仅靠 Fire 即可完成。

## 8. 验证与文档维护

2026-10-11 已通过：主工程编译 0 错误、4 条既有过时 API 警告，57 项闭环检查、53 项天赋专项检查。

`tools/verification/MetaProgressionVerification.cs` 需要复制到隔离项目的 Assets/Editor，在独立 companyName/productName 下运行 `MetaProgressionVerification.Run`；脚本会拒绝在正式项目直接运行。验证脚本当前使用本机项目路径，迁移时需调整 Root 与隔离项目名称。闭环验证脚本在本机 Temp，未纳入仓库。

`Logs` 和 `Temp` 被忽略，不能假定克隆后已有报告或隔离项目；Unity 可能清理 Temp。测试不得用正式进度存档。修改玩法时同步核心数值、专题文档、README 与 CHANGELOG；新规划需标明尚未实现。
