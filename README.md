# Not Enough Items — Casualties: Unknown

NEI 的本地 BepInEx 移植版。背包或 Unknown Inventory Terminal 打开时，左侧显示书签，右侧显示物品目录。侧栏按宿主窗口的实际位置布置；视口太窄时，退回屏幕底部的紧凑面板。界面采用按 [Modernity-GTNH-UI](https://github.com/ModernityGTNH/Modernity-GTNH-UI/tree/29x/assets/nei/textures) 参考独立重绘的银灰／灰紫像素风格，不包含 Minecraft、Modernity 原贴图或字体。

## 安装

- 依赖游戏的 Mono 版、BepInEx 5 和 `net.cucorelib` 1.1+。可选搭配 `com.unknown.inventoryterminal` 0.4.4+。
- 在此目录运行 `dotnet build -c Release`；构建不会安装插件。运行 `dotnet build -c Release -t:Install` 将 DLL 放进本机游戏的 `BepInEx/plugins/NotEnoughItems/`，然后重启游戏。
- 如果源码不在游戏的 `mods/NotEnoughItems`，构建时加 `-p:GameRoot="D:\路径\Casualties Unknown"`；CUCoreLib 不在 `BepInEx/plugins/CUCoreLib.dll` 时另设 `-p:CUCoreLibPath=...`。
- 如果同时装了 **Unknown Enough Items (UEI 1.2.2)**，NEI 默认仅在自己显示时隐藏 UEI 的侧栏，不卸载或修改 UEI。可用 `ReplaceUEIPanel=false` 恢复并排显示。EMI 的原生制作界面模块不受影响。

## 使用

| 操作 | 默认交互 |
|---|---|
| 打开物品索引 | 打开背包或终端；`O` 切换 NEI，`B` 切换书签栏 |
| 查配方 / 用途 | 悬停后 `R` / `U`，或点击 NEI 格子的左键 / 右键。终端真正的单物品格可用悬停键，聚合格未公开悬停接口，请从 NEI 目录查询 |
| 搜索物品 | `Ctrl+F`；普通文字或 `@来源` `$标签` `#描述` `&ID`、`-排除`、`"短语"`，以及 `\|` 分隔的备选词 |
| 书签 | `A` 或中键添加／移除；左栏的 `+` 可建立命名页。配方详情可以单独收藏配方 |
| 查机器配方 | 左下「机器配方」→ 切换分类／配方；搜索支持 `<输入` `>产物` `^工具或机器` `~名称或说明` |
| 规划原料 | 配方中的「配方树」提供一条供参考的依赖路线；「可制作」针对原生游戏制作规则查询。已知的原生配方可以跳转到游戏制作页，不会自动消耗物品 |
| 本地调试工具 | 默认关闭。仅可在确定为单机的运行中使用，并需在 BepInEx 配置打开 `AllowLocalCheats` 后再从 NEI 选项进入本地作弊模式。`Ctrl+左键` 给 1 件、`Ctrl+右键` 给 4 件，加 `Shift` 给 16 件；另有治疗和原生游戏速度工具 |

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

## 验证与范围

```powershell
dotnet build -c Release -warnaserror
dotnet run --project tests/Nei.Tests.csproj -c Release
```

实机检查：分别开启背包和终端，确认左书签、右目录、搜索、正反查、外侧点击不改变原版物品；再打开一个随身容器、缩窄窗口，并确认关闭宿主时两边收起。终端默认 `F7`；若本机的 Combined Cheat Mod 同样绑定 `F7`，可重绑其中一个再验收。

Minecraft 专属的 ME 网络、样板自动制作、Forge 配方槽转移、附魔、昼夜天气、刷怪／区块叠层和快捷栏快照未照搬。没有原生制作接口的机器支持**查阅**，不虚构实际制作、取货或多人作弊能力。
