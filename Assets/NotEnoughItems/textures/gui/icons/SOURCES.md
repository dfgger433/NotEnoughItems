# NEI 可替换图标

本目录的 PNG 是 NotEnoughItems 项目随附的本地 UI 资源，不来自 Modernity-GTNH-UI 或 GTNH NEI 原始图集。图标由项目按现有界面配色绘制，用于替代原先的内置字形和程序箭头：

- `back.png`：配方查看器返回按钮。
- `previous.png` / `next.png`：分页与步骤导航。
- `add.png` / `remove.png`：数量和制作操作。
- `close.png`：关闭依赖树。
- `recipe_arrow.png`：配方输入与输出之间的方向箭头。

运行时通过 `ModernityFrames` 按资源路径加载；PNG 会由项目构建脚本复制到 DLL 旁的 `assets/notenoughitems/textures/gui/icons/`，同时嵌入 DLL 作为缺省回退。替换时请保留对应文件的像素尺寸（普通图标 16×16，配方箭头 33×36）。
