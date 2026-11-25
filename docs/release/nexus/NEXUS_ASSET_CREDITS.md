# 发布配图素材来源与许可 / Release Artwork Asset Credits

本目录的发布配图由项目现有界面贴图合成，不新增手绘图标、纹理或外部图片。配图文件为：

- [`nexus_assets/nexus_header_1300x372.png`](../../../nexus_assets/nexus_header_1300x372.png) — Nexus Header Image
- [`nexus_assets/nexus_showcase_1920x1080.png`](../../../nexus_assets/nexus_showcase_1920x1080.png) — Nexus Showcase Image
- 生成脚本：[`generate_nexus_assets.py`](../../../generate_nexus_assets.py)
- 视觉哲学：[`nexus_visual_philosophy.md`](../../../nexus_visual_philosophy.md)

## 合成所用的现有素材

生成脚本从 [`Assets/NotEnoughItems/textures/gui/`](../../../Assets/NotEnoughItems/textures/gui/) 读取并以像素保持的方式裁切、缩放、叠放下列素材：

- `panels/inventory.png`
- `panels/recipe_background.png`
- `panels/potion_editor.png`
- `panels/catalyst_tab.png`
- `panels/recipe_slot.png`
- `panels/repair_recipe.png`
- `sprites/interface_states.png`
- `sprites/button_backgrounds.png`
- `sprites/search_and_tooltip.png`
- `sprites/toggle_and_radio.png`
- `sprites/language_dropdown.png`

其中 `interface_states.png` 是含有 Modernity 与 AE2 图案并经过本地组装/修改的混合图集；发布配图中出现的状态图案应按混合图集的全部适用来源核对，不能仅凭文件名推断单一版权来源。

## 来源、署名与许可

- **Modernity-GTNH-UI 及其贡献者**：面板与按钮原件的来源、固定提交、文件清单及署名见 [`docs/modernity-ui/SOURCES.md`](../../modernity-ui/SOURCES.md)。随包许可证全文明示 **CC BY-NC-SA 4.0 International**，同提交 README 的旧徽章仍指向 3.0；该冲突及权利链仍需向上游确认。
- **Applied Energistics 2 及其贡献者**：部分状态/导航图案的来源与修改说明见 [`docs/ae2/SOURCES.md`](../../ae2/SOURCES.md)，其 GUI 素材适用 **CC BY-NC-SA 3.0 Unported**。
- **混合图集与本地改编**：`interface_states.png` 同时含有多个来源和本地编辑元素。将其用于这两张合成图属于对现有素材的编排/改编展示；必须保留适用的署名、版权、许可链接，并明确标示本次合成与改动。

代码的 LGPL-3.0-or-later 不会替代或覆盖贴图的第三方许可。完整背景见 [`docs/THIRD_PARTY_NOTICES.md`](../../THIRD_PARTY_NOTICES.md) 与 [`README.md`](../../../README.md) 的“鸣谢与许可”。

## 发布前约束

1. **非商业**：含有上述第三方贴图的配图按 CC BY-NC-SA 约束，仅作非商业分享；不可因项目代码采用 LGPL 就将整包或配图宣称为可商业使用。
2. **署名**：发布页面应保留 Modernity-GTNH-UI、Applied Energistics 2 及其贡献者的归属，链接对应来源记录和许可证文本。
3. **标示修改**：应说明图片是由本项目现有 UI 贴图裁切、缩放、编排合成，且 `interface_states.png` 为混合/本地修改图集。
4. **相同方式共享**：若将配图作为改编素材再分发，应遵守适用的 ShareAlike 条件，并保留本说明与上游许可证信息。
5. **权利链待确认**：`docs/THIRD_PARTY_NOTICES.md` 记录 Modernity 许可版本冲突和混合图集核查事项；在上游确认前，不应把配图作为无条件、清权利的商业宣传素材。
6. **避免未确认控件**：本次配图没有使用 `Assets/NotEnoughItems/textures/gui/controls/vertical_buttons_background*.png` 或 `贴图/vertical_buttons_bg*`，因为其来源与授权尚未确认。

## 建议的发布页署名文本

> Release artwork assembled from the existing Not Enough Items GUI textures. UI assets retain their original authorship and licenses: Modernity-GTNH-UI contributors (CC BY-NC-SA 4.0 as recorded in the bundled license; upstream badge discrepancy noted) and Applied Energistics 2 contributors (CC BY-NC-SA 3.0). The artwork is a non-commercial adapted composition; source textures were cropped, scaled, and arranged, and the mixed `interface_states.png` atlas includes local edits. See `docs/release/nexus/NEXUS_ASSET_CREDITS.md`, `docs/modernity-ui/`, `docs/ae2/`, and `docs/THIRD_PARTY_NOTICES.md`.
