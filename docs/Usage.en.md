# Timberborn Trainer v0.6.0 — user guide

[简体中文](使用说明.md) · [Project source](https://github.com/crayonmmn/TimberbornTrainer)

The user reported successful in-game testing of the manual-language/UI fixes used in v0.6.0. Automated validation is recorded separately in the [acceptance record](release/v0.6.0/acceptance.md). This does not guarantee every game/Mod combination. Back up important saves.

## Install

**Local manual-install directory (Windows)**

- Default: `%USERPROFILE%\Documents\Timberborn\Mods\TimberbornTrainer\`
- If Documents is redirected to OneDrive, use its actual location, for example `%OneDrive%\Documents\Timberborn\Mods\TimberbornTrainer\`. The folder name and drive may differ on your PC.
- To find the correct location, open **Documents** in File Explorer and then `Timberborn\Mods`. The game's Mod Manager also provides **Browse local mods**. Use that folder rather than guessing a drive or username.

Extract the ZIP's `TimberbornTrainer` folder into `Mods`, so `manifest.json` is directly at `Mods\TimberbornTrainer\manifest.json`. Avoid `Mods\TimberbornTrainer\TimberbornTrainer\` nesting. This is the **user Mod folder**, not the Steam game installation folder or `Timberborn_Data\Managed`. If using the Workshop copy, do not also keep a manual copy with the same Mod ID.

Baseline: Steam Timberborn 1.1.2.8-ffc2de0-sw, Unity6000.5.11f1, Mono, Windows64. Use the game's official Mod manager. No BepInEx, Harmony, additional Mod, Unity Editor or development SDK is needed.

Exit the game. Back up the old Mod folder and saves. Extract `TimberbornTrainer-v0.6.0-win64.zip` into `Timberborn/Mods/` under your actual Documents directory, including OneDrive redirection. The result must contain `TimberbornTrainer/manifest.json`, its DLL and `Languages/en-US.json` plus `Languages/zh-CN.json`. Replace the complete old folder; do not mix the v0.4.0 English/Chinese DLLs. Keep backups outside Mods. Only one installation with Mod ID `TimberbornTrainer.Core` may be enabled. Do not copy files into the game's Managed directory.

Enable the mod in the official manager and follow the game's restart instructions. Enter a settlement and open Trainer. Global changes are off on load; enable them manually when needed. GitHub Source code archives are not installation packages. Download the unified package from the [v0.5.0 Release](https://github.com/crayonmmn/TimberbornTrainer/releases/tag/v0.6.0). Historical v0.4.0 remains available.

## Language settings

v0.6.0 uses manual language selection only.

1. Without a valid saved preference, the interface starts in English. Missing/corrupt JSON or an invalid language value also defaults to English. The game's language is not read.
2. Use **Language / 语言** to select English or 简体中文. There is no Auto option. Text changes in place, including previous result/error messages. Native item/building names and IDs still use the game's localization.
3. Manual selection is saved immediately and restored on later launches. Existing `en-US` and `zh-CN` preferences remain valid. It is stored separately at `%LOCALAPPDATA%/TimberbornTrainer/language.json`; no gameplay config format or savegame is changed. If saving fails, the session choice still works and a notice is shown. Removing only this preference file while the game is closed returns to the English default.
4. Both JSON files use UTF-8 and identical text keys. A missing Chinese key falls back to English; if English is unavailable, the other available resource or `[Text unavailable: key]` is used. Corrupt, duplicate-key, invalid-UTF8, missing or oversized packs cannot prevent startup. Restore both files from the unified ZIP and restart to reload them. There is no filesystem polling or hot-edit reload.
5. Switching languages does not re-enable the trainer, change pause/speed, repeat a command, clear input fields or move/rebuild the window. Language feedback is separate from the last business result.

**Enable global changes** permits inventory, research and construction commands and selected hunger/thirst maintenance. Reading remains available while off; inventory editing also requires pausing the game. The label and explanation refresh in the selected language.

## Resource Management

Select **Item inventory** and choose a completed supported warehouse directly on the map. The panel reads the configured item, current amount, capacity and unreserved add/remove allowance. An empty warehouse works if its stored item has been assigned in the native UI. No assigned item, ambiguous inventory, invalid/demolished entity or unsupported target gives a reason instead of a write target.

Enable the global switch and manually pause using the game's native control. Enter a positive whole number, then Add or Remove. Requests exceeding unreserved capacity/stock are rejected as a whole and show allowed quantities; reservations are not cleared. After a call, read-back and feedback are shown. An exception may occur after a change; an unconfirmed result is not an invitation to retry automatically.

Third-party goods registered through the native goods service can be listed, but custom inventory behavior is not universally supported. Construction inventories on roads/houses, production buffers and unknown types are not opened for editing merely because an Inventory component exists. Settlement totals are statistics, not a single editable virtual warehouse. Existing district-center output support remains subject to its native identity and ownership checks; the quick map page is for supported warehouses.

Use **Science points** for the separate native science service. Refresh the count, Add10, Subtract5 or Set a target, then compare with the native game counter. Inputs must be whole numbers from0 to1,000,000,000; subtraction cannot exceed the balance. It is not a warehouse item. Commands require global enable, validate before calling, and read back the result.

## Building & Production

Click **Finish all construction** once to process eligible current sites through native FinishNow. It works as a command, not a persistent toggle; new sites remain normal. Native individual building pause, occupancy and validators are respected. Statistics distinguish confirmed completion, blocked/unfinished, skipped and exceptional/unconfirmed sites. Finishing can delete special entities; no automatic retry or forced state conversion is added.

No production multiplier or extra material-return/transport patch is provided. Native logistics decides what happens to carried materials. Global game pause is not a logistics lock, and is not a requirement for this construction command.

## Beaver Management

Enable **Maintain hunger** and/or **Maintain thirst** independently, then enable the global switch. Changes apply as simulation resumes. The native normal maximum is maintained for living initialized beavers, and only the corresponding base need's updating is paused. Other nutrition needs are unchanged. Already-started meals/drinks or transport may finish natively.

Turning a need or the global switch off restores its previous native update state. New/removed beavers and unloading are handled through the native population/lifecycle. A runtime error stops that scene's maintenance and reports it rather than blindly continuing.

## Window and configuration

Drag the title bar, dock left/right, and let the panel hide after leaving it. Hover/click the edge tab to reveal; drag beyond6 UI units to move only the tab without opening. Drag the full panel away from the edge to keep it expanded. No position is persisted across launches. Ordinary panel controls retain existing WASD routing; text inputs accept typing. The user reports normal operation of the current fixes; additional resolutions, fonts and Mod combinations are not exhaustively verified.

Save/load/import/export gameplay preferences using the existing controls. Loading/importing does not automatically enable global changes or execute commands. Language preference is separate. Invalid input, unsupported targets, failed reads and compatibility diagnostics are shown; do not assume a warning proves an unverified version works.

## Compatibility, uninstall and FAQ

Version/Unity/interface checks warn only; Windows64 and original business conditions remain. Static native interfaces can still fail before or during commands on an unverified game version. Compatibility with all other Mods is not promised.

If the mod is missing, check official enablement, folder nesting and duplicate Mod IDs. If a warehouse is not editable, check completion, configured item, native pause, global enable and the displayed reason. If hunger remains fulfilled but nutrition changes, that is expected: base hunger is not every food need. If language texts are unavailable, restore both JSON files; do not replace game DLLs.

To uninstall, turn off continuous maintenance/global changes, exit the game and remove only this Mod folder. Previously granted items/science or completed buildings are not undone. Local preferences may be retained or removed separately.

Project-owned code is MIT; retain LICENSE and applicable third-party notices. Game/Unity rights remain separate; no official game DLL/assets are bundled. This hobby project is developed primarily with OpenAI Codex, not officially supported by OpenAI. **Provided AS IS, with no commitment to ongoing maintenance, bug fixes, technical support or updates.** Forks/Issues/PRs are welcome without a response guarantee.
