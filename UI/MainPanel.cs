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
    private LocalizedLabel _needStatus;
    private VisualElement _root, _panel, _resourceArea, _resourceTabs, _scienceArea, _buildingArea;
    private ScrollView _body;
    private bool _sciencePage;
    private LocalizedLabel _status, _moduleTitle, _moduleDescription, _currentScience, _constructionStats;
    private readonly LocalizedLabel[] _moduleStates = new LocalizedLabel[3];
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
      _launcher = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0396"), () => SetPanelOpen(_panel.style.display.value == DisplayStyle.None));
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
      LocalizedUI.Tooltip(titleBar, global::TimberbornTrainer.Localization.TextToken.Key("t0365"));
      title.style.flexGrow = 1; titleBar.Add(title);
      titleBar.Add(new ActionButton("×", () => SetPanelOpen(false)) { tooltip = global::TimberbornTrainer.Localization.TextToken.Key("t0190") });
      _panel.Add(titleBar);
      _panel.Add(ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0415") + _session.State.GameVersion + global::TimberbornTrainer.Localization.TextToken.Key("t0026") + TrainerState.ModVersion));
      var compatibility = ControlStyles.Text(_session.State.CompatibilityMessage);
      compatibility.name = "TrainerCompatibilityNotice";
      if (!_session.State.VerifiedVersion) compatibility.style.color = ControlStyles.Error;
      _panel.Add(compatibility);
      foreach (TrainerFeature feature in Enum.GetValues(typeof(TrainerFeature))) {
        var reason = _session.State.InterfaceWarning(feature);
        if (reason != null) { var notice = ControlStyles.Text(reason); notice.name = "TrainerInterfaceWarning" + feature; notice.style.color = ControlStyles.Error; _panel.Add(notice); }
      }
      _enabled = new LocalizedToggle(global::TimberbornTrainer.Localization.TextToken.Key("t0321")) { name = "TrainerEnabled" };
      _enabled.style.color = ControlStyles.Light;
      _enabled.labelElement.style.color = ControlStyles.Light;
      _enabled.style.minHeight = 30;
      _enabled.SetEnabled(_session.State.Compatible);
      _enabled.RegisterValueChangedCallback(e => Run(() => _session.State.SetEnabled(e.newValue),
        e.newValue ? global::TimberbornTrainer.Localization.TextToken.Key("t0327") : global::TimberbornTrainer.Localization.TextToken.Key("t0326")));
      _panel.Add(_enabled);
            var language = new LocalizedDropdown(global::TimberbornTrainer.Localization.TextToken.Key("t0222"), new System.Collections.Generic.List<string> { "English", "简体中文" }, TimberbornTrainer.Localization.RuntimeText.Language == "zh-CN" ? 1 : 0) { name = "TrainerLanguage" };
      language.style.minHeight = 30;
      var languageFeedback = ControlStyles.Text(""); languageFeedback.name = "TrainerLanguageStatus";
      language.RegisterValueChangedCallback(e => {
        bool saved = TimberbornTrainer.Localization.RuntimeText.Select(e.newValue == "简体中文" ? "zh-CN" : "en-US");
        languageFeedback.text = saved ? global::TimberbornTrainer.Localization.TextToken.Key("t0420") : global::TimberbornTrainer.Localization.TextToken.Key("t0421");
      });
      _panel.Add(language);
      _panel.Add(languageFeedback);
      if (TimberbornTrainer.Localization.RuntimeText.LoadWarning != null) _panel.Add(ControlStyles.Text(TimberbornTrainer.Localization.RuntimeText.LoadWarning));
      var navigation = ControlStyles.Row();
      for (int i = 0; i < Chinese.Modules.Length; ++i) {
        int index = i;
        var column = new VisualElement();
        column.style.flexGrow = 1;
        column.style.flexBasis = 0;
        var button = new ActionButton(Chinese.Modules[i], () => SelectModule(index));
        button.style.fontSize = 12;
        column.Add(button);
        _moduleStates[i] = ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0408"));
        _moduleStates[i].style.fontSize = 12;
        column.Add(_moduleStates[i]);
        navigation.Add(column);
      }
      _panel.Add(navigation);
      _resourceTabs = ControlStyles.Row();
      _resourceTabs.Add(new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0392"), () => SelectResourcePage(false)) { name = "TrainerInventoryEntry" });
      _resourceTabs.Add(new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0244"), () => SelectResourcePage(true)) { name = "TrainerScienceEntry" });
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
      _currentScience = ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0158"));
      _currentScience.name = "ScienceCurrentCount";
      _currentScience.style.flexGrow = 1;
      heading.Add(_currentScience);
      heading.Add(new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0357"), RefreshScience));
      _scienceArea.Add(heading);
      _target = AddScienceRow(global::TimberbornTrainer.Localization.TextToken.Key("t0274"), "0", global::TimberbornTrainer.Localization.TextToken.Key("t0341"), ScienceOperation.Set, out _setButton);
      _add = AddScienceRow(global::TimberbornTrainer.Localization.TextToken.Key("t0449"), "100", global::TimberbornTrainer.Localization.TextToken.Key("t0447"), ScienceOperation.Add, out _addButton);
      _subtract = AddScienceRow(global::TimberbornTrainer.Localization.TextToken.Key("t0210"), "100", global::TimberbornTrainer.Localization.TextToken.Key("t0209"), ScienceOperation.Subtract, out _subtractButton);
      _scienceArea.Add(ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0225")));
      _inventory = new MapInventoryPanel(_inventories, _editor, _events) { name = "TrainerInventoryArea" };
      _resourceArea.Add(_inventory);
      _resourceArea.Add(_scienceArea);
      body.Add(_resourceArea);
      _buildingArea = new VisualElement();
      _finishConstruction = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0262"), () => {
        _buildings.CompleteAll();
        Refresh();
      });
      _buildingArea.Add(_finishConstruction);
      _constructionStats = ControlStyles.Text("");
      _buildingArea.Add(_constructionStats);
      _buildingArea.Add(ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0224")));
      _buildingArea.Add(ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0117")));
      body.Add(_buildingArea);
      _beaverArea = new VisualElement();
      _hunger = AddNeedToggle(global::TimberbornTrainer.Localization.TextToken.Key("t0359"), true);
      _thirst = AddNeedToggle(global::TimberbornTrainer.Localization.TextToken.Key("t0360"), false);
      _beaverArea.Add(_hunger); _beaverArea.Add(_thirst);
      _beaverArea.Add(ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0201")));
      _needStatus = ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0202"));
      _beaverArea.Add(_needStatus);
      body.Add(_beaverArea);
      // Compact optional configuration; paths are tooltips instead of wrapping paragraphs.
      var config = new LocalizedFoldout { text = global::TimberbornTrainer.Localization.TextToken.Key("t0290"), value = false };
      config.style.color = ControlStyles.Light;
      var foldToggle = config.Q<Toggle>();
      if (foldToggle != null) foldToggle.labelElement.style.color = ControlStyles.Light;
      _exchangeName = ControlStyles.Input(global::TimberbornTrainer.Localization.TextToken.Key("t0382"), global::TimberbornTrainer.Localization.TextToken.Key("t0383"));
      config.Add(_exchangeName);
      var exchange = ControlStyles.Row();
      exchange.Add(new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0164"), () => Run(() => _session.Store.Export(_exchangeName.value, _session.State.Settings), global::TimberbornTrainer.Localization.TextToken.Key("t0296"))));
      exchange.Add(new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0165"), () => Run(() => {
        var settings = _session.Store.Import(_exchangeName.value);
        settings.Enabled = false;
        _session.State.Apply(settings);
      }, global::TimberbornTrainer.Localization.TextToken.Key("t0297"))));
      config.Add(exchange);
      var location = ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0299"));
      location.style.fontSize = 12;
      location.tooltip = global::TimberbornTrainer.Localization.TextToken.Key("t0292") + _session.Store.DirectoryPath + global::TimberbornTrainer.Localization.TextToken.Key("t0004") + System.IO.Path.Combine(_session.Store.DirectoryPath, "Exchange");
      config.Add(location);
      _panel.Add(config);
      _status = ControlStyles.Text(_session.StartupMessage);
      _status.name = "TrainerStatus";
      _status.style.minHeight = 54;
      _status.style.maxHeight = 110;
      _panel.Add(_status);
      var footer = ControlStyles.Row();
      footer.Add(new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0112"), () => Run(() => _session.Store.Save(_session.State.Settings), global::TimberbornTrainer.Localization.TextToken.Key("t0295"))));
      footer.Add(new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0183"), () => Run(() => {
        var settings = _session.Store.Load();
        settings.Enabled = false;
        _session.State.Apply(settings);
      }, global::TimberbornTrainer.Localization.TextToken.Key("t0298"))));
      footer.Add(new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0203"), () => Run(() => _session.State.RestoreDefaults(), global::TimberbornTrainer.Localization.TextToken.Key("t0342"))));
      footer.Add(new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0190"), () => SetPanelOpen(false)));
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
      var toggle = new LocalizedToggle(text);
      toggle.labelElement.style.color = ControlStyles.Light;
      toggle.RegisterValueChangedCallback(e => Run(() => {
        var settings = _session.State.Settings;
        if (hunger) settings.LockHunger = e.newValue; else settings.LockThirst = e.newValue;
        _session.State.Apply(settings);
      }, global::TimberbornTrainer.Localization.TextToken.Key("t0401")));
      return toggle;
    }

    private void ExecuteScience(ScienceOperation operation, string input) {
      var result = _resources.Science.Execute(operation, input);
      var before = result.Before.HasValue ? result.Before.Value.ToString() : global::TimberbornTrainer.Localization.TextToken.Key("t0376");
      var after = result.After.HasValue ? result.After.Value.ToString() : global::TimberbornTrainer.Localization.TextToken.Key("t0376");
      var delta = result.Delta.HasValue ? result.Delta.Value.ToString("+0;-0;0") : global::TimberbornTrainer.Localization.TextToken.Key("t0385");
      _status.text = result.Message + global::TimberbornTrainer.Localization.TextToken.Key("t0013") + before + global::TimberbornTrainer.Localization.TextToken.Key("t0067") + delta + global::TimberbornTrainer.Localization.TextToken.Key("t0076") + after;
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
      _currentScience.text = readable ? global::TimberbornTrainer.Localization.TextToken.Key("t0156") + points : global::TimberbornTrainer.Localization.TextToken.Key("t0157");
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
      _moduleStates[0].text = _resources.Science.RuntimeError ? global::TimberbornTrainer.Localization.TextToken.Key("t0444") : active ? global::TimberbornTrainer.Localization.TextToken.Key("t0409") : global::TimberbornTrainer.Localization.TextToken.Key("t0408");
      _moduleStates[0].style.color = _resources.Science.RuntimeError ? ControlStyles.Error : active ? ControlStyles.Accent : ControlStyles.Muted;
      _moduleStates[1].text = _buildings.RuntimeError ? global::TimberbornTrainer.Localization.TextToken.Key("t0221") : _buildings.Busy ? global::TimberbornTrainer.Localization.TextToken.Key("t0455") : active ? global::TimberbornTrainer.Localization.TextToken.Key("t0353") : global::TimberbornTrainer.Localization.TextToken.Key("t0408");
      _moduleStates[1].style.color = _buildings.RuntimeError ? ControlStyles.Error : ControlStyles.Muted;
      _finishConstruction.Available(active && !_buildings.Busy);
      _hunger.SetEnabled(_session.State.Compatible && !_beavers.RuntimeError);
      _thirst.SetEnabled(_session.State.Compatible && !_beavers.RuntimeError);
      ControlStyles.ToggleState(_hunger, _session.State.Settings.LockHunger);
      ControlStyles.ToggleState(_thirst, _session.State.Settings.LockThirst);
      _moduleStates[2].text = _beavers.RuntimeError ? global::TimberbornTrainer.Localization.TextToken.Key("t0444") : active &&
        (_session.State.Settings.LockHunger || _session.State.Settings.LockThirst) ? global::TimberbornTrainer.Localization.TextToken.Key("t0409") : global::TimberbornTrainer.Localization.TextToken.Key("t0408");
      _needStatus.text = global::TimberbornTrainer.Localization.TextToken.Key("t0113") + _beavers.HungerCount + global::TimberbornTrainer.Localization.TextToken.Key("t0055") + _beavers.ThirstCount +
        global::TimberbornTrainer.Localization.TextToken.Key("t0074") + _beavers.UnsafeNeeds + global::TimberbornTrainer.Localization.TextToken.Key("t0096");
      _constructionStats.text = _buildings.LastResult?.Message ?? global::TimberbornTrainer.Localization.TextToken.Key("t0338");
      for (int i = 2; i < _moduleStates.Length; ++i) _moduleStates[i].style.color = ControlStyles.Muted;
      _setButton.Available(active);
      _addButton.Available(active);
      _subtractButton.Available(active);
    }

    private void SelectModule(int index) {
      _selected = index;
      _moduleTitle.text = Chinese.Modules[index];
      _moduleDescription.text = index == 0 ? global::TimberbornTrainer.Localization.TextToken.Key("t0445")
        : index == 1 ? global::TimberbornTrainer.Localization.TextToken.Key("t0263") : Chinese.Descriptions[index];
      if (index == 2) _moduleDescription.text = global::TimberbornTrainer.Localization.TextToken.Key("t0204");
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
          ? global::TimberbornTrainer.Localization.TextToken.Key("t0134") + e.Message : global::TimberbornTrainer.Localization.TextToken.Key("t0133");
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
      if (_root != null) LocalizedUI.Release(_root);
      _root?.RemoveFromHierarchy();
      _root = null;
    }
  }
}
