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
        _events.Add("序号=" + (_events.Count + 1) + "；场景=" + _scene + "；事件=" + kind
          + "；线程=" + Thread.CurrentThread.ManagedThreadId + "；探针Tick=" + tick
          + "；单调时间=" + now + "；频率=" + Stopwatch.Frequency + "；原生并行状态=未知");
      }
    }
    public string Stop() {
      lock (_gate) { Record("Trace.Stop", 0); _active = false; return Summary(); }
    }
    public string Summary() {
      lock (_gate) { return "只读时序记录 " + _events.Count + "/" + Limit
        + " 条，最长10秒；不证明原生库存互斥。\n" + string.Join("\n", _events); }
    }
    public void Clear() { lock (_gate) { _active = false; _scene = null; _events.Clear(); } }
  }
  public interface IInventoryTimingDiagnostics {
    void StartTiming();
    string StopTiming();
  }
}
