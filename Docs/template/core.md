# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working on code in this repository.

## 项目概述

BrotatoLike 是一个 Unreal Engine 5.7 top-down 动作生存游戏，类似《Brotato》。核心玩法：玩家控制法师角色移动，武器自动攻击敌人，击杀敌人掉落经验球，升级时从随机选项中选择强化能力。所有游戏逻辑从 Blueprint 向 C++ 迁移中。

- **设计文档**: `Docs/GameCoreDesign.md` — 包含完整的数值公式、类设计、数据表规格、游戏流程。所有代码实现须以此文档为准。
- **Blueprint 资产与配置**: `Docs/BlueprintAssets.md`
- **渲染配置**: `Docs/RenderingConfig.md`
- **更新日志 / 已知修复**: `Docs/Changelog.md`

## 构建 & 开发

- UE5 编辑器版本：**5.7**（安装路径 `D:/games/UE_5.7`）
- 编译：编辑器内 `Ctrl+Alt+F11`（Compile C++ Code），或双击 `.uproject` 触发重新编译
- 命令行编译：`D:\games\UE_5.7\Engine\Binaries\DotNET\UnrealBuildTool\UnrealBuildTool.exe BrotatoLike Win64 Development "D:\User\Documents\Unreal Projects\BrotatoLike\BrotatoLike.uproject" -waitmutex`
- Editor map: `Content/TopDown/Lvl_TopDown.umap`
- 模块：`Source/BrotatoLike/BrotatoLike.Build.cs`（依赖：`Core`, `CoreUObject`, `Engine`, `InputCore`, `UMG`, `NavigationSystem`, `EnhancedInput`）

## 技术架构

**架构模式**: C++ 为主，Blueprint 为辅。所有游戏逻辑在 C++ 层实现，Blueprint 仅用于资源引用、UI 绑定和编辑器配置。

### C++ 源码 (`Source/BrotatoLike/`)

| 文件 | 说明 |
|------|------|
| `GameTypes.h` | 4 个枚举：`EUpgradeType`、`EWeaponType`、`EEnemyType`、`ETargetMode` |
| `GameStructs.h` | 4 个 `FTableRowBase` 结构体：`FWeaponDefinition`、`FEnemyDefinition`、`FUpgradeOption`、`FPlayerStats` |
| `PlayerStatsComponent.h/.cpp` | 玩家属性：基础值、升级累加值、运行时状态、有效值计算、升级/经验、HP/XP/Level/Death 动态委托 |
| `GameCharacter.h/.cpp` | 玩家角色：`StatsComponent`、摄像机臂/跟随相机、6 武器槽、WASD 屏幕空间移动、武器管理、受击死亡 |
| `Weapon.h/.cpp` | 武器抽象基类：攻击循环、伤害计算、暴击、攻速/射程生效值 |
| `Enemy.h/.cpp` | 敌人抽象基类 + 5 个子类（Slime/Skeleton/Bat/ShadowMage/Ghost） |
| `EnemyProjectile.h/.cpp` | 敌人弹幕，命中玩家后自毁 |
| `Projectile.h/.cpp` | 弹丸基类 + `AMagicBulletProjectile` / `AOrbitProjectile` |
| `WeaponTypes.h/.cpp` | 三种武器子类：MagicBullet / FlameThrower / SpellOrbit |
| `XPOrb.h/.cpp` | 经验球，含磁吸逻辑 |
| `WaveManager.h/.cpp` | 波次管理器：30s 波次 + 3s 休息 |
| `MonsterSpawner.h/.cpp` | 怪物生成器，按波次筛选敌人类型 |
| `BrotatoLikeGameMode.h/.cpp` | 游戏模式：创建管理器/HUD、绑定玩家事件、暂停/恢复、生成升级选项 |
| `LevelUpWidget.h/.cpp` | 升级选择界面（C++ 逻辑 + `BlueprintImplementableEvent`） |
| `UpgradeOptionWidget.h/.cpp` | 单个升级选项组件 |
| `MainHUDWidget.h/.cpp` | 主 HUD：提供 `UpdateHP`/`UpdateXP`/`UpdateWave`/`UpdateWeaponSlots` |
| `BrotatoLike.h/.cpp` | 模块入口 |
| `MyCharacter.h/.cpp` | 旧角色类，**计划废弃** |
| `BrotatoLikeCharacter.h/.cpp` | 原模板角色，**不可删除但不再扩展** |

> **C++ 核心层状态**: 全部实现完毕，编译通过（Win64 Development）。

### 关键设计原则（摘自 GameCoreDesign.md）

- **数据驱动**: 武器、敌人、升级数值全部走 DataTable（`FTableRowBase`），编辑器内调数值。
- **Component 分离**: 玩家属性独立为 `UPlayerStatsComponent`，武器和敌人逻辑各自内聚。
- **从简优先**: 第一版保持最小可行设计，保留扩展接口（virtual / 枚举扩展）。
- **无魔法数字**: 所有可调数值暴露为 `UPROPERTY(EditAnywhere)`。
