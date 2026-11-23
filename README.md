# Not Enough Items — Casualties: Unknown

NEI 的本地 BepInEx 移植版。书签与物品目录在原版背包和 Unknown Inventory Terminal 中显示；打开原版制作菜单时只保留左侧书签。打开原版容器、交易、物品自定义、液体转移或医疗面板时隐藏。原版背包和终端的右侧物品目录默认都开启，可在 NEI 扳手设置的「显示」页分别关闭；升级时保留已保存的开关。背包和终端共用以 2560×1440 为基准的默认布局：横纵坐标分别随窗口宽高换算；扳手 → 布局可开启「拖动面板调整布局」，拖动各栏标题移动整栏，拖动边缘把手调整宽度，完成后点「完成」；宽度、位置、上下偏移和物品图标大小都会保存。背包侧栏默认透明，配方查询使用有底色的独立面板。默认爱心使用 [GTNH NEI 原版图集](<assets/notenoughitems/textures/gui/original/SOURCES.md>)，其他已有按钮、书签和扳手继续使用 [Modernity-GTNH-UI 原贴图](<assets/notenoughitems/textures/gui/modernity/SOURCES.md>)，不再读取自绘控件图集。

## 编译、打包和部署

需要 PowerShell 7、.NET SDK 8 或更高、Mono 版游戏、BepInEx 5 和 CUCoreLib 1.1+。Unknown Inventory Terminal 0.4.4+ 为可选依赖。

```powershell
# 编译并运行回归检查，不写入游戏安装目录
pwsh -File ./scripts/Build.ps1

# 同时构建本地使用 ZIP（包含附许可的 Modernity 原贴图）
pwsh -File ./scripts/Build.ps1 -Package

# 预览部署；不编译、不写文件
pwsh -File ./scripts/Deploy.ps1 -WhatIf

# 退出游戏后，构建、测试并安装
pwsh -File ./scripts/Deploy.ps1
```

[编译脚本](<scripts/Build.ps1>)和[部署脚本](<scripts/Deploy.ps1>)默认使用本项目上两级目录作为游戏根。可用 `-GameRoot 'D:\路径\Casualties Unknown'`、`-CUCoreLibPath 'D:\路径\CUCoreLib.dll'`、`-Configuration Debug` 和 `-SkipTests` 覆盖默认值。部署脚本不启动或关闭游戏；检测到目标游戏正在运行时停止，成功复制后还会比对 DLL 的 SHA-256。部署目录为游戏根下的 `BepInEx/plugins/NotEnoughItems/`。

书签分组的交互以项目随附的 [GTNH 中文维基](https://gtnh.huijiwiki.com/wiki/Not_Enough_Items#分组与合成链) 和 [GTNH NEI 2.9.0 的书签面板](https://github.com/GTNewHorizons/NotEnoughItems/blob/8e1541eab1935a2a261afcabaeb822089a94d86b/src/main/java/codechicken/nei/BookmarkPanel.java) 为参照。书签仍使用本插件的 BepInEx 配置；不直接导入 Minecraft 的物品 ID 或 NBT。

同时装有 **Unknown Enough Items (UEI 1.2.2)** 时，NEI 默认只在自身显示期间隐藏 UEI 侧栏，不卸载或修改 UEI；将 `ReplaceUEIPanel` 设为 `false` 可恢复并排显示。EMI 的原生制作界面不受影响。

## 使用

| 操作 | 默认交互 |
|---|---|
| 打开或恢复 NEI | 打开背包或终端后按 `O` 切换。即使侧栏已隐藏，也可在游戏原生「设置 → 游戏 → NEI · 显示侧栏」恢复。 |
| 查配方 / 用途 | 悬停后 `R` / `U`，或点击 NEI 格子的左键 / 右键。用途含直接原料、性质、工具、机器；悬停有内容物的液体容器时也查其液体用途。终端聚合格未公开悬停接口，请从 NEI 目录查询 |
| 浏览查询结果 | 配方背景内上方的方式名与左右箭头切换合成方式，下方页码与左右箭头翻页；两排按钮左右对齐，方式名居中显示。在配方区或页码栏向上／下滚动可翻到上一／下一页；超长配方或展开的长候选列表在配方区内滚动查看，仍可到页码栏翻页。左边显示所选方式的机器（若有）；原料至少显示 3×3，超过九格时加行。爱心设为样板，`+` 在可用时打开并选中原生制作配方，不消耗材料。返回箭头、`Backspace` 或右键方式标题可返回；编辑文本时退格仍删除字符。无匹配配方时只显示物品与收藏按钮；配方和用途仍用 `R` / `U` 或物品格左／右键查询。 |
| 搜索物品 | 在右侧历史记录下方搜索，或按 `Ctrl+F` 聚焦；NEI 不用 `Tab` 在面板控件间切换焦点，右键清空。搜索匹配项中，已加书签或收藏的物品排在前面，其次是随身物品和容器内的流体，其余匹配项仍可浏览。支持普通文字（简体中文界面下可用拼音或首字母）、`@来源` `$标签` `#描述` `&ID`、`-排除`、`"短语"`，以及 `\|` 分隔的备选词。`@来源` 忽略空格和大小写，例如 `@CasualtiesUnknown` 或 `@casualtiesunknown` 可查原版物品；带空格的模组名也可写成 `@"Forge Guild"`。拼音搜索通过可选的 Just-Unknown-Characters 1.0.1 提供；未安装时普通文字与中文搜索照常可用。 |
| 查看或复制 ID | NEI 扳手 →「显示」→「悬浮框 ID」可开关 ID，默认开启；模组名仍会显示。悬停物品后按住任一侧 `Shift`，移到悬浮框的 ID 行并左键点击，即可复制原始 ID。 |
| 选择数量 | 搜索框右侧输入 `1–64` 或用 `+`／`−` 调整。已保存的书签数量不受影响。 |
| 书签 | `A` 或中键添加／移除；`Ctrl+A` 按目录底栏选择的数量收藏；配方页 `Shift+A` 收藏当前配方，`Ctrl+Shift+A` 带数量收藏。左下书本或 `B` 切换书签栏。 |
| 创建、调整书签组 | 在左侧空白边栏拖出一段书签，或把书签拖到另一项上建组。括号左拖／左 `Alt` 拖向外扩组，右拖／右 `Alt` 拖向内缩组。`Shift` 拖物品、原料或括号可分别调整条目、配方内原料或整组的顺序；拖到页尾空白可移到末尾，拖到下方翻页箭头可移入相邻命名页。`Alt+左键` 或 `Ctrl+右键` 括号解组；`Alt+左键` 点组内条目只移出该条，两者均不删除书签。`Shift+A` 删除整组。 |
| 视图与数量 | 左键括号切换队列／密集产物网格；`Ctrl+左键` 括号切换单行摘要。链内对产物 `Shift+Alt+左键` 可折叠该配方独占的上游（未分组产物仍可用 `Alt+左键`），共用的中间步骤仍保留。`Ctrl+滚轮` 调所指条目的数量／配方批数；对普通组按比例调整，对合成链只调目标批数。再按住 `Alt` 则按目录底栏选定的件数调整。 |
| 合成链 | 右键括号启用该组的链（灰括号变绿）。上方页码控制**未分组**书签；`Ctrl+右键` 页码统一开关各组，按住 `Ctrl` 悬停页码查看各组汇总。悬停绿色括号看净产物、所需原料、工具、余料及估计缺料；按住 `Shift` 固定提示并显示逐步配方，移入物品格可查看详情或点击正反查；长清单可滚动。在可替代原料上 `Shift+滚轮` 轮换具体材料和自动选择。 |
| 配方样板与订单 | 点击配方旁的爱心，为悬停预览中的产物指定首选配方；在爱心上滚动可切换副产物。无配方物品可用爱心设为首选原料。爱心不添加普通书签。对爱心按 `Shift+A` 会把最终配方和已标星的上游配方放进一个新组；`Shift+T` 建组并看依赖，目录物品上 `Shift+F` 可按首选配方下单。开启链后对括号或上方页码按 `T` 查看该组依赖和材料。背包中有可满足该原料格的样板首选材料时优先用它，否则按可用的合格材料配平；书签上手动指定的材料仍优先。 |
| 复制、分享与分页 | 点击下方页名创建或切换命名页；`Ctrl+C` 对条目、括号或页码分别复制书签、组或整页，`Ctrl+V` 粘贴到当前页。`Ctrl+L` 分享所指物品或组，`Ctrl+Shift+L` 可连配方一起分享。分享使用可粘贴的文本而非 Minecraft 聊天链接；粘贴组不会与已有组混合。 |
| 查看性质与工具 | 可替代的材料在配方格内自动轮播；普通配方书签也会轮播未锁定的流体候选，并同步更新 mL 用量。悬停流体性质原料格按 `R`，可查看所有含该性质的物品，不限于当前配方可用的候选；在具体物品格按 `R` 仍查它的配方。悬停原料或工具格可看符合要求的候选；按住 `Shift` 固定提示，候选太多时可滚轮翻页。合成树的物品行或图标也可悬停查看详情，按住 `Ctrl` 展开更多物品信息。流体用量显示在格子左下角，以 mL 为基准；放不下时用 `k`、`m`、`g`、`t` 等十进制后缀缩写（`1k` = `1000mL`）。 |
| 制作 | 在 NEI 配方书签按 `Shift+S`，在原生制作菜单中选中配方，不消耗材料。在原生制作菜单内，把鼠标移到该书签或绿色组括号，`Shift+C` 制作目标，`Ctrl+Shift+C` 补齐到目标数量。合成链只展开所需前置，计入可达地面原料；自动制作仅支持单机且遵守原版材料、耐久和解锁约束，遇到不可执行的批次会停止。 |
| 作弊模式 | `Ctrl+左键` 点扳手在「配方→作弊」之间切换。单机须允许 `AllowLocalCheats`；Together 联机按服务器权限判断。作弊模式中，目录、历史栏和普通书签的左键刷取右栏选定的数量（1–64），右键刷取 1 个。单机刷取液体时，每份都是一瓶装满所选液体的原版临时瓶，约 5 分钟游戏时间后消失。`Shift` 保留配方与用途查询，拖动书签或点击配方书签不会刷取。 |
| 拖动液体容器 | 临时隐藏书签栏和书签按钮；松手或取消时按原来的书签开关状态恢复。 |

Together 联机中，房主须启用服务器作弊；客户端同时需要服务器允许客户端作弊并授予管理员命令权限。发放目标是自己。NEI 只提交 Together 已注册的 `give` 命令，不在客户端生成物品；最终是否执行以服务器或 Together 控制台为准。多人发放限 Together `give` 支持的游戏资源物品，不支持将液体装入新瓶发放。旧版或无法确认权限的联机后端保持禁用。

## 替换界面资源

本项目采用 [NeoForge 资源位置](https://docs.neoforged.net/docs/1.21.10/misc/resourcelocation/)和 [客户端贴图](https://docs.neoforged.net/docs/1.21.10/resources/client/textures/)的命名与目录约定，不依赖 Java、Gradle 或 NeoForge 运行时。布局代码仍在 [Screens](<Screens>)，物品图标仍由游戏或内容 Mod 提供。

- [Modernity 原贴图](<assets/notenoughitems/textures/gui/modernity>)保持上游 PNG 原样，用 [ModernityFrames.cs](<ModernityFrames.cs>) 中的坐标读取子区域。主材质包没有 NEI 贴图，使用它在 README 中链接的官方 UI 包同分支。此素材随本地包保留[来源和署名](<assets/notenoughitems/textures/gui/modernity/SOURCES.md>)、[原始许可](<assets/notenoughitems/textures/gui/modernity/LICENSE.txt>)；请按许可处理，不视为可用于商业分发。
- [搜索框与悬浮提示框贴图](<assets/notenoughitems/textures/gui/controls/text_field.png>)是单独的资源。替换时保留 128×128 画布：搜索框使用从顶部算起的第 13–24 行作为常态，第 1–12 行作为聚焦态；悬浮提示框（包括按钮提示）使用第 1–12 行（`0,0–128,12`）并按边缘九宫切片拉伸。数量框和书签页名不使用这张图。
- [语言下拉栏贴图](<assets/notenoughitems/textures/gui/controls/drop_down_list.png>)使用提供的 256×256 图集。替换时保留画布和原有帧位置。
- [ID 与刷取模式开关贴图](<assets/notenoughitems/textures/gui/controls/checkbox.png>)使用提供的 64×64 图集。四帧均为 22×12：左列为常态，右列（`x=22`）为悬浮态；关在 `y=28`，开在 `y=40`。
- 配方背景内的合成方式标题与切换按钮使用[选中贴图](<assets/notenoughitems/textures/gui/controls/vertical_buttons_bg.png>)（48×46）和[未选中贴图](<assets/notenoughitems/textures/gui/controls/vertical_buttons_bg_candidate.png>)（48×43）的视觉样式，替换时保留原尺寸。
- [屏幕背景贴图](<assets/notenoughitems/textures/gui/backgrounds>)默认全透明，仍可单独覆盖，用于背包、制作栏和终端旁的书签／目录侧栏。[recipebg.png](<assets/notenoughitems/textures/gui/modernity/recipebg.png>)用于配方、选项和合成树独立面板，按 184×174 源区域及 9 px 九宫格边框绘制；不会拉伸外框。[图集来源描述](<assets/notenoughitems/atlases/nei_gui.json>)保留 Minecraft/NeoForge 目录形式；实际绘制由 Unity 完成。

换肤时在 DLL 旁创建 `resourcepacks/active/`，在其中按相同的 `assets/notenoughitems/textures/gui/…` 结构放置 PNG，保留图集尺寸和坐标。重启后先读取启用的材质包；缺失或尺寸不兼容的 NEI 图集回退到嵌入的原版，其余资源保留原有覆盖规则。爱心覆盖路径是 `gui/original/nei_sprites.png`。部署只补缺失资源；旧 `Textures` 背景仍兼容，但旧自绘控制和图标不再读取。悬停 NEI 目录、书签或配方中的物品格会显示其名称、描述及可用的附加信息。鼠标在可见 NEI 面板内时不重复显示原版悬浮框；移到原版物品上仍使用游戏原版提示。

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

`ReplaceSource` 与 `ClearSource` 分别替换／清除同一来源的类别和配方。请为类别、配方及虚拟物品使用自己的前缀，避免与游戏或其他模组冲突。完整类型见 [Catalog.cs](<Catalog.cs>)。查询可从 `Catalog.Shared` 获取按产物、用途及分类的索引。

## 1.2.9 悬浮框层级

- NEI IMGUI 使用深度 `100`，不再以 `-100` 抢在原版 UGUI/终端提示之前绘制。
- 原版 `GlobalDark.tooltipRect` 继续使用可恢复 VisualLease 提升，不使用 RenderTexture 或捕获相机。

## 1.2.8 悬浮框层级

- 物品悬浮框现在采用 Unknown Inventory Terminal 相同的可恢复 `VisualLease`：只提升原版 `GlobalDark.tooltipRect`，不提升整张 Canvas。
- 不创建捕获相机或 RenderTexture，也不修改原版 ScreenSpaceOverlay 模式，避免黑屏和全屏 GUI 叠加。

## 1.2.7 悬浮框层级

- 物品悬浮框不再使用 RenderTexture、捕获相机或 ScreenSpaceOverlay Canvas 模式切换，避免黑屏。
- 提示内容和画面合成完全交回原版 Canvas；NEI 只使用可恢复的排序租约。

## 1.2.6 悬浮框层级

- 物品悬浮框保留游戏原版位置、文本和布局，在 NEI GUI 绘制后再次合成原版 `GlobalDark.tooltipRect`，避免 Modernity 图集或侧栏 IMGUI 覆盖原版提示。
- 这不是自绘提示框，也不复制物品实例；原版 Canvas 的兄弟节点只在捕获期间暂时停用并立即恢复。

## 1.2.4 运行时修复

- 书签栏的实物条目在 Cheat 模式下遵循右栏刷取规则；Shift 查询、拖动和配方书签被明确排除。
- 物品提示只调用游戏原版 `PlayerCamera.ItemHoverDescription` / `GlobalDark`，NEI 只在必要时为原版提示避让侧栏。

## 游戏内设置

- 原生游戏设置的「游戏」分类只留 `NEI · 显示侧栏`。按 `O` 隐藏后，仍能在此处重新开启。
- 在背包或终端中点击 NEI 左下角的扳手，打开垂直居中的设置页，可设置两个目录的开关、浏览记录、缩放、语言、UEI 兼容、单机作弊和快捷键。右侧目录底栏的数量同时用于刷取、带数量收藏和调整步进，不会改写已保存的书签。
- 「物品 → 刷取模式」提供「现有模式」（默认，尝试放入容器和空槽，不替换装备）和「原版刷给玩家」（在玩家脚下生成，按原版规则自动拾取或穿戴，无法拾取则留在地上）。切换立即保存，只影响单机作弊下的实物；液体仍刷取满的临时瓶，Together 仍由服务器发放。
- 物品子集已移除，搜索只处理物品文本、来源、标签和 ID。背包和终端的侧栏锚点不随鼠标移动，可在布局页调整宽度和位置。

## 验证与范围

```powershell
dotnet build -c Release -warnaserror
dotnet run --project tests/Nei.Tests.csproj -c Release
```

实机检查：分别开启背包和终端，确认左书签、右目录、搜索、正反查、外侧点击不改变原版物品；再打开一个随身容器、缩窄窗口，并确认关闭宿主时两边收起。悬停篝火配方中的「易燃」原料，检查候选轮播；按住 `Shift` 移入候选格查配方，松开 `Shift` 后应恢复普通悬浮。用篝火样板打开合成树，悬停物品查看描述，并分别在背包有、无首选材料时查看配平结果。终端默认 `F7`；若本机的 Combined Cheat Mod 同样绑定 `F7`，可重绑其中一个再验收。

Minecraft 专属的 ME 网络、样板自动制作、Forge 配方槽转移、附魔、昼夜天气、刷怪／区块叠层和快捷栏快照未照搬。没有原生制作接口的机器支持**查阅**，不虚构实际制作或取货接口。
