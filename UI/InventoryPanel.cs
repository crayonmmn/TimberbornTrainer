using System;
using System.Collections.Generic;
using System.Linq;
using TimberbornTrainer.Modules;
using UnityEngine;
using UnityEngine.UIElements;

namespace TimberbornTrainer.UI {
  internal sealed class InventoryPanel : VisualElement {
    private readonly InventoryBrowser _browser;
    private readonly LocalizedDropdown _source, _inventory, _good;
    private readonly ScrollView _buildings;
    private readonly VisualElement _selector;
    private readonly ActionButton _changeTarget, _focusTarget;
    private readonly LocalizedLabel _status, _target, _stock, _abilities, _scanStatus, _safety;
    private readonly IInventorySafetyDiagnostics _diagnostics;
    private readonly IInventoryEditor _editor;
    private readonly TextField _editAmount;
    private readonly LocalizedLabel _editResult;
    private readonly ActionButton _openList, _scan;
    private readonly ActionButton _add, _remove, _set, _clear;
    private InventoryTarget[] _ownerInventories = Array.Empty<InventoryTarget>();
    private string[] _resourceIds = Array.Empty<string>();
    private bool _painting, _disposed;
    private bool _commandsEnabled, _targetEditable, _resourceAllowed;

    internal InventoryPanel(IInventoryCatalog catalog, IInventoryEditor editor) {
      _browser = new InventoryBrowser(catalog);
      _diagnostics = catalog as IInventorySafetyDiagnostics;
      _editor = editor;
      Add(ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0362")));
      _target = ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0335"));
      _target.name = "InventorySelectionSummary"; Add(_target);
      var selectionActions = ControlStyles.Row();
      _changeTarget = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0188"), () => SetSelectionExpanded(_selector.style.display.value == DisplayStyle.None)) { name = "InventoryChangeTarget" };
      selectionActions.Add(_changeTarget);
      _focusTarget = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0176"), () => {
        var target = _browser.Snapshot?.Target;
        bool focused = target != null && _browser.Focus(target.Id);
        if (!focused) RenderTargets();
        _status.text = focused ? global::TimberbornTrainer.Localization.TextToken.Key("t0410") : _browser.Message;
      });
      selectionActions.Add(_focusTarget);
      selectionActions.Add(new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0462"), RefreshSelected)); Add(selectionActions);
      _selector = new VisualElement { name = "InventorySelector" }; Add(_selector);
      _selector.style.display = DisplayStyle.None;
      _source = Field(global::TimberbornTrainer.Localization.TextToken.Key("t0277"), new List<string> { global::TimberbornTrainer.Localization.TextToken.Key("t0240"), global::TimberbornTrainer.Localization.TextToken.Key("t0459"), global::TimberbornTrainer.Localization.TextToken.Key("t0460") });
      _source.name = "InventorySource"; _selector.Add(_source);
      var actions = ControlStyles.Row();
      _scan = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0332"), Scan) { name = "InventoryScan" }; actions.Add(_scan);
      _openList = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0150"), Discover); actions.Add(_openList);
      _selector.Add(actions);
      _scanStatus = ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0333")); _selector.Add(_scanStatus);
      _status = ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0151")); Add(_status);
      _status.style.minHeight = 50;
      _buildings = new ScrollView(); _buildings.style.maxHeight = 110; _selector.Add(_buildings);
      _inventory = Field(global::TimberbornTrainer.Localization.TextToken.Key("t0234"), new List<string> { global::TimberbornTrainer.Localization.TextToken.Key("t0314") }); _selector.Add(_inventory);
      _inventory.name = "InventoryTarget";
      _good = Field(global::TimberbornTrainer.Localization.TextToken.Key("t0464"), new List<string> { global::TimberbornTrainer.Localization.TextToken.Key("t0313") }); Add(_good);
      _good.name = "InventoryResource";
      _good.tooltip = global::TimberbornTrainer.Localization.TextToken.Key("t0227");
      _stock = ControlStyles.Text(""); Add(_stock);
      Add(ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0248")));
      _editAmount = ControlStyles.Input(global::TimberbornTrainer.Localization.TextToken.Key("t0354"), "10"); _editAmount.name = "InventoryAmount"; Add(_editAmount);
      var editActions = ControlStyles.Row();
      _add = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0446"), () => Edit(InventoryWriteOperation.Add)) { name = "InventoryAdd" }; editActions.Add(_add);
      _remove = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0208"), () => Edit(InventoryWriteOperation.Remove)) { name = "InventoryRemove" }; editActions.Add(_remove);
      _set = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0341"), () => Edit(InventoryWriteOperation.Set)) { name = "InventorySet" }; editActions.Add(_set);
      _clear = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0302"), () => Edit(InventoryWriteOperation.Clear)) { name = "InventoryClear" }; editActions.Add(_clear);
      Add(editActions);
      _editResult = ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0337")); Add(_editResult);
      _editResult.name = "InventoryEditResult"; _stock.name = "InventoryStock";
      _abilities = ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0312")); Add(_abilities);
      var diagnostics = new LocalizedFoldout { text = global::TimberbornTrainer.Localization.TextToken.Key("t0241"), value = false }; Add(diagnostics);
      var diagnose = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0179"), Diagnose); diagnostics.Add(diagnose);
      diagnose.Available(_diagnostics != null);
      _safety = ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0109")); diagnostics.Add(_safety);
      var timing = catalog as IInventoryTimingDiagnostics;
      var traceRow = ControlStyles.Row();
      traceRow.Add(new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0242"), () => { timing?.StartTiming(); _safety.text = global::TimberbornTrainer.Localization.TextToken.Key("t0457"); }));
      traceRow.Add(new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0361"), () => {
        string text = timing?.StopTiming() ?? global::TimberbornTrainer.Localization.TextToken.Key("t0155");
        _safety.text = string.Join("\n", text.Split('\n').Take(8)) + global::TimberbornTrainer.Localization.TextToken.Key("t0010");
        _safety.tooltip = text;
      }));
      diagnostics.Add(traceRow);
      _source.RegisterValueChangedCallback(_ => {
        if (_painting || _disposed) return;
        _browser.Switch((InventorySource)_source.index); RenderTargets();
        if (_browser.Source == InventorySource.Settlement && _browser.Snapshot?.Valid == true) SetSelectionExpanded(false);
      });
      _inventory.RegisterValueChangedCallback(_ => {
        if (_painting || _disposed) return;
        if (_inventory.index >= 0 && _inventory.index < _ownerInventories.Length) {
          _browser.Select(_ownerInventories[_inventory.index].Id); RenderSnapshot();
          if (_browser.Snapshot?.Valid == true) SetSelectionExpanded(false);
        }
      });
      _good.RegisterValueChangedCallback(_ => {
        if (_painting || _disposed) return;
        if (_good.index >= 0 && _good.index < _resourceIds.Length) _browser.SelectGood(_resourceIds[_good.index]);
        RenderResource();
      });
      _openList.SetEnabled(false);
      _focusTarget.Available(false);
      PaintEditButtons();
    }
    private static LocalizedDropdown Field(string label, List<string> choices) {
      var field = new LocalizedDropdown(label, choices, 0);
      field.style.minHeight = 30; field.labelElement.style.color = ControlStyles.Light;
      var input = field.Q(className: "unity-base-popup-field__input");
      if (input != null) { input.style.backgroundColor = new Color(.1f, .2f, .18f); input.style.color = ControlStyles.Light; ControlStyles.Border(input, ControlStyles.Muted); }
      field.style.color = ControlStyles.Light;
      return field;
    }
    internal void Open() {
      if (_disposed) return;
      SetSelectionExpanded(false);
      if (_browser.Opened) return;
      _browser.Switch(InventorySource.Settlement); RenderTargets();
    }
    private void SetSelectionExpanded(bool expanded) {
      _selector.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
      _changeTarget.text = expanded ? global::TimberbornTrainer.Localization.TextToken.Key("t0352") : global::TimberbornTrainer.Localization.TextToken.Key("t0188");
    }
    private void Discover() {
      if (_disposed) return;
      _browser.Discover(); RenderTargets();
    }
    private void Scan() {
      if (_disposed) return;
      _scan.Available(false);
      try { _browser.Scan(); RenderTargets(); }
      finally { _scan.Available(true); }
    }
    private void RefreshSelected() {
      if (_disposed) return;
      if (_browser.Snapshot?.Target != null) {
        _browser.Select(_browser.Snapshot.Target.Id); RenderTargets();
      } else Discover();
    }
    private void RenderTargets() {
      _buildings.Clear(); _ownerInventories = Array.Empty<InventoryTarget>();
      bool settlement = _browser.Source == InventorySource.Settlement;
      _openList.text = _browser.Source == InventorySource.Building ? global::TimberbornTrainer.Localization.TextToken.Key("t0150") : global::TimberbornTrainer.Localization.TextToken.Key("t0149");
      _openList.Available(!settlement);
      _buildings.style.display = settlement ? DisplayStyle.None : DisplayStyle.Flex;
      foreach (var grouping in _browser.Targets.GroupBy(t => t.OwnerId)) {
        var owner = grouping.First();
        var button = new ActionButton(owner.OwnerName + " · " + owner.BuildingType + " · " + grouping.Count() + global::TimberbornTrainer.Localization.TextToken.Key("t0024"),
          () => SelectOwner(owner.OwnerId));
        button.name = "InventoryOwner:" + owner.OwnerId;
        button.style.whiteSpace = WhiteSpace.Normal;
        button.tooltip = global::TimberbornTrainer.Localization.TextToken.Key("t0153");
        button.RegisterCallback<ClickEvent>(e => {
          if (e.clickCount == 2 && !_disposed) {
            bool focused = _browser.Focus(owner.Id);
            if (!focused) RenderTargets();
            _status.text = focused ? global::TimberbornTrainer.Localization.TextToken.Key("t0410") : _browser.Message;
          }
        });
        _buildings.Add(button);
      }
      if (_browser.Snapshot?.Target != null) SelectOwner(_browser.Snapshot.Target.OwnerId, false);
      else if (settlement && _browser.Targets.Length == 1) SelectOwner(_browser.Targets[0].OwnerId, false);
      else RenderSnapshot();
    }
    private void SelectOwner(string ownerId, bool read = true) {
      _ownerInventories = _browser.Targets.Where(t => t.OwnerId == ownerId).ToArray();
      _painting = true;
      _inventory.choices = _ownerInventories.Select(t => t.DisplayName).ToList();
      if (_inventory.choices.Count == 0) _inventory.choices.Add(global::TimberbornTrainer.Localization.TextToken.Key("t0268"));
      int selected = Array.FindIndex(_ownerInventories, t => t.Id == _browser.Snapshot?.Target?.Id);
      _inventory.SetValueWithoutNotify(_inventory.choices[Math.Max(0, selected)]);
      _painting = false;
      if (read && _ownerInventories.Length > 0) _browser.Select(_ownerInventories[Math.Max(0, selected)].Id);
      RenderSnapshot();
      if (read && _browser.Snapshot?.Valid == true) SetSelectionExpanded(false);
    }
    private void RenderSnapshot() {
      _status.text = _browser.Message;
      var scan = _browser.LastScan;
      _scan.text = scan == null ? global::TimberbornTrainer.Localization.TextToken.Key("t0332") : global::TimberbornTrainer.Localization.TextToken.Key("t0461");
      _scanStatus.text = scan == null ? global::TimberbornTrainer.Localization.TextToken.Key("t0334")
        : global::TimberbornTrainer.Localization.TextToken.Key("t0174") + scan.Sequence + global::TimberbornTrainer.Localization.TextToken.Key("t0022") + scan.ElapsedMilliseconds.ToString("F1") + global::TimberbornTrainer.Localization.TextToken.Key("t0025") + (scan.Completed ? global::TimberbornTrainer.Localization.TextToken.Key("t0366") : global::TimberbornTrainer.Localization.TextToken.Key("t0344"))
          + global::TimberbornTrainer.Localization.TextToken.Key("t0017") + scan.RegisteredResources + global::TimberbornTrainer.Localization.TextToken.Key("t0048") + scan.InspectedEntities + global::TimberbornTrainer.Localization.TextToken.Key("t0084") + scan.RecognizedBuildings
          + global::TimberbornTrainer.Localization.TextToken.Key("t0014") + scan.InventoryBuildings + global::TimberbornTrainer.Localization.TextToken.Key("t0050") + scan.ReadableInventories + global::TimberbornTrainer.Localization.TextToken.Key("t0082") + scan.DistrictCenters
          + global::TimberbornTrainer.Localization.TextToken.Key("t0006") + scan.ExcludedObjects + global::TimberbornTrainer.Localization.TextToken.Key("t0059") + scan.ExcludedInventories
          + (scan.ExclusionReasons.Length == 0 ? "" : "\n" + string.Join(global::TimberbornTrainer.Localization.TextToken.Key("t0040"), scan.ExclusionReasons));
      var snapshot = _browser.Snapshot;
      bool valid = snapshot != null && snapshot.Valid;
      _painting = true;
      if (!valid) {
        if (_browser.Targets.Length == 0) _buildings.Clear();
        _ownerInventories = Array.Empty<InventoryTarget>();
        _inventory.choices = new List<string> { global::TimberbornTrainer.Localization.TextToken.Key("t0314") }; _inventory.SetValueWithoutNotify(global::TimberbornTrainer.Localization.TextToken.Key("t0314"));
      }
      _inventory.SetEnabled(valid && _ownerInventories.Length > 1);
      _good.choices = valid ? snapshot.Resources.Select(g => g.DisplayName + global::TimberbornTrainer.Localization.TextToken.Key("t0100") + g.GoodId + global::TimberbornTrainer.Localization.TextToken.Key("t0103")).ToList() : new List<string>();
      _resourceIds = valid ? snapshot.Resources.Select(g => g.GoodId).ToArray() : Array.Empty<string>();
      if (_good.choices.Count == 0) _good.choices.Add(global::TimberbornTrainer.Localization.TextToken.Key("t0266"));
      int goodIndex = valid ? Array.FindIndex(snapshot.Resources, r => r.GoodId == _browser.SelectedGoodId) : -1;
      if (valid && goodIndex < 0 && snapshot.Resources.Length > 0) {
        _good.choices.Insert(0, global::TimberbornTrainer.Localization.TextToken.Key("t0442"));
        _resourceIds = new string[] { null }.Concat(_resourceIds).ToArray();
      }
      _good.SetValueWithoutNotify(_good.choices[Math.Max(0, goodIndex)]); _good.SetEnabled(valid && snapshot.Resources.Length > 0);
      _painting = false;
      _target.text = valid ? snapshot.Target.OwnerName + " · " + snapshot.Target.DisplayName + "\n" + snapshot.Target.Purpose : global::TimberbornTrainer.Localization.TextToken.Key("t0159");
      _target.tooltip = valid ? snapshot.Target.Id : "";
      _focusTarget.Available(valid && snapshot.Target.Source != InventorySource.Settlement);
      var abilities = valid ? snapshot.Target.Abilities : new InventoryAbilities(false, global::TimberbornTrainer.Localization.TextToken.Key("t0336"));
      _abilities.text = global::TimberbornTrainer.Localization.TextToken.Key("t0178") + Ability(abilities.Read)
        + global::TimberbornTrainer.Localization.TextToken.Key("t0002");
      RenderResource();
    }
    private static string Ability(InventoryAbility ability) => (ability.Available ? global::TimberbornTrainer.Localization.TextToken.Key("t0246") : global::TimberbornTrainer.Localization.TextToken.Key("t0118")) + ability.Reason;
    private static string Number(long? value) => value.HasValue ? value.Value.ToString() : global::TimberbornTrainer.Localization.TextToken.Key("t0121");
    private void Edit(InventoryWriteOperation operation) {
      if (_disposed || _editor == null || !_commandsEnabled) return;
      string target = _browser.Snapshot?.Target?.Id, resource = _browser.SelectedGoodId;
      if (target == null || resource == null) { _editResult.text = global::TimberbornTrainer.Localization.TextToken.Key("t0311"); return; }
      var result = _editor.Execute(target, resource, operation, _editAmount.value);
      string feedback = global::TimberbornTrainer.Localization.TextToken.Key("t0465") + resource + global::TimberbornTrainer.Localization.TextToken.Key("t0090") + EditState(result.State) + "\n" + result.Message
        + global::TimberbornTrainer.Localization.TextToken.Key("t0013") + (result.Before?.ToString() ?? global::TimberbornTrainer.Localization.TextToken.Key("t0376")) + global::TimberbornTrainer.Localization.TextToken.Key("t0062") + result.RequestedDelta
        + global::TimberbornTrainer.Localization.TextToken.Key("t0089") + (result.After?.ToString() ?? global::TimberbornTrainer.Localization.TextToken.Key("t0376"))
        + global::TimberbornTrainer.Localization.TextToken.Key("t0001") + (result.MaximumAdd?.ToString() ?? global::TimberbornTrainer.Localization.TextToken.Key("t0376"))
        + global::TimberbornTrainer.Localization.TextToken.Key("t0052") + (result.MaximumRemove?.ToString() ?? global::TimberbornTrainer.Localization.TextToken.Key("t0376"));
      _browser.Select(target); RenderTargets();
      _editResult.text = feedback;
    }
    private static string EditState(InventoryWriteState state) => state == InventoryWriteState.Succeeded ? global::TimberbornTrainer.Localization.TextToken.Key("t0175")
      : state == InventoryWriteState.NoOp ? global::TimberbornTrainer.Localization.TextToken.Key("t0390") : state == InventoryWriteState.Indeterminate ? global::TimberbornTrainer.Localization.TextToken.Key("t0219") : global::TimberbornTrainer.Localization.TextToken.Key("t0233");
    private void Diagnose() {
      if (_disposed || _diagnostics == null) return;
      var snapshot = _browser.Snapshot;
      if (snapshot == null || !snapshot.Valid || _browser.SelectedGoodId == null) {
        _safety.text = global::TimberbornTrainer.Localization.TextToken.Key("t0310"); return;
      }
      var report = _diagnostics.DiagnoseSafety(snapshot.Target.Id, _browser.SelectedGoodId);
      string identity = report.Identity.Status == InventoryIdentityStatus.Valid ? global::TimberbornTrainer.Localization.TextToken.Key("t0115")
        : report.Identity.Status == InventoryIdentityStatus.Invalid ? global::TimberbornTrainer.Localization.TextToken.Key("t0389") : global::TimberbornTrainer.Localization.TextToken.Key("t0376");
      _safety.text = global::TimberbornTrainer.Localization.TextToken.Key("t0458") + report.ObservedUtc.ToLocalTime().ToString("HH:mm:ss")
        + global::TimberbornTrainer.Localization.TextToken.Key("t0009") + identity + global::TimberbornTrainer.Localization.TextToken.Key("t0081") + snapshot.Target.Purpose
        + global::TimberbornTrainer.Localization.TextToken.Key("t0003") + report.Identity.District + global::TimberbornTrainer.Localization.TextToken.Key("t0092") + report.Identity.Resource
        + global::TimberbornTrainer.Localization.TextToken.Key("t0012") + Number(report.ReservedStock) + global::TimberbornTrainer.Localization.TextToken.Key("t0064") + Number(report.ReservedCapacity)
        + global::TimberbornTrainer.Localization.TextToken.Key("t0019") + report.Rows.Count + global::TimberbornTrainer.Localization.TextToken.Key("t0029") + (report.ObservationConsistent ? global::TimberbornTrainer.Localization.TextToken.Key("t0351") : global::TimberbornTrainer.Localization.TextToken.Key("t0187"))
        + global::TimberbornTrainer.Localization.TextToken.Key("t0072")
        + "\n" + report.Identity.Reason + "\n" + report.Reason;
      _safety.tooltip = string.Join("\n", report.Rows.Select(r => r.Resource + global::TimberbornTrainer.Localization.TextToken.Key("t0094") + r.Amount
        + global::TimberbornTrainer.Localization.TextToken.Key("t0033") + r.Unreserved + global::TimberbornTrainer.Localization.TextToken.Key("t0035") + r.Reserved + global::TimberbornTrainer.Localization.TextToken.Key("t0031") + r.CapacityReservation
        + global::TimberbornTrainer.Localization.TextToken.Key("t0034") + r.FreeCapacity + global::TimberbornTrainer.Localization.TextToken.Key("t0038") + r.Limit
        + global::TimberbornTrainer.Localization.TextToken.Key("t0032") + (r.Declared ? global::TimberbornTrainer.Localization.TextToken.Key("t0351") : global::TimberbornTrainer.Localization.TextToken.Key("t0187")) + global::TimberbornTrainer.Localization.TextToken.Key("t0036") + (r.Registered ? global::TimberbornTrainer.Localization.TextToken.Key("t0351") : global::TimberbornTrainer.Localization.TextToken.Key("t0187"))));
    }
    private void RenderResource() {
      _editResult.text = global::TimberbornTrainer.Localization.TextToken.Key("t0340");
      _safety.text = global::TimberbornTrainer.Localization.TextToken.Key("t0110");
      _safety.tooltip = "";
      var snapshot = _browser.Snapshot;
      var row = snapshot?.Resources.FirstOrDefault(r => r.GoodId == _browser.SelectedGoodId);
      _targetEditable = !_disposed && _editor != null && snapshot != null && snapshot.Valid &&
        snapshot.Target.Source != InventorySource.Settlement && row != null && row.Registered;
      _resourceAllowed = row != null && row.Allowed;
      string reason = null;
      if (_targetEditable && _editor is IInventoryEditPreview preview) {
        reason = preview.ReadOnlyReason(snapshot.Target.Id, row.GoodId);
        _targetEditable = reason == null;
      }
      PaintEditButtons();
      if (reason != null) _abilities.text = global::TimberbornTrainer.Localization.TextToken.Key("t0226") + reason;
      if (snapshot == null || !snapshot.Valid || row == null) {
        _stock.text = snapshot?.Valid == true ? global::TimberbornTrainer.Localization.TextToken.Key("t0413") + snapshot.Message : "";
        return;
      }
      bool aggregate = snapshot.Target.Source == InventorySource.Settlement;
      if (aggregate) _abilities.text = global::TimberbornTrainer.Localization.TextToken.Key("t0238");
      string capacity = row.CapacityState == CapacityState.Aggregate ? global::TimberbornTrainer.Localization.TextToken.Key("t0363")
        : row.CapacityState == CapacityState.Unlimited ? global::TimberbornTrainer.Localization.TextToken.Key("t0430")
        : row.CapacityState == CapacityState.Finite ? global::TimberbornTrainer.Localization.TextToken.Key("t0419") + Number(row.TotalCapacity)
        : global::TimberbornTrainer.Localization.TextToken.Key("t0388") + Number(row.TotalCapacity);
      _stock.text = (aggregate ? global::TimberbornTrainer.Localization.TextToken.Key("t0364") : global::TimberbornTrainer.Localization.TextToken.Key("t0452")) + row.Amount
        + "\n" + (aggregate ? global::TimberbornTrainer.Localization.TextToken.Key("t0428") : global::TimberbornTrainer.Localization.TextToken.Key("t0370")) + Number(row.AvailableStock)
        + global::TimberbornTrainer.Localization.TextToken.Key("t0079") + Number(row.ReservedStock) + global::TimberbornTrainer.Localization.TextToken.Key("t0015") + Number(row.ReservedCapacity)
        + global::TimberbornTrainer.Localization.TextToken.Key("t0054") + Number(row.FreeCapacity) + global::TimberbornTrainer.Localization.TextToken.Key("t0008") + capacity
        + global::TimberbornTrainer.Localization.TextToken.Key("t0043") + Number(row.ResourceLimit)
        + (aggregate ? "" : global::TimberbornTrainer.Localization.TextToken.Key("t0018") + (row.Registered ? global::TimberbornTrainer.Localization.TextToken.Key("t0351") : global::TimberbornTrainer.Localization.TextToken.Key("t0187")) + global::TimberbornTrainer.Localization.TextToken.Key("t0056") + (row.Allowed ? global::TimberbornTrainer.Localization.TextToken.Key("t0351") : global::TimberbornTrainer.Localization.TextToken.Key("t0187")))
        + "\n" + row.Details;
    }
    internal void SetCommandsEnabled(bool enabled) {
      _commandsEnabled = enabled;
      PaintEditButtons(); // No inventory reads in the general UI refresh loop.
    }
    private void PaintEditButtons() {
      bool enabled = !_disposed && _commandsEnabled && _targetEditable;
      _add.Available(enabled && _resourceAllowed);
      _remove.Available(enabled); _set.Available(enabled); _clear.Available(enabled);
    }
    internal void Unload() {
      if (_disposed) return;
      _disposed = true; _browser.Dispose(); _ownerInventories = Array.Empty<InventoryTarget>(); _buildings.Clear();
      _resourceIds = Array.Empty<string>();
      _inventory.choices.Clear(); _good.choices.Clear();
    }
  }
}
