using System;
using System.Linq;

namespace TimberbornTrainer.Modules {
  // All scanning is explicit. List discovery reads the current scene directory only.
  public sealed class InventoryBrowser : IDisposable {
    private IInventoryCatalog _catalog;
    private bool _resourceCleared;
    public InventorySource Source { get; private set; }
    public InventoryTarget[] Targets { get; private set; } = Array.Empty<InventoryTarget>();
    public InventorySnapshot Snapshot { get; private set; }
    public string SelectedGoodId { get; private set; }
    public InventoryScanReport LastScan => _catalog?.LastScan;
    public string Message { get; private set; } = "请主动扫描资源与库存。";
    public bool Opened { get; private set; }
    public InventoryBrowser(IInventoryCatalog catalog) { _catalog = catalog; }
    public void Switch(InventorySource source) {
      if (_catalog == null) return;
      if (!Opened || Source != source) Reset();
      Source = source; Opened = true; Discover();
    }
    public void Scan() {
      if (_catalog == null) return;
      try {
        var report = _catalog.Scan();
        if (!report.Completed) { Reset(); Message = report.Message; return; }
        Discover();
      } catch { Reset(); _catalog.Clear(); Message = "扫描失败，已清理目录，请主动重试。"; }
    }
    public void Discover() {
      if (_catalog == null) return;
      string previous = Snapshot?.Target?.Id;
      try {
        var result = _catalog.Discover(Source);
        Targets = result.Targets ?? Array.Empty<InventoryTarget>(); Message = result.Message;
        if (previous != null && Targets.Any(t => t.Id == previous)) Select(previous);
        else {
          ClearSelection();
          if (previous != null) Message = "目录中原库存目标已失效或被安全排除，已清除目标和资源选择。";
          else if (Source == InventorySource.Settlement && Targets.Length == 1) Select(Targets[0].Id);
        }
      } catch { Reset(); Message = "读取扫描目录失败，请主动重新扫描。"; }
    }
    public void Select(string id) {
      if (_catalog == null) return;
      if (!Targets.Any(t => t.Id == id)) { ClearSelection(); Message = "目标不在当前扫描目录中，请重新扫描。"; return; }
      string previousTarget = Snapshot?.Target?.Id;
      string previousGood = SelectedGoodId;
      if (previousTarget != id) { previousGood = null; _resourceCleared = false; }
      try {
        var snapshot = _catalog.Read(id);
        if (!snapshot.Valid) { RemoveSelection(id); Message = snapshot.Message; return; }
        Snapshot = snapshot; Message = snapshot.Message;
        if (previousGood != null && snapshot.Resources.Any(r => r.GoodId == previousGood)) SelectedGoodId = previousGood;
        else if (previousGood != null) {
          SelectedGoodId = null; _resourceCleared = true;
          Message += " 当前资源已移除或不再属于该库存，已清除资源选择；库存目标保留。";
        } else SelectedGoodId = _resourceCleared ? null : snapshot.Resources.FirstOrDefault()?.GoodId;
      } catch { RemoveSelection(id); Message = "目标库存读取异常，已清除该选择及引用，请重新扫描。"; }
    }
    public void SelectGood(string id) {
      if (Snapshot == null || !Snapshot.Valid) return;
      if (id != null && Snapshot.Resources.Any(r => r.GoodId == id)) { SelectedGoodId = id; _resourceCleared = false; }
      else { SelectedGoodId = null; _resourceCleared = true; Message = "资源已失效或不在当前库存快照中，请主动刷新。"; }
    }
    public bool Focus(string id) {
      if (_catalog == null || !Targets.Any(t => t.Id == id)) return false;
      try { if (_catalog.Focus(id)) return true; } catch { }
      RemoveSelection(id); Message = "目标已失效或不支持原生定位，已清除该选择，请重新扫描。"; return false;
    }
    private void RemoveSelection(string id) { Targets = Targets.Where(t => t.Id != id).ToArray(); ClearSelection(); }
    private void ClearSelection() { Snapshot = null; SelectedGoodId = null; _resourceCleared = false; }
    public void Reset() { Targets = Array.Empty<InventoryTarget>(); ClearSelection(); }
    public void Dispose() { Reset(); _catalog?.Clear(); _catalog = null; Opened = false; }
  }
}