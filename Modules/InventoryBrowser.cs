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
    public string Message { get; private set; } = global::TimberbornTrainer.Localization.TextToken.Key("t0318");
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
      } catch { Reset(); _catalog.Clear(); Message = global::TimberbornTrainer.Localization.TextToken.Key("t0330"); }
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
          if (previous != null) Message = global::TimberbornTrainer.Localization.TextToken.Key("t0284");
          else if (Source == InventorySource.Settlement && Targets.Length == 1) Select(Targets[0].Id);
        }
      } catch { Reset(); Message = global::TimberbornTrainer.Localization.TextToken.Key("t0186"); }
    }
    public void Select(string id) {
      if (_catalog == null) return;
      if (!Targets.Any(t => t.Id == id)) { ClearSelection(); Message = global::TimberbornTrainer.Localization.TextToken.Key("t0273"); return; }
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
          Message += global::TimberbornTrainer.Localization.TextToken.Key("t0023");
        } else SelectedGoodId = _resourceCleared ? null : snapshot.Resources.FirstOrDefault()?.GoodId;
      } catch { RemoveSelection(id); Message = global::TimberbornTrainer.Localization.TextToken.Key("t0276"); }
    }
    public void SelectGood(string id) {
      if (Snapshot == null || !Snapshot.Valid) return;
      if (id != null && Snapshot.Resources.Any(r => r.GoodId == id)) { SelectedGoodId = id; _resourceCleared = false; }
      else { SelectedGoodId = null; _resourceCleared = true; Message = global::TimberbornTrainer.Localization.TextToken.Key("t0473"); }
    }
    public bool Focus(string id) {
      if (_catalog == null || !Targets.Any(t => t.Id == id)) return false;
      try { if (_catalog.Focus(id)) return true; } catch { }
      RemoveSelection(id); Message = global::TimberbornTrainer.Localization.TextToken.Key("t0281"); return false;
    }
    private void RemoveSelection(string id) { Targets = Targets.Where(t => t.Id != id).ToArray(); ClearSelection(); }
    private void ClearSelection() { Snapshot = null; SelectedGoodId = null; _resourceCleared = false; }
    public void Reset() { Targets = Array.Empty<InventoryTarget>(); ClearSelection(); }
    public void Dispose() { Reset(); _catalog?.Clear(); _catalog = null; Opened = false; }
  }
}