# Not Enough Items — Casualties: Unknown

NEI 的本地 BepInEx 移植版。书签与物品目录在原版背包和 Unknown Inventory Terminal 中显示；打开原版制作菜单时只保留左侧书签。打开原版容器、交易、物品自定义、液体转移或医疗面板时隐藏。原版背包和终端的右侧物品目录默认都开启，可在 NEI 扳手设置的「显示」页分别关闭；升级时保留已保存的开关。背包和终端共用以 2560×1440 为基准的默认布局：横纵坐标分别随窗口宽高换算；扳手 → 布局可开启「拖动面板调整布局」，拖动各栏标题移动整栏，拖动边缘把手调整宽度，完成后点「完成」；宽度、位置、上下偏移和物品图标大小都会保存。背包侧栏默认透明，配方查询使用有底色的独立面板。爱心、书签、扳手、配方页签等使用 [interface_states.png](<assets/notenoughitems/textures/gui/sprites/interface_states.png>) 的对应切片。

完整版本历史见[更新记录](<docs/CHANGELOG.md>)。

## 安装

游戏需为 Mono 版，并已安装 BepInEx 5 和 CUCoreLib 1.1+。Unknown Inventory Terminal 0.4.4+ 为可选依赖，ZIP 不包含这些依赖。

1. 退出游戏，打开 `NotEnoughItems-<版本>.zip`。包内顶层只有一个 `NotEnoughItems` 文件夹。
2. 将整个 `NotEnoughItems` 文件夹拖入游戏目录的 `BepInEx/plugins/`。安装好的 DLL 应位于 `BepInEx/plugins/NotEnoughItems/NotEnoughItems.dll`；不用自己建目录，也不要拖入已有的 `NotEnoughItems` 文件夹内。
3. 升级时先备份配置、自定义语言和材质包。若手动合并目录，注意不要覆盖想保留的语言文件，也不要在 `plugins/` 的其他位置保留旧版 DLL。

## 编译与部署

源码构建另需 PowerShell 7 和 .NET SDK 8 或更高。

```powershell
# 编译、运行回归测试，并生成拖入 plugins 即可安装的目录和 ZIP
pwsh -File ./scripts/Build.ps1

# 退出游戏后，安装 dist/NotEnoughItems（已有语言文件不覆盖）
pwsh -File ./scripts/Install.ps1
```

[Build.ps1](<scripts/Build.ps1>) 和 [Install.ps1](<scripts/Install.ps1>) 默认使用项目目录上两级作为游戏根；也可通过 `-GameRoot` 指定。未压缩的 `dist/NotEnoughItems/` 文件夹也可直接拖入 `BepInEx/plugins/`。

## 一键生成发布 ZIP

发布打包需要带提交历史的 Git 工作区。从源码根目录运行：

```powershell
# 写完两份更新记录后，自动升补丁版、构建、测试并生成 ZIP
pwsh -File ./scripts/Release.ps1 -Bump patch

# 源码不在游戏的 mods/ 目录下时，可指定游戏根目录：
pwsh -File ./scripts/Release.ps1 -Bump patch -GameRoot 'D:\Games\Casualties Unknown'

# 重跑已同步好版本和日志的构建（不再升版）
pwsh -File ./scripts/Release.ps1
```

[Release.ps1](<scripts/Release.ps1>) 复用构建流程：以 Release 配置编译并运行回归测试，全部通过后，根据 [版本配置](<src/Directory.Build.props>) 中写入 DLL 的版本生成 `dist/NotEnoughItems-<版本>.zip`。ZIP 顶层只有 `NotEnoughItems/`。脚本会打印 ZIP 的 SHA-256，并在旁边生成 `.zip.sha256` 校验文件。它不会安装 Mod 或上传文件；重跑会替换该版本的本地产物。没有 Git 历史或只想试编译时，使用 [Build.ps1](<scripts/Build.ps1>)；它不执行发布门禁。

### 每次修改都要升版并补日志

代码、资源、脚本、测试和文档的修改都适用。把同一次交付的改动作为一批，**在提交或交付前主动升版、补齐两份更新记录**，无需另行提醒。细则见源码树中的 [AGENTS.md](<AGENTS.md>)。

1. 在 [项目更新记录](<docs/CHANGELOG.md>) 顶部的 `## 未发布` 下写本批变更的中文条目，可按「新增 / 修复 / 打包与文档」分节。
2. 在 [Nexus 更新记录](<docs/release/nexus/CHANGELOG_NEXUS.md>) 顶部的 `## 未发布` 下分别写 `### English` 和 `### 中文` 条目。
3. 提交前运行 `pwsh -File ./scripts/Release.ps1 -Bump patch`。普通修复和文档改动用 `patch`，兼容性新增功能用 `minor`，不兼容变更用 `major`；也可指定更高的 `MAJOR.MINOR.PATCH` 版本。将版本配置、两份更新记录和同步后的 [Nexus 页面字段](<docs/release/nexus/NEXUS_METADATA.md>) 与本批改动一起交付。

`-Bump` 会把两份 `## 未发布` 标题改为新版本号，并同步版本配置和 Nexus 页面字段。发布脚本会在构建前拦截版本倒退、日志缺失或版本不一致。相对当前 Git `HEAD` 有已暂存、未暂存或未忽略的新增文件时，版本必须高于 `HEAD` 的版本；Git 忽略的构建产物不算改动。不带 `-Bump` 可重跑干净的已提交版本，或已升版且日志齐全的当前工作区。

## Nexus Mods 发布资料

从 [Nexus 发布指引](<docs/release/nexus/README.md>)进入，或直接打开 [页面字段与发布前检查](<docs/release/nexus/NEXUS_METADATA.md>)。当前构建的 ZIP 将这套文案放在 `NotEnoughItems/docs/release/nexus/`；配图单独保留在源码树，不在安装 ZIP 中。公开上传前请先核对素材许可与署名。

书签分组的交互以项目随附的 [GTNH 中文维基](https://gtnh.huijiwiki.com/wiki/Not_Enough_Items#分组与合成链) 和 [GTNH NEI 2.9.0 的书签面板](https://github.com/GTNewHorizons/NotEnoughItems/blob/8e1541eab1935a2a261afcabaeb822089a94d86b/src/main/java/codechicken/nei/BookmarkPanel.java) 为参照。书签仍使用本插件的 BepInEx 配置；不直接导入 Minecraft 的物品 ID 或 NBT。

## AI 使用与早期版本声明 / AI Use and Early Development Notice

- **AI 使用：** 本模组在开发过程中使用了 AI 工具辅助。AI 辅助不改变第三方代码、素材或依赖的许可与署名要求，详见[第三方鸣谢与许可说明](<docs/THIRD_PARTY_NOTICES.md>)。
- **早期版本：** 本模组仍处于早期开发阶段，可能存在缺陷或兼容性问题，功能和配置格式也可能随版本变化。建议在试用或升级前备份游戏存档和模组配置（含书签）。

- **AI use:** AI tools have been used to assist development of this mod. AI assistance does not change the license or attribution requirements for third-party code, assets, or dependencies. See the [third-party notices](<docs/THIRD_PARTY_NOTICES.md>) for details.
- **Early development:** This mod is still in early development. Bugs and compatibility issues may occur, and features or configuration formats may change between versions. Back up your game saves and mod configuration (including bookmarks) before trying or updating the mod.

## 使用

| 操作 | 默认交互 |
|---|---|
| 打开或恢复 NEI | 打开背包或终端后按 `O` 切换。即使侧栏已隐藏，也可在游戏原生「设置 → 游戏 → NEI · 显示侧栏」恢复。 |
| 查配方 / 用途 | 悬停后 `R` / `U`，或点击 NEI 格子的左键 / 右键。用途含直接原料、性质、工具、机器；悬停有内容物的液体容器时也查其液体用途。终端聚合格未公开悬停接口，请从 NEI 目录查询 |
| 浏览查询结果 | 点击顶部图标页签可直接选择制作方式；顶部左右箭头在页签有多页时显示，整页浏览而不改变当前方式。方式标题行的箭头切换到相邻方式，页码行的箭头只翻当前方式的配方。在配方区或页码行滚动也能翻页；超长配方在配方区内滚动，仍可到页码行翻页。左边显示所选方式的机器（若有）；原料至少显示 3×3。爱心设为样板，`+` 在可用时打开原生制作配方，不消耗材料。返回箭头、`Backspace` 或右键方式标题可返回；编辑文本时退格仍删除字符。无配方时显示物品与收藏按钮。 |
| 搜索物品 | 在右侧历史记录下方搜索，或按 `Ctrl+F` 聚焦；在背包和终端中按 `Tab` 均不会聚焦 NEI 搜索框；右键清空。搜索只过滤目录列表；拾取、丢弃、刷取或收藏都不会改变目录格子的顺序。支持普通文字（简体中文界面下可用拼音或首字母）、`@来源` `$标签` `#描述` `&ID`、`-排除`、`"短语"`，以及 `\|` 分隔的备选词。`@来源` 忽略空格和大小写，例如 `@CasualtiesUnknown` 或 `@casualtiesunknown` 可查原版物品；带空格的模组名也可写成 `@"Forge Guild"`。拼音搜索通过可选的 Just-Unknown-Characters 1.0.1 提供；未安装时普通文字与中文搜索照常可用。 |
| 查看或复制 ID | NEI 扳手 →「物品」→「是否显示物品 ID」可开关 ID，默认开启；模组名仍会显示。不按 `Shift` 时悬浮框只作预览，不接管鼠标。悬停物品后按住任一侧 `Shift`，移到 ID 或模组名一行左键点击即可复制。 |
| 选择数量 | 搜索框右侧输入 `1–64` 或用 `+`／`−` 调整。已保存的书签数量不受影响。 |
| 书签 | `A` 或中键添加／移除；`Ctrl+A` 按目录底栏选择的数量收藏；配方页 `Shift+A` 收藏当前配方，`Ctrl+Shift+A` 带数量收藏。左下书本或 `B` 切换书签栏。 |
| 创建、调整书签组 | 在左侧空白边栏拖出一段书签，或把书签拖到另一项上建组。括号左拖／左 `Alt` 拖向外扩组，右拖／右 `Alt` 拖向内缩组。`Shift` 拖物品、原料或括号可分别调整条目、配方内原料或整组的顺序；拖到页尾空白可移到末尾，拖到下方翻页箭头可移入相邻命名页。`Alt+左键` 或 `Ctrl+右键` 括号解组；`Alt+左键` 点组内条目只移出该条，两者均不删除书签。`Shift+A` 删除整组。 |
| 视图与数量 | 左键括号切换队列／密集产物网格；`Ctrl+左键` 括号切换单行摘要。链内对产物 `Shift+Alt+左键` 可折叠该配方独占的上游（未分组产物仍可用 `Alt+左键`），共用的中间步骤仍保留。`Ctrl+滚轮` 调所指条目的数量／配方批数；对普通组按比例调整，对合成链只调目标批数。再按住 `Alt` 则按目录底栏选定的件数调整。 |
| 合成链 | 右键括号启用该组的链（灰括号变绿）。上方页码控制**未分组**书签；`Ctrl+右键` 页码统一开关各组，按住 `Ctrl` 悬停页码查看各组汇总。悬停绿色括号看净产物、所需原料、工具、余料及估计缺料；按住 `Shift` 固定提示并显示逐步配方，移动鼠标可连续查看不同物品的详情，不会固定第二层悬浮框。物品格仍可点击正反查，左键点可替代原料格查看完整候选；长清单可滚动。在书签栏的可替代原料格上 `Shift+滚轮` 轮换具体材料和自动选择。 |
| 配方样板与订单 | 点击配方旁的爱心，为悬停预览中的产物指定首选配方；在爱心上滚动可切换副产物。无配方物品可用爱心设为首选原料。爱心不添加普通书签。对爱心按 `Shift+A` 会把最终配方和已标星的上游配方放进一个新组；`Shift+T` 建组并看依赖，目录物品上 `Shift+F` 可按首选配方下单。开启链后对括号或上方页码按 `T` 查看该组依赖和材料。背包中有可满足该原料格的样板首选材料时优先用它，否则按可用的合格材料配平；书签上手动指定的材料仍优先。 |
| 复制、分享与分页 | 点击下方页名创建或切换命名页；`Ctrl+C` 对条目、括号或页码分别复制书签、组或整页，`Ctrl+V` 粘贴到当前页。`Ctrl+L` 分享所指物品或组，`Ctrl+Shift+L` 可连配方一起分享。分享使用可粘贴的文本而非 Minecraft 聊天链接；粘贴组不会与已有组混合。 |
| 查看性质与工具 | 可替代的材料在配方格内自动轮播；普通配方书签也会轮播未锁定的流体候选，并同步更新 mL 用量。悬停流体性质原料格按 `R`，可查看所有含该性质的物品，不限于当前配方可用的候选；在具体物品格按 `R` 仍查它的配方。悬停原料或工具格可看符合要求的候选；按住 `Shift` 固定提示，候选太多时可滚轮翻页。合成树的物品行或图标也可悬停查看详情，按住 `Ctrl` 展开更多物品信息。流体用量显示在格子左下角，以 mL 为基准；放不下时用 `k`、`m`、`g`、`t` 等十进制后缀缩写（`1k` = `1000mL`）。 |
| 制作 | 在 NEI 配方书签按 `Shift+S`，在原生制作菜单中选中配方，不消耗材料。在原生制作菜单内，把鼠标移到该书签或绿色组括号，`Shift+C` 制作目标，`Ctrl+Shift+C` 补齐到目标数量。合成链只展开所需前置，计入可达地面原料。支持单机和具备制作回执接口的 Together 联机（已核对 5.5.4 接口），仍遵守游戏的材料、耐久和解锁约束。 |
| 刷取缺料 | 在 NEI 扳手 →「调试」开启「允许刷取合成链缺失原料」（默认关闭），再切到作弊模式。将鼠标移到已启用的绿色合成链括号按 `Ctrl+Shift+M`，或打开依赖树的材料账单点「刷取缺失原料」。按随身和附近库存重新计算缺口，不执行制作。单机按液体缺口 mL 向上取整为满临时瓶，约 5 分钟游戏时间后消失；瓶和固体共用 64 件上限。尚未选定的原料、刷取后无法满足的要求，以及联机的液体缺口会拒绝整批。 |
| 作弊模式 | `Ctrl+左键` 点扳手在「配方→作弊」之间切换。单机须允许 `AllowLocalCheats`；Together 联机按服务器权限判断。作弊模式中，目录、历史栏和普通书签的左键刷取右栏选定的数量（1–64），右键刷取 1 个。单机刷取液体时，每份都是一瓶装满所选液体的原版临时瓶，约 5 分钟游戏时间后消失。`Shift` 保留配方与用途查询，拖动书签或点击配方书签不会刷取。 |
| 拖动液体容器 | 临时隐藏书签栏和书签按钮；松手或取消时按原来的书签开关状态恢复。 |

Together 联机自动制作走普通制作流程，不需要开启作弊。NEI 逐批提交，收到房主／服务器确认后才继续；缺料、拒绝、断线或超时会停止后续批次。超时或关闭面板不撤销已发送的一批，请先检查背包再重试。Together 自行选择已同步的材料，不使用 NEI 的首选材料排序；有手动锁定材料的订单须先解除锁定或改为手动制作。自动取料仍只支持单机。

Together 联机**刷取物品**时，房主须启用服务器作弊；客户端同时需要服务器允许客户端作弊并授予管理员命令权限。发放目标是自己。NEI 只提交 Together 已注册的 `give` 命令，不在客户端生成物品；最终是否执行以服务器或 Together 控制台为准。多人发放限 Together `give` 支持的游戏资源物品，不支持将液体装入新瓶发放。旧版或无法确认权限的联机后端保持禁用。

## 替换界面资源

本项目采用 [NeoForge 资源位置](https://docs.neoforged.net/docs/1.21.10/misc/resourcelocation/)和 [客户端贴图](https://docs.neoforged.net/docs/1.21.10/resources/client/textures/)的命名与目录约定，不依赖 Java、Gradle 或 NeoForge 运行时。[界面贴图](<assets/notenoughitems/textures/gui>)按用途分为 `sprites/`（图集）、`panels/`（面板及部件）和 `controls/`（不随构建分发的参考图）。[图集配置](<assets/notenoughitems/atlases/nei_gui.json>)收录 `sprites/` 和 `panels/`，Unity 仍按原像素坐标读取切片。动态物品图标仍由游戏或内容 Mod 提供。

- [interface_states.png](<assets/notenoughitems/textures/gui/sprites/interface_states.png>) 是 256×256 图集。返回使用原有双色精灵 `(0,80,16,16)`，返回、翻页、书签、扳手等图标按钮的图案和通用底板同步响应悬停，按下时保持悬停外观。禁用样式和点击功能不变。加号、减号由程序绘制，不读取图集帧；按钮底板仍可随材质包替换。
- 制作方式页签使用 [interface_states.png](<assets/notenoughitems/textures/gui/sprites/interface_states.png>) 中的选中 `(160,112,48,47)` 和未选中 `(208,112,48,47)` 背景；底部高光与配方面板顶边对齐。顶部翻页按钮仍贴住当前页最外侧的页签，即使最后一页未满也不留空隙。制作方式名称和配方页码继续使用 `#A5A6AC` 平面背景，方式图标无法绘制时仍显示简称。
- NEI 设置开关与刷取模式单选框共用 [开关及单选框图集](<assets/notenoughitems/textures/gui/sprites/toggle_and_radio.png>)。[按钮底板](<assets/notenoughitems/textures/gui/sprites/button_backgrounds.png>)、[搜索框与悬浮提示框](<assets/notenoughitems/textures/gui/sprites/search_and_tooltip.png>)、[语言下拉栏](<assets/notenoughitems/textures/gui/sprites/language_dropdown.png>)使用各自的图集。
- 配方查看器、选项及合成树仍共用 [配方面板背景](<assets/notenoughitems/textures/gui/panels/recipe_background.png>)，以 184×174 源区域及 9 px 九宫格边框绘制。侧栏不再随包提供全透明背景文件；未提供覆盖时保持透明。[来源及署名](<docs/modernity-ui/SOURCES.md>)、[上游许可](<docs/modernity-ui/LICENSE.txt>) 仍随本地包保留。

换肤时在 DLL 旁创建 `resourcepacks/active/`，按 `assets/notenoughitems/textures/` 下相同的目录结构放置 PNG，并保留尺寸和帧坐标。读取顺序为启用的材质包（先新路径，再尝试旧路径）、DLL 旁的贴图、DLL 内嵌贴图；如果状态图集的返回帧是空白或占位网格，只对返回图标使用内嵌贴图，其他帧照常读取材质包。侧栏可以继续通过 `gui/backgrounds/default.png`、`catalog.png`、`bookmarks.png` 或旧 `Textures` 背景路径覆盖。

## 鸣谢与许可

**代码：** 本项目自有代码（包括插件源码、测试代码及构建和安装脚本）采用与 [GTNewHorizons NotEnoughItems 的 GTNH 修改部分](https://github.com/GTNewHorizons/NotEnoughItems/blob/6b7e465306810ad11d2413214077237798be13a7/README.md#L85-L93)相同的 **LGPL-3.0-or-later**（GNU LGPL 第 3 版或任何后续版本）。见 [LGPL 第 3 版原文](<LICENSE>)和它所引用的 [GNU GPL 第 3 版原文](<COPYING>)。第三方代码及依赖保留各自的许可与署名。

**资源：** 上述代码许可不适用于贴图，包括内嵌于 DLL 的贴图。第三方素材继续适用其原有许可：

- **Modernity-GTNH-UI** 项目及其贡献者：按上游许可证全文记录为 **CC BY-NC-SA 4.0 International**；上游 README 徽章指向 3.0 的差异仍待确认。见[来源与署名](<docs/modernity-ui/SOURCES.md>)及[许可证全文](<docs/modernity-ui/LICENSE.txt>)。
- **Applied Energistics 2** 项目及其贡献者：其 GUI 贴图许可为 **CC BY-NC-SA 3.0 Unported**。见[来源、署名及修改说明](<docs/ae2/SOURCES.md>)和[许可证全文](<docs/ae2/LICENSE.txt>)。

第三方署名、上游许可证和素材来源统一存放在 [docs/](<docs>)，构建和安装后也随插件放在 DLL 旁的 `docs/`。代码采用 LGPL 不代表含贴图的整包可以商业使用或直接公开分发。混合图集、两张来源待确认的控件图，以及第三方代码与依赖的署名和发布义务，见[第三方鸣谢与许可说明](<docs/THIRD_PARTY_NOTICES.md>)。

## 机器扩展接口

在引用本插件的 BepInEx Mod 中设置 `[BepInDependency("casualtiesunknown.notenoughitems")]`，然后在 Unity 主线程登记。可以只登记展示数据，**不需要现场存在机器或安装机器插件**；外部 Mod 仍负责机器逻辑。原版及 CUCoreLib 已注入游戏的制作配方自动读取；原版加热器烹饪（带 `meat` 标签的物品 → 牛排）单列为机器类别，不要求当前地图上有加热器。

```csharp
using NotEnoughItems;

const string owner = "example.press";
RecipeApi.RegisterCategory(new RecipeCategory("example:press", "Press",
    machineId: "example:press_device", source: owner));
RecipeApi.RegisterRecipe(new NotEnoughItems.Recipe("example:press_plate", "example:press",
    new[] { new RecipeIngredient("example:blank", amount: 2) },
    new[] { new RecipeOutput("example:plate") },
    machineId: "example:press_device", name: "Press a plate", source: owner));
```

`ReplaceSource` 与 `ClearSource` 分别替换／清除同一来源的类别和配方。请为类别、配方及虚拟物品使用自己的前缀，避免与游戏或其他模组冲突。完整类型见 [Catalog.cs](<src/NotEnoughItems/Api/Catalog.cs>)。查询可从 `Catalog.Shared` 获取按产物、用途及分类的索引。

## 界面与悬浮框层级演进

- **1.2.9 及后续：** NEI IMGUI 使用深度 `100`（原为 `-100`），避免抢在游戏原生 UGUI 与终端提示之前绘制；原版 `GlobalDark.tooltipRect` 使用可恢复的 VisualLease 提升，不使用 RenderTexture 或捕获相机。
- **1.2.8：** 悬浮框对齐 Unknown Inventory Terminal 的方案，采用可恢复 `VisualLease` 仅提升 `GlobalDark.tooltipRect` 子树而非整张 Canvas；不创建捕获相机或切换全屏 Overlay 模式，从根源杜绝黑屏问题。
- **1.2.7：** 彻底移除不安全的 RenderTexture 捕获机制，画面合成完全交由游戏原生 Canvas 处理，NEI 仅管理排序租约。
- **1.2.6：** 物品提示完整保留游戏原版位置、文本与富文本排版，由原版 Canvas 合成，避免被 NEI 面板或图集覆盖。
- **1.2.4：** 作弊模式下书签栏实物条目遵循右栏刷取规则，Shift 查询、拖动与配方书签被明确排除；物品提示直接调用游戏原版 `PlayerCamera.ItemHoverDescription` / `GlobalDark`，仅在必要时避让侧栏。

## 游戏内设置

- 原生游戏设置的「游戏」分类只留 `NEI · 显示侧栏`。按 `O` 隐藏后，仍能在此处重新开启。
- 在背包或终端中点击 NEI 左下角的扳手，打开垂直居中的设置页，可设置两个目录的开关、浏览记录、缩放、语言、单机作弊和快捷键。右侧目录底栏的数量同时用于刷取、带数量收藏和调整步进，不会改写已保存的书签。
- 「物品 → 刷取模式」并排显示「NEI（默认）」（尝试放入容器和空槽，不替换装备）和「原版刷给玩家」（在玩家脚下生成，按原版规则自动拾取或穿戴）。蓝框标出当前模式，切换立即保存。此选项只影响单机作弊下的实物；液体仍刷取满的临时瓶，Together 仍由服务器发放。
- NEI 扳手 →「调试」→「预览制作方式页签」默认关闭，仅在本次游戏会话有效。打开配方或用途查询后，顶部会显示编号测试页签。顶部箭头在两页编号页签间切换，方式标题行的箭头逐个切换方式；选中项超出当前页时，页签栏跟随选中项。预览不含真实配方，不写入目录或书签；关闭开关即可恢复正常查询。
- 物品子集已移除，搜索只处理物品文本、来源、标签和 ID。背包和终端的侧栏锚点不随鼠标移动，可在布局页调整宽度和位置。

## 修改界面语言

NEI 自己的界面文字存放在 [Lang/en_us.json](<Lang/en_us.json>)（英文）和 [Lang/zh_cn.json](<Lang/zh_cn.json>)（简体中文）。游戏原有的物品名、说明以及其他模组提供的内容继续使用各自的数据。

- 安装后可直接修改 `BepInEx/plugins/NotEnoughItems/Lang/` 内的文件，不用重新编译。用 UTF-8 保存，重启游戏后生效。界面语言仍可在 NEI 扳手 →「显示」中切换。
- 只修改 JSON 冒号右侧的文字，保留左侧键名和 `{0}`、`{1}`、`{0:P0}` 等占位符。中文缺少某项或文件无法读取时，使用英文；英文也没有可用译文时，使用内置英文默认值。
- 安装脚本不会覆盖已有语言文件。升级后想采用新版翻译，可先备份，再对照新版语言文件合并；若没有自定义内容，也可删除旧语言文件后重新安装。

## 源码结构

- [Api](<src/NotEnoughItems/Api>)：公开的物品目录与配方扩展接口；[Catalog](<src/NotEnoughItems/Catalog>)：目录排序、搜索和制作方式。
- [Bookmarks](<src/NotEnoughItems/Bookmarks>)：书签、分组和合成链；[Integrations](<src/NotEnoughItems/Integrations>)：插件入口、原版游戏与 Together 对接。
- [Input](<src/NotEnoughItems/Input>)：输入策略及原版悬浮提示；[UI](<src/NotEnoughItems/UI>)：窗口和屏幕，布局与贴图样式分别在 [Layout](<src/NotEnoughItems/UI/Layout>)、[Styling](<src/NotEnoughItems/UI/Styling>)。

目录调整不改变 `NotEnoughItems` 命名空间或现有扩展接口。

## 验证与范围

```powershell
# Build and run all regression checks
pwsh -File ./scripts/Build.ps1

# Or build, test, and install in one workflow after closing the game
pwsh -File ./scripts/Install.ps1 -Build
```

实机检查：分别开启背包和终端，确认左书签、右目录、搜索、正反查、外侧点击不改变原版物品；再打开一个随身容器、缩窄窗口，并确认关闭宿主时两边收起。悬停篝火配方中的「易燃」原料，检查候选轮播；按住 `Shift` 移入候选格查配方，松开 `Shift` 后应恢复普通悬浮。用篝火样板打开合成树，悬停物品查看描述，并分别在背包有、无首选材料时查看配平结果。终端默认 `F7`；若本机的 Combined Cheat Mod 同样绑定 `F7`，可重绑其中一个再验收。

联机实机检查：用 Together 5.5.4 在私有房间中保持作弊关闭，分别用房主和普通客户端执行一条需要先制作中间件的书签链，再用 `Ctrl+Shift+C` 补齐；核对双方看到的材料扣除和产物是否一致。另测缺料、关闭制作面板和断线，确认不会开始后续批次；已经提交的那一批可能仍会完成。

Minecraft 专属的 ME 网络、样板自动制作、Forge 配方槽转移、附魔、昼夜天气、刷怪／区块叠层和快捷栏快照未照搬。没有原生制作接口的机器支持**查阅**，不虚构实际制作或取货接口。
