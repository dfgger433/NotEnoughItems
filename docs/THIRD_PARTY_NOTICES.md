# 第三方鸣谢与许可说明

本说明记录本项目的代码许可、第三方来源及各自的适用范围。代码与贴图分开授权，第三方作品保留原有许可。

## 本项目代码许可

本项目自有代码（插件源码、测试代码及构建和安装脚本）采用 **LGPL-3.0-or-later**，与 GTNH NEI 的 GTNH 修改部分相同。构建和安装会在 DLL 同级保留 [LGPL 第 3 版原文](<../LICENSE>)和其所引用的 [GNU GPL 第 3 版原文](<../COPYING>)。第三方代码依其各自声明；上述代码许可不覆盖第三方贴图或参考资料。

## 公开分发前的核查

构建会把资源与通知复制到输出，并将贴图内嵌到 DLL；仅分发 DLL 也会一并分发这些贴图。目前素材有非商业与相同方式共享条件、Modernity 上游版本标记差异、混合图集及两张来源待确认的控件图。添加代码许可并未解决这些问题；免费发布也不自动满足“非商业”条件。公开前应核实或替换相关素材，核对干净构建与拟发布的 Git 历史，并按代码适用条款提供与发布二进制对应的源码及构建脚本。

## 鸣谢与素材许可

### Modernity-GTNH-UI

感谢 [Modernity-GTNH-UI 项目及其贡献者](https://github.com/ModernityGTNH/Modernity-GTNH-UI/graphs/contributors)。本插件使用其 `29x` 分支提交 [`10b9e676c2cb45ac5cee2c1df3285bf085ad53f6`](https://github.com/ModernityGTNH/Modernity-GTNH-UI/tree/10b9e676c2cb45ac5cee2c1df3285bf085ad53f6) 中的八张原始 PNG（七张面板图和一张按钮图）。原件清单、上游路径及图集说明见 [Modernity 来源记录](<modernity-ui/SOURCES.md>)；[原始许可证全文](<modernity-ui/LICENSE.txt>)随包保留。

该提交的 `LICENSE.txt` 明示 [CC BY-NC-SA 4.0 International](https://creativecommons.org/licenses/by-nc-sa/4.0/)，但同提交 [README 的许可徽章](https://github.com/ModernityGTNH/Modernity-GTNH-UI/blob/10b9e676c2cb45ac5cee2c1df3285bf085ad53f6/README.md#licensing)链接到 3.0。应向上游确认这一冲突及相关素材的权利链。按许可证全文，分享适用素材须保留署名、版权及许可信息，标示修改；改编素材还须遵守相同方式共享条件，且仅可用于非商业目的。不要将这些条件套用为本插件代码的许可证。

### Applied Energistics 2（AE2）

感谢 [Applied Energistics 2 项目及贡献者](https://github.com/AppliedEnergistics/Applied-Energistics-2)。其 GUI 素材保留 **CC BY-NC-SA 3.0 Unported**，版权 **© 2020 Ridanisaurus Rid；© 2013–2020 AlgorithmX2 et al.**；固定提交、素材对应关系及修改见随包的[来源与署名](<ae2/SOURCES.md>)和[许可证全文](<ae2/LICENSE.txt>)。

### 混合及来源待确认的贴图

[interface_states.png](<../assets/notenoughitems/textures/gui/sprites/interface_states.png>) 同时使用 Modernity 与 AE2 图案，也有本地修改。分别标示来源和许可不等于为整张混合图集解决了授权；公开前应逐区域核实来源及适用条款，或拆分、替换相关素材。两张垂直按钮控件样图 `vertical_buttons_background.png` 和 `vertical_buttons_background_variant.png` 的来源和授权尚未确认，仅留作源码工作区参考，不复制到构建输出，也不内嵌到 DLL。核实或替换前，不应发布包含它们的包。

### GTNewHorizons NotEnoughItems（GTNH NEI）

感谢 [GTNH NEI 项目及贡献者](https://github.com/GTNewHorizons/NotEnoughItems)，其书签交互、方法行为及图集坐标是实现时的参考。项目随附 [NEI 行为说明](<../README.md>) 和 [界面素材来源记录](<modernity-ui/SOURCES.md>) 记有具体参考位置。源码 [BookmarkPull.cs](<../src/NotEnoughItems/UI/Screens/BookmarkPull.cs#L21>) 还注明相关操作“Mirrors GTNH NEI 2.8.171-GTNH”；发布前需逐段区分算法/行为参考与对受版权保护实现表达的翻译或移植，当前尚不能断言不存在实质性复用。

GTNH NEI 固定提交 [`6b7e465306810ad11d2413214077237798be13a7`](https://github.com/GTNewHorizons/NotEnoughItems/tree/6b7e465306810ad11d2413214077237798be13a7) 的[许可声明](https://github.com/GTNewHorizons/NotEnoughItems/blob/6b7e465306810ad11d2413214077237798be13a7/README.md#L85-L93)注明 GTNH 修改部分采用 **LGPL-3.0-or-later**，版权归 **© 2019–2024 mitchej123 与 GTNH Team**。本项目采用的 LGPL 文本原样取自该提交的[许可证文件](https://github.com/GTNewHorizons/NotEnoughItems/blob/6b7e465306810ad11d2413214077237798be13a7/LICENSE.txt)。上游另注明部分内容参考、借用或回移自 JEI，JEI 原始代码采用 MIT、© 2014–2015 mezz。本项目采用相同的 LGPL 不代表已完成第三方代码来源审查；如果确认复用了受保护的代码，应保留受影响部分的原有版权与许可通知，并履行相应分发义务。

当前发行 DLL 中未发现 GTNH 的三张旧版 PNG。仓库先前版本/历史中曾包含这些原图；切换到当前资产目录并不会删除 Git 历史中的文件，公开仓库前也应审查实际要公开的提交历史。图片授权应按具体图片核对，不能仅以代码许可证推断。

## 运行时依赖与可选互操作

- **CUCoreLib**：必需运行时依赖（项目声明最低版本 1.1），由 [插件入口](<../src/NotEnoughItems/Integrations/Plugin.cs#L16-L20>) 使用；构建将其引用标为不复制，因此它不在本插件发行包中。CUCoreLib v1.1 的 [LICENSE](https://github.com/jimmyking9999999/CUCoreLib/blob/v1.1/LICENSE) 为 LGPLv3。分发链接该库的插件时，应按 LGPL 组合作品条款处理，随包提供相应许可和显著依赖通知，并允许用户以接口兼容版本替换库；不要禁止为调试对库所作修改而进行必要的逆向。若另行打包该库，还需履行该库本身的分发及对应源代码义务。
- **BepInEx 与 HarmonyX**：构建和运行时框架引用，不由当前插件安装脚本复制。BepInEx 项目说明其采用 LGPL-2.1；HarmonyX v2.9.0 的 [LICENSE](https://github.com/BepInEx/HarmonyX/blob/v2.9.0/LICENSE) 为 MIT。若另行随包分发这些项目的文件，应保留各自版权和许可证通知。
- **Just Unknown Characters**：可选拼音搜索互操作，仅通过反射调用；生产插件不打包其 DLL 或拼音字典。[上游 LICENSE](https://github.com/XeronOwO/Just-Unknown-Characters/blob/master/LICENSE) 为 MIT，版权 © 2026 Xeron。若将来捆绑该项目或其实质部分，需一并保留 MIT 版权和许可文本。
- **Together**：可选联机互操作，通过反射调用其公开游戏接口，本插件没有打包 Together DLL。上游仓库提供 LGPL 许可证，但尚未将其与所支持的确切发行二进制版本对应核验；若之后捆绑或移植其代码，应先核对固定版本的许可和对应源码。
