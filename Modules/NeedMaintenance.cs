using System;
using System.Collections.Generic;
using System.Linq;

namespace TimberbornTrainer.Modules {
  public interface IMaintainedNeed {
    object Owner { get; }
    bool Valid { get; }
    bool Enabled { get; }
    bool UpdateEnabled { get; }
    float Points { get; }
    float Maximum { get; }
    float Starting { get; }
    void Reset();
    void EnableUpdate();
    void DisableUpdate();
  }

  // Native per-need update control, no additive effects or saved reserve/floor.
  public sealed class NeedMaintenance<T> {
    private sealed class Entry { public IMaintainedNeed Need; public bool WasUpdating; }
    private readonly Dictionary<T, Entry> _entries = new Dictionary<T, Entry>();
    public int Count => _entries.Count;
    public bool Maintain(T key, object owner, Func<IMaintainedNeed> create) {
      if (_entries.TryGetValue(key, out var entry) && !ReferenceEquals(entry.Need.Owner, owner)) {
        _entries.Remove(key); Restore(entry); entry = null;
      }
      if (entry == null) {
        var need = create();
        if (!need.Valid || !need.Enabled || !Finite(need.Maximum) || need.Maximum <= 0 || !Finite(need.Starting) ||
            Math.Abs(need.Starting - need.Maximum) > .0001f || !Finite(need.Points)) return false;
        entry = new Entry { Need = need, WasUpdating = need.UpdateEnabled };
        _entries.Add(key, entry); // Before Reset: its native state events may disable/unload this module.
      }
      if (!entry.Need.Valid || !entry.Need.Enabled) { _entries.Remove(key); Restore(entry); return false; }
      if (entry.Need.UpdateEnabled) entry.Need.DisableUpdate();
      if (!Finite(entry.Need.Points)) throw new InvalidOperationException("需求值无效。");
      if (Math.Abs(entry.Need.Points - entry.Need.Maximum) > .0001f) entry.Need.Reset();
      if (!_entries.TryGetValue(key, out var current) || !ReferenceEquals(current, entry)) return true;
      if (!entry.Need.Valid || !Finite(entry.Need.Points) || Math.Abs(entry.Need.Points - entry.Need.Maximum) > .0001f)
        throw new InvalidOperationException("原生需求重置未达到正常上限。");
      return true;
    }
    public void Prune(HashSet<T> present) {
      foreach (var key in _entries.Keys.Where(k => !present.Contains(k)).ToArray()) {
        var entry = _entries[key]; _entries.Remove(key); Restore(entry);
      }
    }
    public void Clear() {
      var entries = _entries.Values.ToArray(); _entries.Clear();
      foreach (var entry in entries) Restore(entry);
    }
    private static void Restore(Entry entry) {
      if (!entry.Need.Valid) return;
      if (entry.WasUpdating) entry.Need.EnableUpdate(); else entry.Need.DisableUpdate();
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
  }
}
