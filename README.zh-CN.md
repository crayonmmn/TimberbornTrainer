# Timberborn Trainer（海狸修改工具）

[English](README.md) | 简体中文

**中英文为两个独立安装包，使用相同Mod ID，只能安装其中一个。历史v0.3.0版本另行保留。**

**版本：v0.4.0** · Windows 64位 · Steam Timberborn **1.1.2.8-ffc2de0-sw**

**测试说明：** 英文版已通过自动化构建、回归及本地化测试，但尚未经过人工游戏内实机验收，仍可能存在界面或兼容性问题。如发现问题，欢迎通过 GitHub Issues 反馈。

**Testing notice:** The English version has passed automated build, regression, and localization tests, but has not been manually tested in-game. Some UI or compatibility issues may remain. Please report any problems through GitHub Issues.

在游戏内使用中文面板管理仓库物品、科研点数、建造和基础生存需求。使用游戏官方 Mod 加载器，无需 BepInEx、Harmony、其他 Mod 或独立修改器。

## 下载与安装

[v0.4.0 Release](https://github.com/crayonmmn/TimberbornTrainer/releases/tag/v0.4.0) · [中文版ZIP](https://github.com/crayonmmn/TimberbornTrainer/releases/download/v0.4.0/TimberbornTrainer-v0.4.0-zh-CN-win64.zip) · [English ZIP](https://github.com/crayonmmn/TimberbornTrainer/releases/download/v0.4.0/TimberbornTrainer-v0.4.0-en-win64.zip) · [SHA256SUMS.txt](https://github.com/crayonmmn/TimberbornTrainer/releases/download/v0.4.0/SHA256SUMS.txt)。仅选一个语言包安装，不要混用DLL；GitHub自动生成的Source code压缩包不是安装包。

1. 退出游戏，备份重要存档。
2. 将 ZIP 中的 `TimberbornTrainer` 文件夹解压到**实际“文档”目录**下的 `Timberborn/Mods/`。文档目录可能由 OneDrive 重定向。
3. 确认层级为 `Timberborn/Mods/TimberbornTrainer/manifest.json`，同层有 `TimberbornTrainer.dll`。更新时移走旧版本，避免同一 Mod ID 重复安装。
4. 启动游戏，在官方 Mod 管理界面启用“海狸修改工具”。进入聚居地后点击“修改工具”，按需打开“全局修改器”。

详见完整的[中文使用说明](docs/使用说明.md)，包含操作、配置、卸载和常见问题。

## 悬浮面板

拖动标题栏移动窗口；在左右边缘附近松开鼠标即可停靠。鼠标离开且没有正在进行的操作时，约0.5秒后滑入边缘，只留下“修改工具”标签；移入标签自动展开。拖回中央后保持展开，再次停靠才恢复自动隐藏。

普通面板操作不拦截WASD；编辑文字时保留输入框焦点，点击标题或普通控件即可退出文字输入。隐藏和展开不重置功能设置。关闭面板后用“修改工具”按钮重新打开；本版不跨游戏启动保存窗口位置。

## 功能与边界

| 功能 | 实际行为 |
| --- | --- |
| 地图选仓增减物品 | 选中已完工、已设置存储物品的原生单物品仓库，自动识别物品并增减；空仓库也可操作。须手动暂停并全局启用，遵守容量及预留约束。 |
| 科研点管理 | “资源管理 → 科研点管理”读取、增加、减少或设置点数，操作后回读；整数0～十亿，不能减成负数。 |
| 立即完成所有建造 | 一次点击处理当前有效工地，不自动处理之后的新工地。整体暂停时也可用，保留建筑自身暂停和其他原版阻挡，不修改物流。 |
| 饥饿、口渴维持 | 独立维持基础需求正常满值并暂停相应自然扣减；关闭后恢复原更新状态。其他营养需求不锁定，已开始的进食/饮水动作不强制取消。 |
| JSON配置 | 保存、读取、导入、导出、恢复默认；与游戏存档分离。读取/导入/新场景均不会自动开启全局修改器或重放一次性命令。 |

完整验证范围仍为上述游戏版本及 Unity 6000.5.11f1。其他游戏或Unity版本会显示兼容性警告，仍允许尝试初始化和手动启用；未经验证不代表正式支持。接口检查发现缺失或签名不符时也只显示警告，不以检查结果禁用功能；用户仍可尝试操作，实际调用失败由原有异常处理反馈，不自动重试。Windows 64位平台限制保留，Linux、macOS未适配。`MinimumGameVersion` 保持1.1.2.8，不代表承诺兼容所有后续版本。

动态读取原生资源ID及仓库存储配置；使用相同原生组件的第三方建筑/资源具备兼容路径，**未对具体第三方 Mod 做全面实测**。施工库存、生产缓冲、地区交汇点和用途不明的库存不开放编辑；独立自定义库存不适配。聚居地汇总不是可直接写入的仓库，历史地区中心/多库存服务保留底层实现，但不在本版默认地图选仓界面提供入口。

仅保留资源管理、建造与生产、海狸管理三个模块。生产倍率已取消，高倍时间加速暂缓。页面“建造与生产”名称不表示提供生产倍率。原生物流边界、特殊工地、复杂预留和大型/多地区场景的覆盖有限。修改的物品、点数和建筑数据可能随正常保存写入存档；全局关闭、恢复默认及卸载都不是撤销操作，请先备份。

## v0.4.0验证说明

[中文更新日志](https://github.com/crayonmmn/TimberbornTrainer/blob/master/docs/release/CHANGELOG.zh-CN.md) · [English changelog](https://github.com/crayonmmn/TimberbornTrainer/blob/master/docs/release/CHANGELOG.en.md) · [SHA256](https://github.com/crayonmmn/TimberbornTrainer/blob/master/docs/release/SHA256SUMS.txt)

中文版此前已由用户完成核心功能实机测试；英文版尚未独立实机验证。两种语言各704项回归和Release构建通过，不代表英文实机通过。英文版以自动化验收发布，不宣称人工实机通过。

## 历史验证状态

用户于2026-11-09确认当前版本完成实机验收并授权正式发布；这是用户人工验收，不是自动化或全场景兼容保证。历史调查与阶段报告按当时状态保留，不代表当前版本仍停留于对应开发阶段。

发布前重新执行完整回归和Release构建，实际结果见[发布记录](https://github.com/crayonmmn/TimberbornTrainer/blob/master/docs/发布记录-v0.3.0.md)。自动化使用纯逻辑/原生接口及UI替身，不能替代游戏实际行为验证。

## 开发与构建

C# / netstandard2.1，官方加载器、Bindito、Unity UI Toolkit；本版UI由C#创建，无需Unity Editor或AssetBundle。游戏引用仅从本机合法安装目录读取，不随包分发。

```powershell
./build/Test.ps1 -Language zh-CN -GamePath '<Timberborn安装目录>'
./build/Build.ps1 -Language zh-CN -GamePath '<Timberborn安装目录>'
```

脚本使用已安装PowerShell中的Roslyn（可用 `-CompilerDirectory` 指定）。不自动安装依赖或启动游戏。中文版Release内容位于 `dist/zh-CN/TimberbornTrainer`，英文版位于 `dist/en/TimberbornTrainer`，包含DLL、manifest、README、使用说明、LICENSE和第三方声明/来源记录。标准SDK工程另见 `TimberbornTrainer.csproj`；SDK工作流并非本版主要验收构建方式。

`Core/` 核心与配置，`Modules/` 业务及原生适配，`UI/` 中文界面，`Tests/` 自动化，`build/` 构建脚本，`docs/` 使用说明及完整开发记录。请勿提交游戏DLL、存档、个人配置或构建缓存。

## 许可与贡献

自有代码采用 [MIT](LICENSE)。[第三方声明](THIRD_PARTY_NOTICES.md)及[来源记录](docs/第三方代码来源记录.md)说明参考范围；不包含游戏程序集、美术资源或其他 Mod。开发须遵循 [AGENTS.md](https://github.com/crayonmmn/TimberbornTrainer/blob/master/AGENTS.md) 的原生机制优先与许可证审查要求。

## AI开发声明与维护说明

本项目主要由 OpenAI Codex 根据作者提出的功能需求、设计方案和测试反馈完成代码编写、调试及构建。

本项目属于个人兴趣作品，已进行一定程度的自动化测试和实际游戏测试，但仍可能存在未知错误、兼容性问题或存档风险。建议使用前备份重要存档。

**本项目不承诺持续维护、Bug 修复、技术支持或后续版本更新。**

项目按现状（AS IS）提供。未来是否更新，完全取决于作者个人意愿。Issues即使开放，也不代表作者有义务回复或处理。本项目不是由OpenAI官方发布或支持。

### English Notice

This is a personal hobby project developed primarily with OpenAI Codex.

The project has undergone automated and limited in-game testing, but may contain undiscovered bugs or compatibility issues. Back up important save files before use.

**This project is provided AS IS, with no commitment to ongoing maintenance, bug fixes, technical support, or future updates.**

This project is not an official OpenAI release and is not supported by OpenAI.

## Fork、二次开发与贡献

欢迎Fork本项目，欢迎自行修改、扩展和增加功能，也欢迎提交Issue和Pull Request。作者不承诺回复、采纳、合并或持续维护。

重新分发须遵守实际[MIT许可证](LICENSE)，在软件副本或实质性部分中保留版权和许可声明，并遵守适用的第三方声明。第三方修改版本的维护与兼容性由修改者自行负责，原作者不承担该版本的维护承诺。本说明不增加或修改MIT法律条款。

Forks, independent modifications, extensions, Issues and Pull Requests are welcome. Redistribution must comply with the actual MIT license and applicable notices. Fork maintainers are responsible for their own modified versions; no response, acceptance or ongoing maintenance is promised by the original author.

## 卸载

先关闭饥饿/口渴维持及全局修改器，正常退出游戏，再备份并移除实际文档目录下的`Timberborn/Mods/TimberbornTrainer`文件夹。不要删除其他Mod或存档；此前已修改的世界数据不会撤销。详见[使用说明](docs/使用说明.md)。
