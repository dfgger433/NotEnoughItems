# Not Enough Items (NEI) — Nexus Mods 更新记录 / Changelog

在 Nexus Mods 的 **Files** 页面上传新版本或在 **Changelog** 选项卡发布更新时，可直接复制此处的说明。

---

## 0.1.32

### English
- **Streamlined Packaging**: Single top-level `NotEnoughItems` folder inside the ZIP; extract directly into `BepInEx/plugins/`. Existing custom language files are preserved during installation.
- **Unified Documentation**: License notices, upstream attributions, and asset sources consolidated into `docs/` and distributed alongside `NotEnoughItems.dll`.
- **Standalone Localization**: UI strings migrated to standalone JSON files (`Lang/en_us.json` and `Lang/zh_cn.json`). Can be customized directly without recompilation.
- **Texture Standardization**: Clean asset layout adhering to NeoForge naming conventions under `assets/notenoughitems/textures/gui/`, maintaining backwards compatibility with custom resource packs.
- **Enhanced Recipe Tabs**: Pixel-perfect slicing for selected and unselected crafting category tabs with dedicated highlights aligned with recipe panel borders.
- **Improved Dropdown & Buttons**: Fixed arrow rendering in language dropdowns. Refined button borders and states under non-integer display scaling factors.
- **Together Multiplayer Crafting**: `Shift+C` and `Ctrl+Shift+C` wait for server confirmation between batches on compatible Together builds. Orders with manually locked ingredients are not supported in multiplayer.
- **Overlay and Tooltips**: Native game tooltips remain in front where NEI overlaps the inventory; NEI no longer uses a capture camera for this.
- **Quantity & Search Refinements**: Multi-digit quantity selector (`1–64`) unified between cheats and bookmark orders. Added case/space-insensitive `@ModName` filtering and optional Chinese Pinyin support via Just-Unknown-Characters.
- **Fluid & Material Property Previews**: Accurate mL fluid indicators with metric abbreviations (`1k = 1000mL`); rotating candidate cycling for ore/property ingredients with `R` usage lookup.

### 中文
- **精简安装包结构**：ZIP 压缩包顶层直接为 `NotEnoughItems` 单独目录，解压即可直接拖入 `BepInEx/plugins/`；安装脚本保留已有语言配置文件。
- **文档与授权规范化**：第三方鸣谢、开源许可证与素材溯源统一收录至 `docs/`，并随 DLL 一并打包分发。
- **独立外置本地化**：界面文本全面迁移至 `Lang/` 目录下的 JSON 语言包（`zh_cn.json` / `en_us.json`），玩家可自由修改润色，重启游戏生效。
- **现代贴图目录架构**：贴图资源按规范整理至 `assets/notenoughitems/textures/gui/`，保留材质包覆盖接口。
- **制作方式页签重构**：制作方式选中与未选中背景高光对齐配方面板，顶部翻页控件支持整页浏览。
- **控件与高分屏优化**：修复语言下拉栏与通用按钮在非整数界面缩放下的微小像素对齐与拉伸问题。
- **Together 联机批量制作**：在兼容版本中使用 `Shift+C` 或 `Ctrl+Shift+C` 制作合成链，每批等待服务器确认。不支持含手动锁定材料的联机自动制作订单。
- **界面层级与悬浮提示**：NEI 侧栏与原版背包槽重叠时保留原版物品提示，不再通过捕获相机合成画面。
- **数量选择与搜索语法**：数量选择框升级为多位数（1–64），作弊与带量收藏共用；模组过滤 `@` 忽略空格与大小写；集成可选拼音首字母搜索。
- **流体单位与性质候选**：流体容量精准显示 mL 及科学单位（`1k = 1000mL`）；矿物字典/性质原料支持格内动态轮播，按 `R` 可查询全游戏满足条件的容器或物品。

---

完整项目历史见[仓库更新记录](<../../CHANGELOG.md>)。 / See the [project changelog](<../../CHANGELOG.md>) for the full release history.
