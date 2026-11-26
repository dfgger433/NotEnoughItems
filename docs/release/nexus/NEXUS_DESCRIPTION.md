# Not Enough Items (NEI) — Casualties: Unknown

> **Comprehensive In-Game Item Catalog, Recipe/Usage Viewer, Crafting Chain Planner & Inventory Utility for Casualties: Unknown.**  
> 面向《Casualties: Unknown》的完整物品目录、配方/用途正反查、书签合成链规划与背包辅助模组。

---

## 目录 / Table of Contents
1. [模组简介 / Overview](#模组简介--overview)
2. [核心功能 / Features](#核心功能--features)
3. [安装要求与步骤 / Installation & Requirements](#安装要求与步骤--installation--requirements)
4. [快捷键与操作速查 / Controls & Keybindings](#快捷键与操作速查--controls--keybindings)
5. [兼容性与联机说明 / Compatibility & Multiplayer](#兼容性与联机说明--compatibility--multiplayer)
6. [界面自定义与材质包 / Themes & Customization](#界面自定义与材质包--themes--customization)
7. [开发者与机器配方 API / Developer API](#开发者与机器配方-api--developer-api)
8. [许可与第三方鸣谢 / Licenses & Acknowledgments](#许可与第三方鸣谢--licenses--acknowledgments)
9. [AI 使用与早期版本声明 / Notice](#ai-使用与早期版本声明--notice)

---

## 模组简介 / Overview

### 中文
**Not Enough Items (NEI)** 是将经典物品与配方查询体验无缝移植至《Casualties: Unknown》的本地 BepInEx 插件。本模组不仅完全适配原版背包与主流扩展终端（如 Unknown Inventory Terminal），更针对游戏的原生机制进行了深度优化：
- **直观无感**：与原版容器、制作、交易、液体转移和医疗面板完美协同，按需智能隐藏与恢复；
- **全方位查询**：支持物品正查配方（Recipe）、反查用途（Usage）、机器与性质查询、流体含量展示及自动材料轮播；
- **工业级规划**：拥有 GTNH 级别的书签分组、配方样板收藏与自动配平合成链（Crafting Chains）；
- **多人联机支持**：在单机作弊之余，深度集成 Together 联机协议，支持联机环境下的自动排队批量制作与受控物品分发。

### English
**Not Enough Items (NEI)** is a dedicated BepInEx port of the quintessential inventory catalog and recipe viewer for *Casualties: Unknown*. Built with deep game engine integration, NEI brings order to complex crafting trees and sprawling item collections:
- **Seamless Integration:** Elegantly embeds into the vanilla player inventory and community terminals (Unknown Inventory Terminal), automatically tucking away when native trade, medical, or fluid transfer menus open;
- **Bi-Directional Recipe Discovery:** Inspect direct recipes (`R`), reverse lookup usages (`U`), explore machine processes, verify fluid capacities in mL, and browse property-driven alternatives;
- **Industrial Crafting Chains:** Organize goals with drag-and-drop bookmark groups, bookmark preferred recipes with hearts, and let NEI balance dependencies, required materials, tools, and residual outputs;
- **Multiplayer & Cheat Integration:** Enjoy single-player spawning and cheat tools, plus native Together multiplayer cooperative crafting with server-side receipt confirmation.

---

## 核心功能 / Features

### 1. 物品目录与高级搜索 (Catalog & Advanced Search)
- **固定有序索引**：右侧物品目录按照稳定逻辑排列，拾取、丢弃或刷取均不会打乱目录格子布局。
- **高级过滤语法**：支持普通文本快速模糊匹配，同时支持专业过滤前缀：
  - `@来源` 或 `@"Mod Name"`：按模组名或原版筛选（忽略大小写与空格，例如 `@CasualtiesUnknown`）；
  - `$标签`：按物品标签（Tags）筛选；
  - `#描述`：在物品说明与详情文本中搜索；
  - `&ID`：精确按物品内部 ID 查找；
  - `-排除`、`"短语"`、`|`：支持逻辑或与否定排除语法。
- **拼音与首字母搜索**：支持安装可选的 *Just-Unknown-Characters* 插件，在简体中文界面下直接键入拼音或首字母拼音过滤（如 `st` 匹配 `石头`）。
- **ID 快捷复制**：按住任一侧 `Shift` 悬停物品，点击 ID 或模组名即可直接复制到剪贴板。

### 2. 配方与用途查看器 (Recipe & Usage Viewer)
- **快捷正反查**：对任意物品悬停按 `R` 查看制作配方，按 `U` 查看所有可用用途（含直接原料、机器加工、性质原料与工具）。
- **分页与制作方式导航**：顶部页签直接切换工作台、熔炉、加热器、各模组自定义机器等方式；箭头支持平滑翻页与逐个切换。
- **动态替代材料与流体**：可替代的材料（如矿物字典/性质需求）在格内自动轮播；流体在左下角精确标注 mL 容量（支持 `1k` = `1000mL` 等清晰进位），按 `R` 可查询全游戏所有满足该性质的容器或材料。
- **爱心样板 (Preferred Recipe)**：点击配方旁的爱心可将该配方标为首选产出方式；即使无配方物品也可标为首选原料。

### 3. 书签管理与合成链规划 (Bookmarks & Crafting Chains)
- **书签收藏**：对物品按 `A` 或鼠标中键快速加入左侧书签栏，`Shift+A` 收藏当前配方，`Ctrl+Shift+A` 携带目标批数收藏。
- **直观分组与缩放**：在书签栏左侧空白处拖动即可划选建组；通过拖动外扩/内缩括号调整分组范围；`Alt+左键` 移出或解组。
- **自动化合成链（灰括号变绿）**：右键点击分组括号开启合成链计算：
  - 悬停查看净产物、原料清单、必要工具、副产物及当前库存估算缺料；
  - 按住 `Shift` 展开逐步制作配方和依赖树；按住 `Ctrl` 展开原版物品原生属性预览；
  - 自动展开多级依赖中间件，并优先按背包实际库存配平原料。
- **一键制作与补齐**：对书签按 `Shift+S` 即可在游戏制作面板中调出该配方；在制作菜单内对绿色合成链按 `Shift+C` 自动制作目标，`Ctrl+Shift+C` 自动补齐所缺批次。

### 4. 自由布局与可调界面 (Customizable Layout & Themes)
- **自由拖拽排版**：点击左下角扳手图标进入设置，在「布局」页开启「拖动面板调整布局」，可任意拖动左右栏标题位置，拖拽边缘把手调整宽度，支持缩放比例调节，配置自动持久化保存。
- **原版悬浮提示**：NEI 不使用捕获相机，并在与原版背包槽重叠时保留原版物品悬浮提示。
- **独立外置本地化**：所有界面文字采用外置 `Lang/zh_cn.json` 与 `Lang/en_us.json`，无需重新编译即可自行修改汉化或润色。

---

## 安装要求与步骤 / Installation & Requirements

### 运行环境要求 (Prerequisites)
1. **Casualties: Unknown**（游戏需为 Mono 运行库版本，非 IL2CPP）；
2. **BepInEx 5.4.x (Mono)** 模组框架；
3. **CUCoreLib 1.1 或更高版本**（核心基础库，游戏模组必备）。

### 可选推荐依赖 (Optional Dependencies)
- **Unknown Inventory Terminal (0.4.4+)**：扩展物品终端模组；NEI 会自动适配终端侧栏与分类。
- **Just Unknown Characters (1.0.1)**：为简体中文目录提供拼音和拼音首字母搜索。
- **Together（已核对 5.5.4 的制作回执接口）**：支持联机批量制作；含手动锁定材料的订单不支持联机自动制作。

### 安装步骤 (Installation Guide)
1. 彻底退出《Casualties: Unknown》游戏；
2. 下载发布包 `NotEnoughItems-<版本号>.zip`；
3. 解压压缩包。压缩包顶层仅包含一个 `NotEnoughItems` 文件夹；
4. 将整个 `NotEnoughItems` 文件夹拖入游戏根目录下的 `BepInEx/plugins/` 目录中；
   - 正确的安装结构：`<游戏根目录>/BepInEx/plugins/NotEnoughItems/NotEnoughItems.dll`；
   - *注意：不要将文件夹嵌套拖入已有的同名文件夹中，也不要重命名核心 DLL。*
5. 启动游戏即可体验。

### 升级与卸载 (Upgrading & Uninstallation)
- **升级**：直接用新版替换 `BepInEx/plugins/NotEnoughItems/` 目录。若您曾自定义 `Lang/` 语言包或 `resourcepacks/` 材质包，请在覆盖前做好备份。
- **卸载**：直接删除 `BepInEx/plugins/NotEnoughItems/` 文件夹即可彻底卸载，不影响游戏原始存档。

---

## 快捷键与操作速查 / Controls & Keybindings

| 操作行为 | 默认键位 / 鼠标操作 | 详细说明 |
|---|---|---|
| **显示 / 隐藏 NEI** | `O` | 在背包或终端打开时切换；原版游戏「设置 → 游戏 → NEI · 显示侧栏」也可重新唤出 |
| **查询配方 (Recipe)** | 悬停按 `R` / 左键点击物品 | 查询如何制作该物品（含工作台、加热器、机器、烹饪等） |
| **查询用途 (Usage)** | 悬停按 `U` / 右键点击物品 | 查询该物品可用于制作什么、充当什么工具或原料 |
| **聚焦搜索框** | `Ctrl + F` 或 左键点击 | 快速聚焦右侧过滤框；按 `Tab` 键不会抢占输入焦点；右键快速清空输入 |
| **添加 / 移除书签** | 悬停按 `A` 或 鼠标中键 | 添加选中物品到左侧书签；在配方页按 `Shift + A` 收藏当前配方 |
| **书签栏切换** | `B` 或 点击左下角书本 | 展开 / 收起左侧书签面板 |
| **复制物品 ID / 模组名** | 按住 `Shift` + 左键点击 ID 行 | 悬停物品时按住 Shift 接管光标，点击即复制对应标识符 |
| **调节制作/刷取数量** | 搜索栏右侧数字框 / `+` `-` 按钮 | 在 `1–64` 之间调整当前批数；作弊刷取与带数量收藏共用该值 |
| **书签分组 / 缩进** | 拖动左侧空白边栏 | 拖出框线建组；拖动括号边缘向外/内调整分组范围；`Alt + 左键` 解组 |
| **启用合成链** | 右键点击分组括号 | 灰色普通括号变为绿色合成链括号，激活多层原料与工具配平推导 |
| **查看依赖与逐步配方** | 悬停绿色括号 + 按住 `Shift` | 固定悬浮窗，可滚动查看原料树、替代方案与中间工序；按住 `Ctrl` 展开属性 |
| **自动制作合成链** | 制作菜单中 `Shift + C` | 自动展开并逐批次制作目标产物（支持单机与 Together 联机） |
| **一键补齐缺少数量** | 制作菜单中 `Ctrl + Shift + C` | 自动对比当前背包库存，仅制作达到目标差额所需的数量 |
| **作弊模式切换** | `Ctrl + 左键` 点击左下角扳手 | 在「配方查询」与「作弊模式」之间切换（单机需在设置中允许作弊） |
| **作弊刷取物品** | 作弊模式下 左键 / 右键 | 左键刷取设定数量（1–64），右键刷取 1 个；单机刷取液体生成临时容器 |
| **返回上一级界面** | `Backspace` 或 点击返回箭头 | 在配方详情页返回上一次查询的结果 |

---

## 兼容性与联机说明 / Compatibility & Multiplayer

### 游戏原生界面联动
- **智能避让**：当玩家打开游戏原生宝箱、战利品容器、NPC 交易、医疗、流体转移或物品自定义面板时，NEI 会自动避让隐藏，关闭容器后立即无缝恢复。
- **拖拽保护**：拖动游戏内实物或液体容器时，NEI 控件不会拦截鼠标释放事件，彻底防止物品意外丢失或卡死。

### Together 联机模组支持
- **联机制作**：在兼容的 Together 联机环境中，NEI 等待服务器确认当前批次后才提交下一批。缺料、连接超时或服务器拒绝会停止后续批次；已提交的一批可能仍会完成，重试前先检查背包。
- **联机物品发放**：联机作弊功能由房主/服务器权限统一管控。仅在房主开启作弊且授权管理员权限时，客户端可通过已注册的 `give` 命令自发基础物品；多人模式不支持向新瓶灌装液体作弊。

---

## 界面自定义与材质包 / Themes & Customization

NEI 遵循现代化的资源组织架构，支持免重编译自定义主题：
1. **材质包覆盖**：在 DLL 所在目录下创建 `resourcepacks/active/` 文件夹；
2. 按照 `assets/notenoughitems/textures/gui/` 的相同相对路径放入替换 PNG 贴图；
3. 优先级为：`自定义材质包 > 本地外置贴图 > DLL 内置备用切片`；
4. 所有的九宫格切片与图集配置均在 `assets/notenoughitems/atlases/nei_gui.json` 中统一管理。

---

## 开发者与机器配方 API / Developer API

外部模组只需软依赖 `casualtiesunknown.notenoughitems`，即可在主线程向 NEI 注册自定义机器与合成配方，**完全无需在场景中放置实际机器对象**：

```csharp
using NotEnoughItems;

// 注册配方类别（机器）
RecipeApi.RegisterCategory(new RecipeCategory(
    id: "mymod:compressor", 
    displayName: "Matter Compressor",
    machineId: "mymod:compressor_block", 
    source: "mymod"
));

// 注册具体配方
RecipeApi.RegisterRecipe(new NotEnoughItems.Recipe(
    id: "mymod:dense_iron", 
    category: "mymod:compressor",
    inputs: new[] { new RecipeIngredient("game:iron_ingot", amount: 9) },
    outputs: new[] { new RecipeOutput("mymod:dense_iron_plate", amount: 1) },
    machineId: "mymod:compressor_block", 
    name: "Compress Iron Ingot", 
    source: "mymod"
));
```

---

## 许可与第三方鸣谢 / Licenses & Acknowledgments

本项目尊重并严格遵守开源软件与自由素材的知识产权要求。代码与视觉素材采用独立许可授权：

### 1. 模组自有代码许可 (Code License)
- 本模组的独立开发代码、测试用例与构建自动化脚本采用 **GNU Lesser General Public License v3.0 or later (LGPL-3.0-or-later)**，与 [GTNewHorizons NotEnoughItems](https://github.com/GTNewHorizons/NotEnoughItems) 的 GTNH 修改部分开源协议保持一致。详见随包附带的 `LICENSE` (LGPL v3) 与 `COPYING` (GPL v3)。

### 2. 界面视觉素材与鸣谢 (Assets & Credits)
- **Modernity-GTNH-UI**：感谢项目及贡献者。随包的上游许可正文标为 **CC BY-NC-SA 4.0 International**，但同一提交的 README 徽章指向 3.0；该版本差异及混合图集的授权仍待核实。详见[来源记录](<../../modernity-ui/SOURCES.md>)与[第三方许可说明](<../../THIRD_PARTY_NOTICES.md>)。
- **Applied Energistics 2 (AE2)**：感谢 AE2 团队及其贡献者（AlgorithmX2、Ridanisaurus Rid 等）。部分 GUI 质感遵循 **CC BY-NC-SA 3.0 Unported**（详见 `docs/ae2/`）。
- **GTNewHorizons NEI**：感谢 GTNH 团队在书签编组、状态链算法及经典布局上带来的深厚启发。

### 3. 运行时生态库 (Runtime Libraries)
- **CUCoreLib**：LGPL-3.0（Casualties Unknown 核心支持库，动态链接调用）；
- **BepInEx & HarmonyX**：LGPL-2.1 / MIT（模组底层注入力学）。

*特别提示：代码采用 LGPL 并不代表内含非商业授权贴图的整包可以进行商业化使用。请在遵守 CC BY-NC-SA 协议的前提下分享与再创作。*

### 发布配图的素材声明 / Release Artwork Asset Notice
本页配图由项目现有 `assets/notenoughitems/textures/gui/` 界面贴图裁切、缩放和编排合成，未新增手绘图标或外部图片；`interface_states.png` 为含多来源图案和本地编辑的混合图集。配图及其中第三方贴图须保留原作者署名，按适用的 **CC BY-NC-SA** 条款仅作非商业分享，标示改编并遵守相同方式共享。完整文件清单、来源链接、许可文本和发布前限制见 [`NEXUS_ASSET_CREDITS.md`](<NEXUS_ASSET_CREDITS.md>)；Modernity 许可版本冲突及混合图集权利链仍待上游确认。

---

## AI 使用与早期版本声明 / Notice

- **AI 使用说明 (AI Assistance Notice)**：本模组在代码重构、测试校验与文档本地化过程中使用了 AI 工具辅助开发。AI 辅助绝不改变任何第三方依赖、代码或素材的既有授权协议与署名义务。
- **早期开发版本 (Early Development Notice)**：本模组处于持续迭代中，可能会随游戏版本更新演进。建议在初次安装或重大版本升级前做好游戏存档与配置文件的日常备份。
