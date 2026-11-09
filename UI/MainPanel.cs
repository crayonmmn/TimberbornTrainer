using System;
using Timberborn.SingletonSystem;
using Timberborn.UILayoutSystem;
using TimberbornTrainer.Core;
using TimberbornTrainer.Modules;
using TimberbornTrainer.UI.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimberbornTrainer.UI {
  public sealed class MainPanel : ILoadableSingleton, IUnloadableSingleton, IUpdatableSingleton {
    private readonly TrainerSession _session;
    private readonly UILayout _layout;
    private readonly ResourceModule _resources;
    private readonly NativeInventoryCatalog _inventories;
    private readonly NativeInventoryEditor _editor;
    private readonly BuildingModule _buildings;
    private readonly BeaverModule _beavers;
    private readonly WeatherModule _weather;
    private VisualElement _weatherArea;
    private Label _weatherStatus, _weatherResult;
    private ActionButton _weatherSet;
    private InventoryPanel _inventory;
    private VisualElement _beaverArea;
    private Toggle _hunger, _thirst;
    private Label _needStatus;
    private VisualElement _root, _panel, _resourceArea, _resourceTabs, _scienceArea, _buildingArea;
    private ScrollView _body;
    private bool _sciencePage;
    private Label _status, _moduleTitle, _moduleDescription, _currentScience, _constructionStats;
    private readonly Label[] _moduleStates = new Label[5];
    private Toggle _enabled;
    private TextField _exchangeName, _target, _add, _subtract;
    private ActionButton _setButton, _addButton, _subtractButton, _finishConstruction;
    private float _nextRefresh;
    private int _selected;
    private bool _loaded;

    public MainPanel(TrainerSession session, UILayout layout, ResourceModule resources, BuildingModule buildings, BeaverModule beavers, NativeInventoryCatalog inventories, NativeInventoryEditor editor, WeatherModule weather) {
      _session = session; _layout = layout; _resources = resources; _buildings = buildings;
      _beavers = beavers;
      _inventories = inventories;
      _editor = editor;
      _weather = weather;
    }

    public void Load() {
      _root = new VisualElement { name = "TimberbornTrainer" };
      // Stay away from the bottom-right automation toast and the game's top/left HUD.
      _root.style.position = Position.Absolute;
      _root.style.left = 260;
      _root.style.top = 105;
      _root.Add(new ActionButton("修改工具", () => {
        _panel.style.display = _panel.style.display.value == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
        RefreshScience();
        if (_panel.style.display.value != DisplayStyle.None && _selected == 0 && !_sciencePage) _inventory.Open();
      }));
      _panel = new VisualElement { name = "TrainerPanel" };
      _panel.style.width = 600;
      _panel.style.maxHeight = 750;
      _panel.style.color = ControlStyles.Light;
      _panel.style.backgroundColor = new Color(.08f, .13f, .12f, .99f);
      _panel.style.paddingLeft = _panel.style.paddingRight = 12;
      _panel.style.paddingTop = _panel.style.paddingBottom = 10;
      ControlStyles.Border(_panel, ControlStyles.Muted);
      _root.Add(_panel);
      var title = ControlStyles.Text(Chinese.Title);
      title.style.fontSize = 21;
      _panel.Add(title);
      _panel.Add(ControlStyles.Text("游戏：" + _session.State.GameVersion + "　模组：" + TrainerState.ModVersion));
      _panel.Add(ControlStyles.Text(_session.State.CompatibilityMessage));
      _enabled = new Toggle("全局修改器") { name = "TrainerEnabled" };
      _enabled.style.color = ControlStyles.Light;
      _enabled.labelElement.style.color = ControlStyles.Light;
      _enabled.style.minHeight = 30;
      _enabled.SetEnabled(_session.State.Compatible);
      _enabled.RegisterValueChangedCallback(e => Run(() => _session.State.SetEnabled(e.newValue),
        e.newValue ? "全局已启用；实验功能仅在独立测试局验证。" : "全局已关闭，修改命令和持续性修改已停止。"));
      _panel.Add(_enabled);
      var navigation = ControlStyles.Row();
      for (int i = 0; i < Chinese.Modules.Length; ++i) {
        int index = i;
        var column = new VisualElement();
        column.style.flexGrow = 1;
        column.style.flexBasis = 0;
        var button = new ActionButton(Chinese.Modules[i], () => SelectModule(index));
        button.style.fontSize = 12;
        column.Add(button);
        _moduleStates[i] = ControlStyles.Text("未实现");
        _moduleStates[i].style.fontSize = 12;
        column.Add(_moduleStates[i]);
        navigation.Add(column);
      }
      _panel.Add(navigation);
      _resourceTabs = ControlStyles.Row();
      _resourceTabs.Add(new ActionButton("物品库存", () => SelectResourcePage(false)) { name = "TrainerInventoryEntry" });
      _resourceTabs.Add(new ActionButton("科研点管理", () => SelectResourcePage(true)) { name = "TrainerScienceEntry" });
      _panel.Add(_resourceTabs);
      _body = new ScrollView();
      var body = _body;
      body.style.maxHeight = 300;
      body.style.minHeight = 150;
      _panel.Add(body);
      _moduleTitle = ControlStyles.Text("");
      _moduleTitle.style.fontSize = 17;
      _moduleDescription = ControlStyles.Text("");
      body.Add(_moduleTitle);
      body.Add(_moduleDescription);
      _resourceArea = new VisualElement { name = "TrainerResources" };
      _scienceArea = new VisualElement { name = "TrainerScienceArea" };
      var heading = ControlStyles.Row();
      _currentScience = ControlStyles.Text("当前科研点数：读取中");
      _currentScience.name = "ScienceCurrentCount";
      _currentScience.style.flexGrow = 1;
      heading.Add(_currentScience);
      heading.Add(new ActionButton("刷新点数", RefreshScience));
      _scienceArea.Add(heading);
      _target = AddScienceRow("目标点数", "0", "设置目标", ScienceOperation.Set, out _setButton);
      _add = AddScienceRow("增加数量", "100", "增加点数", ScienceOperation.Add, out _addButton);
      _subtract = AddScienceRow("减少数量", "100", "减少点数", ScienceOperation.Subtract, out _subtractButton);
      _scienceArea.Add(ControlStyles.Text("仅接受零至十亿的整数；减少数量不能超过余额。"));
      _inventory = new InventoryPanel(_inventories, _editor) { name = "TrainerInventoryArea" };
      _resourceArea.Add(_inventory);
      _resourceArea.Add(_scienceArea);
      body.Add(_resourceArea);
      _buildingArea = new VisualElement();
      _finishConstruction = new ActionButton("立即完成所有建造", () => {
        _buildings.CompleteAll();
        Refresh();
      });
      _buildingArea.Add(_finishConstruction);
      _constructionStats = ControlStyles.Text("");
      _buildingArea.Add(_constructionStats);
      _buildingArea.Add(ControlStyles.Text("仅处理点击时的工地快照，调用原生完工接口；游戏暂停或运行时均可使用。建筑自身暂停、占位与原生阻挡仍然有效。"));
      _buildingArea.Add(ControlStyles.Text("不干预仓库、携带材料或运输预留。受阻工地可能随后按原版流程完工；本功能尚待实机验证。"));
      body.Add(_buildingArea);
      _beaverArea = new VisualElement();
      _hunger = AddNeedToggle("锁定饥饿需求（实验功能）", true);
      _thirst = AddNeedToggle("锁定口渴需求（实验功能）", false);
      _beaverArea.Add(_hunger); _beaverArea.Add(_thirst);
      _beaverArea.Add(ControlStyles.Text("维持启用时的需求下限，允许进食和饮水改善；关闭后停止补偿，原生需求继续更新。不会自动救治已为零或负值的需求，请先供应食物和水。"));
      _needStatus = ControlStyles.Text("待实机验证，包括现有海狸、新生海狸与关闭后的恢复。");
      _beaverArea.Add(_needStatus);
      body.Add(_beaverArea);
      _weatherArea = new VisualElement();
      _weatherStatus = ControlStyles.Text("点击刷新读取本周期天气。");
      _weatherArea.Add(_weatherStatus);
      _weatherArea.Add(new ActionButton("刷新天气", RefreshWeather));
      var durationInput = ControlStyles.Input("灾害总天数", "10");
      _weatherArea.Add(durationInput);
      _weatherSet = new ActionButton("设置本周期灾害总时长", () => {
        var result = _weather.SetDuration(durationInput.value);
        _weatherResult.text = result.Message;
        _weatherResult.style.color = result.Success ? ControlStyles.Accent : ControlStyles.Error;
        RefreshWeather();
      });
      _weatherArea.Add(_weatherSet);
      _weatherResult = ControlStyles.Text("尚未执行时长修改。");
      _weatherArea.Add(_weatherResult);
      _weatherArea.Add(ControlStyles.Text("仅修改本周期已选干旱或恶水的总时长，一至一千整数天；不是剩余天数。需手动暂停并启用全局。不能短于已经到达的日期，不切换天气、不修改后续周期。"));
      _weatherArea.Add(ControlStyles.Text("实验功能，尚待实机验证。一次性修改不写入配置，恢复默认不会撤销已修改的本周期时长。"));
      body.Add(_weatherArea);

      // Compact optional configuration; paths are tooltips instead of wrapping paragraphs.
      var config = new Foldout { text = "配置导入与导出", value = false };
      config.style.color = ControlStyles.Light;
      var foldToggle = config.Q<Toggle>();
      if (foldToggle != null) foldToggle.labelElement.style.color = ControlStyles.Light;
      _exchangeName = ControlStyles.Input("文件名", "我的配置.json");
      config.Add(_exchangeName);
      var exchange = ControlStyles.Row();
      exchange.Add(new ActionButton("导出配置", () => Run(() => _session.Store.Export(_exchangeName.value, _session.State.Settings), "配置已导出。")));
      exchange.Add(new ActionButton("导入配置", () => Run(() => {
        var settings = _session.Store.Import(_exchangeName.value);
        settings.Enabled = false;
        _session.State.Apply(settings);
      }, "配置已导入，全局保持关闭；不会执行科研命令。")));
      config.Add(exchange);
      var location = ControlStyles.Text("配置与交换目录（悬停查看完整路径）");
      location.style.fontSize = 12;
      location.tooltip = "配置目录：" + _session.Store.DirectoryPath + "\n交换目录：" + System.IO.Path.Combine(_session.Store.DirectoryPath, "Exchange");
      config.Add(location);
      _panel.Add(config);
      _status = ControlStyles.Text(_session.StartupMessage);
      _status.name = "TrainerStatus";
      _status.style.minHeight = 54;
      _status.style.maxHeight = 110;
      _panel.Add(_status);
      var footer = ControlStyles.Row();
      footer.Add(new ActionButton("保存配置", () => Run(() => _session.Store.Save(_session.State.Settings), "配置已保存。")));
      footer.Add(new ActionButton("读取配置", () => Run(() => {
        var settings = _session.Store.Load();
        settings.Enabled = false;
        _session.State.Apply(settings);
      }, "配置已读取，全局保持关闭；科研点数没有回滚。")));
      footer.Add(new ActionButton("恢复默认", () => Run(() => _session.State.RestoreDefaults(), "设置已恢复默认；已获得的科研点数不会撤销。")));
      footer.Add(new ActionButton("关闭面板", () => _panel.style.display = DisplayStyle.None));
      _panel.Add(footer);
      _session.State.Changed += Refresh;
      _loaded = true;
      SelectModule(0);
      Refresh();
      RefreshScience();
      _panel.style.display = DisplayStyle.None;
      _layout.AddAbsoluteItem(_root);
    }

    private TextField AddScienceRow(string label, string initial, string action, ScienceOperation operation, out ActionButton button) {
      var row = ControlStyles.Row();
      var field = ControlStyles.Input(label, initial);
      field.name = "Science" + operation + "Input";
      field.style.maxWidth = 370;
      row.Add(field);
      button = new ActionButton(action, () => ExecuteScience(operation, field.value));
      button.name = "Science" + operation + "Button";
      row.Add(button);
      _scienceArea.Add(row);
      return field;
    }
    private Toggle AddNeedToggle(string text, bool hunger) {
      var toggle = new Toggle(text);
      toggle.labelElement.style.color = ControlStyles.Light;
      toggle.RegisterValueChangedCallback(e => Run(() => {
        var settings = _session.State.Settings;
        if (hunger) settings.LockHunger = e.newValue; else settings.LockThirst = e.newValue;
        _session.State.Apply(settings);
      }, "需求维持偏好已更新；须全局启用才生效。"));
      return toggle;
    }

    private void ExecuteScience(ScienceOperation operation, string input) {
      var result = _resources.Science.Execute(operation, input);
      var before = result.Before.HasValue ? result.Before.Value.ToString() : "未知";
      var after = result.After.HasValue ? result.After.Value.ToString() : "未知";
      var delta = result.Delta.HasValue ? result.Delta.Value.ToString("+0;-0;0") : "无法确认";
      _status.text = result.Message + "\n修改前：" + before + "；实际变化：" + delta + "；修改后：" + after;
      _status.style.color = result.Success ? ControlStyles.Accent : ControlStyles.Error;
      RefreshScience();
    }

    public void UpdateSingleton() {
      if (!_loaded || _panel.style.display.value == DisplayStyle.None || Time.unscaledTime < _nextRefresh) return;
      _nextRefresh = Time.unscaledTime + .5f;
      RefreshScience();
    }

    private void RefreshScience() {
      if (!_loaded) return;
      int points;
      bool readable = _resources.Science.TryRead(out points);
      _currentScience.text = readable ? "当前科研点数：" + points : "当前科研点数：读取失败";
      _currentScience.style.color = readable ? ControlStyles.Light : ControlStyles.Error;
      Refresh();
    }

    private void Refresh() {
      if (!_loaded) return;
      _enabled.SetValueWithoutNotify(_session.State.Settings.Enabled);
      var check = _enabled.Q("unity-checkmark");
      if (check != null) {
        ControlStyles.Border(check, ControlStyles.Accent);
        check.style.backgroundColor = _session.State.EffectiveEnabled ? ControlStyles.Accent : new Color(.07f, .1f, .1f);
      }
      bool active = _session.State.EffectiveEnabled;
      _inventory.SetCommandsEnabled(active);
      _weatherSet.Available(_weather.Available && !_weather.Duration.Busy);
      _moduleStates[3].text = active ? "时长编辑·待实测" : "时长编辑·已禁用";
      _moduleStates[4].text = "共用本周期时长";
      _moduleStates[0].text = _resources.Science.RuntimeError ? "运行异常" : active ? "已启用" : "已禁用";
      _moduleStates[0].style.color = _resources.Science.RuntimeError ? ControlStyles.Error : active ? ControlStyles.Accent : ControlStyles.Muted;
      _moduleStates[1].text = _buildings.RuntimeError ? "结果需检查" : _buildings.Busy ? "执行中" : active ? "手动命令·待实测" : "已禁用";
      _moduleStates[1].style.color = _buildings.RuntimeError ? ControlStyles.Error : ControlStyles.Muted;
      _finishConstruction.Available(active && !_buildings.Busy);
      _hunger.SetEnabled(_session.State.Compatible && !_beavers.RuntimeError);
      _thirst.SetEnabled(_session.State.Compatible && !_beavers.RuntimeError);
      ControlStyles.ToggleState(_hunger, _session.State.Settings.LockHunger);
      ControlStyles.ToggleState(_thirst, _session.State.Settings.LockThirst);
      _moduleStates[2].text = _beavers.RuntimeError ? "运行异常" : active &&
        (_session.State.Settings.LockHunger || _session.State.Settings.LockThirst) ? "已启用·待实测" : "已禁用·实验";
      _needStatus.text = "本场景维持：饥饿 " + _beavers.HungerCount + "；口渴 " + _beavers.ThirstCount +
        "；需先恢复供给的需求 " + _beavers.UnsafeNeeds + "。新生与停用恢复待实测。";
      _constructionStats.text = _buildings.LastResult?.Message ?? "尚未执行批量完工。";
      for (int i = 2; i < _moduleStates.Length; ++i) _moduleStates[i].style.color = ControlStyles.Muted;
      _setButton.Available(active);
      _addButton.Available(active);
      _subtractButton.Available(active);
    }

    private void SelectModule(int index) {
      _selected = index;
      _moduleTitle.text = Chinese.Modules[index];
      _moduleDescription.text = index == 0 ? "聚居地统计只读；仓储与地区中心自身库存可暂停编辑，施工和生产缓冲只读；科研保留。"
        : index == 1 ? "立即完成所有建造：单次按钮，不持续生效；旧建造开关已废弃。" : "未实现：" + Chinese.Descriptions[index];
      if (index == 2) _moduleDescription.text = "饥饿与口渴独立维持；实验功能，尚未完成实机验收。";
      if (index == 3 || index == 4) _moduleDescription.text = "本周期灾害总时长编辑；天气切换、剩余时间直接设置及未来周期覆盖尚未实现。";
      _resourceArea.style.display = _resourceTabs.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
      if (index == 0) SelectResourcePage(_sciencePage);
      _buildingArea.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
      _beaverArea.style.display = index == 2 ? DisplayStyle.Flex : DisplayStyle.None;
      _weatherArea.style.display = index == 3 || index == 4 ? DisplayStyle.Flex : DisplayStyle.None;
      if (index == 3 || index == 4) RefreshWeather();
    }

    private void SelectResourcePage(bool science) {
      _sciencePage = science;
      _scienceArea.style.display = science ? DisplayStyle.Flex : DisplayStyle.None;
      _inventory.style.display = science ? DisplayStyle.None : DisplayStyle.Flex;
      _body.scrollOffset = Vector2.zero;
      if (science) RefreshScience();
      else if (_root.panel != null && _panel.style.display.value != DisplayStyle.None) _inventory.Open();
    }

    private void RefreshWeather() {
      _weatherStatus.text = _weather.Describe();
      Refresh();
    }

    private void Run(Action action, string success) {
      try { action(); _status.text = success; _status.style.color = ControlStyles.Accent; }
      catch (Exception e) {
        _status.text = e is ArgumentException || e is InvalidOperationException
          ? "操作失败：" + e.Message : "操作失败，请检查文件内容和目录权限。";
        _status.style.color = ControlStyles.Error;
      }
      Refresh();
    }

    public void Unload() {
      _loaded = false;
      _inventory?.Unload();
      _inventory = null;
      if (_session.State != null) _session.State.Changed -= Refresh;
      _root?.RemoveFromHierarchy();
      _root = null;
    }
  }
}
