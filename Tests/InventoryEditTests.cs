using System;
using TimberbornTrainer.Modules;

namespace TimberbornTrainer.Tests {
  public static class InventoryEditTests {
    private static int _checks;
    private static void Check(bool value, string message) { ++_checks; if (!value) throw new Exception(message); }
    private sealed class Stock : IWarehouseStock {
      public int Amount = 20, Reserved = 3, Free = 50, Writes;
      public bool Valid = true, ThrowAfterWrite, BadRead, InvalidateOnRead;
      public bool Usable => Valid;
      public int Read() { if (BadRead) throw new Exception(); if (InvalidateOnRead) Valid = false; return Amount; }
      public int FreeCapacity() => Free;
      public int UnreservedStock() => Amount - Reserved;
      public void Give(int n) { ++Writes; Amount += n; if (ThrowAfterWrite) throw new Exception(); }
      public void Take(int n) { ++Writes; Amount -= n; if (ThrowAfterWrite) throw new Exception(); }
    }
    private static InventoryEditResult Run(Stock s, InventoryWriteOperation op, string input = "10", bool permitted = true) =>
      InventoryEditCommands.Execute(() => s, () => permitted, op, input);
    public static string Run() {
      _checks = 0;
      var s = new Stock(); var r = Run(s, InventoryWriteOperation.Add);
      Check(r.State == InventoryWriteState.Succeeded && s.Amount == 30 && s.Writes == 1, "有预留仍按原生空位增加，单次调用。");
      s = new Stock(); r = Run(s, InventoryWriteOperation.Remove, "5");
      Check(r.State == InventoryWriteState.Succeeded && s.Amount == 15 && s.Reserved == 3, "减少不动预留。");
      s = new Stock(); r = Run(s, InventoryWriteOperation.Set, "31");
      Check(r.State == InventoryWriteState.Succeeded && s.Amount == 31 && r.RequestedDelta == 11, "设置上调使用实时差值。");
      s = new Stock(); r = Run(s, InventoryWriteOperation.Set, "5");
      Check(r.State == InventoryWriteState.Succeeded && s.Amount == 5 && s.Writes == 1, "设置下调不是先清空再增加。");
      s = new Stock { Reserved = 0 }; r = Run(s, InventoryWriteOperation.Clear);
      Check(r.State == InventoryWriteState.Succeeded && s.Amount == 0 && s.Writes == 1, "无预留可清空。");
      s = new Stock(); r = Run(s, InventoryWriteOperation.Clear);
      Check(r.State == InventoryWriteState.Rejected && s.Writes == 0 && r.MaximumRemove == 17, "有物品预留整单拒绝清空并显示上限。");
      s = new Stock { Free = 4 }; r = Run(s, InventoryWriteOperation.Add);
      Check(r.State == InventoryWriteState.Rejected && s.Writes == 0 && r.MaximumAdd == 4, "容量不足不静默截断。");
      s = new Stock(); r = Run(s, InventoryWriteOperation.Remove, "18");
      Check(r.State == InventoryWriteState.Rejected && s.Writes == 0 && r.MaximumRemove == 17, "移除量超过未预留量拒绝。");
      s = new Stock(); r = Run(s, InventoryWriteOperation.Set, "2");
      Check(r.State == InventoryWriteState.Rejected && s.Writes == 0, "设置不能跨过预留下限。");
      s = new Stock(); r = Run(s, InventoryWriteOperation.Set, "20");
      Check(r.State == InventoryWriteState.NoOp && s.Writes == 0, "相同目标不写入。");
      s = new Stock { Amount = 0, Reserved = 0 }; r = Run(s, InventoryWriteOperation.Clear);
      Check(r.State == InventoryWriteState.NoOp && s.Writes == 0, "空库存清空不写入。");
      foreach (string bad in new[] { "-1", "1.5", "NaN", "2147483648", "99999999999999999999999999999999999", "" }) {
        s = new Stock(); r = Run(s, InventoryWriteOperation.Add, bad);
        Check(r.State == InventoryWriteState.Rejected && s.Writes == 0, "非法输入：" + bad);
      }
      s = new Stock(); Check(Run(s, InventoryWriteOperation.Add, "0").State == InventoryWriteState.Rejected, "增加零拒绝。");
      s = new Stock(); Check(Run(s, InventoryWriteOperation.Remove, "0").State == InventoryWriteState.Rejected, "减少零拒绝。");
      s = new Stock { Amount = int.MaxValue, Reserved = 0 }; r = Run(s, InventoryWriteOperation.Add, "1");
      Check(r.State == InventoryWriteState.Rejected && s.Writes == 0 && r.MaximumAdd == 0, "整数边界不溢出。");
      s = new Stock { Free = 0 }; r = Run(s, InventoryWriteOperation.Add);
      Check(r.State == InventoryWriteState.Rejected && s.Writes == 0, "不允许资源以零原生空位拒绝。");
      s = new Stock { Valid = false }; r = Run(s, InventoryWriteOperation.Set, "20");
      Check(r.State == InventoryWriteState.Rejected && s.Writes == 0, "无效目标即使NoOp也拒绝。");
      s = new Stock(); r = Run(s, InventoryWriteOperation.Add, "10", false);
      Check(r.State == InventoryWriteState.Rejected && s.Writes == 0, "全局关闭或未暂停拒绝。");
      s = new Stock { InvalidateOnRead = true }; r = Run(s, InventoryWriteOperation.Add);
      Check(r.State == InventoryWriteState.Rejected && s.Writes == 0, "执行前目标再次失效拒绝。");
      s = new Stock { ThrowAfterWrite = true }; r = Run(s, InventoryWriteOperation.Add);
      Check(r.State == InventoryWriteState.Indeterminate && r.After == 30 && s.Writes == 1 && !r.AutomaticRetryAllowed,
        "变更后事件异常，即使达目标也未确认，无重试或逆操作。");
      s = new Stock { BadRead = true }; r = Run(s, InventoryWriteOperation.Add);
      Check(r.State == InventoryWriteState.Rejected && s.Writes == 0 && r.After == null, "读取异常不编造零。");
      return "库存编辑命令：" + _checks + " 项断言通过。";
    }
  }
}
