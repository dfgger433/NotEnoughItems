# Not Enough Items — Casualties: Unknown

NEI 的本地 BepInEx 移植版。背包或 Unknown Inventory Terminal 打开时，左侧显示书签，右侧显示物品目录。原生背包侧栏基于固定屏幕锚点，不随鼠标或镜头漂移；终端侧栏避让终端边界。视口太窄时，退回屏幕底部的紧凑面板。默认界面背景像素为全透明，只绘制按钮、交互高亮、图标和文字。按钮、槽位、标签、书签和扳手使用 [Modernity-GTNH-UI 原贴图](<assets/notenoughitems/textures/gui/modernity/SOURCES.md>)，不再读取自绘控件图集。

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

同时装有 **Unknown Enough Items (UEI 1.2.2)** 时，NEI 默认只在自身显示期间隐藏 UEI 侧栏，不卸载或修改 UEI；将 `ReplaceUEIPanel` 设为 `false` 可恢复并排显示。EMI 的原生制作界面不受影响。

## 使用

| 操作 | 默认交互 |
|---|---|
| 打开或恢复 NEI | 打开背包或终端后按 `O` 切换。即使侧栏已隐藏，也可在游戏原生「设置 → 游戏 → NEI · 显示侧栏」恢复。 |
| 查配方 / 用途 | 悬停后 `R` / `U`，或点击 NEI 格子的左键 / 右键。用途含直接原料、性质、工具、机器；悬停有内容物的液体容器时也查其液体用途。终端聚合格未公开悬停接口，请从 NEI 目录查询 |
| 搜索物品 | `Ctrl+F`；普通文字或 `@来源` `$标签` `#描述` `&ID`、`-排除`、`"短语"`，以及 `\|` 分隔的备选词 |
| 书签 | `A` 或中键添加／移除；`Ctrl+A` 按数量框收藏；配方页 `Shift+A` 收藏当前配方，`Ctrl+Shift+A` 带数量收藏。左下书本或 `B` 切换书签栏。 |
| 书签分组 | 左键把一个书签拖到另一个上创建／加入组，`Shift` 拖动调整顺序；组括号左键折叠、`Alt+左键` 解组、`Shift+A` 删除该组。`Ctrl+滚轮` 调数量，加入 `Alt` 按右下数量步进。点击页名可建立命名页、复制或粘贴页。 |
| 合成链 | 右键组括号计算该组，右键页码计算整页；加 `Shift` 直接看生产步骤。配方页「用已收藏配方建组」从已收藏的中间配方建立新组。图标显示目标、净需求、缺料、余料；在性质输入图标上 `Shift+滚轮` 可指定材料。 |
| 查看性质与工具 | 详情中，每种性质或工具先显示一个代表物品，优先使用游戏会选中的实际物品。点带 `+` 的格子展开所有具有该性质的物品或工具；`!` 表示未达到本配方的要求。再点 `−` 或按返回收起 |
| 制作 | 「可制作」使用原生配方规则检查。合成链只计算与查看，不调用会消耗物品的自动制作；点步骤图标可查看对应配方，并从配方页打开原生制作菜单。 |
| 单机调试 | 默认关闭。在 NEI 设置中明确开启 `AllowLocalCheats` 后，`Ctrl+左键` 点扳手按「配方→实用→作弊」循环。实用模式有治疗和原生游戏速度。作弊模式中右栏物品左键按数量框给予，右键给予 1；按住 `Shift` 保留配方与用途查询。 |

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

## 游戏内设置

- 在原生游戏设置的「游戏」分类中，`NEI · 显示侧栏` 是持久开关。按 `O` 或设置页隐藏后，仍能在此行重新开启；设置文件是 `BepInEx/config/casualtiesunknown.notenoughitems.cfg`。
- 物品子集已移除，搜索只处理物品文本、来源、标签和 ID。背包的侧栏锚点不会读取鼠标位置；终端侧栏只根据 `TerminalApi.TryGetBounds` 避让终端。

## 验证与范围

```powershell
dotnet build -c Release -warnaserror
dotnet run --project tests/Nei.Tests.csproj -c Release
```

实机检查：分别开启背包和终端，确认左书签、右目录、搜索、正反查、外侧点击不改变原版物品；再打开一个随身容器、缩窄窗口，并确认关闭宿主时两边收起。终端默认 `F7`；若本机的 Combined Cheat Mod 同样绑定 `F7`，可重绑其中一个再验收。

Minecraft 专属的 ME 网络、样板自动制作、Forge 配方槽转移、附魔、昼夜天气、刷怪／区块叠层和快捷栏快照未照搬。没有原生制作接口的机器支持**查阅**，不虚构实际制作、取货或多人作弊能力。
