using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace TimberbornTrainer.Modules {
  // Bounded opt-in observation, never a scheduler, queue, barrier or execution permit.
  public sealed class InventoryTimingTrace {
    private readonly List<string> _events = new List<string>();
    private readonly object _gate = new object();
    private long _started;
    private string _scene;
    private bool _active;
    private const int Limit = 256;
    public void Start(string scene) {
      lock (_gate) {
        _events.Clear(); _scene = scene; _started = Stopwatch.GetTimestamp(); _active = !string.IsNullOrEmpty(scene);
        Record("Trace.Start", 0);
      }
    }
    public void Record(string kind, long tick) {
      lock (_gate) {
        if (!_active) return;
        long now = Stopwatch.GetTimestamp();
        if (_events.Count >= Limit || (now - _started) / (double)Stopwatch.Frequency >= 10) { _active = false; return; }
        _events.Add(global::TimberbornTrainer.Localization.TextToken.Key("t0404") + (_events.Count + 1) + global::TimberbornTrainer.Localization.TextToken.Key("t0041") + _scene + global::TimberbornTrainer.Localization.TextToken.Key("t0068") + kind
          + global::TimberbornTrainer.Localization.TextToken.Key("t0075") + Thread.CurrentThread.ManagedThreadId + global::TimberbornTrainer.Localization.TextToken.Key("t0071") + tick
          + global::TimberbornTrainer.Localization.TextToken.Key("t0042") + now + global::TimberbornTrainer.Localization.TextToken.Key("t0060") + Stopwatch.Frequency + global::TimberbornTrainer.Localization.TextToken.Key("t0083"));
      }
    }
    public string Stop() {
      lock (_gate) { Record("Trace.Stop", 0); _active = false; return Summary(); }
    }
    public string Summary() {
      lock (_gate) { return global::TimberbornTrainer.Localization.TextToken.Key("t0456") + _events.Count + "/" + Limit
        + global::TimberbornTrainer.Localization.TextToken.Key("t0028") + string.Join("\n", _events); }
    }
    public void Clear() { lock (_gate) { _active = false; _scene = null; _events.Clear(); } }
  }
  public interface IInventoryTimingDiagnostics {
    void StartTiming();
    string StopTiming();
  }
}
