# Nexus Mods 发布文档包 / Nexus Release Documentation

本目录是 **Not Enough Items (NEI)** 在 [Casualties: Unknown 的 Nexus Mods 板块](https://www.nexusmods.com/scavprototype)发布时使用的文案草稿。从 [NEXUS_METADATA.md](<NEXUS_METADATA.md>)开始填写页面字段。

源码位置是 `docs/release/nexus/`，重新构建后也可在 ZIP 内的 `NotEnoughItems/docs/release/nexus/` 找到同一套文案。如果旧 ZIP 没有这个目录，请用当前源码重建；运行时依赖和发布配图都不包含在安装 ZIP 中。

## 包含文件清单 / File Manifest

| 文件 | 用途 |
|---|---|
| [NEXUS_METADATA.md](<NEXUS_METADATA.md>) | 标题、短简介、分类、版本、标签、依赖、许可与主文件字段 |
| [NEXUS_DESCRIPTION_BBCODE.txt](<NEXUS_DESCRIPTION_BBCODE.txt>) | 粘贴到 Nexus 的 BBCode 描述编辑器 |
| [NEXUS_DESCRIPTION.md](<NEXUS_DESCRIPTION.md>) | 便于阅读和维护的 Markdown 页面描述 |
| [CHANGELOG_NEXUS.md](<CHANGELOG_NEXUS.md>) | 对应版本的中英文更新记录 |
| [NEXUS_ASSET_CREDITS.md](<NEXUS_ASSET_CREDITS.md>) | 配图的来源、署名、许可与发布限制 |

---

## 配套发布资源 / Associated Assets

以下素材和生成工具需从源码树获取。[生成脚本](<../../../generate_nexus_assets.py>)直接解析 NEI 的 C# 帧表和 PNG，按 1280×720 预览布局重建书签、目录和配方面板。两张图都不是游戏内截图，不含游戏背景、物品图标或运行时状态：
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

1. **核对公开分发权限**：按[第三方鸣谢与许可](<../../THIRD_PARTY_NOTICES.md>)与[配图素材来源](<NEXUS_ASSET_CREDITS.md>)核对混合图集、Modernity 许可版本差异和应随包提供的源码／署名。未核清前只整理本地草稿，不公开上传。
2. **填写页面与依赖**：按[页面字段表](<NEXUS_METADATA.md>)填写标题、短简介、分类、标签和权限。将 **BepInEx 5 (Mono)** 列为必需外部依赖，**CUCoreLib 1.1+** 列为必需站内依赖，其余列为可选。粘贴 [BBCode 描述](<NEXUS_DESCRIPTION_BBCODE.txt>) 后，在页面预览中检查排版和披露信息。
3. **升版并核对 Main Files**：先在[更新记录](<../../CHANGELOG.md>)与[本目录更新记录](<CHANGELOG_NEXUS.md>)顶部的 `## 未发布` 下写好本次内容，再从源码根目录运行 `pwsh -File ./scripts/Release.ps1 -Bump <版本>`。脚本会把 `## 未发布` 改名为版本号，同步[页面字段表](<NEXUS_METADATA.md>)中的版本、ZIP 名和显示名，校验各处版本一致后构建、运行回归测试，并生成 `dist/NotEnoughItems-<版本>.zip` 和配套 `.zip.sha256`（并打印 SHA-256）。打开 ZIP，确认顶层只有 `NotEnoughItems/`，其中有 `NotEnoughItems.dll`、`LICENSE`、`COPYING` 和本清单列出的 `docs/release/nexus/` 文件。按[页面字段表](<NEXUS_METADATA.md>)设置主文件版本，并粘贴[更新记录](<CHANGELOG_NEXUS.md>)中同版本的条目。
4. **准备 Media / Images**：从源码树取用上方两张发布图，按[配图素材来源](<NEXUS_ASSET_CREDITS.md>)署名，并标注为离线界面示意图。确认许可后再上传。
