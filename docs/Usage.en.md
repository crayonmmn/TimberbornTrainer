# Timberborn Trainer — English user guide

[简体中文](使用说明.md) · [Project overview](../README.md)

## Choose one language

The English and Simplified Chinese packages have identical features, safety conditions and Mod ID (`TimberbornTrainer.Core`). **Install only one.** There is no in-game language switch. To change language, exit the game, back up the installed trainer folder and replace it with the other package. Never mix their DLLs or install them side by side.

**Testing notice:** The English version has passed automated build, regression, and localization tests, but has not been manually tested in-game. Some UI or compatibility issues may remain. Please report any problems through GitHub Issues.

Get the English or Chinese v0.4.0 ZIP from [GitHub Release](https://github.com/crayonmmn/TimberbornTrainer/releases/tag/v0.4.0). Historical v0.3.0 remains separate.

## Requirements and installation

Verified environment: Steam Timberborn **1.1.2.8-ffc2de0-sw**, Unity **6000.5.11f1**, Mono, Windows 64-bit. Uses the game's official mod loader. No BepInEx, Harmony or extra mod is required.

1. Exit Timberborn and back up important saves.
2. Extract the chosen ZIP into `Timberborn/Mods/` in your actual Documents folder. Check OneDrive redirection if relevant.
3. The result must be `Timberborn/Mods/TimberbornTrainer/manifest.json`, with `TimberbornTrainer.dll` beside it. Do not place files in the game's `Managed` directory.
4. Remove duplicate installations of this Mod ID. Preserve backups outside `Mods`.
5. Enable the mod in the game's official mod manager and follow any game reload instructions.
6. Enter a settlement, open **Trainer**, and enable **Enable trainer** manually when you want to make changes. Loading preferences does not automatically enable it.

## Resource Management

### Warehouse items

Select a completed standard single-good warehouse on the map. The trainer reads its configured good automatically; it does not guess the intended good from old stock. An empty warehouse is usable if a good is configured in the game's warehouse panel.

1. Open **Resource Management → Inventory**.
2. Select the warehouse in the game.
3. Confirm its name, good ID, actual amount and native capacity.
4. Manually pause using the game's pause controls and enable the trainer.
5. Enter a positive integer and click **Add** or **Remove**.
6. Check the displayed before/after readback and the game's native inventory panel.

Native capacity and reservations are respected. An excessive request is rejected in full; quantities are not silently reduced. The panel reports how much can currently be added or removed. Removing reserved stock is not allowed. Unknown, construction, production-buffer and unsupported custom inventories are not editable warehouse targets. Settlement aggregate totals are not a single warehouse that can be written to.

If no good is configured, set one in the native warehouse UI first. If the target changes, is demolished or becomes invalid, select a valid target again. After a native exception, the inventory may already have changed: check the readback before trying again. There is no automatic retry or rollback.

Names come from the game's own good definitions and localization. The package language does not force the game's item-name language. IDs, including third-party native registered IDs, are preserved. Independently implemented third-party inventory systems are unsupported.

### Science points

Open **Resource Management → Science Points**. Read/refresh the current balance, then use the add, subtract or set controls. Inputs must be integers from 0 to 1,000,000,000; subtraction cannot produce a negative balance. Science points use the native science service, not warehouse items. Each command reads back the result. Restoring preferences does not undo points already changed.

## Building & Production

**Finish all construction** is a one-shot command for eligible construction sites that exist when clicked. It uses the native completion mechanism. It is not a persistent automatic-construction toggle: later sites follow normal rules until you click again.

The command works whether the overall game simulation is paused or running. Individual building pause, occupancy and native blockers remain effective. Sites still unfinished after the call are reported honestly; they are not repeatedly forced. Results distinguish completed, blocked/unfinished, invalid/skipped and exceptions/unconfirmed outcomes.

Carried materials, warehouses and hauling reservations are not modified directly. Existing hauling behavior is left to the game. Some observed in-transit materials were redirected to storage during user testing; this does not guarantee every special case. There is no production multiplier.

## Beaver Management

Enable **Maintain hunger** and/or **Maintain thirst** independently, with the trainer enabled. Maintenance applies to living, initialized beavers when simulation resumes. It restores the basic need to the normal native maximum and suspends its natural depletion. Turning it off restores the original update state held by this module.

Separate food-variety/nutrition needs are not locked. An already started eating, drinking or hauling action is not forcibly cancelled. Actual native errors may stop maintenance for the scene and are reported; compatibility diagnostics alone do not disable it.

## Floating panel and keyboard

Drag the title bar to move the panel. Release near the left or right edge to dock. After the pointer leaves and interactions finish, it hides after about half a second. Hover or click the remaining tab to open it.

A tab gesture becomes a drag only after moving more than six UI coordinate units. Only the tab moves; releasing after a drag should not open the full panel. After a drag, leave and re-enter the tab to trigger hover wake again. Full-panel title dragging is separate. Drag the panel away from the edges to keep it expanded.

No window position is saved across game launches. Ordinary controls do not intentionally intercept WASD camera input. Text boxes retain keyboard input while editing; click a normal control or the title to leave text editing. Check real input behavior in your game; automated UI substitutes are not a guarantee.

## Preferences

Save, load, import, export or restore preferences using the panel footer. Preferences are stored separately from game saves under the operating system's local application-data folder, in `TimberbornTrainer`. Hover the folder label to see the actual preferences and `Exchange` paths.

Import/export accepts a standalone `.json` filename, not a path or `config.json`. Importing, loading or entering a new settlement leaves the trainer disabled and never replays construction/science commands. Language is determined at build time, not saved in preferences. Disabling, restoring defaults and uninstalling do not undo previous world changes.

## Compatibility and warnings

The verified environment is listed above. Other game or Unity versions and interface-signature differences produce warnings only; they do not themselves disable features or the global switch. You decide whether to try an unverified environment. Existing operational safety checks still apply, and real calls can fail.

Older versions may truly lack APIs: the inspected 1.0.13.1 inventory has `Give`/`Take`, while this code targets `GiveExisting`/`TakeExisting` from 1.1.2.8. Warnings do not supply missing methods. No old-version adapter is included. Fundamental missing game assemblies/types can prevent the loader from loading a mod before its own diagnostics run.

`MinimumGameVersion` is not a promise that every later version works. No universal third-party Mod compatibility is claimed. Important changes may be saved by the game; back up saves before use.

## Uninstalling

Turn off hunger/thirst maintenance and the trainer, then exit the game normally. Back up the trainer folder and remove it from the actual `Mods` directory. Preferences may be retained separately if desired. Do not delete unrelated mods or saves. Already changed resources, science points and completed buildings remain part of the game state; use an earlier save if you need to undo them.

## Troubleshooting

- **Mod missing:** check the actual Documents folder, ZIP nesting, native mod-manager enablement and duplicate Mod IDs. Do not install both language packages.
- **Warehouse not recognized:** select a valid completed standard warehouse, not a construction inventory or production buffer.
- **Empty warehouse cannot be edited:** configure its stored good in the native panel first.
- **Addition rejected:** confirm global enable and native pause, then check allowed goods, unreserved capacity and reservations.
- **Where are science controls?** Resource Management → Science Points.
- **Beavers still eat/drink:** other nutrition needs and already started actions are not forcibly cancelled.
- **Interface warning:** trying is permitted, but the actual call may fail; inspect the result and do not blindly repeat it.
- **Need a log?** Unity's log is usually `%USERPROFILE%/AppData/LocalLow/Mechanistry/Timberborn/Player.log`. Share only relevant lines after removing personal paths and save names.

## License, AI development and maintenance

Project code is MIT licensed; see the included `LICENSE`, `THIRD_PARTY_NOTICES.md` and source-audit document. Game assemblies/assets are not distributed.

This project was developed primarily with OpenAI Codex from the author's feature requests, design choices and testing feedback. It is a personal hobby project, with automated and limited in-game testing, and may contain undiscovered bugs, compatibility issues or save-file risks. Back up important saves. It is not officially published or supported by OpenAI.

**Provided AS IS, with no commitment to ongoing maintenance, bug fixes, technical support or future updates.** Future updates depend entirely on the author's personal choice. Open Issues do not create an obligation to respond or resolve them. Existing license terms are unchanged.
