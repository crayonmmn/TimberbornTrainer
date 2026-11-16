using System;
using System.Globalization;

namespace TimberbornTrainer.Modules {
  public sealed class InventoryEditResult {
    public InventoryWriteState State { get; internal set; } = InventoryWriteState.Rejected;
    public int? Before { get; internal set; }
    public int? After { get; internal set; }
    public long RequestedDelta { get; internal set; }
    public int? MaximumAdd { get; internal set; }
    public int? MaximumRemove { get; internal set; }
    public string Message { get; internal set; } = global::TimberbornTrainer.Localization.TextToken.Key("t0380");
    public bool NativeCallStarted { get; internal set; }
    public bool AutomaticRetryAllowed => false;
  }
  public interface IInventoryEditor {
    InventoryEditResult Execute(string targetId, string resource, InventoryWriteOperation operation, string input);
  }
  // UI hint only. Every command still performs its own live identity/capacity/reservation checks.
  public interface IInventoryEditPreview {
    string ReadOnlyReason(string targetId, string resource);
  }

  // Whole-request validation. No clipping, retries, reverse writes, UI cache or global-zero-reservation policy.
  public static class InventoryEditCommands {
    public static InventoryEditResult Execute(Func<IWarehouseStock> resolve, Func<bool> permitted,
        InventoryWriteOperation operation, string input) {
      var r = new InventoryEditResult();
      IWarehouseStock stock = null;
      try {
        if (!permitted()) { r.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0303"); return r; }
        if (!Enum.IsDefined(typeof(InventoryWriteOperation), operation)) { r.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0378"); return r; }
        int n = 0;
        if (operation != InventoryWriteOperation.Clear && (input == null || input.Length > 32
            || !int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out n))) {
          r.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0306"); return r;
        }
        if ((operation == InventoryWriteOperation.Add || operation == InventoryWriteOperation.Remove) && n == 0) {
          r.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0448"); return r;
        }
        stock = resolve();
        if (stock == null || !stock.Usable) { r.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0270"); return r; }
        int before = stock.Read(), available = stock.UnreservedStock(), free = stock.FreeCapacity();
        if (before < 0 || available < 0 || available > before || free < 0) throw new InvalidOperationException(global::TimberbornTrainer.Localization.TextToken.Key("t0435"));
        r.Before = before; r.MaximumAdd = Math.Min(free, int.MaxValue - before); r.MaximumRemove = available;
        long target = operation == InventoryWriteOperation.Clear ? 0 : operation == InventoryWriteOperation.Set ? n
          : operation == InventoryWriteOperation.Add ? (long)before + n : (long)before - n;
        long delta = target - before; r.RequestedDelta = delta;
        if (target < 0 || target > int.MaxValue || delta > r.MaximumAdd || -delta > available) {
          r.After = before;
          r.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0453") + r.MaximumAdd + global::TimberbornTrainer.Localization.TextToken.Key("t0039") + available
            + global::TimberbornTrainer.Localization.TextToken.Key("t0053") + (before - available) + global::TimberbornTrainer.Localization.TextToken.Key("t0108") + ((long)before + r.MaximumAdd) + global::TimberbornTrainer.Localization.TextToken.Key("t0099");
          return r;
        }
        if (!permitted() || !stock.Usable) { r.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0454"); return r; }
        if (delta == 0) { r.State = InventoryWriteState.NoOp; r.After = before; r.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0356"); return r; }
        r.NativeCallStarted = true;
        if (delta > 0) stock.Give(checked((int)delta)); else stock.Take(checked((int)-delta));
        r.After = stock.Read();
        r.State = r.After == target ? InventoryWriteState.Succeeded : InventoryWriteState.Indeterminate;
        r.Message = r.State == InventoryWriteState.Succeeded ? global::TimberbornTrainer.Localization.TextToken.Key("t0425")
          : global::TimberbornTrainer.Localization.TextToken.Key("t0424");
      } catch (Exception) {
        if (r.NativeCallStarted) {
          r.State = InventoryWriteState.Indeterminate;
          try { if (stock != null && stock.Usable) r.After = stock.Read(); } catch { }
          r.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0426");
        } else r.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0182");
      }
      return r;
    }
  }
}
