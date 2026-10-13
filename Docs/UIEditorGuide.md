# 商店 UI 编辑器维护指南

更新日期：2026-10-12。本文记录当前手工搭建流程及编辑器与脚本的职责。适用于 `Assets/Scenes/SampleScene.unity`。

## 1. 职责分工

| 内容 | 配置位置 |
|---|---|
| 层级、位置、锚点、尺寸、字体、图片、颜色 | Unity 编辑器中的场景与 Prefab |
| 固定布局、默认隐藏、按钮视觉状态 | Hierarchy / Inspector |
| 商品卡片模板 | Assets/Prefabs/UI/HealingCard.prefab |
| 金币、生命、价格、购买状态和提示文字 | WaveShopUI.Refresh |
| 购买资格、扣金币、治疗、每波限购、继续游戏 | GameManager |
| 购买和悬停事件 | WaveShopUI.Awake |
| 继续按钮事件 | Inspector 中的 Button.On Click |

商店不再由代码创建 Canvas、Image、文字或商品卡片。Awake 添加 EventTrigger 仅用于交互事件，不生成视觉布局。主菜单天赋入口的迁移见第 7 节；天赋图内部仍沿用运行时生成方式。

## 2. 层级与布局

复用原 Canvas：Screen Space - Overlay、Constant Pixel Size。编辑时使用 1280 × 720 Game 预览，参考布局单位；目前没有迁移整个 Canvas 的缩放策略，不宣称已完成多分辨率适配。

```text
Canvas
└── WaveShopRoot
    └── ShopPanel
        ├── PanelBackground
        ├── ShopTitle
        ├── StatusPanel
        │   └── StatusText
        ├── HealingCard
        │   ├── CardBackground
        │   ├── ItemSlot
        │   │   └── HealingIcon
        │   ├── ItemName
        │   ├── PricePanel
        │   │   └── PriceText
        │   └── PurchaseButton
        │       └── ButtonText
        ├── ReservedCard1
        ├── ReservedCard2
        ├── ShopHint
        ├── ContinueButton
        │   └── ButtonText
        └── HealingTooltip
            ├── TooltipBackground
            └── TooltipText
```

WaveShopRoot 在原 Canvas 子对象的最后，作为遮罩和商店根节点；HealingTooltip 在 ShopPanel 子对象的最后，显示时覆盖其他卡片。装饰底板放在各自父对象的第一个子对象位置。

主要对象采用中央锚点、中央 Pivot、Scale=1。下表为本次教学采用的参数，后续调整以保存的场景资产为准。

| 对象 | 相对父对象的位置 X / Y | 宽 / 高 |
|---|---|---|
| ShopPanel | 0 / 0 | 920 / 560 |
| ShopTitle | -225 / 221 | 380 / 48 |
| StatusPanel | 0 / 151 | 840 / 60 |
| HealingCard | -276 / -36 | 244 / 280 |
| ReservedCard1 / ReservedCard2 | 0 / -36、276 / -36 | 244 / 280 |
| ItemSlot | 0 / 43 | 104 / 104 |
| HealingIcon（父对象为 ItemSlot） | 0 / 0 | 68 / 68 |
| ItemName | 0 / -28 | 210 / 32 |
| PricePanel | 0 / -67 | 204 / 30 |
| PurchaseButton | 0 / -104 | 204 / 32 |
| ShopHint | -205 / -219 | 420 / 50 |
| ContinueButton | 270 / -219 | 280 / 60 |
| HealingTooltip | -3 / -21 | 274 / 172 |

WaveShopRoot 横纵拉伸、四边为 0，Image 为黑色且 Alpha 约 190/255，开启 Raycast Target 阻挡后面的界面。PanelBackground、CardBackground、TooltipBackground 横纵拉伸、四边为 12，颜色 #211D1A，完全不透明。StatusPanel 和 ItemSlot 使用深色纯色背景。

商店根节点和详情默认关闭。正文使用现有支持中文的 TMP Font Asset；文字和装饰 Image 关闭 Raycast Target，购买与继续按钮的 Image 保留。场景里已有 EventSystem，不重复创建。

## 3. 素材导入

- Panel.png / Button.png：Sprite、Single、PPU=100、Point、无压缩、无 Mip Maps；Sprite Editor 四边 Border=6。Image 使用 Sliced，Pixels Per Unit Multiplier=0.5。
- Sliced 保持四角形状并拉伸边和中心；空心边框的中心仍透明，所以要另加底板。
- DungeonItemsUI.png 是 DungeonArt 原图的副本，使用 Sprite / Multiple，手动切出 HealingPotion（288,144,16,16），Pivot 为 Center、Border 为 0。
- HealingIcon 使用 Simple 和 Preserve Aspect，保持 68 × 68，不点击 Set Native Size。父对象必须是 ItemSlot；位置相对于父对象计算。
- 来源与许可证见 [美术说明](ArtAdaptation.md) 及 `Assets/Resources/PixelDungeonUI/README.md`。

## 4. Prefab 实例覆盖

将 HealingCard 拖入 Assets/Prefabs/UI 保存为模板。两个预留卡片使用该 Prefab 的实例，修改位置和文字，关闭各自 HealingIcon 游戏对象，并取消 PurchaseButton.Interactable。

这些差异保留为实例覆盖，不向原药水模板 Apply All；否则隐藏图标或禁用购买可能影响正式药水卡片。修改共用边框等模板样式时，先明确哪些内容要影响所有实例。

## 5. Inspector 引用及事件

WaveShopUI 必须挂在 GameManager 所在的对象，Awake 使用 GetComponent 获取管理器。九个字段都必须填写，缺失时报告错误并停止初始化。

| 字段 | 对象 |
|---|---|
| Root | WaveShopRoot |
| Status | StatusPanel/StatusText |
| Purchase Text | HealingCard/PricePanel/PriceText |
| Purchase Label | HealingCard/PurchaseButton/ButtonText |
| Purchase | HealingCard/PurchaseButton |
| Healing Icon | HealingCard/ItemSlot/HealingIcon |
| Tooltip | HealingTooltip |
| Tooltip Text | HealingTooltip/TooltipText |
| Hint | ShopHint |

PurchaseButton.On Click 留空，由 Awake 注册 PurchaseHealing。ContinueButton.On Click 在 Inspector 中引用场景 GameManager，并选择 ContinueFromShop。不要给购买按钮重复绑定 BuyHealing。

事件链：购买点击 → PurchaseHealing → GameManager.BuyHealing → Refresh；金币变化 → OnGoldChanged → Refresh。详情绑定在购买按钮，悬停或选中时显示，两个状态都解除后隐藏。GameManager.SetState 通过 SetVisible 控制商店显隐。

## 6. 验证与常见问题

已由用户确认基本接入及显示正常，修正图标层级、按钮底边距和预留图标；以下完整购买清单仍待逐项重新记录：

- 满血、金币不足、本波已购买时禁用购买。
- 有金币且缺血时购买，正确扣款与治疗，不能重复购买。
- 悬停/移开、键盘选中/取消及禁用状态下的说明显示。
- 继续下一波隐藏商店并恢复 Playing；新波次解除本波限购。
- 返回菜单、重开与其他面板的显隐关系。

菜单 `Tools > Pixel Dungeon UI > Preview Shop` 可快速进入商店。旧批量入口 PixelDungeonShopPreview.Run 仍使用 PurchaseHealing、ContinueWave、WaveShopCanvas 等旧对象名称，尚未适配手工布局，不作为新版验证命令。旧截图和历史 PASS 只对应当时版本。

编辑与保存要退出 Play Mode；Scene 透视或辅助网格不用于判断最终 UI 效果，以 Game 窗口为准。按钮贴边时计算控件上下边与父边框的内边距，而不是只确认控件没有超出父矩形。

## 7. 主菜单天赋入口迁移（编辑器接线待完成）

脚本已停止生成主菜单的 OpenTalents 和 MetaProgressionStatus，改为使用 MainMenuUI 的 Inspector 引用。场景未由脚本修改，必须在退出 Play 模式后完成以下步骤，天赋入口才会显示。

1. 在 Canvas/MainMenuPanel 下创建 UI > Button - TextMeshPro，命名 OpenTalents；子文字改为“天赋图”。可复制 StartButton 保留字体和样式，但应清空复制得到的 Button.On Click。
2. 在同一父对象下创建 UI > Text - TextMeshPro，命名 MetaProgressionStatus。字体使用现有支持中文的 TMP Font Asset，编辑态文字可填写“天赋进度”。
3. 两个根对象 Anchor Min、Anchor Max、Pivot 均为 (0.5, 0.5)，Rotation=(0,0,0)、Scale=(1,1,1)、Pos Z=0。按下表设置。

| 对象 | Pos X / Y | Width / Height |
|---|---|---|
| OpenTalents | 0 / -150 | 210 / 48 |
| MetaProgressionStatus | 0 / -245 | 700 / 130 |

统计区域比旧版本增加高度，容纳两行进度、上局奖励以及可能出现的存档提示。位置基于 1280 × 720 预览、现有 Constant Pixel Size Canvas，以及上一轮主菜单布局；其他预览尺寸需另行检查。

4. OpenTalents 子文字 Rect Transform 横纵拉伸，Left/Right=8、Top/Bottom=4。两个 TMP 组件字号均为 20，关闭 Auto Size，水平 Center、垂直 Middle，Margins 四项为 0，关闭文字 Raycast Target。按钮自身 Image 保留 Raycast Target。
5. 选中 MainMenuPanel，在 MainMenuUI 中，将 OpenTalents 拖给 Open Talents Button，将 MetaProgressionStatus 拖给 Meta Progression Status。OpenTalents 的 On Click 留空，由 MainMenuUI.Awake 注册事件。
6. 保存场景，进入 Play：确认没有重复控件、统计正常更新，天赋图能打开并返回；开始游戏后主菜单控件随面板隐藏，再返回主菜单后正常显示。

位置、字体、颜色和尺寸均由编辑器维护；MainMenuUI 处理入口点击，TalentGraphUI.Refresh 更新统计内容。缺失引用时 MainMenuUI 会报告接线错误，不再生成替代控件。
