# BrotatoLike 🧙

> Unity Top-Down 动作生存游戏 Demo — 类《Brotato》玩法  
> **目标岗位**: 游戏客户端开发实习生 / Unity 客户端开发实习生

---

## 游戏截图


 ![主菜单](Screenshots/menu.png) 
 ![游戏进行中](Screenshots/gameplay.png) 
 ![升级界面](Screenshots/levelup.png) 
 ![暂停菜单](Screenshots/stop.png) 
 ![结算界面](Screenshots/gameover.png)



---

## 快速开始

### 环境要求

| 工具 | 版本 |
|------|------|
| Unity | 6.5 (6000.5.4f1) |
| 输入系统 | Input System Package |

### 打开项目

```bash
git clone <repo-url>
# 用 Unity Hub 打开项目文件夹
# 打开 Assets/Scenes/SampleScene.unity → 点击 Play
```

### 操作方式

| 操作 | 按键 |
|------|------|
| 移动 | WASD / 方向键 |
| 暂停 / 继续 | ESC |
| 选择升级选项 | 鼠标点击 |

### 游戏规则

1. 在圆形竞技场内生存，抵御无限波次的敌人
2. 武器**自动攻击**范围内的敌人
3. 击杀敌人掉落经验球，靠近自动拾取
4. 升级时**暂停游戏**，从 3 个随机选项中选 1 个强化
5. 死亡后显示结算：存活波次、等级、击杀数、存活时间
6. 目标：活得越久越好

---

## 功能特性

### 游戏系统

| 系统 | 说明 |
|------|------|
|  **3 种武器** | 魔法弹（单目标跟踪）、火焰喷射（扇形范围）、飞弹环绕（被动碰撞） |
|  **5 种敌人** | 史莱姆、骷髅、蝙蝠、暗影法师（远程）、幽灵（冲刺） |
|  **波次系统** | 20 秒/波 + 1 秒休息，敌人属性随波次指数增长 |
|  **升级系统** | 9 种属性升级 + 新武器，算法自动生成 3 选 1 |
|  **结算统计** | 波次、等级、击杀数、存活时间 |
|  **暂停菜单** | 继续 / 重新开始 / 返回主菜单 |
|  **地图选择** | 预留接口，后续可扩展多地图 |

### UI 界面

| 界面 | 说明 |
|------|------|
| 主菜单 | 游戏标题 + 地图选择 + 开始游戏 |
| 游戏 HUD | 血量条、经验条、波次倒计时、等级 |
| 升级界面 | 暂停时弹出，3 个选项含武器属性和描述 |
| 暂停菜单 | 半透明背景 + 三个操作按钮 |
| 结算界面 | 完整统计 + 再来一次 / 返回主菜单 |
| 伤害飘字 | 世界空间浮动数字，暴击橙红色 + `!` |

---

## 技术架构

### 整体设计

```
数据层 (ScriptableObject)
    ↓
逻辑层 (MonoBehaviour Components)
    ↓
表现层 (UI / VFX / 伤害数字)
```

### 核心架构模式

| 模式 | 应用 |
|------|------|
| **数据驱动** | 武器、敌人、升级全部通过 ScriptableObject 配置，数值调整无需改代码 |
| **Component 分离** | 玩家属性独立为 `PlayerStatsComponent`，武器和敌人逻辑各自内聚 |
| **状态机** | `GameManager.EGameState` 统一管理 5 种游戏状态（主菜单/游戏中/暂停/升级/结算） |
| **事件驱动 UI** | HUD 通过事件监听（`OnHPChanged` / `OnXPChanged`），不轮询 |
| **抽象基类** | `Weapon` 基类提供通用流程，子类只需实现 `Fire()` |

### 性能优化

| 优化 | 说明 |
|------|------|
| **敌人注册表** | 敌人出生/死亡时 O(1) 注册注销，索敌遍历注册表 + 缓存目标，替代 `FindObjectsByType` 全场景扫描，查询零 GC 分配 |
| **对象池** | 敌人、子弹、经验球、火焰粒子四类高频对象全部池化复用，消除运行时 Instantiate/Destroy |
| **蓄水池采样** | 随机索敌单遍均匀采样，避免收集列表分配 |
| **平方距离比较** | 索敌/拾取用 `sqrMagnitude` 替代 `Vector3.Distance`，省去开方 |

### 项目结构

```
Assets/
├── Scripts/
│   ├── GameTypes.cs              ← 核心枚举定义
│   ├── Definitions/              ← ScriptableObject 定义
│   ├── Game/                     ← 游戏管理器
│   │   ├── GameManager.cs        ← 全局状态机
│   │   ├── WaveManager.cs        ← 波次推进
│   │   └── MonsterSpawner.cs     ← 怪物生成
│   ├── Player/                   ← 玩家系统
│   ├── Weapons/                  ← 武器系统（基类 + 3 种实现）
│   ├── Projectiles/              ← 投射物
│   ├── Enemies/                  ← 敌人系统（基类 + 5 种实现）
│   └── UI/                       ← UI 界面（6 个界面脚本）
├── Data/                         ← ScriptableObject 资产
│   ├── Weapons/                  ← 3 个武器配置
│   ├── Enemies/                  ← 5 个敌人配置
│   └── Upgrades/                 ← 10 个升级选项配置
└── Scenes/
    └── SampleScene.unity         ← 游戏场景（含 Canvas + 所有 UI）
```

### 游戏状态机

```
MainMenu ──"开始游戏"──→ Playing ──"ESC"──→ Paused
   ↑                       │   ↑              │
   │    "返回主菜单"        │   └──"ESC"───────┘
   │                       │   "继续"
   │                       ├── LevelUp (升级弹出)
   │                       ├── 死亡 ──→ GameOver
   │                       │              │
   └───────"返回主菜单" ←──┘              │
                                           ├──"再来一次"──→ Playing
                                           └──"返回主菜单"──→ MainMenu
```

---

## 技术栈

| 技术 | 用途 |
|------|------|
| **Unity 6.5 (6000.5.4f1)** | 游戏引擎 |
| **C#** | 全部游戏逻辑 |
| **Input System Package** | WASD 移动 + 鼠标瞄准 |
| **TextMeshPro** | UI 文字渲染 |
| **ScriptableObject** | 数据驱动配置 |
| **UI Toolkit / uGUI** | 游戏界面 |

---

## 设计文档

项目设计细节见 `Docs/GameCoreDesign.md`，包含：

- 完整的数值公式（伤害计算、经验曲线、波次缩放）
- 类设计与 API 定义
- ScriptableObject 规格
- 敌人和升级的详细数值表

结构变更记录见 `Docs/CHANGELOG.md`。

---

## 面试展示亮点

| 亮点 | 说明 |
|------|------|
| **数据驱动架构** | 所有游戏数值走 ScriptableObject，策划可独立调参 |
| **Component 分离设计** | `PlayerStatsComponent` 独立负责属性，职责单一 |
| **性能意识** | 敌人注册表 + 四类对象池，高频路径零 GC 分配，体现对 GC 的关注 |
| **完整游戏流程** | 主菜单 → 游戏 → 暂停 → 升级 → 结算 → 重新开始，闭环完整 |
| **可扩展架构** | 抽象 Weapon 基类 + virtual 方法，新增武器只需实现 Fire() |
| **事件驱动 UI** | 避免每帧轮询，通过事件监听数据变化 |

---

## 后续规划

- [ ] 新武器：冰锥（穿透）、闪电链（连锁）、毒雾（地面 AoE）
- [ ] 精英怪 / Boss 波次
- [ ] 多角色选择
- [ ] 音效系统
- [ ] 武器进化系统
- [ ] 编辑器工具脚本（数据检查器、竞技场辅助线）

---

## 许可证

本项目仅用于求职 Demo 展示。