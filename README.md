# BrotatoLike

Unity 俯视角动作生存游戏。玩家操控法师，自动攻击敌人，通过局内升级、波次间商店与局外天赋逐步成长。

文档更新：2026-10-12。当前版本使用 0x72 DungeonTileset II 像素美术，商店引入 Kenney Pixel Adventure CC0 素材并在编辑器中搭建，保留三维移动与物理机制。

## 快速开始

环境：Unity 6000.5.4f1、项目现有 Input System、TextMeshPro 和 uGUI。美术及截图由 Git LFS 管理。

```bash
git lfs install
git clone https://github.com/yagumi0411/BrotatoLike_u3d.git
cd BrotatoLike_u3d
git lfs pull
```

通过 Unity Hub 打开项目，打开 `Assets/Scenes/SampleScene.unity`，进入 Play 后点击“开始游戏”。项目启动先显示主菜单，不自动刷怪。

| 操作 | 方式 |
|---|---|
| 移动 | WASD / 方向键 |
| 暂停、继续 | ESC |
| 局内强化 | 鼠标选择升级选项 |
| 商店 | 悬停购买按钮查看详情，点击购买或继续下一波 |
| 天赋图 | 主菜单入口；拖拽移动、滚轮缩放、悬停查看、选中后投入点数 |
| 天赋重置 | 天赋图内免费重置，下一局应用新分配 |

## 游戏循环

1. 每波实际战斗 45 秒，武器自动攻击。
2. 击杀掉落经验球，并直接获得 1 金币；金币无需拾取。
3. 升级时暂停并选择强化，连续升级依次处理。
4. 波末进入暂停的像素装备面板商店，药水 8 金币恢复 4 点生命，每波限购一次；手动继续下一波。
5. 死亡、重开、返回菜单按本局表现结算独立的天赋经验，同一局只结算一次。
6. 主菜单分配法师天赋，下一局生效；天赋进度跨局保存。

有效对局需至少击杀 1 只怪物或战斗 15 秒。每局可以积累经验，前期较快获得天赋点，后期需要积累；即时退出无奖励。

## 当前功能

| 系统 | 当前行为 |
|---|---|
| 美术 | 0x72 DungeonTileset II v1.7，2D 精灵与正交相机，保留 CharacterController / Rigidbody |
| 武器 | 魔法弹、火焰喷射、飞弹环绕，最多 6 个武器槽 |
| 敌人 | 史莱姆、骷髅、蝙蝠、暗影法师、幽灵，分别从第 1/3/4/6/8 波出现 |
| 波次 | 45 秒实际战斗，敌人属性线性增长；商店、暂停、升级时冻结计时 |
| 商店 | 编辑器搭建的三个商品卡片，左侧治疗药水，另两栏预留 |
| 主菜单 / HUD | 开始游戏、天赋入口及进度；生命、局内经验、波次、等级、本局金币 |
| 法师天赋 | 伤害、范围、生存路线，18 个可分配节点、33 级；相邻解锁、多级节点、免费重置、最终可全满 |
| 难度模式 | 支持 Fixed / Adaptive，当前 SampleScene 使用 Fixed |
| 重开与退出 | 清理上一局战斗对象；金币、局内等级和升级重置，局外天赋保留 |

局内升级需求为 `24 + 14g + 3g²`，其中 `g = 等级 - 1`。首波基础刷怪约 1 只/秒，每波增加 0.12，上限 4；Adaptive 模式另乘难度倍率。

## 实际界面预览

![像素场景](Screenshots/dungeon_art_preview.png)
![旧版货架式商店（新版编辑器布局截图待更新）](Screenshots/wave_shop_preview.png)
![法师天赋图](Screenshots/mage_talent_graph_preview.png)

天赋预览中的点数分配来自隔离测试存档。其他旧菜单、升级和结算截图保留在 Screenshots 中，未作为最新版本预览。

## 模块与扩展

| 目录 / 模块 | 职责 |
|---|---|
| `Assets/Scripts/Game` | 游戏状态、波次、刷怪、难度调节、对象池 |
| `Assets/Scripts/Definitions`、`Assets/Data` | 武器、敌人和局内升级的 ScriptableObject 配置 |
| `Assets/Scripts/Player`、`Weapons`、`Enemies`、`Projectiles` | 属性、移动、攻击、敌人行为与命中 |
| `Assets/Scripts/Visuals` | 精灵切片、动画、朝向和竞技场表现 |
| `Assets/Scripts/Progression` | 天赋图规则、效果注册、局外经验与存档 |
| `Assets/Scripts/UI` | HUD、菜单、升级、商店与天赋图 |
| `Assets/Resources/DungeonArt` | 图集、坐标、材质与资源说明 |
| `Assets/Resources/Progression/MageTalentGraph.json` | 天赋节点、连线、效果 ID、经验规则 |
| `Assets/Editor/DungeonArtSetup.cs` | 美术导入和场景适配工具 |
| `tools/verification/MetaProgressionVerification.cs` | 隔离场景天赋验证入口 |

游戏状态包括 MainMenu、Playing、Paused、LevelUp、GameOver、Shop。伤害、刷怪、攻击与经验吸收受 Playing 状态约束。

武器、敌人和局内升级通过 ScriptableObject 调整；天赋通过 JSON 图配置和效果注册扩展。天赋节点、连线、资格校验、效果及存档分离，稳定 ID 不依赖显示名称或列表位置。

敌人、魔法弹和经验球采用对象池；火焰的基础粒子分支池化，VFX 分支仍创建临时对象。索敌使用敌人注册表、目标缓存与蓄水池采样，避免建立随机候选列表；不宣称整个战斗过程零分配。

## 局外存档与验证

存档：`Application.persistentDataPath/meta_progression_v1.json`，结算与分配后立即保存，保留 `.bak`。局内金币不跨局；天赋经验、点数和分配跨局保存。

已完成的隔离验证记录：57 项游戏闭环检查、53 项天赋专项检查，属于 2026-10-11 版本。2026-10-12 编辑器版商店由用户确认基本接入及显示正常，当前运行时代码编译通过（0 错误、4 条既有过时 API 警告）；完整购买流程尚未重新逐项记录，本次文档同步未重新运行游戏测试。

测试输出位于本机 `Logs`，隔离项目位于 `Temp`，两者不纳入版本库，Unity 可能清理 Temp。专项验证脚本要求隔离项目及独立存档身份，不能直接在正式项目执行。

## 文档

- [核心设计与数值](Docs/GameCoreDesign.md)
- [当前实现与扩展指南](Docs/ImplementationGuide.md)
- [像素美术适配](Docs/ArtAdaptation.md)
- [金币与波次商店](Docs/GoldAndWaveShopDesign.md)
- [商店 UI 编辑器维护指南](Docs/UIEditorGuide.md)
- [局外成长与法师天赋图](Docs/MetaProgressionDesign.md)
- [组合与图鉴规划](Docs/ComboAndCollectionDesign.md)
- [历史变更记录](Docs/CHANGELOG.md)

## 后续方向

- 组合联动和永久图鉴：保留材料、增加联动；首次选择后点亮，图鉴悬停只显示材料。
- 商店素材商品、限额跨局存款。
- 武器特色外围天赋、更多初始角色与片区。
- 外部 mod 内容发现、版本和依赖校验、创意工坊 SDK 接入。
- 新武器、精英/Boss、音效与竞技场物理边界。

上述内容尚未实现或尚未确定细节，不属于当前版本功能。

## 美术许可

本项目用于游戏 Demo 展示。0x72 DungeonTileset II 美术采用 CC0，来源与许可说明见 [CREDITS](Assets/Resources/DungeonArt/CREDITS.txt)。项目自身代码未新增许可授权。
