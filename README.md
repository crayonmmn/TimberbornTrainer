# Timberborn Trainer（海狸修改工具）

Windows 64 位 Steam Timberborn `1.1.2.8-ffc2de0-sw` 游戏内 Mod，版本 `0.3.0-dev`（建造开发检查点）。
使用官方加载器、Bindito、Unity UI Toolkit 和 C# / netstandard2.1，不使用 BepInEx 或 Harmony。

## 当前功能

- 科研点数：显示当前真实点数，设置目标、增加、减少；使用原生 ScienceService。
- 科研命令仅在全局启用且精确版本匹配时执行，操作后回读真实值。
- 输入为 0～1,000,000,000 的整数；减少不得超过余额，增加后的总量不得超过上限。
- 零增减与相同目标不调用写入接口；设置为零允许使用原生减点接口。
- 中文面板包含五个入口和明确状态。资源管理目前仅科研可用；建造页新增待实测瞬间施工，免材料尚未接入；库存、生产、需求、天气、灾害均未实现。
- JSON 配置支持保存、读取、导入、导出和恢复默认；SchemaVersion 保持为 1，与旧配置兼容。
- 导入、读取和恢复默认不会执行科研命令，也不会撤销已经获得的点数。

0.1.0 已通过基础加载与交互实测，0.2.0 已完成科研实测。当前 0.3.0-dev 构建与 111 项断言通过；
0.3.0-dev 未安装、未实机验证，免材料运行时尚未实现，四组合只做请求策略模拟，不能视为完整建造功能。参阅 `docs/建造模块续开发记录.md`。

## UI 与模块状态

面板从右下移至左侧，避开操作提示；按钮显式设置浅色文字、边框及正常、悬停、按下、禁用配色。
输入框有焦点边框；重要操作固定在底部，长路径藏在悬停提示中，导入导出默认折叠。
资源管理的状态为已启用、已禁用或运行异常，建造项显示实验状态，其他三项保持未实现。
资源管理的“已启用”仅指科研能力，页面始终明确库存尚未实现。

修改结果显示修改前、实际变化、修改后及是否成功。原生调用异常或回读不符时不报告成功，
也不自动重试；如果接口可能已生效但回读失败，请先检查游戏实际点数。
每半秒刷新一次可见面板中的点数；正常科研获取继续使用游戏机制。

## 构建

使用[官方支持的独立 DLL 工作流](https://github.com/mechanistry/timberborn-modding/wiki/Coding-basics)。
当前 UI 全部由 C# 创建，无需 Unity Editor 或 AssetBundle；
若后续引入 UXML、USS 或自定义资源，应使用 Unity 6000.5.11f1 和匹配的官方 Modding Tools。
引用本机合法安装的游戏 DLL，仅供编译，不随 Mod 分发。不使用 publicize 或私有字段写入。

已有 PowerShell 7 安装若带有 Roslyn：

```powershell
./build/Build.ps1 -GamePath '<Timberborn 安装目录>'
./build/Test.ps1 -GamePath '<Timberborn 安装目录>'
```

脚本从 `$PSHOME` 读取已有编译器，也可用 `-CompilerDirectory` 指定。
构建只读游戏目录，仅写项目 bin、dist 和 TestResults；不自动安装、启动或下载依赖。
Release 包在 `dist/TimberbornTrainer`，包含自有 DLL、manifest、README 与 LICENSE。
测试在 PowerShell 的 .NET 运行时执行，使用模拟科研接口，不是游戏实机测试。

另提供标准 SDK 工程，SDK 构建路径尚未在本机验证；推荐已安装的受支持 .NET 10 SDK：

```powershell
dotnet build ./TimberbornTrainer.csproj -c Release -p:GamePath='<Timberborn 安装目录>'
```

也可设置 TIMBERBORN_GAME_PATH。SDK 初次还原可能需要标准框架引用包，应单独取得依赖下载授权。
项目没有新增 NuGet 包依赖。

## 安装与测试

先自行退出游戏，再将 `dist/TimberbornTrainer` 复制到实际文档目录下的 `Timberborn/Mods`。
在官方 Mod 管理器中仅启用“海狸修改工具”，使用专用新局测试。
不要覆盖运行中的 DLL，不要放入 Managed 目录，不要覆盖游戏原始文件。
Windows 文档路径可能被 OneDrive 重定向。
运行时精确检查完整游戏版本、Unity 版本及 Windows 64 位进程；不兼容时禁止科研命令。

## 配置

目录为 `Environment.SpecialFolder.LocalApplicationData/TimberbornTrainer`，与存档分离。
主文件 config.json；导入导出使用 Exchange 子目录中的独立 JSON 文件名，不接受任意路径。
加载、读取和导入后全局开关保持关闭；需要用户手动启用。
恢复默认只改变设置，需点击保存持久化，不回滚科研点数、资源或建筑等一次性变化。
科研目标、增加和减少输入不写入配置，导入配置不会重放命令。
原有配置版本、范围校验、大小限制、未知/重复字段拒绝和原子替换保存机制保持不变。
配置中的未来天气和持续性开关仅为预留偏好，不意味着对应功能可用。

## 目录

Core/：加载、版本和配置；Modules/：科研命令与原生适配器、其他模块占位；
UI/：中文界面与控件样式；Patches/：空预留；Tests/：配置及科研命令测试；
build/：构建测试脚本；docs/：历次开发和验证记录。

## 开源

MIT 许可证已由项目发起者确认。游戏和第三方程序集不属于本项目许可范围。
不要提交游戏 DLL、个人路径、运行时配置、存档或测试产物；.gitignore 已排除常见产物。
尚未创建 GitHub 仓库或推送代码。

## 建造开发检查点与后续顺序

优先完成独立的瞬间建造/免材料及四组合实机测试，再做单仓库资源，最后海狸需求、生产和天气灾害。
瞬间施工只在全局和独立开关均启用时随模拟 Tick 工作；必须材料齐全，使用 IncreaseBuildTime，保留原生完工校验。
不调用会补材料的 FinishNow，不修改共享 BuildingSpec 或重新初始化现有库存。免材料开关禁用，旧配置偏好不生效。
本轮未安装该开发版，未操作游戏或创建测试存档；专用基准存档可行，但建立与保存须先确认。
