# Timberborn Trainer 海狸修改工具

[English](README.md) · [中文使用说明](docs/使用说明.md) · [English guide](docs/Usage.en.md)

**当前版本：v0.5.0**

轻量悬浮修改器，保留资源管理、建造与生产、海狸管理三个模块。现在使用一套DLL和外置JSON语言包，在游戏内切换English／简体中文。

下载[v0.5.0统一双语安装包](https://github.com/crayonmmn/TimberbornTrainer/releases/download/v0.5.0/TimberbornTrainer-v0.5.0-win64.zip)，见[最新Release](https://github.com/crayonmmn/TimberbornTrainer/releases/latest)和[SHA256清单](https://github.com/crayonmmn/TimberbornTrainer/releases/download/v0.5.0/SHA256SUMS.txt)。历史v0.4.0独立中英文包保留。

## 功能

- **资源管理：** 地图选中已完工的支持仓储，读取其已配置物品并增减数量；已指定物品的空仓库也可操作。保留原生容量、预留、身份、全局开关和手动暂停检查；不支持的库存只读。科研点有独立的读取、增加、减少、设置入口。
- **建造与生产：** “立即完成所有建造”仅处理点击时符合条件的工地，不自动处理新工地，不绕过建筑暂停／阻挡，不新增生产倍率。材料后续由原版物流处理。
- **海狸管理：** 分别维持基础饥饿与口渴，关闭后恢复原版更新。不锁定其他营养需求，不强制取消已经开始的进食饮水。
- 保留窗口拖动、左右吸附、靠边隐藏、独立边缘标签手势及原有键盘交互。

## 安装与语言

已验证环境基准：Windows64、Steam Timberborn **1.1.2.8-ffc2de0-sw**、Unity **6000.5.11f1**、Mono。使用游戏官方Mod加载器，无需BepInEx、Harmony、其他Mod、Unity Editor或开发SDK。其他游戏／Unity版本及接口诊断只警告，不保证实际功能可用；Windows64平台与原有业务安全检查保留。不承诺兼容所有第三方Mod。

v0.5.0统一包为 `TimberbornTrainer-v0.5.0-win64.zip`。退出游戏、备份重要存档和旧Mod目录后，将完整文件夹放到实际“文档”目录的 `Timberborn/Mods/`（注意OneDrive重定向）：

```text
TimberbornTrainer/
  TimberbornTrainer.dll
  manifest.json
  Languages/en-US.json
  Languages/zh-CN.json
  ...README、使用说明与许可证
```

Languages必须保留在DLL旁。Mod ID仍为 `TimberbornTrainer.Core`，不能同时保留旧版中英文DLL或多份同ID安装。不要放入游戏Managed目录。通过官方管理器启用Mod。

首次启动按游戏当前语言选择：简体中文用zh-CN，英文或不支持／无法识别的语言用en-US。在面板的“界面语言”中手动选择后，立即保存到本地应用数据目录 `TimberbornTrainer/language.json`，之后启动优先使用手动选择。保存失败会提示，本次会话仍可切换；语言偏好不写入游戏存档，也不改变业务配置格式。

切换只刷新文字，包括已显示的结果和错误，不重置业务状态、输入框、窗口或标签位置。游戏物品名称继续使用原生本地化。缺失中文键先回退英文；英文也不可用时使用其他可用资源或安全提示。文件缺失／损坏不会阻止启动，请从完整安装包恢复两个JSON。详见[使用说明](docs/使用说明.md)。

## 验证与限制

**用户已于2026-11-16完成v0.5.0人工实机验收并确认功能正常。** 发布前中英文回归上下文各742通过，本地化／包结构24项通过，构建0错误0警告；不代表所有分辨率、存档、游戏版本或Mod组合均已验证。见[验收记录](docs/release/v0.5.0/acceptance.md)。为保持已验收哈希，安装ZIP原样保留，其中的准备期说明由本仓库及Release最终状态说明补充。没有天气、自然水源或生产倍率功能。

## 构建

PowerShell7及Roslyn、合法安装的匹配游戏为开发依赖，玩家无需安装开发工具。源码及命令见[英文README](README.md#build-and-tests)。统一输出在 `dist/unified/TimberbornTrainer`，测试的Language参数只控制验收上下文，不再生成两种发行DLL。

## 许可证、AI开发与维护

自有代码采用[MIT](LICENSE)，保留版权、许可与[第三方声明](THIRD_PARTY_NOTICES.md)。游戏、Unity、他人代码／资产不由本项目重新许可；[来源记录](docs/第三方代码来源记录.md)。不打包游戏官方DLL或资产。

本项目主要由OpenAI Codex根据作者需求、设计和测试反馈完成开发，属于个人兴趣作品，不是OpenAI官方发布或支持。虽有自动化及有限实机测试，仍可能有未知错误、兼容或存档风险，**使用前请备份重要存档**。

**按现状（AS IS）提供，不承诺持续维护、Bug修复、技术支持或后续更新。** 未来是否更新取决于作者意愿，不改变许可证法律条款。

欢迎Fork、二次开发、扩展功能、[Issue](https://github.com/crayonmmn/TimberbornTrainer/issues)和Pull Request。重新分发须遵守实际许可证并保留声明；修改版由修改者自行负责维护与兼容。作者不承诺回复、合并或修复。

Forks and contributions are welcome. Redistribute under the actual MIT license and notices. Fork maintainers own their maintenance and compatibility. This project is provided AS IS without ongoing support commitments.

## 卸载

关闭持续维生及全局开关，退出游戏后备份／移除本Mod文件夹。此前对游戏世界的修改不会撤销；本地应用数据目录的语言和业务偏好可单独保留或移除，不属于存档。