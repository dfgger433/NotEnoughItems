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

[生成脚本](<../../../generate_nexus_assets.py>) 直接解析 NEI 的 C# 帧表和 PNG，按默认 1280×720 布局重建书签、目录和配方面板。下面两张图是离线配图，不是游戏内截图；游戏背景、物品图标和运行时状态未带入发布图：
- 顶部横幅 Banner：[nexus_header_1300x372.png](<../../../nexus_assets/nexus_header_1300x372.png>) (1300×372)
- 详细展示图 Showcase：[nexus_showcase_1920x1080.png](<../../../nexus_assets/nexus_showcase_1920x1080.png>) (1920×1080)
- 视觉设计说明：[nexus_visual_philosophy.md](<../../../nexus_visual_philosophy.md>)
- 素材来源与许可约束：[NEXUS_ASSET_CREDITS.md](<NEXUS_ASSET_CREDITS.md>)（含 CC BY-NC-SA 署名、非商业、相同方式共享与混合图集说明）

### 离线导出

在模组源码根目录用 Python 3.10+ 和 Pillow 10.1+ 运行：

```powershell
python -m pip install "Pillow>=10.1"
python generate_nexus_assets.py                    # 导出贴图并重新生成发布图
python generate_nexus_assets.py --extract-only     # 只导出，不覆盖发布图
```

`dist/nei-ui/` 包含逐张 RGBA 裁切 PNG、来源与裁切坐标 `frames.json`、界面坐标 `layout.json`、透明版和深色底预览，以及打包好的 `nei-ui-textures.zip`。`--plugin-dir` 可传 NEI DLL 所在目录，优先读取活动资源包和替换 PNG；没有有效替换时使用当前源码自带 PNG，不读取旧 DLL 的内嵌资源。界面文字使用离线替代字体，不模拟 Unity 字体渲染。

要用本机游戏的原版贴图再做一份绷带示例（不进入发布图或 ZIP）：

```powershell
python -m pip install UnityPy
python generate_nexus_assets.py --extract-only --game-data '..\..\CasualtiesUnknown_Data'
```

该样本直接读取原版 Unity 资源中的物品贴图，不启动游戏；只演示两块撕裂敷料合成一卷绷带，不代表当前存档、完整目录或其他模组的物品与配方状态。

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
   - 上传 根目录 [`nexus_assets/nexus_header_1300x372.png`](<../../../nexus_assets/nexus_header_1300x372.png>) 并设置为 Header Image。
   - 上传 根目录 [`nexus_assets/nexus_showcase_1920x1080.png`](<../../../nexus_assets/nexus_showcase_1920x1080.png>) 作为首选展示图。
