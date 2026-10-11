# 0x72 DungeonTileset II 美术适配

文档更新：2026-10-12；适配与验证完成于 2026-10-11。

本次将 `Assets/Scenes/SampleScene.unity` 的角色与竞技场适配为二维像素外观。
直接打开该场景并进入 Play Mode 即可生效，无需手动拖拽素材。

## 素材来源

- 作者：Robert / 0x72；原页面另注明 GrafxKid 的调色贡献。
- 官方页面：https://0x72.itch.io/dungeontileset-ii
- 版本：v1.7；2026-10-11 从作者官方免费入口下载。
- 授权：CC0。资源原图及坐标表保存在 `Assets/Resources/DungeonArt`。
- 原图未修改；游戏运行时按作者坐标切片，并应用缩放、配色与动画。

## 游戏内对应关系

| 对象 | 图集素材 | 表现 |
|---|---|---|
| 玩家 | wizzard_m、weapon_red_magic_staff | 待机、移动、左右朝向、受击变色、法杖 |
| 史莱姆 | tiny_slug | 绿色软体怪物动画 |
| 骷髅 | skelet | 待机、移动、受击变色 |
| 蝙蝠 | imp | 紫色小恶魔，替代快速敌人外观 |
| 暗影法师 | necromancer | 法师待机动画、受击变色 |
| 幽灵 | angel | 半透明青色浮空外观、移动动画 |
| 经验球、魔法弹 | coin | 蓝色动画精灵 |
| 敌人投射物 | imp | 小型红色魔法精灵 |
| 竞技场 | floor_1 至 floor_8、wall_mid、column | 石砖地面、外围装饰、石柱 |

蝙蝠、幽灵、史莱姆采用视觉替代，敌人名称、行为和数值沿用原配置。
火焰和环绕武器沿用已有粒子特效。2026-10-12 商店 UI 单独改为像素装备面板，其他 UI 的迁移尚未完成。

## 商店 UI 素材（2026-10-12）

- 使用 [Kenney UI Pack - Pixel Adventure](https://kenney.nl/assets/ui-pack-pixel-adventure)，包内 License.txt 标识版本 2.0，授权 CC0。
- 边框、按钮、价格牌及槽位原素材保存在 `Assets/Resources/PixelDungeonUI`，来源映射见该目录 README.md；只修改 Unity 导入配置，不重绘原图。
- Panel 和 Button 以 Sprite 导入，通过 Sprite Editor 配置四边 6 像素 Border，Image 使用 Sliced 与 0.5 Pixels Per Unit Multiplier。空心边框另加不透明纯色底板，避免背景透出。
- 药水使用 0x72 图集副本 `DungeonItemsUI.png`，Multiple 模式手动切出 HealingPotion：X=288、Y=144、W=16、H=16，Pivot=Center，Border=0。原 DungeonArt 图集仍供战斗运行时切片。
- 商店布局保存在 SampleScene，商品模板保存在 `Assets/Prefabs/UI/HealingCard.prefab`，具体操作与脚本分工见 [商店 UI 编辑器维护指南](UIEditorGuide.md)。
- `Screenshots/wave_shop_preview.png` 和早期 `Outputs/PixelUI` 图片属于旧布局，尚未更新为当前编辑器版。

## 实现范围

- 保留三维移动、CharacterController、Rigidbody、命中和对象池。
- `DungeonSpriteVisual` 负责朝向相机、按移动播放动画、左右翻转和深度排序。
- 敌人移动判定使用 Rigidbody 速度，并保留 0.12 秒短暂移动状态；避免物理与渲染更新频率不同导致骷髅待机/跑动反复切换。切换动画时重置帧计时，跑动 8 FPS、待机 5 FPS，暂停时冻结动画。
- 相机采用正交投影；精灵 Point 采样、关闭 mipmap 和压缩。
- 石砖地面合并为一张网格，装饰不添加碰撞；原 Ground 碰撞和鼠标地面射线保留。
- 地面随机变化使用独立固定种子，不消耗战斗随机数。
- `Tools/Dungeon Art/Configure SampleScene` 可重新应用导入和场景配置。

## 验证

已通过编译及 Play Mode 检查：玩家及五种敌人的精灵、对象池复用、暂停/继续、
重开、返回菜单和实际渲染。测试用高血量未保存到场景或配置资产。
最终预览保存在 `Screenshots/dungeon_art_preview.png`。美术验证脚本未纳入仓库，本机 Temp 下的隔离项目与中间输出不属于可分发资产；Unity 可能清理 Temp。

相机为正交投影、size 8.5、俯角 60°；三维 Ground 碰撞保留。SampleScene 的 ArenaRadius 为 40，装饰不会构成物理边界。货架和天赋预览分别见 `Screenshots/wave_shop_preview.png`、`Screenshots/mage_talent_graph_preview.png`。

本次文档同步未重新运行编译或 Play Mode 测试。

注意：Unity 会清理 `Temp`。此次验证时 `Temp/CodexPackages` 中的 ZIP 被清理，
已根据修改前的 Git 版本和现存配置重建项目备份，并根据现存文档重建论文材料备份，
保存到 `Backups/ProjectPackages`。重建 ZIP 不保证与原 ZIP 的字节及打包范围完全一致。
