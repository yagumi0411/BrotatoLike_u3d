# Pixel Dungeon UI

Source: https://kenney.nl/assets/ui-pack-pixel-adventure

Downloaded on 2026-10-12 from Kenney's official archive. The bundled License.txt
identifies the pack as UI Pack - Pixel Adventure (2.0), licensed under CC0.

包含以下四张来自 `Tiles/Large tiles/Thick outline` 的原始 PNG。另有
DungeonItemsUI.png，是 0x72 DungeonTileset II 图集副本，不属于 Kenney 素材；
其 CC0 来源见 Assets/Resources/DungeonArt/CREDITS.txt。

| Local file | Original file | Use |
| --- | --- | --- |
| Panel.png | tile_0009.png | Shop, product cards, tooltip |
| Slot.png | tile_0008.png | Inset item slots, status strip |
| Button.png | tile_0001.png | Continue button |
| Price.png | tile_0000.png | Price labels |

当前商店使用编辑器配置的 Sprite 和场景布局。Panel / Button 使用 Single、
Point、无 mipmap、无压缩，四边 Border=6，Image 为 Sliced，
Pixels Per Unit Multiplier=0.5。Slot / Price 为保留素材，当前槽位与价格区采用纯色 Image。

药水从 DungeonItemsUI 以 Multiple 模式手动切出 HealingPotion：
X=288、Y=144、W=16、H=16，Pivot=Center，Border=0；保留现有中文 TMP 字体。
PixelDungeonUI.cs 是初版代码生成样板的辅助类，当前 WaveShopUI 不再调用它。

In Unity, use `Tools > Pixel Dungeon UI > Preview Shop` to enter Play Mode and
open the shop. Exit Play Mode after previewing; scene assets are not saved.
`PixelDungeonShopPreview.Run` 是旧版批量验证入口，仍依赖旧对象名称，尚未适配
当前编辑器布局。Outputs/PixelUI 中的既有图片和结果不代表当前场景的最新验证。

层级、字段引用、Prefab 实例覆盖和手工维护步骤见 Docs/UIEditorGuide.md。
