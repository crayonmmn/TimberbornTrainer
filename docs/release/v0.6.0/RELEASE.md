# Timberborn Trainer v0.6.0

Manual language selection and UI label fixes, with the existing gameplay features unchanged.

- Choose **English** or **简体中文** under **Language / 语言**. There is no Auto mode and the game language is no longer read.
- With no valid preference, English is the default. Existing `en-US` / `zh-CN` choices remain valid and are restored from `%LOCALAPPDATA%/TimberbornTrainer/language.json`.
- Fix missing UI labels by using the native UI Toolkit label property. The global switch is now clearly named **Enable global changes**, with an explanation of its purpose and inventory pause requirements.
- Resource inventory, research, one-shot construction and basic hunger/thirst maintenance retain their algorithms and safety checks. Window and edge-tab interactions are unchanged.

## Install

Download **TimberbornTrainer-v0.6.0-win64.zip**, exit the game and back up the previous Mod folder and important saves. Extract the complete `TimberbornTrainer` folder into your actual Documents/Timberborn/Mods directory, including redirected Documents folders. Keep `Languages` beside the DLL and enable the Mod with the official manager. Install only one copy of `TimberbornTrainer.Core`; do not mix manual and Workshop copies.

The existing [Workshop item](https://steamcommunity.com/sharedfiles/filedetails/?id=3840799761) remains Private. Its upload status is tracked separately; a GitHub release does not prove the Workshop files have been updated.

Verified game environment: Steam Timberborn **1.1.2.8-ffc2de0-sw**, Unity6000.5.11f1, Mono, Windows64. No extra Mod, BepInEx or separate Harmony installation is required in this environment. Compatibility checks warn only; native interfaces and original business checks still apply. Unverified game versions and third-party Mod combinations are not guaranteed compatible.

Testing: 755 regression assertions passed in each language context, plus 27 localization/package checks; Release build 0 errors and 0 warnings. The user confirmed in-game testing of these fixes. Automated UI substitutes do not establish exhaustive real UI, save or compatibility coverage.

To uninstall, disable maintenance/global changes, exit the game, then remove the manual copy or unsubscribe from the Workshop copy. Existing world edits are not undone. Back up saves.

Project-owned code is MIT; retain LICENSE and third-party notices. No official game DLLs/assets are included. Forks, Issues and Pull Requests are welcome, without a response or maintenance commitment.

This is a personal project provided as-is. Ongoing maintenance and compatibility with future game versions are not guaranteed. Bug fixes, support and updates are not promised. This is not an official OpenAI or Mechanistry product.

Development: This project was developed by the author with AI-assisted collaboration. The author directed the design, implementation, testing, and release process, with AI tools assisting in research, coding, debugging, documentation, and translation.

Source: https://github.com/crayonmmn/TimberbornTrainer

---

## 简体中文

v0.6.0 调整为纯手动语言选择并修复 UI 标签，原有业务功能不变。

- 在 **Language / 语言** 中选择 English 或简体中文，不再读取游戏语言，无 Auto 模式。
- 没有有效偏好时默认英文，旧版 `en-US` / `zh-CN` 选择继续有效，保存于 `%LOCALAPPDATA%/TimberbornTrainer/language.json` 并在重启后恢复。
- 修复原生 UI Toolkit 标签节点缺失；全局开关明确显示为 **启用全局修改**，补充开关作用及库存修改需暂停的说明。
- 不改变库存、科研、一次性建造、饥饿／口渴维持算法和安全条件，不改变悬浮窗口或标签交互。

下载 **TimberbornTrainer-v0.6.0-win64.zip**，退出游戏并备份旧 Mod 和重要存档，将完整 TimberbornTrainer 文件夹解压到实际文档目录下的 Timberborn/Mods。保留同级 Languages，通过官方管理器启用。同 ID 只装一份，不混装手动和 Workshop 副本。

现有 [Workshop 条目](https://steamcommunity.com/sharedfiles/filedetails/?id=3840799761) 保持私人，其文件更新状态需单独确认，GitHub 发布成功不等于 Workshop 已同步。

已验证环境为 Steam1.1.2.8-ffc2de0-sw、Unity6000.5.11f1、Mono、Windows64，无需额外 Mod、BepInEx 或单独安装 Harmony。其他版本只提示兼容警告，不保证所有新版本或第三方 Mod 组合兼容。

中英文回归上下文各755通过，本地化／包检查27项通过，Release构建0错误0警告；用户确认本次修复已通过实机测试。不代表穷尽所有界面、存档或兼容情况。

卸载前停用维生和全局修改，退出游戏后移除手动副本或取消订阅；已经修改的游戏世界状态不会自动撤销。自有代码MIT，保留许可证及第三方声明，不包含官方游戏DLL或资产。欢迎Fork和社区贡献。

本项目为个人开发项目，按现状提供，不承诺持续维护、Bug修复、技术支持或未来兼容。非OpenAI或Mechanistry官方产品。

开发说明：本项目由作者主导设计与开发，并与 AI 合作完成。作者负责整体设计、开发方向、功能决策、测试及发布，AI 工具参与资料调查、代码编写、调试、文档整理和翻译等辅助工作。
