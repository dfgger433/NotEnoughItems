# GTNH NEI 默认贴图

本目录的贴图来自 [GTNewHorizons/NotEnoughItems](https://github.com/GTNewHorizons/NotEnoughItems) 及其贡献者，固定在提交 [`6b7e465306810ad11d2413214077237798be13a7`](https://github.com/GTNewHorizons/NotEnoughItems/tree/6b7e465306810ad11d2413214077237798be13a7)。文件从[上游资源目录](https://github.com/GTNewHorizons/NotEnoughItems/tree/6b7e465306810ad11d2413214077237798be13a7/src/main/resources/assets/nei/textures)原样取得，不是 Modernity 资源包的版本，也没有裁剪、重绘或重新编码。

| 文件 | 尺寸 | Git blob SHA-1 | SHA-256 |
|---|---|---|---|
| [nei_sprites.png](nei_sprites.png) | 512×512 | `75138b2a91a2ac1e8bf598f9a5276874f231b0ed` | `31c38f414ef425f654e09e4727e5395657f5e9c62b1a5431f129c9e470c0f28d` |
| [nei_tabbed_sprites.png](nei_tabbed_sprites.png) | 256×256 | `09e61ac1111ef2ee16796c57594cf4ea56005738` | `a204e6357dbf8bf3e0d3bf4714e2df9bd40f7ba483d3f50720cb95ad37262f1e` |
| [slot.png](slot.png) | 18×18 | `5efbd42887bcfb8a5626da2b14f009e728f81b07` | `e7bba48e51b49054abd626c661a27c10f1901cc07d42dd1974d43cb8f639f7b9` |
| [recipebg.png](recipebg.png) | 256×256 | `3524f865e32e05292a8a163bf9315dc9049351b1` | `153091daa1981af79502347c6f5f2a4973e89d2000fcbc924c29a6ea339fa97a` |

上游仓库附带 [GNU Lesser General Public License 第 3 版](LICENSE.txt)（[同提交原文](https://github.com/GTNewHorizons/NotEnoughItems/blob/6b7e465306810ad11d2413214077237798be13a7/LICENSE.txt)）。许可证原文随贴图保留；它不改变本插件其他文件或 Modernity 素材的许可。

收藏按钮坐标来自[上游 `GuiFavoriteButton`](https://github.com/GTNewHorizons/NotEnoughItems/blob/6b7e465306810ad11d2413214077237798be13a7/src/main/java/codechicken/nei/recipe/GuiFavoriteButton.java#L35-L48)。以 PNG 左上角为原点，未收藏使用 `(20,152,18,20)`，已收藏使用 `(38,152,18,20)`；上游 Java 使用 256×256 的 UV，故坐标与尺寸均乘 2。

配方查看器的灰色背景使用上表 `recipebg.png` 的 `(0,0,184,174)` 区域，四边九宫格边框为 9 px；制作方式页签另由本插件随附的 `gui/controls/vertical_buttons_bg*.png` 提供。页数与制作方式名称横条是程序绘制的黑色 `0x30` 半透明覆盖，不属于此图集。
