using System;
using System.Collections.Generic;

namespace TimberbornTrainer.Modules {
  public interface INeedValue {
    float Read();
    void AddEffect(float points);
  }

  // Maintain the favorable value captured on activation. Never disables native updates.
  public sealed class NeedLock<T> {
    private readonly Dictionary<T, float> _floors = new Dictionary<T, float>();
    public int Count => _floors.Count;
    public void Clear() => _floors.Clear();
    public void Prune(HashSet<T> present) {
      var removed = new List<T>();
      foreach (var key in _floors.Keys) if (!present.Contains(key)) removed.Add(key);
      foreach (var key in removed) _floors.Remove(key);
    }
    public bool Maintain(T key, INeedValue value) {
      float current = value.Read();
      if (float.IsNaN(current) || float.IsInfinity(current)) throw new InvalidOperationException("需求数值异常。");
      float floor;
      if (!_floors.TryGetValue(key, out floor)) {
        // Locking a starving/dehydrated value is not a rescue operation.
        if (current <= 0) return false;
        _floors.Add(key, current);
        return true;
      }
      if (current < floor) {
        value.AddEffect(floor - current);
        float after = value.Read();
        if (float.IsNaN(after) || float.IsInfinity(after) || Math.Abs(after - floor) > .0001f)
          throw new InvalidOperationException("需求效果未达到预期，已暂停锁定。");
      }
      return true;
    }
  }
}
