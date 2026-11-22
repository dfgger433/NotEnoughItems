# Not Enough Items — Casualties: Unknown

NEI 的本地 BepInEx 移植版。在背包、容器、制作、交易、物品自定义、液体转移和 Unknown Inventory Terminal 提供书签与物品目录。原版背包默认只显示左侧书签，可通过扳手菜单的「背包右侧目录」开启右栏；终端目录独立设置。医疗面板打开时不显示 NEI。已开启的侧栏基于固定屏幕锚点，不随鼠标或镜头漂移，优先使用宿主界面外的空余位置。默认界面背景像素为全透明，只绘制按钮、交互高亮、图标和文字。按钮、槽位、标签、书签和扳手使用 [Modernity-GTNH-UI 原贴图](<assets/notenoughitems/textures/gui/modernity/SOURCES.md>)，不再读取自绘控件图集。

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
| 搜索物品 | `Ctrl+F`；普通文字或 `@来源` `$标签` `#描述` `&ID`、`-排除`、`"短语"`，以及 `\|` 分隔的备选词 |
| 书签 | `A` 或中键添加／移除；`Ctrl+A` 按数量框收藏；配方页 `Shift+A` 收藏当前配方，`Ctrl+Shift+A` 带数量收藏。左下书本或 `B` 切换书签栏。 |
| 创建、调整书签组 | 在左侧空白边栏拖出一段书签，或把书签拖到另一项上建组。括号左拖／左 `Alt` 拖向外扩组，右拖／右 `Alt` 拖向内缩组。`Shift` 拖物品、原料或括号可分别调整条目、配方内原料或整组的顺序；拖到页尾空白可移到末尾，拖到下方翻页箭头可移入相邻命名页。`Ctrl+右键` 括号解组，`Shift+A` 删除组。 |
| 视图与数量 | 左键括号切换队列／密集产物网格；`Alt+左键` 切换单行摘要。链内对产物 `Alt+左键` 可折叠该配方独占的上游，共用的中间步骤仍保留。`Ctrl+滚轮` 调所指条目的数量／配方批数；对普通组按比例调整，对合成链只调目标批数。加入 `Alt` 按数量框步进（支持 1024 等大步长；作弊给予仍限 64）。右栏关闭时可点左下页名找到数量框。 |
| 合成链 | 右键括号启用该组的链（灰括号变绿）。上方页码控制**未分组**书签；`Ctrl+右键` 页码统一开关各组，按住 `Ctrl` 悬停页码查看各组汇总。悬停绿色括号看净产物、所需原料、工具、余料及估计缺料；按 `Shift` 看逐步配方，长清单可滚动。在可替代原料上 `Shift+滚轮` 轮换具体材料和自动选择。 |
| 配方样板与订单 | 点击配方旁的爱心指定首选配方，不会添加普通书签。对爱心按 `Shift+A` 会把最终配方和已标星的上游配方放进一个新组；`Shift+T` 建组并看依赖，目录物品上 `Shift+F` 可按首选配方下单。开启链后对括号或上方页码按 `T` 查看该组依赖和材料。 |
| 复制、分享与分页 | 点击下方页名创建或切换命名页；`Ctrl+C` 对条目、括号或页码分别复制书签、组或整页，`Ctrl+V` 粘贴到当前页。`Ctrl+L` 分享所指物品或组，`Ctrl+Shift+L` 可连配方一起分享。分享使用可粘贴的文本而非 Minecraft 聊天链接；粘贴组不会与已有组混合。 |
| 查看性质与工具 | 详情中，每种性质或工具先显示一个代表物品，优先使用游戏会选中的实际物品。点带 `+` 的格子展开所有具有该性质的物品或工具；`!` 表示未达到本配方的要求。再点 `−` 或按返回收起 |
| 容器备料 | 在书签格或组括号上按 `V`，按目标数取料；`Shift+V` 先扣除身上已有的数量。单机只从当前打开的原生容器取实物，需有空槽且不超重；不会凭空生成物品。液体需手动转移，库存终端的只读接口只给清单。 |
| 制作 | `Shift+S` 在原生制作菜单中选中收藏配方，不消耗材料。**在已经打开的原生制作面板中**，对启用的链括号按 `Shift+C` 追加整组制作，`Ctrl+Shift+C` 仅补齐随身库存的缺口；对产物格操作只制作该目标及其上游，不执行下游步骤。实际执行会消耗游戏物品，受原生选料、解锁、制作成功率和库存限制。机器／仅展示配方不会由 NEI 代为执行；联机不自动制作。 |
| 实用／作弊 | `Ctrl+左键` 点扳手按「配方→实用→作弊」循环。单机须允许 `AllowLocalCheats`；Together 联机按服务器权限判断，不受单机开关限制。作弊模式中目录或实物书签左键按数量框给予，右键给予 1；`Shift` 保留配方与用途查询。拖动书签或点击配方书签不会给予物品。 |
| 拖动液体容器 | 临时隐藏书签栏和书签按钮；松手或取消时按原来的书签开关状态恢复。 |

Together 联机中，房主须启用服务器作弊；客户端同时需要服务器允许客户端作弊并授予管理员命令权限。发放与治疗的目标是自己，游戏速度另受服务器时间规则限制。NEI 只提交 Together 已注册的命令，不在客户端生成物品；最终是否执行以服务器或 Together 控制台为准。多人发放限 Together `give` 支持的游戏资源物品。旧版或无法确认权限的联机后端保持禁用。

## 替换界面资源

本项目采用 [NeoForge 资源位置](https://docs.neoforged.net/docs/1.21.10/misc/resourcelocation/)和 [客户端贴图](https://docs.neoforged.net/docs/1.21.10/resources/client/textures/)的命名与目录约定，不依赖 Java、Gradle 或 NeoForge 运行时。布局代码仍在 [Screens](<Screens>)，物品图标仍由游戏或内容 Mod 提供。

- [Modernity 原贴图](<assets/notenoughitems/textures/gui/modernity>)保持上游 PNG 原样，用 [ModernityFrames.cs](<ModernityFrames.cs>) 中的坐标读取子区域。主材质包没有 NEI 贴图，使用它在 README 中链接的官方 UI 包同分支。此素材随本地包保留[来源和署名](<assets/notenoughitems/textures/gui/modernity/SOURCES.md>)、[原始许可](<assets/notenoughitems/textures/gui/modernity/LICENSE.txt>)；请按许可处理，不视为可用于商业分发。
- [屏幕背景](<assets/notenoughitems/textures/gui/backgrounds>)默认全透明，仍可单独覆盖。[图集来源描述](<assets/notenoughitems/atlases/nei_gui.json>)保留 Minecraft/NeoForge 目录形式；实际绘制由 Unity 完成。

换肤时在 DLL 旁放置同路径的 PNG，保留图集尺寸和坐标，重启后生效。部署只补缺失资源；旧 `Textures` 背景仍兼容，但旧自绘控制和图标不再读取。悬浮物品信息由游戏原版绘制；存在真实物品实例时直接读取它的耐久、液体等信息，只有目录类型时显示类型描述，不生成临时物品。

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

- 在原生游戏设置的「游戏」分类中，`NEI · 显示侧栏` 是持久开关。按 `O` 或设置页隐藏后，仍能在此行重新开启；设置文件是 `BepInEx/config/casualtiesunknown.notenoughitems.cfg`。
- 物品子集已移除，搜索只处理物品文本、来源、标签和 ID。背包的侧栏锚点不会读取鼠标位置；终端侧栏只根据 `TerminalApi.TryGetBounds` 避让终端。

## 验证与范围

```powershell
dotnet build -c Release -warnaserror
dotnet run --project tests/Nei.Tests.csproj -c Release
```

实机检查：分别开启背包和终端，确认左书签、右目录、搜索、正反查、外侧点击不改变原版物品；再打开一个随身容器、缩窄窗口，并确认关闭宿主时两边收起。终端默认 `F7`；若本机的 Combined Cheat Mod 同样绑定 `F7`，可重绑其中一个再验收。

Minecraft 专属的 ME 网络、样板自动制作、Forge 配方槽转移、附魔、昼夜天气、刷怪／区块叠层和快捷栏快照未照搬。没有原生制作接口的机器支持**查阅**，不虚构实际制作或取货接口。
