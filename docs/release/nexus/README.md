# Nexus Mods 发布文档包 / Nexus Release Documentation

本目录（`docs/release/nexus/`）为《Casualties: Unknown》（虚妄轮回 / 意外伤亡）的 **Not Enough Items (NEI)** 模组在 [Nexus Mods](https://www.nexusmods.com/) 上发布所需的全部文案与元数据配置包。

---

## 包含文件清单 / File Manifest

| 文件名 | 类型 | 说明 / 用途 |
|---|---|---|
| [README.md](<docs/release/nexus/README.md>) | Markdown | 本目录概览与发布操作指引 |
| [NEXUS_DESCRIPTION.md](<docs/release/nexus/NEXUS_DESCRIPTION.md>) | Markdown | 完整的 Nexus 模组主页描述文档（中英双语、排版完整、含功能/安装/兼容/快捷键/许可与鸣谢） |
| [NEXUS_DESCRIPTION_BBCODE.txt](<docs/release/nexus/NEXUS_DESCRIPTION_BBCODE.txt>) | BBCode | **直接复制粘贴**至 Nexus Mods 网页富文本/BBCode 描述编辑器的纯文本文件 |
| [NEXUS_METADATA.md](<docs/release/nexus/NEXUS_METADATA.md>) | Markdown | Nexus Mods 页面创建表单速查表（模组名称、短简介、分类、前置依赖、标签、权限设置等） |
| [CHANGELOG_NEXUS.md](<docs/release/nexus/CHANGELOG_NEXUS.md>) | Markdown | 适用于 Nexus Mods 文件更新日志（Changelog tab）的发布更新记录 |

---

## 配套发布资源 / Associated Assets

模组根目录中已生成专用的高品质视觉宣传图集，可直接上传至 Nexus 页面媒体库：
- 顶部横幅 Banner：[nexus_assets/nexus_header_1300x372.png](<nexus_assets/nexus_header_1300x372.png>) (1300×372)
- 详细展示图 Showcase：[nexus_assets/nexus_showcase_1920x1080.png](<nexus_assets/nexus_showcase_1920x1080.png>) (1920×1080)
- 视觉设计说明：[nexus_visual_philosophy.md](<nexus_visual_philosophy.md>)

---

## 发布步骤建议 / Recommended Release Workflow

1. **创建模组页面 (Add a Mod)**：
   - 打开 [NEXUS_METADATA.md](<docs/release/nexus/NEXUS_METADATA.md>)，对照填入模组标题、255字符短描述、分类、标签与权限选项。
   - 在 Requirements（依赖项）中添加 **BepInEx 5 (Mono)** 和 **CUCoreLib (1.1+)**。
2. **填写模组描述 (Description)**：
   - 如果使用 Nexus 原生编辑器或 BBCode 模式：直接全选复制 [NEXUS_DESCRIPTION_BBCODE.txt](<docs/release/nexus/NEXUS_DESCRIPTION_BBCODE.txt>) 粘贴即可，内含折叠面板（`[spoiler]`）、标题高亮及快捷键表。
   - 如果编辑器支持标准 Markdown：可直接使用 [NEXUS_DESCRIPTION.md](<docs/release/nexus/NEXUS_DESCRIPTION.md>)。
3. **上传文件 (Files)**：
   - 上传由构建脚本 `pwsh -File ./scripts/Build.ps1` 生成的 `dist/NotEnoughItems-0.1.32.zip`。
   - 将 [CHANGELOG_NEXUS.md](<docs/release/nexus/CHANGELOG_NEXUS.md>) 对应版本的说明粘贴至文件的 Changelog 输入框中。
4. **上传宣传图片 (Media / Images)**：
   - 上传 `nexus_assets/nexus_header_1300x372.png` 并设置为 Header Image。
   - 上传 `nexus_assets/nexus_showcase_1920x1080.png` 作为首选展示图。
