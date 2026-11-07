using System;
using System.Globalization;

namespace TimberbornTrainer.Modules {
  public interface IWarehouseStock {
    bool Usable { get; }
    int Read();
    int FreeCapacity();
    int UnreservedStock();
    void Give(int amount);
    void Take(int amount);
  }

  // Same command/result pattern as science; stock limits and reservations differ.
  public sealed class InventoryCommands {
    public const int InputLimit = 1000000;
    private readonly Func<bool> _enabled;
    public InventoryCommands(Func<bool> enabled) { _enabled = enabled; }
    public ScienceResult Execute(IWarehouseStock stock, ScienceOperation operation, string input) {
      var result = new ScienceResult { Message = "库存修改失败。" };
      if (!_enabled()) { result.Message = "全局修改器已关闭或版本不匹配。"; return result; }
      if (operation != ScienceOperation.Add && operation != ScienceOperation.Set && operation != ScienceOperation.Subtract) {
        result.Message = "不支持的库存操作。"; return result;
      }
      int amount;
      if (input == null || input.Length > 32 || !int.TryParse(input.Trim(), NumberStyles.None,
          CultureInfo.InvariantCulture, out amount) || amount > InputLimit) {
        result.Message = "请输入零至一百万之间的整数。"; return result;
      }
      bool writeStarted = false;
      try {
        if (stock == null || !stock.Usable) { result.Message = "请选择仍然有效的已建成仓库及允许存储的资源。"; return result; }
        int before = stock.Read();
        if (before < 0) throw new InvalidOperationException();
        result.Before = before;
        long expected = operation == ScienceOperation.Set ? amount : operation == ScienceOperation.Add
          ? (long)before + amount : (long)before - amount;
        long delta = expected - before;
        if (expected < 0 || expected > InputLimit) { result.After = before; result.Message = "目标库存超出范围，未修改。"; return result; }
        if (delta > 0 && delta > stock.FreeCapacity()) {
          result.After = before; result.Message = "未预留容量不足；请检查仓库资源设置及在途搬运。"; return result;
        }
        if (delta < 0 && -delta > stock.UnreservedStock()) {
          result.After = before; result.Message = "未预留库存不足；不移除已被海狸或搬运任务预留的物品。"; return result;
        }
        if (delta != 0) {
          writeStarted = true;
          if (delta > 0) stock.Give(checked((int)delta));
          else stock.Take(checked((int)-delta));
        }
        result.After = stock.Read();
        result.Success = result.After == expected;
        result.Message = result.Success ? "成功：已回读单个仓库的真实库存。"
          : "真实库存与预期不一致，不能确认成功；请勿重复执行。";
      } catch {
        if (writeStarted) {
          try { result.After = stock.Read(); } catch { }
          result.Message = "原生接口可能已生效，但未能确认结果；请核对仓库，不要重复执行。";
        } else result.Message = "读取或校验库存失败，未执行修改。";
      }
      return result;
    }
  }
}
