# 开发会话总结（Session Summary）

> 本文档汇总 2026-07-31 ~ 2026-08-17 全部开发会话的关键信息，供后续会话快速恢复上下文、避免重复讨论。
> 覆盖：项目决策、未完成工作（接线清单/待办）、当前数值体系、可复用素材。

---

## 1. 仓库与分支状态

| 项 | 状态 |
|----|------|
| 仓库 | https://github.com/yagumi0411/BrotatoLike_u3d（origin） |
| `main` | 单机版基线 `dd596d6`，论文/答辩专用，**不允许改动** |
| `feature/multiplayer` | 当前开发分支（阶段 0-2 已完成），本地领先远端 1 个提交（readme 更新 `7cbef7b`，推送时遇网络问题，待推） |
| 环境 | Git 2.55.0.3 在 `D:\Git`（PATH 需新终端生效）；Unity 6.5 (6000.5.4f1)；NGO 2.13.1 |

---

## 2. 会话时间线（全部历史）

| 日期 | 会话 | 内容 |
|------|------|------|
| 7/31 | 6e24bbbd / 69d94ff3 | 环境准备：Git 安装排查（实为 PATH 快照问题）；加载用户级 CLAUDE.md 规范 |
| 7/31 | 04941eaf | 毕业论文规划：7 章大纲（1.2 万字）、34 篇文献清单（`thesis/03_文献清单.md`）、模板格式规范；发现 README 宣称与代码不符 |
| 8/4 | 18934a3e | MonsterSpawner 三轮重构：视野外刷怪环带 + 修复"开局贴脸史莱姆"（三层根因） |
| 8/4 | e6c733a6 | 数值重设计 + 性能重构：对象池/敌人注册表/蓄水池采样（CHANGELOG 记载项） |
| 8/15 | 0494ec0d（worktree） | readme 更新（去 emoji，含双人/联机内容，已 cherry-pick 到主分支）；简历项目描述（含"核心挑战及解决方案"） |
| 8/16 | 696867c2 | 简历核实与修改；**GameFeel.cs 诞生（未接线）** |
| 8/15-17 | 64e82bd8 | 联机改造：分支策略、NGO 版本升级、阶段 0-2（当前主工作线；git 提交日期记录为 8/15） |

---

## 3. 关键决策记录（勿重复讨论）

**论文**
- 定位"设计 + 实现 + 评测"（B 方案），7 章结构，正文目标 1.2 万字
- 毕设功能"不新增，写进展望"——需求分析按现有功能写
- 参考文献硬指标：≥25 篇；中文一半以上；外文 ≥5；近 4-5 年占一半以上（当前 34 篇候选压线，建议补 2-3 篇 2023+ 中文期刊）
- 论文与项目**同步优化**（不是项目做完再写）

**联机（阶段 0-2 已定，阶段 3 待做）**
- 继续改造本项目，不新开（单机→联机的演进是面试故事）
- 移动 = 客户端权威（`NetworkTransform` AuthorityMode=Owner）；伤害/击杀/升级 = 服务器权威
- 升级选项由服务器生成 + RPC 广播（规避随机种子不同步）
- 对象池 × 网络：前期"正确性优先"，暂不接入 NGO 官方池化（面试可讲权衡）
- NGO 2.3.0 与 Unity 6.5 不兼容（CS0619），必须 2.13.1+

**刷怪**
- 贴边时"地图内优先"：宁可缺口方向不来怪，也不刷在视野内
- 方向相关环带（MonsterSpawner.cs:96）：下限 = 该方向穿出可视四边形距离 + 3

**简历**
- 手感反馈（7 项）"先实现再保留"——GameFeel 接线后才与简历声明相符
- 时间改"2026.5 - 至今"；双人模式写入简历

**工作偏好（用户级 CLAUDE.md，8/4 用户亲自修改）**
- 优先讨论、有不清楚先问、得到明确指令再改文件；最小改动；根因优先；范围控制

---

## 4. ⚠️ GameFeel.cs 接线清单（未完成，最高优先级）

`Assets/Scripts/Game/GameFeel.cs`（165 行，完整实现无 TODO）**已写好但零调用**。
简历声明"受击闪烁、击退、屏幕震动、Hitstop、命中特效、死亡消散"目前**只有伤害飘字真实存在**。

**接线点**（会话 8/16 中断处的规划）：
| 位置 | 改动 |
|------|------|
| `GameManager.Update` | 调 `GameFeel.Tick()`（顿帧计时） |
| `GameManager.Awake` | 调 `GameFeel.Reset()`（重开清理特效池） |
| `CameraFollow.LateUpdate` | 叠加 `GameFeel.GetShakeOffset()`（屏幕震动） |
| `PlayerStatsComponent.TakeDamage` | `AddShake(0.5f)` + `HitStop(0.08f, 0.15f)`（受击） |
| `Enemy.OnDeath` | `HitStop(0.06f, 0.2f)` + `SpawnHitEffect(pos, isCrit)`（击杀） |
| `Enemy` 受击 | 闪白（MaterialPropertyBlock + `_BaseColor`）、击退、死亡消散（Update 计时 + unscaledDeltaTime）——**此部分 GameFeel.cs 未包含，需在 Enemy 实现** |
| `ShadowMageEnemy` / `GhostEnemy` | 补 `IsDying` 防御（防消散中重复触发） |

注意：GameFeel 内部已正确处理与暂停/结算的 timeScale 冲突（`GetTargetTimeScale` 回查 GameManager 状态）。

---

## 5. 当前数值体系（8/4 定稿，已同步至 GameCoreDesign.md）

| 项 | 值 |
|----|-----|
| 波次缩放 | `1.06^(n-1)`（温和指数）；怪物移速缩放仅生效 30% |
| 演示节奏 | 波次 20s + 休息 1s，一局 2-3 分钟 |
| XP 需求 | `8×(L+1)` |
| 武器 | 魔法弹 7/0.6s（DPS≈11.7）、火焰 2.5/tick、飞弹 2.5 |
| 刷怪 | 环形带 `[13, 20]`（MonsterSpawner 最终版为方向相关环带，MinSpawnDistance=13、SpawnMargin=3） |
| 场景 | ArenaRadius=40、玩家 (0,1.15,0)、相机 (0,16.15,-10) |

---

## 6. 遗留待办（论文 / 项目 / 简历）

**论文（thesis/ 目录）**
- [ ] 第 5.7 节性能优化按优化结果定稿
- [ ] 第 6 章评测：Profiler 采集优化前后数据（GC 分配、帧耗时、20-50 敌同屏）
- [ ] 第 7 章结论与展望
- [ ] 章节正式写作（第 1 章绪论优先，引用 `thesis/03_文献清单.md` 的 34 篇）
- [ ] 确认学院名称（模板为"生物与环境工程学院"，计科专业需确认）
- [ ] 补 2-3 篇 2023+ 中文文献

**项目（CHANGELOG 官方遗留项）**
- [ ] 环绕飞弹（SpellOrbitWeapon）伤害为生成时快照，后期伤害升级无效
- [ ] DamagePopup 未池化（波 8+ 每秒 30+ 实例）
- [ ] 竞技场无物理边界墙
- [ ] 玩家朝向依赖 Ground 层射线（场景无碰撞体时模型朝向不更新）

**联机**
- [ ] 阶段 3：玩法同步（服务器权威伤害/击杀/升级、波次/刷怪/敌人同步）
- [ ] 阶段 4：打磨 + 面试准备（断线处理、readme 同步、面试话术）
- [ ] 用户需完成：PlayerNetwork.prefab + NetworkManager 场景配置 + 双端联机测试（阶段 2 代码已完成但**未验证**）
- [ ] readme 更新 `7cbef7b` 未推送（网络问题，待重试 `git push`）

**简历**
- [ ] GameFeel 接线完成前，简历中"7 项手感反馈"声明与实际不符

---

## 7. 可复用素材

**面试叙述**
- "单机→联机"演进：`IInputProvider` 输入抽象（键盘/方向键/网络三源复用）→ 同屏双人 → NGO 双客户端
- 性能四件套：对象池 / 敌人注册表（O(1) swap-remove）/ 蓄水池采样 / sqrMagnitude
- 刷怪算法：方向相关环带（射线-四边形求交、约束前置 vs 生成后处理）
- 升级不暂停 + 短暂无敌（双人玩法设计）+ 升级选项随机不同步问题
- "README 宣称与代码不符"教训：文档必须与代码同步（论文 5.7/6.3 章的优化对比素材）

**数学/算法素材**
- 凸集极值：可视地面是凸四边形，内部点到锚点最大距离必在顶点
- 射线-圆求交 `t²+2(p·d)t+(|p|²−R²)=0` 正根（优于生成后 clamp）
- 射影变换保直线性：屏幕边缘透视投影到地面仍是直线段

**文献**（`thesis/03_文献清单.md`，GB/T 7714 格式齐全）
- PCG/Roguelike 8 篇、数值平衡 8 篇、Unity 架构 9 篇、性能优化 9 篇
- 重点：Togelius 2011、Hunicke 2005（DDA）、Wattanapornprom 2024（2.5D Rogue-Lite 直接对标）、Sulyma 2025（零 GC 分配）、Unity 官方 GC 最佳实践

---

## 8. 环境与基础设施

- Git：`D:\Git`（2.55.0.3）；winget `--location` 可自定义安装目录
- academic-research-skills 插件已装（论文类任务可用 /ars-* 命令）
- `thesis/`：01_配置记录.md / 02_待优化事项.md / 03_文献清单.md（持续维护，勿重建）
- `Docs/`：CHANGELOG.md（结构变更）、GameCoreDesign.md（数值设计）、ProjectReview_求职评估.md
- GitHub zip 打开后 Hierarchy 为空 = 无 Library 缓存，双击 SampleScene.unity 即可，无需重拖
