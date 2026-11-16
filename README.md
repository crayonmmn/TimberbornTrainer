# Timberborn Trainer

[简体中文](README.zh-CN.md) · [English guide](docs/Usage.en.md) · [中文使用说明](docs/使用说明.md)

**Version: v0.5.0**

A lightweight floating trainer for **Resource Management**, **Building & Production**, and **Beaver Management**. One shared DLL now supports runtime English and Simplified Chinese through external JSON language files.

Download the [unified v0.5.0 package](https://github.com/crayonmmn/TimberbornTrainer/releases/download/v0.5.0/TimberbornTrainer-v0.5.0-win64.zip) from [GitHub Releases](https://github.com/crayonmmn/TimberbornTrainer/releases/latest). [SHA256 checksums](https://github.com/crayonmmn/TimberbornTrainer/releases/download/v0.5.0/SHA256SUMS.txt). Historical v0.4.0 separate-language packages remain available.

## Features

- **Resource Management:** select a completed supported warehouse on the map, read its configured item and add/remove stock, including an empty warehouse with an assigned item. Native capacity, reservations, identity checks, global enable and manual pause remain required. Unsupported targets remain read-only. Science has a separate page with read, add, subtract and set operations.
- **Building & Production:** **Finish all construction** processes eligible current sites once using the native API. It does not auto-complete future sites, override building pause/blockers or add production multipliers. Native logistics handles materials.
- **Beaver Management:** independently maintain basic hunger and thirst. Other nutrition needs are not locked; started eating/drinking actions are not forcibly cancelled. Turning maintenance off restores native updating.
- Movable floating window, left/right docking, delayed edge hiding, independent edge-tab gestures and existing keyboard behavior remain in place.

## Installation and language

Verified baseline: Steam Timberborn **1.1.2.8-ffc2de0-sw**, Unity **6000.5.11f1**, Mono, Windows64. Uses the official mod loader; **no BepInEx, Harmony, extra Mod, Unity Editor or development SDK is required**. Other game/Unity versions and interface checks warn rather than disabling features; actual calls can fail. Windows64 and original business safety conditions remain. Compatibility with every third-party Mod is not guaranteed.

For v0.5.0, use `TimberbornTrainer-v0.5.0-win64.zip`. Exit the game, back up saves and the old Mod folder, and replace the complete folder in your actual Documents directory (which may be redirected to OneDrive):

```text
Timberborn/Mods/TimberbornTrainer/
  TimberbornTrainer.dll
  manifest.json
  Languages/en-US.json
  Languages/zh-CN.json
  ...README, guides and license notices
```

Keep the **Languages** folder beside the DLL. Do not mix v0.4.0 language DLLs or install another copy of Mod ID `TimberbornTrainer.Core`. Do not copy files into the game's Managed folder. Enable the Mod in the official manager.

On first use, the game's current Simplified Chinese selects `zh-CN`; English or an unsupported/unreadable language selects `en-US`. Use **Interface language** in the panel to select **English** or **简体中文**. A manual choice is saved immediately to `language.json` under LocalApplicationData/TimberbornTrainer and takes priority on later starts. A save failure is reported and the session choice remains usable. It is separate from gameplay configuration and does not change the savegame.

Text updates in place, including cached results and errors; gameplay state, input values, window/tab position and native item names are preserved. Missing Chinese keys fall back to English, then to another available pack or safe fallback text. Missing/corrupt language files cannot block startup; restore the original two JSON files from the unified package. [Full guide](docs/Usage.en.md).

## Validation and limitations

**The user completed v0.5.0 in-game acceptance and confirmed normal operation on 2026-11-16.** This release also passed742 regression assertions per language context and24 localization/package checks, with0 build errors/warnings. User acceptance does not certify every resolution, save, game version or Mod combination. See [acceptance record](docs/release/v0.5.0/acceptance.md). The fixed, accepted ZIP retains preparation-stage guide wording; this repository and Release notice record the final acceptance status. No weather, natural-water or production-multiplier feature is provided.

## Build and tests

Use PowerShell7 with installed Roslyn and a legally installed matching game. No game DLL is distributed in this repository or package.

```powershell
./build/Build.ps1 -GamePath '<game root>'
./build/Test.ps1 -GamePath '<game root>' -Language en
./build/Test.ps1 -GamePath '<game root>' -Language zh-CN
./build/Test-Localization.ps1 -GamePath '<game root>'
```

Build.ps1 produces one DLL under `dist/unified/TimberbornTrainer` and copies external language resources. Build/Test support `-CompilerDirectory`; localization checks use Roslyn from `$PSHOME`. Tests regenerate their own output. The test language flag selects expectations; it does not produce different shipping DLLs.

## License, AI development and maintenance

Project-owned code is [MIT](LICENSE); retain copyright/license and applicable [third-party notices](THIRD_PARTY_NOTICES.md). Game/Unity/third-party rights are not relicensed by this project. [Source records](docs/第三方代码来源记录.md). The package contains no game DLLs or assets.

This project was developed primarily with OpenAI Codex from the author's requirements, designs and testing feedback. It is a personal hobby project, not an official OpenAI release or supported by OpenAI. Automated and limited in-game testing cannot rule out undiscovered bugs, compatibility issues or save risks. **Back up important saves.**

**Provided AS IS, with no commitment to ongoing maintenance, bug fixes, technical support or future updates.** Future updates are entirely at the author's discretion. Existing license terms remain unchanged.

Forks, modifications, extensions, [Issues](https://github.com/crayonmmn/TimberbornTrainer/issues) and Pull Requests are welcome. Redistributors must follow the actual MIT license and notices. Fork maintainers are responsible for their own compatibility and maintenance; an open Issue does not create an obligation to respond or fix it.

欢迎Fork、二次开发、Issue和Pull Request。重新分发须遵守实际许可证并保留声明；修改版由修改者自行负责维护和兼容，原作者不承诺回复、修复或持续更新。

## Uninstall

Turn off continuous maintenance and the global switch, exit the game, then back up/remove only this Mod folder. Earlier world changes are not undone. Language/gameplay preferences may be retained or removed separately from LocalApplicationData/TimberbornTrainer; they are not save files.