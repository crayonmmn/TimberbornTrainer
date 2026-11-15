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
    private MapInventoryPanel _inventory;
    private readonly EventBus _events;
    private VisualElement _beaverArea;
    private Toggle _hunger, _thirst;
    private Label _needStatus;
    private VisualElement _root, _panel, _resourceArea, _resourceTabs, _scienceArea, _buildingArea;
    private ScrollView _body;
    private bool _sciencePage;
    private Label _status, _moduleTitle, _moduleDescription, _currentScience, _constructionStats;
    private readonly Label[] _moduleStates = new Label[3];
    private Toggle _enabled;
    private TextField _exchangeName, _target, _add, _subtract;
    private ActionButton _setButton, _addButton, _subtractButton, _finishConstruction;
    private float _nextRefresh;
    private int _selected;
    private bool _loaded;
    private FloatingWindow _window;
    private ActionButton _launcher;

    public MainPanel(TrainerSession session, UILayout layout, ResourceModule resources, BuildingModule buildings, BeaverModule beavers, NativeInventoryCatalog inventories, NativeInventoryEditor editor, EventBus events) {
      _session = session; _layout = layout; _resources = resources; _buildings = buildings;
      _beavers = beavers;
      _inventories = inventories;
      _editor = editor;
      _events = events;
    }

    public void Load() {
      _root = new VisualElement { name = "TimberbornTrainer" };
      _root.style.position = Position.Absolute;
      _root.style.left = 260;
      _root.style.top = 105;
      _launcher = new ActionButton("修改工具", () => SetPanelOpen(_panel.style.display.value == DisplayStyle.None));
      _launcher.style.width = 140; _root.Add(_launcher);
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
      var titleBar = ControlStyles.Row(); titleBar.name = "TrainerTitleBar";
      titleBar.tooltip = "拖动标题栏移动；靠左或靠右停靠后可自动隐藏";
      title.style.flexGrow = 1; titleBar.Add(title);
      titleBar.Add(new ActionButton("×", () => SetPanelOpen(false)) { tooltip = "关闭面板" });
      _panel.Add(titleBar);
      _panel.Add(ControlStyles.Text("游戏：" + _session.State.GameVersion + "　模组：" + TrainerState.ModVersion));
      var compatibility = ControlStyles.Text(_session.State.CompatibilityMessage);
      compatibility.name = "TrainerCompatibilityNotice";
      if (!_session.State.VerifiedVersion) compatibility.style.color = ControlStyles.Error;
      _panel.Add(compatibility);
      foreach (TrainerFeature feature in Enum.GetValues(typeof(TrainerFeature))) {
        var reason = _session.State.InterfaceWarning(feature);
        if (reason != null) { var notice = ControlStyles.Text(reason); notice.name = "TrainerInterfaceWarning" + feature; notice.style.color = ControlStyles.Error; _panel.Add(notice); }
      }
      _enabled = new Toggle("全局修改器") { name = "TrainerEnabled" };
      _enabled.style.color = ControlStyles.Light;
      _enabled.labelElement.style.color = ControlStyles.Light;
      _enabled.style.minHeight = 30;
      _enabled.SetEnabled(_session.State.Compatible);
      _enabled.RegisterValueChangedCallback(e => Run(() => _session.State.SetEnabled(e.newValue),
        e.newValue ? "全局已启用。" : "全局已关闭，修改命令和持续性修改已停止。"));
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
        _moduleStates[i] = ControlStyles.Text("已禁用");
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
      body.style.minHeight = 0; body.style.flexShrink = 1;
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
      _inventory = new MapInventoryPanel(_inventories, _editor, _events) { name = "TrainerInventoryArea" };
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
      _buildingArea.Add(ControlStyles.Text("不干预仓库、携带材料或运输预留。受阻工地可能随后按原版流程完工。"));
      body.Add(_buildingArea);
      _beaverArea = new VisualElement();
      _hunger = AddNeedToggle("锁定饥饿需求", true);
      _thirst = AddNeedToggle("锁定口渴需求", false);
      _beaverArea.Add(_hunger); _beaverArea.Add(_thirst);
      _beaverArea.Add(ControlStyles.Text("恢复饥饿/口渴至原版正常满值，并暂停对应需求的自然扣减；关闭后归还原更新状态。只作用于存活海狸，食物品种满足度等其他需求不锁定。恢复模拟后生效，已开始的进食/运输可能按原版完成。"));
      _needStatus = ControlStyles.Text("恢复模拟后开始维持需求。");
      _beaverArea.Add(_needStatus);
      body.Add(_beaverArea);
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
      footer.Add(new ActionButton("关闭面板", () => SetPanelOpen(false)));
      _panel.Add(footer);
      _session.State.Changed += Refresh;
      _loaded = true;
      SelectModule(0);
      Refresh();
      RefreshScience();
      _panel.style.display = DisplayStyle.None;
      _layout.AddAbsoluteItem(_root);
      _window = new FloatingWindow(_root, _panel, titleBar, title);
    }

    private void SetPanelOpen(bool open) {
      _panel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
      _launcher.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
      _window?.SetOpen(open);
      RefreshScience();
      if (!open) _inventory.Close();
      else if (_selected == 0 && !_sciencePage) _inventory.Open();
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
      if (_selected == 0 && !_sciencePage) _inventory.RefreshSelection();
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
      _moduleStates[0].text = _resources.Science.RuntimeError ? "运行异常" : active ? "已启用" : "已禁用";
      _moduleStates[0].style.color = _resources.Science.RuntimeError ? ControlStyles.Error : active ? ControlStyles.Accent : ControlStyles.Muted;
      _moduleStates[1].text = _buildings.RuntimeError ? "结果需检查" : _buildings.Busy ? "执行中" : active ? "手动命令" : "已禁用";
      _moduleStates[1].style.color = _buildings.RuntimeError ? ControlStyles.Error : ControlStyles.Muted;
      _finishConstruction.Available(active && !_buildings.Busy);
      _hunger.SetEnabled(_session.State.Compatible && !_beavers.RuntimeError);
      _thirst.SetEnabled(_session.State.Compatible && !_beavers.RuntimeError);
      ControlStyles.ToggleState(_hunger, _session.State.Settings.LockHunger);
      ControlStyles.ToggleState(_thirst, _session.State.Settings.LockThirst);
      _moduleStates[2].text = _beavers.RuntimeError ? "运行异常" : active &&
        (_session.State.Settings.LockHunger || _session.State.Settings.LockThirst) ? "已启用" : "已禁用";
      _needStatus.text = "本场景维持：饥饿 " + _beavers.HungerCount + "；口渴 " + _beavers.ThirstCount +
        "；无法安全接管的需求 " + _beavers.UnsafeNeeds + "。";
      _constructionStats.text = _buildings.LastResult?.Message ?? "尚未执行批量完工。";
      for (int i = 2; i < _moduleStates.Length; ++i) _moduleStates[i].style.color = ControlStyles.Muted;
      _setButton.Available(active);
      _addButton.Available(active);
      _subtractButton.Available(active);
    }

    private void SelectModule(int index) {
      _selected = index;
      _moduleTitle.text = Chinese.Modules[index];
      _moduleDescription.text = index == 0 ? "在地图中选中仓库，自动读取其已配置物品；修改需暂停。科研点使用独立入口。"
        : index == 1 ? "立即完成所有建造：点击时处理现有工地，不会自动处理新工地。" : Chinese.Descriptions[index];
      if (index == 2) _moduleDescription.text = "饥饿与口渴可分别开启，关闭后恢复正常消耗。";
      _resourceArea.style.display = _resourceTabs.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
      if (index != 0) _inventory.Close();
      if (index == 0) SelectResourcePage(_sciencePage);
      _buildingArea.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
      _beaverArea.style.display = index == 2 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SelectResourcePage(bool science) {
      _sciencePage = science;
      _scienceArea.style.display = science ? DisplayStyle.Flex : DisplayStyle.None;
      _inventory.style.display = science ? DisplayStyle.None : DisplayStyle.Flex;
      _body.scrollOffset = Vector2.zero;
      if (science) { _inventory.Close(); RefreshScience(); }
      else if (_root.panel != null && _panel.style.display.value != DisplayStyle.None) _inventory.Open();
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
      _window?.Dispose(); _window = null;
      _inventory?.Unload();
      _inventory = null;
      if (_session.State != null) _session.State.Changed -= Refresh;
      _root?.RemoveFromHierarchy();
      _root = null;
    }
  }
}
