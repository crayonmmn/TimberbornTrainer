# Timberborn Trainer v0.5.0

User in-game acceptance completed; officially released v0.5.0.

One unified English/Simplified Chinese Mod for Timberborn's official loader.

- Resource Management: native map-selected warehouse item editing and separate science controls; existing pause/global/identity/capacity/reservation checks remain.
- Building & Production: Finish all construction is a one-shot command; native blockers and logistics remain authoritative. No production multiplier.
- Beaver Management: independent basic hunger/thirst maintenance; other nutrition needs and started native actions are unchanged.
- Runtime language switch with external UTF-8 JSON files. First use follows the game's Simplified Chinese, otherwise English. Manual selection is saved separately and wins next time. Same Mod ID; no separate language DLLs.

Exit the game, back up saves/old Mod folder, and extract TimberbornTrainer-v0.5.0-win64.zip into the real Documents/Timberborn/Mods directory. Keep Languages beside the DLL. Remove duplicate same-ID installations; do not mix old language DLLs. Use the official Mod manager. No BepInEx, Harmony or additional Mod is required. To uninstall, turn off maintenance/global changes, exit, and remove only this Mod folder. World changes are not undone.

Baseline: Windows64, Timberborn Steam1.1.2.8-ffc2de0-sw, Unity6000.5.11f1, Mono. Other game/Unity/interface diagnostics warn, without promising compatibility. Native interfaces can still fail. No universal third-party Mod compatibility is promised.

Testing: The user completed v0.5.0 in-game acceptance and confirmed normal operation on2026-11-16. Automated builds/regressions/localization checks also passed. This is not exhaustive validation of every resolution, save, game version or Mod combination. The accepted ZIP is uploaded unchanged; its bundled pre-acceptance guide wording is superseded by this notice and online documentation.

Project-owned code is MIT; retain LICENSE and applicable THIRD_PARTY_NOTICES. No game DLLs/assets included. Mainly developed with OpenAI Codex from author requirements and feedback; not an official OpenAI release. AS IS, with no commitment to ongoing maintenance, fixes, support or updates. Forks, Issues and PRs welcome; fork maintainers own their compatibility and no response is promised.

Source / issues: https://github.com/crayonmmn/TimberbornTrainer
https://github.com/crayonmmn/TimberbornTrainer/issues

---

## 简体中文

统一双语安装包，使用游戏官方加载器。保留资源管理（地图选仓与独立科研）、建造与生产（一次性完成工地）、海狸管理（基础饥饿口渴维持）。不改变原有全局、暂停、身份、容量、预留和异常处理，不新增生产倍率。

新增外置UTF-8中英文JSON及运行时切换。首次按游戏简体中文选择，其他默认英文；手动选择单独保存并优先恢复。一套DLL、相同Mod ID，不能与旧中英文包混装。

退出游戏并备份存档及旧Mod，将完整统一包解压至实际文档目录Timberborn/Mods，保留DLL旁Languages目录，通过官方管理器启用。无需BepInEx、Harmony或额外Mod。卸载前关闭维生与全局开关，退出后移除本Mod文件夹，已发生的世界修改不会撤销。

基准Windows64、Steam1.1.2.8-ffc2de0-sw、Unity6000.5.11f1、Mono。其他版本和接口只警告，不保证兼容。用户已于2026-11-16完成v0.5.0实机验收并确认功能正常，自动化验证亦通过；不代表所有分辨率、存档、游戏版本和Mod组合均已验证。已验收ZIP原样上传，内含准备期文案由本说明及在线文档的最终验收状态补充。

自有代码MIT，保留许可与第三方声明，不分发游戏DLL/资产。主要由OpenAI Codex根据作者需求和反馈开发，非OpenAI官方作品。按现状提供，不承诺维护、修复、支持或更新。欢迎Fork/Issue/PR，修改者自负维护兼容责任。
## Final automated results / 最终自动化结果

Unified Release: 0 errors, 0 warnings. English and Chinese regression contexts: **742 passed, 0 failed, 0 unexecuted each** (704 original +27 language-core +11 UI-adapter tests). External-language/package static suite: **24 passed**. These do not constitute in-game acceptance.

统一Release0错误0警告；中英文回归上下文各742通过、0失败、0未执行，本地化／包结构24项通过；不等于游戏内验收。

Unified ZIP SHA256 / 统一包校验：

43E827F25704E41E58A6D5E9DB3AE79B0B245126B0A282CD363B6A30204FDEDF

ZIP bytes: 100598; packaged files: 11.

## Downloads and guides / 下载与说明

- [Unified ZIP](https://github.com/crayonmmn/TimberbornTrainer/releases/download/v0.5.0/TimberbornTrainer-v0.5.0-win64.zip)
- [SHA256SUMS.txt](https://github.com/crayonmmn/TimberbornTrainer/releases/download/v0.5.0/SHA256SUMS.txt)
- [English guide](https://github.com/crayonmmn/TimberbornTrainer/blob/v0.5.0/docs/Usage.en.md) · [中文说明](https://github.com/crayonmmn/TimberbornTrainer/blob/v0.5.0/docs/使用说明.md)
- [Acceptance record](https://github.com/crayonmmn/TimberbornTrainer/blob/v0.5.0/docs/release/v0.5.0/acceptance.md)
