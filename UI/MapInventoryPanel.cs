using System.Linq;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using TimberbornTrainer.Modules;
using UnityEngine.UIElements;

namespace TimberbornTrainer.UI {
  internal sealed class MapInventoryPanel : VisualElement {
    private readonly IMapInventorySource _source;
    private readonly IInventoryEditor _editor;
    private readonly EventBus _events;
    private readonly LocalizedLabel _target, _stock, _result;
    private readonly TextField _amount;
    private readonly ActionButton _add, _remove;
    private InventorySnapshot _snapshot;
    private bool _active, _disposed, _commandsEnabled;
    internal MapInventoryPanel(IMapInventorySource source, IInventoryEditor editor, EventBus events) {
      _source = source; _editor = editor; _events = events;
      _target = ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0317")); _target.name = "MapInventoryTarget"; Add(_target);
      _stock = ControlStyles.Text(""); _stock.name = "MapInventoryStock"; Add(_stock);
      _amount = ControlStyles.Input(global::TimberbornTrainer.Localization.TextToken.Key("t0450"), "10"); _amount.name = "MapInventoryAmount"; Add(_amount);
      var row = ControlStyles.Row();
      _add = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0446"), () => Edit(InventoryWriteOperation.Add)) { name = "MapInventoryAdd" }; row.Add(_add);
      _remove = new ActionButton(global::TimberbornTrainer.Localization.TextToken.Key("t0208"), () => Edit(InventoryWriteOperation.Remove)) { name = "MapInventoryRemove" }; row.Add(_remove); Add(row);
      _result = ControlStyles.Text(global::TimberbornTrainer.Localization.TextToken.Key("t0223")); _result.name = "MapInventoryResult"; Add(_result);
      Paint();
    }
    internal void Open() { if (_disposed) return; if (!_active) { _events.Register(this); _active = true; } RefreshSelection(); }
    internal void Close() {
      if (_active) _events.Unregister(this); _active = false;
      _source.ReleaseSelectedWarehouse(); _snapshot = null; Paint();
    }
    [OnEvent] public void OnSelected(SelectableObjectSelectedEvent e) { if (_active) RefreshSelection(); }
    [OnEvent] public void OnUnselected(SelectableObjectUnselectedEvent e) { if (_active) RefreshSelection(); }
    internal void RefreshSelection() {
      if (!_active || _disposed) return;
      var next = _source.ReadSelectedWarehouse();
      if (_snapshot?.Target?.Id != next.Target?.Id || _snapshot?.Resources.FirstOrDefault()?.GoodId != next.Resources.FirstOrDefault()?.GoodId)
        _result.text = global::TimberbornTrainer.Localization.TextToken.Key("t0172");
      _snapshot = next; Paint();
    }
    internal void SetCommandsEnabled(bool enabled) { _commandsEnabled = enabled; Paint(); }
    private void Paint() {
      var resource = _snapshot?.Resources.SingleOrDefault();
      bool valid = _active && _snapshot?.Valid == true && resource != null;
      string reason = valid && _editor is IInventoryEditPreview preview ? preview.ReadOnlyReason(_snapshot.Target.Id, resource.GoodId) : null;
      _target.text = valid ? _snapshot.Target.OwnerName + global::TimberbornTrainer.Localization.TextToken.Key("t0011") + resource.DisplayName + global::TimberbornTrainer.Localization.TextToken.Key("t0100") + resource.GoodId + global::TimberbornTrainer.Localization.TextToken.Key("t0103") : _snapshot?.Message ?? global::TimberbornTrainer.Localization.TextToken.Key("t0317");
      _stock.text = valid ? global::TimberbornTrainer.Localization.TextToken.Key("t0162") + resource.Amount + global::TimberbornTrainer.Localization.TextToken.Key("t0085") + resource.TotalCapacity +
        global::TimberbornTrainer.Localization.TextToken.Key("t0005") + resource.FreeCapacity + global::TimberbornTrainer.Localization.TextToken.Key("t0051") + resource.AvailableStock +
        (reason == null ? "" : global::TimberbornTrainer.Localization.TextToken.Key("t0016") + reason) + (_commandsEnabled ? "" : global::TimberbornTrainer.Localization.TextToken.Key("t0007")) : "";
      _add.Available(valid && reason == null); _remove.Available(valid && reason == null);
    }
    private void Edit(InventoryWriteOperation operation) {
      if (!_active || _disposed) return;
      if (!_commandsEnabled) {
        _result.text = global::TimberbornTrainer.Localization.TextToken.Key("t0309");
        _result.style.color = ControlStyles.Error; return;
      }
      string id = _snapshot?.Target?.Id, good = _snapshot?.Resources.SingleOrDefault()?.GoodId;
      RefreshSelection();
      if (id == null || good == null || id != _snapshot?.Target?.Id || good != _snapshot?.Resources.SingleOrDefault()?.GoodId) {
        _result.text = global::TimberbornTrainer.Localization.TextToken.Key("t0171"); return;
      }
      var result = _editor.Execute(id, good, operation, _amount.value);
      RefreshSelection();
      string state = result.State == InventoryWriteState.Succeeded ? global::TimberbornTrainer.Localization.TextToken.Key("t0144") : result.State == InventoryWriteState.Indeterminate
        ? global::TimberbornTrainer.Localization.TextToken.Key("t0218") : result.State == InventoryWriteState.NoOp ? global::TimberbornTrainer.Localization.TextToken.Key("t0390") : global::TimberbornTrainer.Localization.TextToken.Key("t0379");
      _result.text = state + global::TimberbornTrainer.Localization.TextToken.Key("t0093") + result.Message + global::TimberbornTrainer.Localization.TextToken.Key("t0013") + result.Before + global::TimberbornTrainer.Localization.TextToken.Key("t0077") + result.After;
      _result.style.color = result.State == InventoryWriteState.Succeeded ? ControlStyles.Accent : ControlStyles.Error;
    }
    internal void Unload() { Close(); _disposed = true; }
  }
}
