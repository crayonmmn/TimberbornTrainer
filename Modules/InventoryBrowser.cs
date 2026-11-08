using System;
using System.Linq;

namespace TimberbornTrainer.Modules {
  // No timers, Tick callbacks, write commands or permanent settlement inventory cache.
  public sealed class InventoryBrowser : IDisposable {
    private IInventoryCatalog _catalog;
    public InventorySource Source { get; private set; }
    public InventoryTarget[] Targets { get; private set; } = Array.Empty<InventoryTarget>();
    public InventorySnapshot Snapshot { get; private set; }
    public string Message { get; private set; } = "打开资源页面后按需读取。";
    public bool Opened { get; private set; }
    public InventoryBrowser(IInventoryCatalog catalog) { _catalog = catalog; }
    public void Switch(InventorySource source) {
      if (_catalog == null) return;
      Reset(); Source = source; Opened = true;
      if (source == InventorySource.Building) { Message = "请主动打开建筑列表；当前没有扫描建筑。"; return; }
      Discover();
    }
    public void Discover() {
      if (_catalog == null) return;
      string previous = Snapshot?.Target?.Id;
      Targets = Array.Empty<InventoryTarget>(); Snapshot = null;
      _catalog.Clear();
      try {
        var result = _catalog.Discover(Source);
        Targets = result.Targets ?? Array.Empty<InventoryTarget>(); Message = result.Message;
        if (previous != null && Targets.Any(t => t.Id == previous)) Select(previous);
        else if (Source == InventorySource.Settlement && Targets.Length == 1) Select(Targets[0].Id);
      } catch { _catalog.Clear(); Targets = Array.Empty<InventoryTarget>(); Message = "发现库存失败；未执行任何写入。"; }
    }
    public void Select(string id) {
      Snapshot = null;
      if (_catalog == null || !Targets.Any(t => t.Id == id)) { Message = "选择已失效，请重新打开列表。"; return; }
      try {
        Snapshot = _catalog.Read(id);
        Message = Snapshot.Message;
        if (!Snapshot.Valid) { Reset(); Message = "库存已失效或尚未初始化，请重新打开列表。"; }
      } catch { Reset(); Message = "库存读取失败，已清除对象引用；请重新打开列表。"; }
    }
    public bool Focus(string id) {
      if (_catalog == null || !Targets.Any(t => t.Id == id)) return false;
      try {
        if (_catalog.Focus(id)) return true;
        Reset(); Message = "建筑无法定位，已清除选择；请重新打开列表。"; return false;
      } catch { Reset(); Message = "建筑无法定位，可能已经删除或不支持选择。"; return false; }
    }
    public void Reset() { Targets = Array.Empty<InventoryTarget>(); Snapshot = null; _catalog?.Clear(); }
    public void Dispose() { Reset(); _catalog = null; Opened = false; }
  }
}
