# Not Enough Items — Casualties: Unknown

NEI 的本地 BepInEx 移植版。背包或 Unknown Inventory Terminal 打开时，左侧显示书签，右侧显示物品目录。原生背包侧栏基于固定屏幕锚点，不随鼠标或镜头漂移；终端侧栏避让终端边界。视口太窄时，退回屏幕底部的紧凑面板。默认界面背景像素为全透明，只绘制按钮、交互高亮、图标和文字。界面采用按 [Modernity-GTNH-UI](https://github.com/ModernityGTNH/Modernity-GTNH-UI/tree/29x/assets/nei/textures) 参考独立重绘的银灰／灰紫像素风格，不包含 Minecraft、Modernity 原贴图或字体。

## 安装

- 依赖游戏的 Mono 版、BepInEx 5 和 `net.cucorelib` 1.1+。可选搭配 `com.unknown.inventoryterminal` 0.4.4+。
- 在此目录运行 `dotnet build -c Release`；构建不会安装插件。运行 `dotnet build -c Release -t:Install` 将 DLL 放进本机游戏的 `BepInEx/plugins/NotEnoughItems/`，然后重启游戏。
- 如果源码不在游戏的 `mods/NotEnoughItems`，构建时加 `-p:GameRoot="D:\路径\Casualties Unknown"`；CUCoreLib 不在 `BepInEx/plugins/CUCoreLib.dll` 时另设 `-p:CUCoreLibPath=...`。
- 如果同时装了 **Unknown Enough Items (UEI 1.2.2)**，NEI 默认仅在自己显示时隐藏 UEI 的侧栏，不卸载或修改 UEI。可用 `ReplaceUEIPanel=false` 恢复并排显示。EMI 的原生制作界面模块不受影响。

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

布局与交互分在 [Screens](<Screens>)：物品目录、书签、配方、设置和物品格分别维护。默认 PNG 与图集坐标在 [Textures](<Textures>)。发布的 DLL 自带默认资源；可在 DLL 同目录放置 `Textures` 目录中的同名 PNG 来覆盖默认外观，保留图集尺寸、坐标和 Alpha 通道。物品图标仍由游戏或内容 Mod 提供。搜索不再使用物品子集。

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

## 1.1 资源与设置

- 资源覆盖目录是插件旁置的 `BepInEx/plugins/NotEnoughItems/Textures/`；只替换已有 PNG/JSON，不需要重新编译，也不会被后续 `Install` 覆盖。
- 在原生游戏设置的「游戏」分类中，`NEI · 显示侧栏` 是持久开关。按 `O` 或设置页隐藏后，仍能在此行重新开启；设置文件是 `BepInEx/config/casualtiesunknown.notenoughitems.cfg`。
- 物品子集已移除，搜索只处理物品文本、来源、标签和 ID。背包的侧栏锚点不会读取鼠标位置；终端侧栏只根据 `TerminalApi.TryGetBounds` 避让终端。

## 验证与范围

```powershell
dotnet build -c Release -warnaserror
dotnet run --project tests/Nei.Tests.csproj -c Release
```

实机检查：分别开启背包和终端，确认左书签、右目录、搜索、正反查、外侧点击不改变原版物品；再打开一个随身容器、缩窄窗口，并确认关闭宿主时两边收起。终端默认 `F7`；若本机的 Combined Cheat Mod 同样绑定 `F7`，可重绑其中一个再验收。

Minecraft 专属的 ME 网络、样板自动制作、Forge 配方槽转移、附魔、昼夜天气、刷怪／区块叠层和快捷栏快照未照搬。没有原生制作接口的机器支持**查阅**，不虚构实际制作、取货或多人作弊能力。
