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
    public string Message { get; internal set; } = "未执行。";
    public bool NativeCallStarted { get; internal set; }
    public bool AutomaticRetryAllowed => false;
  }
  public interface IInventoryEditor {
    InventoryEditResult Execute(string targetId, string resource, InventoryWriteOperation operation, string input);
  }

  // Whole-request validation. No clipping, retries, reverse writes, UI cache or global-zero-reservation policy.
  public static class InventoryEditCommands {
    public static InventoryEditResult Execute(Func<IWarehouseStock> resolve, Func<bool> permitted,
        InventoryWriteOperation operation, string input) {
      var r = new InventoryEditResult();
      IWarehouseStock stock = null;
      try {
        if (!permitted()) { r.Message = "请开启全局修改器，并使用游戏原生按钮手动暂停后再操作。"; return r; }
        if (!Enum.IsDefined(typeof(InventoryWriteOperation), operation)) { r.Message = "未知库存操作。"; return r; }
        int n = 0;
        if (operation != InventoryWriteOperation.Clear && (input == null || input.Length > 32
            || !int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out n))) {
          r.Message = "请输入零至2147483647之间的整数，不接受负数、小数或溢出。"; return r;
        }
        if ((operation == InventoryWriteOperation.Add || operation == InventoryWriteOperation.Remove) && n == 0) {
          r.Message = "增加和减少数量必须大于零。"; return r;
        }
        stock = resolve();
        if (stock == null || !stock.Usable) { r.Message = "目标、库存组件或资源已失效；请重新选择。"; return r; }
        int before = stock.Read(), available = stock.UnreservedStock(), free = stock.FreeCapacity();
        if (before < 0 || available < 0 || available > before || free < 0) throw new InvalidOperationException("原生数量不自洽");
        r.Before = before; r.MaximumAdd = Math.Min(free, int.MaxValue - before); r.MaximumRemove = available;
        long target = operation == InventoryWriteOperation.Clear ? 0 : operation == InventoryWriteOperation.Set ? n
          : operation == InventoryWriteOperation.Add ? (long)before + n : (long)before - n;
        long delta = target - before; r.RequestedDelta = delta;
        if (target < 0 || target > int.MaxValue || delta > r.MaximumAdd || -delta > available) {
          r.After = before;
          r.Message = "整单未执行：本次最多可增加 " + r.MaximumAdd + "，最多可减少 " + available
            + "；可设置范围 " + (before - available) + "～" + ((long)before + r.MaximumAdd) + "。已有预留不会被解除。";
          return r;
        }
        if (!permitted() || !stock.Usable) { r.Message = "执行前暂停、全局开关或目标状态发生变化，未修改。"; return r; }
        if (delta == 0) { r.State = InventoryWriteState.NoOp; r.After = before; r.Message = "数量已经符合目标，无需修改。"; return r; }
        r.NativeCallStarted = true;
        if (delta > 0) stock.Give(checked((int)delta)); else stock.Take(checked((int)-delta));
        r.After = stock.Read();
        r.State = r.After == target ? InventoryWriteState.Succeeded : InventoryWriteState.Indeterminate;
        r.Message = r.State == InventoryWriteState.Succeeded ? "原生调用正常返回，真实库存回读与目标一致。"
          : "原生调用已返回，但实际库存与目标不同，结果未确认；不会自动重试或补差。";
      } catch (Exception) {
        if (r.NativeCallStarted) {
          r.State = InventoryWriteState.Indeterminate;
          try { if (stock != null && stock.Usable) r.After = stock.Read(); } catch { }
          r.Message = "原生调用中出现异常，库存可能已改变；已尝试回读，结果未确认。禁止自动重试或回滚。";
        } else r.Message = "读取或原生身份校验失败，未调用库存修改接口。";
      }
      return r;
    }
  }
}
