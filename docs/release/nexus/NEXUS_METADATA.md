# Nexus Mods 页面创建与元数据速查表 / Nexus Mod Page Metadata

在 [Nexus Mods](https://www.nexusmods.com/) 创建新模组页面（Add a Mod）或编辑模组信息时，可直接参考并复制本表中的各项内容。

---

## 1. 基础信息 / Basic Information

| 表单字段 / Field | 建议填入内容 (中文 / English) | 字符限制与说明 |
|---|---|---|
| **Mod Name** (模组名称) | `Not Enough Items (NEI)` 或 `Not Enough Items (NEI) - Casualties Unknown` | 简洁、易于搜索的主名称 |
| **Short Description** (一句话简述) | `In-game item catalog, bi-directional recipe & usage viewer, bookmark crafting chains, and inventory utility for Casualties: Unknown.` | 必须在 255 字符以内；显示在搜索列表与卡片流中 |
| **Category** (分类) | `User Interface` / `Utilities` (根据 Casualties: Unknown 游戏板块开放的分类选择，优先 UI 或 工具类) | 确保玩家在 UI 或实用工具中快速检索 |
| **Version** (初始发布版本) | `0.1.32` | 对应工程当前构建版本 `Directory.Build.props` |
| **Language** (语言支持) | `English`, `Chinese - Simplified` | 模组原生内建双语支持 |

---

## 2. 标签 / Tags

在 Nexus 标签选择器中勾选或添加：
- `User Interface` (界面)
- `Quality of Life` (易用性/便利性)
- `Crafting` (制作/合成)
- `Cheats` (作弊工具，可选)
- `Multiplayer Compatible` (联机兼容)
- `Utilities` (实用工具)

---

## 3. 依赖项配置 / Requirements (Nexus Requirements Tab)

在模组发布页面的 **Requirements** 区域，配置以下依赖信息：

### 必需前置依赖 (Nexus Requirements)
1. **BepInEx 5 (Mono)**
   - 链接/来源：GitHub / BepInEx 官方分发
   - 说明：`Requires BepInEx 5.4.x (Mono x64). IL2CPP versions are not supported.`
2. **CUCoreLib**
   - 版本要求：`1.1+`
   - 说明：`Core foundational library for Casualties: Unknown modding.`

### 可选协同模组 (Optional Compatibility)
1. **Unknown Inventory Terminal**
   - 版本要求：`0.4.4+`
   - 说明：`NEI automatically anchors to terminal panels alongside vanilla inventory.`
2. **Just-Unknown-Characters**
   - 版本要求：`1.0.1+`
   - 说明：`Enables Chinese Pinyin and initial-letter search in NEI search field.`
3. **Together**
   - 版本要求：`5.5.4+`
   - 说明：`Multiplayer mod; enables receipt-verified co-op bookmark batch crafting.`

---

## 4. 权限与版权声明设置 / Permissions and Credits

在 Nexus Mods 的 **Permissions** 设置步骤中：

| 权限项 / Permission Item | 建议设置 | 说明理由 / Basis |
|---|---|---|
| **Modification approval** (修改权限) | Yes with credit / As per license | 自有代码遵循 LGPL-3.0-or-later 许可协议 |
| **Conversion approval** (跨游戏移植) | Yes with credit | 遵循开源协议，保留署名 |
| **Asset use approval** (素材使用权限) | No / Conditional | **重要**：因包含 CC BY-NC-SA 4.0 (Modernity) 及 CC BY-NC-SA 3.0 (AE2) 第三方素材，不能无条件开放商业使用 |
| **Asset upload permission** (重新上传) | No / Check upstream | 建议只允许链接或引用原模组页面 |
| **Donation Points** (赞赏积分) | Opt-out (建议关闭或仅限自有代码部分) | 包含 CC BY-NC-SA（非商业）素材，避免产生商业收益争议 |
| **Credits** (鸣谢文本框) | 见下方复制框 | 填入上游开源项目与素材贡献者名单 |

### Credits 文本框粘贴内容：
```text
- GTNewHorizons Team: For NotEnoughItems (GTNH fork) inspiration, bookmark algorithms, and classic workflow design.
- Modernity-GTNH-UI Team: For beautiful open-source UI panel artwork (CC BY-NC-SA 4.0).
- Applied Energistics 2 Team (AlgorithmX2, Ridanisaurus Rid et al.): For GUI textures and assets (CC BY-NC-SA 3.0).
- jimmyking9999999: For CUCoreLib framework and Casualties: Unknown modding utilities.
- BepInEx & HarmonyX teams: For robust Unity/Mono plugin injection libraries.
```

---

## 5. 文件上传配置 / Main File Upload Form

| 表单字段 / Field | 填入内容 |
|---|---|
| **File Name** (文件名称) | `NotEnoughItems-0.1.32.zip` |
| **Display Name** (展示名称) | `Not Enough Items 0.1.32 (Mono)` |
| **Version** (版本) | `0.1.32` |
| **Category** (文件类别) | `Main Files` |
| **Brief Overview** (文件简述) | `Main release. Extract and place the 'NotEnoughItems' folder directly into 'BepInEx/plugins/'. Requires BepInEx 5 (Mono) and CUCoreLib 1.1+.` |
| **Changelog** (更新记录) | 复制 `docs/release/nexus/CHANGELOG_NEXUS.md` 中的对应版本内容 |
