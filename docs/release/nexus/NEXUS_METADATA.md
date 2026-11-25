# Nexus Mods 页面字段 / Nexus Mod Page Metadata

适用于在 [Casualties: Unknown（原 Scav Prototype）](https://www.nexusmods.com/scavprototype) 游戏板块创建或编辑 NEI 页面。以下是投稿草稿，不代表 ZIP 或配图已获公开分发许可。

## 1. 页面信息 / Mod Details

| Nexus 字段 | 建议填写 |
|---|---|
| **Mod Name** | `Not Enough Items (NEI)` |
| **Game** | `Casualties: Unknown (Former Scav Prototype)` |
| **Summary / Short Description** | `Search items, look up recipes and their uses, and plan crafting chains with bookmarks in Casualties: Unknown.` |
| **Category** | 优先选择 `User Interface`；如果该板块没有此分类，选最接近的 `Utilities` 或界面／易用性类别。以表单中的实际选项为准。 |
| **Version** | `0.1.33`，以 [版本配置](<../../../src/Directory.Build.props>)（仅源码树有此文件）和本次上传 ZIP 的版本为准。 |
| **Languages** | `English`, `Simplified Chinese`（NEI 界面）。 |
| **Full Description** | 在 BBCode 模式粘贴 [NEXUS_DESCRIPTION_BBCODE.txt](<NEXUS_DESCRIPTION_BBCODE.txt>)，预览后保存；[NEXUS_DESCRIPTION.md](<NEXUS_DESCRIPTION.md>) 是便于维护的 Markdown 版。 |

如果需要中文短简介，可用「在《Casualties: Unknown》背包中搜索物品、查询配方和用途，并用书签规划合成链。」英文短简介单独填写即可，不必把两段一起粘贴。

## 2. 标签 / Tags

按标签选择器的实际选项，优先选择与核心功能匹配的 `User Interface`、`Quality of Life`、`Crafting`；如有 `Utilities` 或 `Cheats`，也符合工具功能和可选作弊模式。只有能说明适用范围时才选择联机标签：**支持带制作回执的 Together，不代表兼容所有联机模组**。

页面说明应保留 AI 使用披露。按 Nexus 当前的 [AI 披露与标签规则](https://help.nexusmods.com/article/180-ai-policy-changes-2026)，根据实际使用范围选择标签（例如使用 AI 生成页面描述时核对 `AI Media`）；不要将离线重建的 UI 配图标为实机截图。

## 3. 依赖 / Requirements

下列依赖均不包含在 NEI ZIP 中。只把前两项标为必需；后三项可在 Requirements 中标为可选，或列在 Description 的兼容性部分。

| 类型 | 名称和版本 | 下载来源 | 可粘贴的英文说明 |
|---|---|---|---|
| 必需（外部依赖） | **BepInEx 5.4.x (Mono, Windows x64)** | [BepInEx 官方发布页](https://github.com/BepInEx/BepInEx/releases) | `Install the BepInEx 5 Windows x64 build for the Mono version of Casualties: Unknown. IL2CPP is not supported.` |
| 必需（Nexus 站内依赖） | **CUCoreLib 1.1+** | [CUCoreLib](https://www.nexusmods.com/scavprototype/mods/341) | `Required. Install CUCoreLib 1.1 or newer before starting the game.` |
| 可选 | **Unknown Inventory Terminal 0.4.4+** | [Unknown Inventory Terminal](https://www.nexusmods.com/scavprototype/mods/1096) | `Optional. Shows NEI alongside the terminal; the vanilla inventory works without it.` |
| 可选 | **Just Unknown Characters 1.0.1** | [Just Unknown Characters](https://www.nexusmods.com/scavprototype/mods/402) | `Optional. Adds Pinyin and initial-letter search in the Simplified Chinese NEI catalog.` |
| 可选 | **Together（已核对 5.5.4 的制作回执接口）** | [Together](https://www.nexusmods.com/scavprototype/mods/67) | `Optional. Co-op batch crafting requires a compatible Together crafting receipt API (checked against 5.5.4). Orders with manually locked ingredients are not supported in multiplayer.` |

## 4. 权限、许可与鸣谢 / Permissions, Licenses & Credits

- **自有代码**：`LGPL-3.0-or-later`。随包保留 [LICENSE](<../../../LICENSE>) 与 [COPYING](<../../../COPYING>)。若页面只能填一种许可，应明确它仅适用于自有代码，不覆盖贴图（包括 DLL 内嵌贴图）或配图。
- **第三方 GUI 贴图**：Modernity-GTNH-UI 随附许可正文为 `CC BY-NC-SA 4.0 International`，但上游 README 徽章指向 `3.0`；AE2 GUI 贴图为 `CC BY-NC-SA 3.0 Unported`。保留来源、署名和许可，标明修改，并遵守适用的非商业及相同方式共享条款。
- **Nexus 权限选项**：不要对整包勾选无条件的修改、移植、素材复用或重新上传许可，也不要将页面选项写成对开源代码既有权利的额外限制。包含非商业素材的版本先关闭 Donation Points；免费发布或关闭积分本身不等于满足非商业条款。
- **公开发布前**：按[第三方鸣谢与许可](<../../THIRD_PARTY_NOTICES.md>)和[配图素材来源](<NEXUS_ASSET_CREDITS.md>)确认或替换有疑问的素材，尤其是混合图集，检查拟发布源码与构建包，并按适用的代码许可提供与二进制对应的源码和构建脚本。相关条款未核清前，不公开 ZIP 或使用相关宣传图。

**Credits 文本框：**

```text
Thanks to the GTNewHorizons NotEnoughItems contributors for the bookmark and recipe-viewer workflow references. GUI artwork uses material from Modernity-GTNH-UI contributors (CC BY-NC-SA 4.0 in the upstream license text; the upstream badge links to 3.0) and Applied Energistics 2 contributors (CC BY-NC-SA 3.0; © 2020 Ridanisaurus Rid, © 2013–2020 AlgorithmX2 et al.). Some GUI atlas regions were adapted. See the bundled docs/THIRD_PARTY_NOTICES.md, docs/modernity-ui/ and docs/ae2/ for sources, attributions and license texts. Thanks to Jimmyking for CUCoreLib and to the BepInEx and HarmonyX contributors.
```

## 5. 主文件上传 / Main File Upload

| Nexus 字段 | 建议填写 |
|---|---|
| **Archive** | `dist/NotEnoughItems-0.1.33.zip`（从源码树构建后生成） |
| **Display Name** | `Not Enough Items 0.1.33 (Mono)` |
| **Version** | `0.1.33` |
| **File Category** | `Main Files` |
| **File Description** | `Extract the NotEnoughItems folder into BepInEx/plugins/. Requires BepInEx 5 (Mono) and CUCoreLib 1.1 or newer.` |
| **Changelog** | 粘贴 [CHANGELOG_NEXUS.md](<CHANGELOG_NEXUS.md>) 中与 ZIP 版本一致的条目。 |

归档顶层只有 `NotEnoughItems/`；[本表和其余投稿文档](<README.md>)在归档内的 `NotEnoughItems/docs/release/nexus/`。上传步骤和核对项见[发布指引](<README.md#发布步骤建议--recommended-release-workflow>)。
