# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working on code in this repository.

## 项目概述

BrotatoLike 是一个 Unity 6.5 (6000.5.4f1) top-down 动作生存游戏 Demo，类《Brotato》玩法。核心玩法：玩家控制法师角色移动，武器自动攻击敌人，击杀敌人掉落经验球，升级时从随机选项中选择强化能力。

- **设计文档**: `Docs/GameCoreDesign.md` — 包含完整的数值公式、类设计、ScriptableObject 规格、游戏流程。所有代码实现须以此文档为准。
- **Unity 版本**: 6.5 (6000.5.4f1)
- **渲染管线**: URP (Universal Render Pipeline)
- **输入系统**: Input System Package

## 构建 & 开发

- Unity 编辑器版本：**6000.5.4f1**（Unity 6.5）
- 编译：编辑器内 `Ctrl+B`（Build），或保存脚本自动编译
- 场景: `Assets/Scenes/SampleScene.unity`
- 脚本目录: `Assets/Scripts/`
- 渲染管线: URP（Universal Render Pipeline）

## 技术架构

**架构模式**: C# 为主，Unity 组件系统。所有游戏逻辑在 C# 层实现，Editor 脚本仅用于编辑器扩展。

### 游戏流程

```
MainMenu ──"开始游戏"──→ Playing ──"ESC"──→ Paused
   ↑                       │   ↑              │
   │    "返回主菜单"        │   └──"ESC"───────┘
   │                       │   "继续"
   │                       ├──LevelUp(升级弹出)
   │                       ├──死亡──→ GameOver
   │                       │           │
   └───────"返回主菜单" ←──┘           │
                                        ├──"再来一次"──→ Playing
                                        └──"返回主菜单"──→ MainMenu
```

### 游戏状态 (GameManager.EGameState)

| 状态 | 说明 | Time.timeScale |
|------|------|---------------|
| MainMenu | 主菜单 | 0 |
| Playing | 游戏进行中 | 1 |
| Paused | ESC暂停 | 0 |
| LevelUp | 升级选项界面 | 0 |
| GameOver | 死亡结算 | 0 |

### C# 源码 (`Assets/Scripts/`)

| 文件/文件夹 | 说明 |
|------------|------|
| `GameTypes.cs` | 4 个枚举：`EUpgradeType`、`EWeaponType`、`EEnemyType`、`ETargetMode` |
| `Definitions/` | ScriptableObject 定义：`WeaponDefinition`、`EnemyDefinition`、`UpgradeOption` |
| `Game/` | 游戏管理器 |
| ├ `GameManager.cs` | **全局状态机**，管理UI显隐/暂停/升级生成/击杀/计时/地图选择 |
| ├ `WaveManager.cs` | 波次推进，30秒/波 + 3秒休息 |
| └ `MonsterSpawner.cs` | 竞技场边缘生成怪物，按波次解锁类型 |
| `Player/` | 玩家相关 |
| ├ `PlayerController.cs` | WASD移动（Input System），武器槽管理 |
| └ `PlayerStatsComponent.cs` | 玩家属性计算 + 升级应用 + 事件广播 |
| `Weapons/` | 武器基类和各武器实现 |
| ├ `Weapon.cs` | **武器基类**：冷却管理、索敌(限频0.2s)、伤害/暴击计算 |
| ├ `MagicBulletWeapon.cs` | 魔法弹：单目标跟踪弹丸 |
| ├ `FlameThrowerWeapon.cs` | **火焰喷射**：扇形区域检测（非粒子碰撞），视觉粒子纯特效 |
| └ `SpellOrbitWeapon.cs` | 飞弹环绕：环绕碰撞伤害 |
| `Projectiles/` | 投射物 |
| ├ `MagicBulletProjectile.cs` | 魔法弹丸：直线飞行，命中销毁 |
| └ `OrbitProjectile.cs` | 环绕飞弹：绕玩家旋转，碰撞冷却 |
| `Enemies/` | 敌人基类和实现 |
| ├ `Enemy.cs` | **基类**：物理移动、碰撞排斥、接触伤害、击杀通知+经验球生成 |
| ├ `SlimeEnemy.cs` | 史莱姆：慢速肉盾 |
| ├ `SkeletonEnemy.cs` | 骷髅：标准近战 |
| ├ `BatEnemy.cs` | 蝙蝠：快速脆皮群怪 |
| ├ `ShadowMageEnemy.cs` | 暗影法师：站桩远程弹幕 |
| └ `GhostEnemy.cs` | 幽灵：周期性冲刺 |
| `EnemyProjectile.cs` | 敌人弹幕投射物 |
| `XPOrb.cs` | 经验球：磁吸逻辑，拾取触发升级 |
| `CameraFollow.cs` | 俯视角相机平滑跟随 |
| `VisualHelper.cs` | 运行时几何体创建辅助 |
| `UI/` | UI 界面 |
| ├ `MainMenuUI.cs` | **主菜单**：标题+地图选择(预留)+开始游戏 |
| ├ `MainHUD.cs` | **游戏HUD**：HP/XP/波次/倒计时（事件驱动） |
| ├ `LevelUpUI.cs` | **升级选择**：3选1，暂停时弹出 |
| ├ `PauseUI.cs` | **暂停菜单**：继续/重新开始/返回主菜单 |
| ├ `GameOverUI.cs` | **结算界面**：波次/等级/击杀/时间+再来一次/返回主菜单 |
| └ `DamagePopup.cs` | **伤害飘字**：世界空间TMP，向上飘动+渐隐 |

> **废弃**: `UpgradeManager.cs` 已从场景移除，功能由 `GameManager` 统一管理。

### 关键设计原则

- **数据驱动**: 武器、敌人、升级数值全部走 ScriptableObject，编辑器内调数值。
- **Component 分离**: 玩家属性独立为 `PlayerStatsComponent`，武器和敌人逻辑各自内聚。
- **从简优先**: 第一版保持最小可行设计，保留扩展接口（virtual / ScriptableObject / 枚举扩展）。
- **无魔法数字**: 所有可调数值暴露为 `[Header]` 或 `[SerializeField]` Inspector 可编辑字段。
- **事件驱动 UI**: UI 通过 `OnHPChanged`/`OnXPChanged`/`OnLevelUp` 等事件监听数据，不轮询。

### 物理单位

Unity 中建议 1 单位 = 1 米，但本游戏采用缩放比例：

- 玩家半径: ~0.5 米
- 移动速度: 6 米/秒 (约 UE 600)
- 攻击范围: 15 米 (约 UE 1500)
- 竞技场半径: 30 米 (约 UE 3000)

对应 UE 单位的换算: Unity 1 = UE 100 (使用 100x 缩放)