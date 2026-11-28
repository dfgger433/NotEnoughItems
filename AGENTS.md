# Not Enough Items 协作规则

## 何时升版并补日志

- 一批改动只要涉及 `src/` 源码或构建配置、`assets/` 贴图或相关资源、项目脚本（包括 `scripts/` 和根目录的工具脚本），或 `tests/` 测试代码，就必须在提交或交付前升版并补齐两份更新记录；同时修改文档也一样。同一批只升一次。
- 没有用户明确要求时，只升补丁号（`patch`，即 `MAJOR.MINOR.PATCH` 的最后一段）。仅在用户明确指定时，才用 `minor`、`major` 或指定的更高版本。
- 只改 `docs/`、`README.md`、协作说明或发布文案等纯文档时，不升版，不强制补发布日志，也不必重新打发布包。只读检查不升版。
- 需要升版时，在 [项目更新记录](<docs/CHANGELOG.md>) 顶部的 `## 未发布` 下写中文条目；在 [Nexus 更新记录](<docs/release/nexus/CHANGELOG_NEXUS.md>) 顶部的 `## 未发布` 下分别在 `### English` 和 `### 中文` 下写真实变更条目。不要只加标题、留占位文案或改写旧版本条目来代替新记录。
- 需要升版时，提交前默认运行 `pwsh -File ./scripts/Release.ps1 -Bump patch`，自动同步 [版本配置](<src/Directory.Build.props>)、两份日志的版本标题和 [Nexus 页面字段](<docs/release/nexus/NEXUS_METADATA.md>)，再构建和运行回归测试。将版本和日志文件与本批改动一起交付；不要先提交需要升版的改动，再用干净工作区绕过发布检查。[本地构建与发布说明](<README.md#一键生成发布-zip>)有重跑和指定游戏根目录的命令。
