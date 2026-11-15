# Timberborn Trainer

[简体中文](README.zh-CN.md) · [English guide](docs/Usage.en.md) · [中文使用说明](docs/使用说明.md)

**Prepared version: v0.4.0 — not released yet.**

A lightweight in-game control panel for **Resource Management**, **Building & Production**, and **Beaver Management**, using Timberborn's official mod loader.

**Verified environment:** Steam Timberborn **1.1.2.8-ffc2de0-sw**, Unity **6000.5.11f1**, Mono, Windows 64-bit.

> v0.4.0 bilingual packages are prepared on this development branch. No new bilingual release has been published. The existing public v0.3.0 assets predate this work.

## Downloads and installation

[Public releases](https://github.com/crayonmmn/TimberbornTrainer/releases) · [Latest published release](https://github.com/crayonmmn/TimberbornTrainer/releases/latest)

| Language | Prepared package filename | Availability |
| --- | --- | --- |
| English | `TimberbornTrainer-v0.4.0-en-win64.zip` | Prepared; Release pending |
| 简体中文 | `TimberbornTrainer-v0.4.0-zh-CN-win64.zip` | Prepared; Release pending |

**Install ONE language package only. Both use Mod ID `TimberbornTrainer.Core`; they must not be installed together.** They share the same code and behavior. There is no in-game language selector. To change language, exit the game and replace the installed package.

1. Exit the game and back up important saves.
2. Extract the ZIP's `TimberbornTrainer` folder into `Timberborn/Mods/` under your **actual Documents folder**. Documents may be redirected to OneDrive.
3. Confirm `Timberborn/Mods/TimberbornTrainer/manifest.json` and `TimberbornTrainer.dll` are at the same level. Avoid an extra nested folder and duplicate Mod IDs.
4. Enable the mod in the game's official mod manager, then enter a settlement and open **Trainer** (Chinese package: **修改工具**).
5. Enable the trainer manually when needed. Loading preferences or entering a scene never automatically enables it.

No BepInEx, Harmony, external trainer or additional mod is required. Do not download GitHub's source archive as an installation package.

## Features

| Module | Available features |
| --- | --- |
| **Resource Management** | Select a completed, configured single-good warehouse on the map. Read stock, add/remove items, including an empty warehouse with a configured good. Respects native capacity and reservations; manual pause and global enable are required. Science points have their own page: read, add, subtract and set. |
| **Building & Production** | **Finish all construction** calls the native completion mechanism once for current eligible sites. Works while the game is running or paused. Building-specific pause and native blockers are respected. New sites do not finish automatically. No production multiplier is included. |
| **Beaver Management** | Maintain basic hunger and thirst independently. Turning off restores the original need-update state. Separate nutrition needs are not locked; existing eating/drinking actions are not forcibly cancelled. |

Drag the title bar to move the panel. Dock left or right for delayed auto-hide; hover or click the edge tab to open it. Tab dragging uses a threshold of more than six UI units and moves only the tab. Dragging the full panel back to the center keeps it expanded. Window position is not persisted across game launches. Text inputs retain normal editing; ordinary panel interactions do not deliberately block WASD.

Item names come from the game's own localization, even when the package language differs from the game language. Native good IDs are unchanged.

## Compatibility and limitations

- The environment above is the verified baseline, not a promise of compatibility with future or older versions.
- Other game or Unity versions display a warning and may still be tried. Interface diagnostics also **warn only**, without disabling features. Actual calls can fail; existing safety checks and exception handling remain in effect. No automatic retry or rollback is performed.
- `MinimumGameVersion: 1.1.2.8` is not a promise that every later version works. Windows 64-bit remains the supported platform.
- Native registered goods and standard storage components provide a compatibility path for third-party mods, but specific mods have not all been tested. Independent custom inventory systems are unsupported.
- Construction inventories, production buffers, district crossings and unknown inventory roles are not editable targets. Settlement totals are aggregates, not a single editable warehouse.
- World changes can be saved by the game. Disabling the trainer or uninstalling does not undo resources, science points or completed buildings. Back up important saves.

The Chinese core functionality has user in-game validation. The English package has not been independently tested in-game; automated checks do not prove real UI behavior.

## Screenshots

Only real game screenshots will be used. No suitable screenshots are currently included. The [capture checklist](docs/screenshots/README.md) lists the images needed before publication; no generated or fabricated screenshots are used.

## Building

Run each language separately in a fresh PowerShell process:

```powershell
./build/Test.ps1 -GamePath '<Timberborn installation>' -Language en
./build/Build.ps1 -GamePath '<Timberborn installation>' -Language en
./build/Test.ps1 -GamePath '<Timberborn installation>' -Language zh-CN
./build/Build.ps1 -GamePath '<Timberborn installation>' -Language zh-CN
```

The script defaults to English. Language-specific outputs are under `dist/en/TimberbornTrainer` and `dist/zh-CN/TimberbornTrainer`. Both compile the same source; only explicitly catalogued string literal tokens are localized during compilation. No runtime language switching or language preference is added. Use these scripts for bilingual release preparation; the raw SDK project alone does not apply the text catalog.

Run `./build/Test-Localization.ps1 -GamePath '<Timberborn installation>'` after building both languages to check text coverage, package metadata and non-text IL equivalence.

The build uses existing Roslyn assemblies and the verified local game's references, without shipping game DLLs. Technical records remain under `docs/`.

[v0.4.0 English changelog](https://github.com/crayonmmn/TimberbornTrainer/blob/feature/natural-water-control/docs/release/CHANGELOG.en.md) · [中文更新日志](https://github.com/crayonmmn/TimberbornTrainer/blob/feature/natural-water-control/docs/release/CHANGELOG.zh-CN.md) · [SHA256](https://github.com/crayonmmn/TimberbornTrainer/blob/feature/natural-water-control/docs/release/SHA256SUMS.txt)

## License, AI development and maintenance

Project code is [MIT licensed](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.md) and the [source audit](docs/第三方代码来源记录.md). Game assemblies and assets are not included.

This project was developed primarily with OpenAI Codex from the author's feature requests, design choices and testing feedback.

This is a personal hobby project. It has undergone automated and limited in-game testing, but may contain undiscovered bugs, compatibility issues or save-file risks. **Back up important save files before use.** It is not published, endorsed or supported by OpenAI.

**This project is provided AS IS, with no commitment to ongoing maintenance, bug fixes, technical support, or future updates.** Any future update is entirely at the author's discretion. Open Issues do not imply an obligation to respond or resolve them. The existing license terms are unchanged.

## Forks and contributions

Forks, independent modifications, extensions and new features are welcome. You may open Issues and Pull Requests; there is no promise of a response, acceptance or maintenance.

Redistribution must comply with the actual [MIT license](LICENSE), including retaining its copyright and permission notice in copies or substantial portions, and any applicable third-party notices. Each fork's maintainer is responsible for that modified version's maintenance and compatibility; the original author does not undertake that work. This explanation adds no conditions to the license.

欢迎Fork、自行修改、扩展功能以及提交Issue和Pull Request。二次分发须遵守实际MIT许可证并保留版权与许可声明；第三方修改版由其修改者自行负责维护与兼容，原作者不承诺响应、合并或维护。

## Uninstalling

Turn off continuous need maintenance and the trainer, then exit the game. Back up and remove only the `TimberbornTrainer` folder from your actual Documents `Timberborn/Mods/` directory. Previously modified world data is not undone. See the [full guide](docs/Usage.en.md#uninstalling).
