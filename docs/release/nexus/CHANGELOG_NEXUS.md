# Not Enough Items (NEI) — Nexus Mods 更新记录 / Changelog

在 Nexus Mods 的 **Files** 页面上传新版本或在 **Changelog** 选项卡发布更新时，可直接复制此处的说明。

---

## 0.1.37

### English
- **Debug tab:** Moved “Retry catalog refresh” directly below the heading, followed by “Allow spawning missing chain materials” and “Fill two pages of method tabs.”

### 中文
- **调试页：** 将「重试刷新目录」移到标题正下方，之后依次是「允许刷取合成链缺失原料」和「填充两页制作方式」开关。

## 0.1.36

### English
- **Chain shortage spawning:** The Debug opt-in (off by default) and bill button are back. The opt-in also gates `Ctrl+Shift+M`. Single-player converts missing liquid mL into full temporary bottles with the usual five-minute in-game lifetime. Bottles and solids share the 64-object cap. Together still uses the server's `give` permission for solids and rejects a batch with any liquid shortage before giving anything.
- **Release packaging:** Uncommitted changes can no longer be packaged under the version in Git `HEAD`. Both changelogs must have release notes, including English and Chinese Nexus entries. Contributors must bump the version and add notes with each batch of changes.

### 中文
- **合成链缺料刷取：** 恢复默认关闭的调试开关及账单按钮，并用同一个开关控制 `Ctrl+Shift+M`。单机按缺口 mL 刷取满临时瓶，沿用 5 分钟游戏时间寿命；瓶和固体共用 64 件上限。Together 的纯固体批次仍使用服务端 `give` 权限，含液体缺口时整批拒绝。
- **发布打包：** 有未提交改动时，不再允许使用 Git `HEAD` 的原版本号打包。两份更新记录都必须有条目，Nexus 须包含中英文。每批改动都要同时升版和补日志。

## 0.1.35

### English
- **Release tooling:** `scripts/Release.ps1 -Bump <version>` now names the release in both changelogs, updates the package version and Nexus metadata in one step, and refuses to package when any version reference is stale or a `## 未发布` section is still pending.
- **Documentation:** The per-version steps are written down: add entries under `## 未发布`, then run the bump command to build the versioned ZIP and its `.zip.sha256`. No in-game behavior changed in this update.

### 中文
- **发布工具：** `scripts/Release.ps1 -Bump <版本>` 现在会改写两份更新记录的标题、同步安装包版本与 Nexus 页面字段；任何版本引用不一致或仍留有 `## 未发布` 时都拒绝打包。
- **文档：** 每次升版的步骤已写入发布文档：先在 `## 未发布` 下补条目，再运行升版命令生成带版本号的 ZIP 与 `.zip.sha256`。本次没有游戏内行为变化。

## 0.1.34

### English
- **Catalog ordering:** The item catalog now lists vanilla items first, then each mod's items as one continuous block sorted by the mod's display name. Names ignore case and surrounding spaces, and consecutive digits are compared numerically (so `A2` comes before `A10`). Items keep their registration order within a mod, and searching only filters results without reordering them.
- **Debug material spawning (opt-in):** A new "Allow spawning missing chain materials" developer option, off by default, adds a "Spawn missing materials" button to bookmark chain bills in Cheat mode. It re-checks carried and nearby stock and gives only the concrete solid shortages without crafting; liquid requirements, unresolved ingredient choices, and unsupported damaged-only requirements reject the whole batch. Multiplayer uses the server's `give` permission and the existing per-command limit.
- **Packaging:** `scripts/Release.ps1` builds, runs the regression tests, and produces a versioned install ZIP with a `.zip.sha256` checksum file. Asset folder casing is unified so the packaged texture paths no longer rely on Windows being case-insensitive.
- **Installation:** Extract the ZIP’s single `NotEnoughItems` folder into `BepInEx/plugins/`. If you have customized translations, back them up before replacing files; the install script preserves existing language files.

### 中文
- **目录排序：** 物品目录先显示原版物品，再按模组显示名称把各模组的物品排成连续区块。名称忽略大小写和首尾空白，连续数字按数值比较（如 `A2` 在 `A10` 前）。模组内保持注册顺序，搜索只筛选结果，不改变顺序。
- **调试刷取（默认关闭）：** 「开发调试」页新增可保存的「允许刷取合成链缺失原料」选项。开启后，作弊模式的书签合成链账单会显示「刷取缺失原料」按钮，重新核对随身和附近库存，只发放缺少的具体实物，不执行制作；液体需求、尚未选定的原料和无法满足的损坏要求会整批拒绝。联机沿用服务器的 `give` 权限和单批上限。
- **打包：** `scripts/Release.ps1` 会构建、运行回归测试并生成带版本号的安装 ZIP 与 `.zip.sha256` 校验文件；资源目录大小写已统一，包内贴图路径不再依赖 Windows 的大小写不敏感行为。
- **安装：** 将 ZIP 中唯一的 `NotEnoughItems` 文件夹解压至 `BepInEx/plugins/`。手动覆盖前请备份自定义翻译；安装脚本会保留已有语言文件。

## 0.1.33

### English
- **Crafting chain fix:** Bookmark chains now use intermediate items already in your inventory before scheduling prerequisite crafts. When stock covers the requirement, the chain skips that step and its raw materials. When stock is short, it schedules only the batches needed to cover the shortfall. The chain tooltip and dependency tree update their material and shortage lists accordingly.
- **Installation:** Extract the ZIP’s single `NotEnoughItems` folder into `BepInEx/plugins/`. If you have customized translations, back them up before replacing files; the install script preserves existing language files.

### 中文
- **合成链修复：** 书签合成链优先使用背包已有的中间产物。库存足够时跳过对应前置步骤，不再计入其原料；库存不足时只为缺口安排所需批次。合成链提示与依赖树会显示重新计算的用料和缺料。
- **安装：** 将 ZIP 中唯一的 `NotEnoughItems` 文件夹解压至 `BepInEx/plugins/`。手动覆盖前请备份自定义翻译；安装脚本会保留已有语言文件。

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
