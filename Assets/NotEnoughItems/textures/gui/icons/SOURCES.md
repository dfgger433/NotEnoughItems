# NEI 可替换图标

本目录是 NotEnoughItems 的独立界面资源：

- [back.png](<back.png>)：配方查看器返回按钮，可单独替换。
- [close.png](<close.png>)：关闭依赖树。
- [recipe_arrow.png](<recipe_arrow.png>)：配方输入与输出之间的方向箭头。

翻页和制作方式导航使用按钮图集内置的 `<`／`>` 字符；加减按钮使用内置 `+`／`−`。独立翻页、加减 PNG 属于旧资源，不再读取。

运行时通过 [ModernityFrames.cs](<../../../../../src/NotEnoughItems/ModernityFrames.cs>) 按资源路径加载；PNG 构建时复制到 DLL 旁，并嵌入 DLL 作为回退。替换时请保留对应文件的像素尺寸（返回与关闭 16×16，配方箭头 33×36）。
