using Timberborn.TickSystem;

namespace TimberbornTrainer.Modules {
  public sealed partial class NativeInventoryCatalog : ITickableSingleton, IInventoryTimingDiagnostics {
    private readonly InventoryTimingTrace _timing = new InventoryTimingTrace();
    private long _observedTicks;
    public void StartTiming() { if (_ready) _timing.Start(_scene); }
    public string StopTiming() {
      string summary = _timing.Stop();
      UnityEngine.Debug.Log("海狸修改工具：" + summary);
      return summary;
    }
    public void Tick() {
      if (!_ready) return;
      ++_observedTicks;
      _timing.Record("ITickableSingleton.Tick.Enter", _observedTicks);
      // Deliberately no inventory reads/writes, no queue execution, no forced Tick or wait.
      _timing.Record("ITickableSingleton.Tick.Exit", _observedTicks);
    }
  }
}
