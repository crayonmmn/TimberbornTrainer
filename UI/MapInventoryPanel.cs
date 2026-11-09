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
    private readonly Label _target, _stock, _result;
    private readonly TextField _amount;
    private readonly ActionButton _add, _remove;
    private InventorySnapshot _snapshot;
    private bool _active, _disposed, _commandsEnabled;
    internal MapInventoryPanel(IMapInventorySource source, IInventoryEditor editor, EventBus events) {
      _source = source; _editor = editor; _events = events;
      _target = ControlStyles.Text("请在地图中选择一个仓库。"); _target.name = "MapInventoryTarget"; Add(_target);
      _stock = ControlStyles.Text(""); _stock.name = "MapInventoryStock"; Add(_stock);
      _amount = ControlStyles.Input("增减数量", "10"); _amount.name = "MapInventoryAmount"; Add(_amount);
      var row = ControlStyles.Row();
      _add = new ActionButton("增加", () => Edit(InventoryWriteOperation.Add)) { name = "MapInventoryAdd" }; row.Add(_add);
      _remove = new ActionButton("减少", () => Edit(InventoryWriteOperation.Remove)) { name = "MapInventoryRemove" }; row.Add(_remove); Add(row);
      _result = ControlStyles.Text("仅操作地图当前选中的仓库；请先暂停游戏并启用全局修改器。"); _result.name = "MapInventoryResult"; Add(_result);
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
        _result.text = "地图选择或物品配置已变化，尚未执行本目标的新操作。";
      _snapshot = next; Paint();
    }
    internal void SetCommandsEnabled(bool enabled) { _commandsEnabled = enabled; Paint(); }
    private void Paint() {
      var resource = _snapshot?.Resources.SingleOrDefault();
      bool valid = _active && _snapshot?.Valid == true && resource != null;
      string reason = valid && _editor is IInventoryEditPreview preview ? preview.ReadOnlyReason(_snapshot.Target.Id, resource.GoodId) : null;
      _target.text = valid ? _snapshot.Target.OwnerName + "\n物品：" + resource.DisplayName + "（" + resource.GoodId + "）" : _snapshot?.Message ?? "请在地图中选择一个仓库。";
      _stock.text = valid ? "当前数量：" + resource.Amount + "；原生容量：" + resource.TotalCapacity +
        "\n可增加：" + resource.FreeCapacity + "；可减少（未预留）：" + resource.AvailableStock +
        (reason == null ? "" : "\n只读：" + reason) : "";
      _add.Available(valid && reason == null && _commandsEnabled); _remove.Available(valid && reason == null && _commandsEnabled);
    }
    private void Edit(InventoryWriteOperation operation) {
      if (!_active || _disposed || !_commandsEnabled) return;
      string id = _snapshot?.Target?.Id, good = _snapshot?.Resources.SingleOrDefault()?.GoodId;
      RefreshSelection();
      if (id == null || good == null || id != _snapshot?.Target?.Id || good != _snapshot?.Resources.SingleOrDefault()?.GoodId) {
        _result.text = "地图选择或存储配置已改变，未执行旧目标命令。"; return;
      }
      var result = _editor.Execute(id, good, operation, _amount.value);
      RefreshSelection();
      string state = result.State == InventoryWriteState.Succeeded ? "成功" : result.State == InventoryWriteState.Indeterminate
        ? "结果未确认" : result.State == InventoryWriteState.NoOp ? "无需修改" : "未执行";
      _result.text = state + "：" + result.Message + "\n修改前：" + result.Before + "；修改后真实回读：" + result.After;
      _result.style.color = result.State == InventoryWriteState.Succeeded ? ControlStyles.Accent : ControlStyles.Error;
    }
    internal void Unload() { Close(); _disposed = true; }
  }
}
