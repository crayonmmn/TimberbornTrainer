# Timberborn Trainer v0.4.0

A bilingual in-game trainer using Timberborn's official mod loader, with three existing modules:

- **Resource Management:** map-selected warehouse items and a separate science-points page. Native capacity, reservations, valid targets, global enable and pause requirements remain in effect.
- **Building & Production:** **Finish all construction** completes eligible existing sites once through the native API. New sites do not auto-complete; native building pause and blockers are respected.
- **Beaver Management:** independent basic hunger/thirst maintenance. Other nutrition needs and already-started eating/drinking actions are not forcibly cancelled.

## Choose one language

- `TimberbornTrainer-v0.4.0-en-win64.zip` — English.
- `TimberbornTrainer-v0.4.0-zh-CN-win64.zip` — 简体中文.
- `SHA256SUMS.txt` — SHA256 checksums for both ZIPs.

**Install only one package. Both use Mod ID `TimberbornTrainer.Core`; do not install them together or mix DLLs.** They share business source and behavior. Language is fixed at build time. Item names use the game's own localization, and native IDs are unchanged.

## Testing

**Testing notice:** The English version has passed automated build, regression, and localization tests, but has not been manually tested in-game. Some UI or compatibility issues may remain. Please report any problems through GitHub Issues.

- English and Chinese Release builds: **0 errors, 0 warnings each**.
- Full regression: **704 passed, 0 failed, 0 unexecuted per language**.
- Localization/package checks: **20 passed**, including non-text IL comparison of **759 method bodies**.
- Additional English headless text-control style checks: **7 passed**.
- Final ZIP extraction/file hashes verified. The Chinese version retains prior user in-game validation.

Automated native-interface/UI substitutes do not prove real fonts, clipping, DPI, mouse input, camera controls or native simulation behavior. No English manual in-game acceptance is claimed. See the [acceptance record](https://github.com/crayonmmn/TimberbornTrainer/blob/v0.4.0/docs/release/v0.4.0-automated-acceptance.md).

## Install and uninstall

Exit the game and back up important saves. Extract one ZIP's `TimberbornTrainer` folder into `Timberborn/Mods/` under your actual Documents folder (possibly redirected to OneDrive). Confirm the DLL and manifest share the same directory, then enable the mod in the official mod manager. No BepInEx, Harmony or extra mod is required. GitHub's automatic Source code archives are not installation packages.

To uninstall, turn off continuous need maintenance and the trainer, exit the game, then back up/remove only this Mod folder. Prior world changes are not undone. [English guide](https://github.com/crayonmmn/TimberbornTrainer/blob/v0.4.0/docs/Usage.en.md) · [中文使用说明](https://github.com/crayonmmn/TimberbornTrainer/blob/v0.4.0/docs/使用说明.md).

## Compatibility and license

Verified baseline: Steam Timberborn **1.1.2.8-ffc2de0-sw**, Unity **6000.5.11f1**, Mono, Windows 64-bit. Other game/Unity versions and interface diagnostics warn only, without themselves disabling features; actual calls can fail. Existing business safety checks and exception handling remain. `MinimumGameVersion` is not a promise that all later versions work. Third-party Mod compatibility is not guaranteed.

Project code is [MIT licensed](https://github.com/crayonmmn/TimberbornTrainer/blob/v0.4.0/LICENSE); keep applicable copyright/license and [third-party notices](https://github.com/crayonmmn/TimberbornTrainer/blob/v0.4.0/THIRD_PARTY_NOTICES.md). Game assemblies/assets are not included. Forks, modifications, Issues and Pull Requests are welcome; each fork maintains its own compatibility. [Report a problem](https://github.com/crayonmmn/TimberbornTrainer/issues).

This is a personal hobby project developed primarily with OpenAI Codex from the author's requirements, design choices and testing feedback. It is not officially published or supported by OpenAI. Back up important saves.

**Provided AS IS, with no commitment to ongoing maintenance, bug fixes, technical support or future updates.** Existing license terms are unchanged.

---

## 简体中文说明

本版提供资源管理、建造与生产、海狸管理三个模块：地图选仓增减物品及科研点管理；一次性完成当前符合条件的工地；分别维持基础饥饿与口渴。保留原版操作条件、容量、预留及异常处理，不强行处理海狸已开始的进食／饮水动作。

**测试说明：** 英文版已通过自动化构建、回归及本地化测试，但尚未经过人工游戏内实机验收，仍可能存在界面或兼容性问题。如发现问题，欢迎通过 GitHub Issues 反馈。

中文版保留此前用户人工实机验收记录。两种语言各704项回归通过、0失败、0未执行，Release构建均0错误0警告；本地化专项20项、英文控件样式约束7项通过。UI替身与模拟接口测试不等于真实游戏验收。

中英文ZIP使用相同Mod ID，**只能安装一个，不得混装**。退出游戏、备份存档，解压其中一个包内的TimberbornTrainer文件夹到实际文档目录的Timberborn/Mods，并通过官方Mod管理器启用。卸载前关闭持续维生和全局开关，退出后移除本Mod文件夹；已发生的世界变化不会撤销。

已验证环境为Steam 1.1.2.8-ffc2de0-sw、Unity6000.5.11f1、Mono、Windows64位。其他版本／接口检查只警告，不保证实际调用成功，也不保证所有第三方Mod兼容。实际失败请查看反馈，不自动重试或回滚。

项目采用MIT许可证，须保留版权、许可及适用第三方声明；不分发游戏官方DLL或资产。欢迎Fork、Issue和PR，修改版由其维护者自行负责兼容。项目主要由OpenAI Codex根据作者需求和反馈开发，不是OpenAI官方产品，按现状提供，不承诺持续维护、修复、支持或更新。使用前请备份重要存档。
