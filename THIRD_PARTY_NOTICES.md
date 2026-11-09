# 第三方参考与声明

截至 2026-11-09，本次库存模块审计未发现将下列三个项目的具体代码、二进制或资产复制/改编纳入 Trainer 的证据。下列条目是参考来源记录，不表示这些项目已成为运行依赖，也不将其版权归入本项目 MIT。

| 项目 | 核对版本 | 许可与范围 | 当前使用分类 |
| --- | --- | --- | --- |
| [datvm/TimberbornMods](https://github.com/datvm/TimberbornMods) / DistroStorage | a10fb8c97dec090df1cef28f9efa9dd2ba524f4f | [MIT](https://github.com/datvm/TimberbornMods/blob/a10fb8c97dec090df1cef28f9efa9dd2ba524f4f/LICENSE)，Copyright (c) 2024 Luke Vo | 用户提供的 API/思路参考；此次实际读取用于审计，未发现具体代码纳入 |
| [ihsoft/TimberbornMods](https://github.com/ihsoft/TimberbornMods) / DualDistrictStorage | 68c72748242b769330d80a3c2c0f15cc2d62d501 | 根 [Unlicense](https://github.com/ihsoft/TimberbornMods/blob/68c72748242b769330d80a3c2c0f15cc2d62d501/LICENSE)，目标文件另标 Public Domain，作者 IgorZ | 预留/转移技术参考候选；此次读取用于审计，未发现镜像等实现纳入 |
| [Jordan-Jarvis/timberborn-deep-frontier](https://github.com/Jordan-Jarvis/timberborn-deep-frontier) / PileVisualizationProbe.cs | d4183949d1f4d76603165b5750c0ff74360263db | 根 [LICENSE](https://github.com/Jordan-Jarvis/timberborn-deep-frontier/blob/d4183949d1f4d76603165b5750c0ff74360263db/LICENSE) 对指定 C# 目录为 MIT，Copyright (c) 2026 Jordan Jarvis；美术保留全部权利 | API 参考候选；此次读取用于审计，未发现探针代码或资产纳入 |

详细目标文件、依赖边界、对照方法及不确定项见 [第三方代码来源记录](docs/第三方代码来源记录.md)。由于未发现实际复用，本次不把未纳入的代码列成随包第三方组件，也不为这些参考项目新增虚假的代码归属声明。将来实际复用时必须补上完整适用许可证和版权声明，并检查发布包携带情况，不能只保留此处链接。

Timberborn、Unity 及游戏安装目录中程序集的权利由其各自权利人保留；本项目许可证不重新许可这些内容。本次是库存参考来源审计，不是所有平台/运行时依赖的完整许可证清单。
