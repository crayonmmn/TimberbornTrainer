# v0.5.0 runtime-language development and acceptance record

2026-11-16. Feature branch: feature/runtime-localization-v0.5.0. Base master/v0.4.0: c03bb7fee29cb9cc725397c42ea69fd55903a4b0. The pre-existing untracked v0.4.0 audit report is retained. No Git commit, tag, push, deployment, game launch or publication is performed.

## Implementation

- One shipping DLL, external Languages/en-US.json and zh-CN.json, 477 matching stable keys. Reused 465 existing messages plus9 formatting entries unchanged; three new entries are language controls/feedback. Old compile-time UI/Localization/strings.en.json is replaced, not maintained as a second catalog.
- Source literal calls use TextToken.Key IDs; messages remain language-neutral when concatenated or cached. RuntimeText.Render replaces only explicit tokens, never searches/replaces native item names or IDs. RuntimeText.Format provides guarded dynamic formatting. Minimal English diagnostics remain available if both packs are unusable; the full catalogs are not embedded in DLL resources or literals.
- Localized labels/buttons/field labels/tooltips/choices keep their original raw messages in ConditionalWeakTable bindings. LanguageChanged refreshes text on existing controls. Weak references and explicit UI release avoid retaining scene UI. No per-frame language polling, new gameplay Tick, patch, window reconstruction or module reinitialization.
- Manual preference is separate language.json in LocalApplicationData/TimberbornTrainer, saved atomically. Invalid/corrupt preference reverts to game-language selection. Write failure preserves session selection and reports it. Business Config schema and import/export semantics are unchanged.
- Startup reads IModEnvironment.ModPath and game-scoped ILocalizationService.CurrentLanguage. Current 1.1.2.8 metadata confirms both public properties return string. TrainerSession has exactly one parameterful constructor. First-use zhCN/zh-CN/zhHans selects Chinese; enUS and other/unknown identifiers select English. A valid manual preference wins. Actual game DI/new-map loading remains to be tested.
- Language feedback has its own label, preserving prior business results. Window/tab state machines and input handlers are unchanged; only the tab label type and text-binding cleanup are added to FloatingWindow.

## Business protection

All20 Modules files were compared to the pre-edit snapshot using Roslyn tokens after reversing text-key replacements, the one constant-to-readonly presentation field and log rendering wrappers: token streams matched. No inventory/science/construction/need algorithm or safety check changed. FloatingWindowState.cs and EdgeTabGesture.cs are byte-preserved. Former weather/natural-water features are not restored. No new runtime third-party dependency; the existing game Localization assembly is now explicitly referenced for game-language identification.

## Automated validation

Final results are recorded after execution below. Original704 business/window assertions remain. The legacy regression harness normalizes displayed text for substring comparisons and translates expected wording only; business numeric/state assertions are not removed or relaxed. Standalone plan tests still reference neither game/Unity/Bindito nor Json.NET directly. New tests cover real RuntimeText and actual MainPanel/event adapters with UI/native-shaped substitutes.

Core language tests cover initial Chinese/English/unsupported/unknown detection; manual choices and restart precedence; corrupt/invalid preferences; missing single key; missing English, all resources missing, bad JSON; safe formatting; native-name preservation; cached results; no business-state events and one notification per actual language change.

UI tests exercise the actual MainPanel language entry, same control/window identity, unchanged global state/science count/native subscriptions, unchanged coordinates/tab position, an actual science command and its cached result/error switching, repeated switching, text input preservation and unload.

An early new assertion helper was accidentally rewritten into itself by the test-only display transformation, causing a test-process stack overflow. The helper is now excluded from that transformation. Another initial UI fixture queried the tab beneath the panel root although the real window reparents it as a sibling; the fixture now inspects the actual window tab. These failures were not recorded as successful runs. A repeated-build copy experiment produced nested Languages output; the build now copies exactly two files and repeat-build/package checks pass. No failing test was skipped.

## Pending in-game acceptance

Neither language of the v0.5.0 runtime implementation is manually verified. Test startup/new-map Bindito resolution, native game-language identifiers, manual persistence across exits, missing/corrupt packs, saved results/errors, long English wrapping, dropdown popup sizing, true fonts/DPI, WASD/focus, title/tab drag/docking/hide and business regression in a dedicated backed-up save. v0.4.0 Chinese manual history is preserved as historical evidence only.

## Packaging and publication boundary

Unified package: TimberbornTrainer-v0.5.0-win64.zip, same Mod ID TimberbornTrainer.Core, one0.5.0.0 DLL and two external JSON files,11 runtime/document files. No official game DLL/assets, framework binaries, test artifacts or credentials. See SHA256SUMS.txt in this directory for the final ZIP hash. Local drafts cover bilingual Release, About/Topics and Workshop; no remote page is changed. Workshop structural preparation is not proof of acceptance by Steam's uploader.

v0.4.0 ZIPs/tag/Release and complete/lite backups remain unchanged. Final delivery is uncommitted on the feature branch; master stays at the released commit. Wait for user acceptance before publication.
## Final executed results (2026-11-16)

- Unified Release: 0 errors, 0 warnings.
- English regression context: 742 passed, 0 failed, 0 unexecuted (original704 +27 runtime core +11 actual UI-adapter assertions).
- Chinese regression context: 742 passed, 0 failed, 0 unexecuted, same breakdown.
- Unified localization/package static checks: 24 passed,477 matching external keys,11 package files and a single Bindito session constructor.
- These are final successful reruns after the recorded intermediate test/build failures were fixed. No tests were skipped. No game or clean native scene was launched.

Unified ZIP SHA256 / 统一包校验：

43E827F25704E41E58A6D5E9DB3AE79B0B245126B0A282CD363B6A30204FDEDF

ZIP bytes: 100598; packaged files: 11.

Final protection check: v0.4.0 tag object remains 40567e342346724dfc11c847a70367c6891009f2; master remains c03bb7fee29cb9cc725397c42ea69fd55903a4b0. Original v0.4.0 en/zh ZIPs and full/lite archive SHA256 matched their recorded values. Read-only remote verification still reports latest Release v0.4.0. New DLL has36 game/framework references (including native Timberborn.Localization),0 embedded resources, no Harmony/BepInEx/Roslyn runtime dependency. Package extraction and all11 file hashes match Release. No game launch, install, commit, remote write or Workshop upload occurred.

## Superseding user acceptance and publication authorization — 2026-11-16

The user reports v0.5.0 in-game acceptance completed and normal operation, and explicitly authorizes GitHub publication. This supersedes the earlier pending status above, which records the development timeline. No individual test scenario or additional game version is inferred from the report. The formal package remains the exact accepted100598-byte ZIP (SHA25643E827F25704E41E58A6D5E9DB3AE79B0B245126B0A282CD363B6A30204FDEDF). Its bundled preparation-stage documentation is deliberately not rewritten; online README/guides and Release state the final status. Publication task re-ran both742-assertion suites and24 static checks successfully, and verified all11 package files against the rebuild before documentation finalization. No runtime/source algorithm changes were made during publication.
