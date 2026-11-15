# Timberborn Trainer v0.4.0 — Release notes draft

This is local release preparation. The development branch is being prepared for push; no v0.4.0 tag or Release is authorized.

## Packages

- `TimberbornTrainer-v0.4.0-en-win64.zip` — English interface.
- `TimberbornTrainer-v0.4.0-zh-CN-win64.zip` — 简体中文界面。

Install **one package only**. Both use `TimberbornTrainer.Core`; do not install them together or mix DLLs. Change language by exiting the game and replacing the complete package.

## Features

Resource Management (warehouse items and science points), Building & Production (one-shot Finish all construction), and Beaver Management (independent basic hunger/thirst maintenance). Shared business code, native operational conditions and exception handling are unchanged.

Verified baseline: Timberborn Steam 1.1.2.8-ffc2de0-sw, Unity 6000.5.11f1, Mono, Windows 64-bit. Game/Unity/interface diagnostics warn only; actual native calls can still fail in unverified versions. No universal third-party Mod compatibility is promised.

## Installation

Exit the game, back up saves and extract the chosen ZIP's `TimberbornTrainer` folder into your actual Documents `Timberborn/Mods/` directory. Enable it through the game's official mod manager. See README and the included English/Chinese user guides. Do not install source-code archives as mods.

## Limitations and validation

No production multiplier, high-speed feature, weather control or natural-water feature is included. Existing water, inventory reservations and logistics are not overridden. Existing core functionality has user in-game reports, but English layout and both package interfaces still require in-game review. Real screenshots have not been supplied. Local automated results and package hashes are recorded separately; fill the final download URLs and SHA256 from the approved release artifact, not from a stale build.

## AI development and maintenance

This is a personal hobby project developed primarily with OpenAI Codex.

The project has undergone automated and limited in-game testing, but may contain undiscovered bugs or compatibility issues. Back up important save files before use.

**This project is provided AS IS, with no commitment to ongoing maintenance, bug fixes, technical support, or future updates.**

The author is not committing to future work. This is not an official OpenAI release or an OpenAI-supported product. Existing MIT license terms are unchanged. Open Issues do not imply an obligation to respond.

本项目主要由OpenAI Codex根据作者提出的功能需求、设计方案和测试反馈完成代码编写、调试及构建。个人兴趣作品，按现状提供；不承诺持续维护、Bug修复、技术支持或后续更新。使用前请备份重要存档。


Final bilingual changelogs: [English](release/CHANGELOG.en.md) / [简体中文](release/CHANGELOG.zh-CN.md). Checksums: [SHA256SUMS.txt](release/SHA256SUMS.txt).