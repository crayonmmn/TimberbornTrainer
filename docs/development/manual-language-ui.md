# 纯手动语言与标签修复（未发布）

日期：2026-11-16。分支：`feature/manual-language-ui`。基线：正式 v0.5.0 / `87c8c92dc280ac72f478c1011f68146ace24c52b`。

## 改动范围

完整修改文件清单（相对项目根，共16个；不含原有未跟踪审计）：

| 类型 | 文件 |
|---|---|
| 初始化与语言服务 | Core/RuntimeText.cs、Core/TrainerMod.cs |
| UI | UI/LocalizedControls.cs、UI/MainPanel.cs |
| 文案 | Languages/en-US.json、Languages/zh-CN.json |
| 测试 | Tests/RuntimeLocalizationTests.cs、Tests/RuntimeUiLocalizationTests.cs、Tests/UiTestDoubles.cs |
| 验证流程 | build/Test.ps1、build/Test-Localization.ps1 |
| 说明 | README.md、README.zh-CN.md、docs/Usage.en.md、docs/使用说明.md |
| 本记录（新增） | docs/development/manual-language-ui.md |

- `RuntimeText.Configure` 仅接收语言目录和偏好路径。删除游戏语言标准化、自动选择方法及第三参数；无有效偏好一律选择 en-US。
- `TrainerSession` 移除 ILocalizationService 注入及 CurrentLanguage 读取，使用唯一无参构造函数。游戏原生物品名称本地化继续保留，不属于自动选择修改器语言。
- `%LOCALAPPDATA%/TimberbornTrainer/language.json` 结构仍为 `{"Language":"en-US"}` 或 `{"Language":"zh-CN"}`，旧偏好原样读取，不迁移、不覆写。缺失、损坏、无效值回退英文，不自动创建偏好文件；手选时才写入。
- 两份 JSON 仍共用相同键，现各478键；复用原477键并新增全局开关帮助一键。语言标签固定为 `Language / 语言`，只有 English / 简体中文，无 Auto 选项。
- 全局开关名分别为 `Enable global changes` / `启用全局修改`，新增说明：控制命令和已选择的维生，关闭时仍可读取，库存编辑还需暂停。不改变开关原有判断。

## 标签丢失根因及修复

原 LocalizedToggle/Dropdown 使用空标签构造。当前游戏 UnityEngine.UIElementsModule 的 `BaseField<T>.set_label` 在空标签时移除 Label，并添加无标签 USS 变体；恢复标签需由该属性重新插入节点并清除变体。旧本地化仅赋 `labelElement.text`，跳过了原生节点与样式更新，所以资源里虽有文案，画面仍可能缺失。

LocalizedUI 改用原生 `field.label` / `toggle.label` / `dropdown.label`，保留现有弱引用刷新机制，不重建控件。测试替身补充相同的标签挂载行为，并断言语言/全局开关标签的 parent，避免再次只测字符串而漏掉层级问题。语言标签设置可见文字颜色、140宽度下限和换行；全局说明自动换行。

## UI 对照（自动化验证，非游戏截图）

| 控件 | English | 简体中文 |
|---|---|---|
| 语言标签 | Language / 语言 | Language / 语言 |
| 选项 | English、简体中文 | English、简体中文 |
| 全局开关 | Enable global changes | 启用全局修改 |
| 帮助 | Enables inventory, research and construction commands, plus selected hunger/thirst maintenance. Reading remains available while off. Inventory editing also requires pausing the game. | 开启后允许库存、科研和建造命令，以及已选择的饥饿／口渴维持。关闭时仍可读取信息；修改物品还需暂停游戏。 |

## 实际执行验证

- Release：0错误、0警告，使用当前1.1.2.8游戏引用；未安装。
- 完整回归：英文上下文755通过、0失败、0未执行；中文上下文755通过、0失败、0未执行（相同套件分别执行，不是1510种独立场景）。
- 其中语言核心32项、实际UI适配器＋替身19项；原窗口88项、标签56项均通过。
- 本地化/包/构造函数静态检查27项通过。无自动语言读取残留，只有测试中的禁止模式检查仍提及旧标识符。
- `git diff --check` 通过。Modules、Patches、窗口状态机、边缘标签手势、TrainerState及manifest均无差异；未新增依赖。
- 正式 ZIP SHA256保持 `43E827F25704E41E58A6D5E9DB3AE79B0B245126B0A282CD363B6A30204FDEDF`；v0.5.0标签未改。没有修改GitHub、Workshop、存档、真实用户语言配置或历史备份。

## 待实机与版本建议

尚未启动游戏。需检查真实UI标签、两种语言下的换行/下拉框、不同分辨率，以及旧中文偏好启动、手选英文后重启、连续切换时窗口/输入/开关状态。替身测试不证明Unity实际渲染或WASD行为。

建议验收后作为 **v0.5.1** 小范围修复发布；本轮不改版本常量、标签或正式包，开发构建元数据仍为0.5.0，不能误当成原正式DLL部署。没有制作新ZIP或上传。所有改动未提交，原有未跟踪 docs/audit/ 保留。
