# v0.4.0 changelog

[简体中文](CHANGELOG.zh-CN.md)

**Testing notice:** The English version has passed automated build, regression, and localization tests, but has not been manually tested in-game. Some UI or compatibility issues may remain. Please report any problems through GitHub Issues.

## Included

- Two independent English and Simplified Chinese installation packages, compiled from one shared source. They use the same Mod ID; install only one.
- Resource Management: native map-selected warehouse editing and a separate science-points page, with native operational safety checks.
- Building & Production: one-shot native completion of eligible existing construction sites; no production multiplier.
- Beaver Management: independent basic hunger/thirst maintenance.
- Floating-panel title dragging, left/right docking, auto-hide and edge-tab gestures. No cross-launch window-position persistence.
- Game/Unity version and interface diagnostics warn only, without disabling functions merely because of those diagnostics. Actual native calls can still fail; existing validation and exception handling remain.
- English repository homepage, Chinese README, bilingual usage guides, community Fork/contribution information and retained MIT/third-party notices.

Only the three modules above remain in the current UI. Removed standalone weather/disaster controls are not part of this version. Production multipliers remain cancelled; high-speed controls remain deferred.

## Validation status

| Scope | Evidence |
| --- | --- |
| Chinese version | Prior Chinese core functionality has user in-game validation. This preparation changes version labels, packaging and documentation, without substantive new UI layout changes. No redundant full manual retest was performed. |
| English version | Not independently tested in-game. Automated text, UI-substitute and package checks do not prove real layout/input behavior. |
| Both builds | Release: 0 errors, 0 warnings each. |
| Full regression | 704 passed, 0 failed, 0 unexecuted per language. |
| Localization/package checks | 20 passed; 759 method bodies match after ignoring string references. |

Verified baseline: Steam Timberborn **1.1.2.8-ffc2de0-sw**, Unity **6000.5.11f1**, Mono, Windows 64-bit. Other versions are unverified, not necessarily compatible. No universal third-party Mod compatibility is promised. Native item names follow the game's localization; IDs are unchanged.

## Downloads

- `TimberbornTrainer-v0.4.0-en-win64.zip`
- `TimberbornTrainer-v0.4.0-zh-CN-win64.zip`
- SHA256 values: [SHA256SUMS.txt](SHA256SUMS.txt)

Exit the game, back up important saves, then extract one ZIP's `TimberbornTrainer` folder into your actual Documents `Timberborn/Mods/` directory. Do not install both language variants. See [English guide](../Usage.en.md) or [中文说明](../使用说明.md).

## Community and maintenance

Forks, modifications, extensions, Issues and Pull Requests are welcome. Redistribution must comply with the actual MIT license and applicable notices. Each fork's maintainer is responsible for that modified version's maintenance and compatibility. No response, acceptance or maintenance is promised by the original author.

This is a personal hobby project developed primarily with OpenAI Codex. Automated and limited in-game testing cannot exclude unknown bugs or save risks. Back up important saves. This is not an official OpenAI product.

**Provided AS IS, with no commitment to ongoing maintenance, bug fixes, technical support or future updates.** Existing license terms are unchanged.
