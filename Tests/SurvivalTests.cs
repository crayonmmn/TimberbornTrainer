using System;
using System.Collections.Generic;
using TimberbornTrainer.Modules;

namespace TimberbornTrainer.Tests {
  public static class SurvivalTests {
    private static int _checks;
    private static void Check(bool condition, string message) { ++_checks; if (!condition) throw new Exception(message); }
    private sealed class Stock : IWarehouseStock {
      public bool Usable { get; set; } = true;
      public int Value = 10, Capacity = 30, Incoming = 4, Reserved = 3, Writes;
      public bool FailRead, FailAfterWrite, PartialFailure;
      public int Read() { if (FailRead || (FailAfterWrite && Writes > 0)) throw new Exception(); return Value; }
      public int FreeCapacity() => Math.Max(0, Capacity - Value - Incoming);
      public int UnreservedStock() => Value - Reserved;
      public void Give(int n) { ++Writes; Value += n; if (PartialFailure) throw new Exception(); }
      public void Take(int n) { ++Writes; Value -= n; }
    }
    private sealed class Need : INeedValue {
      public float Value = .6f;
      public int Writes;
      public bool Reject;
      public float Read() => Value;
      public void AddEffect(float points) { ++Writes; if (!Reject) Value += points; }
    }
    public static string Run() {
      _checks = 0;
      bool enabled = true;
      var command = new InventoryCommands(() => enabled);
      var a = new Stock(); var b = new Stock();
      var r = command.Execute(a, ScienceOperation.Add, "5");
      Check(r.Success && r.Before == 10 && r.After == 15 && r.Delta == 5, "增加后回读。");
      Check(b.Value == 10 && b.Writes == 0, "单个仓库不分配到其他库存。");
      r = command.Execute(a, ScienceOperation.Set, "20");
      Check(r.Success && a.Value == 20, "设置较高目标。");
      r = command.Execute(a, ScienceOperation.Set, "17");
      Check(r.Success && a.Value == 17, "设置较低目标。");
      r = command.Execute(a, ScienceOperation.Subtract, "2");
      Check(r.Success && a.Value == 15, "减少未预留物品。");
      foreach (string bad in new[] { "-1", "abc", "NaN", "1.5", "2147483648", "1000001", "+1", "" }) {
        int writes = a.Writes;
        Check(!command.Execute(a, ScienceOperation.Add, bad).Success && writes == a.Writes, "非法输入不写入：" + bad);
      }
      foreach (var op in new[] { ScienceOperation.Add, ScienceOperation.Subtract, ScienceOperation.Set }) {
        int writes = a.Writes;
        Check(command.Execute(a, op, op == ScienceOperation.Set ? "15" : "0").Success && a.Writes == writes, "零差额不写入。");
      }
      a = new Stock();
      Check(!command.Execute(a, ScienceOperation.Add, "17").Success && a.Writes == 0, "不挤占在途容量。");
      Check(!command.Execute(a, ScienceOperation.Set, "2").Success && a.Writes == 0, "不移除预留库存。");
      Check(!command.Execute(a, ScienceOperation.Subtract, "11").Success && a.Writes == 0, "不允许库存负数。");
      enabled = false;
      Check(!command.Execute(a, ScienceOperation.Add, "1").Success && a.Writes == 0, "全局关闭禁止命令。");
      enabled = true; a.Usable = false;
      Check(!command.Execute(a, ScienceOperation.Add, "1").Success && a.Writes == 0, "失效工地或已删仓库不写入。");
      Check(!command.Execute(null, ScienceOperation.Add, "1").Success, "空选择拒绝。");
      a = new Stock { FailRead = true };
      Check(!command.Execute(a, ScienceOperation.Add, "1").Success && a.Writes == 0, "读失败不写。");
      a = new Stock { FailAfterWrite = true };
      r = command.Execute(a, ScienceOperation.Add, "1");
      Check(!r.Success && r.After == null && a.Value == 11 && a.Writes == 1, "写后回读失败不伪报成功、不自动重试。");
      a = new Stock { PartialFailure = true };
      r = command.Execute(a, ScienceOperation.Add, "1");
      Check(!r.Success && r.After == 11 && a.Writes == 1, "部分生效的异常要回读并报告。");
      a = new Stock { Value = 1000000, Capacity = int.MaxValue };
      Check(!command.Execute(a, ScienceOperation.Add, "1").Success && a.Writes == 0, "总量上限。");
      Check(!command.Execute(a, (ScienceOperation)99, "1").Success, "未知命令拒绝。");

      var hunger = new NeedLock<string>(); var thirst = new NeedLock<string>();
      var h = new Need(); var t = new Need { Value = .8f };
      Check(hunger.Maintain("现有海狸", h) && h.Writes == 0, "启用仅记录当前需求。");
      h.Value = .4f;
      Check(hunger.Maintain("现有海狸", h) && Math.Abs(h.Value - .6f) < .0001 && h.Writes == 1, "下降由效果补偿。");
      Check(t.Value == .8f && thirst.Count == 0, "饥饿不影响口渴。");
      h.Value = .9f; hunger.Maintain("现有海狸", h);
      Check(h.Value == .9f && h.Writes == 1, "不撤销正常进食改善。");
      Check(hunger.Maintain("新生海狸", new Need()) && hunger.Count == 2, "新生对象独立捕获。");
      thirst.Maintain("现有海狸", t); t.Value = .5f; thirst.Maintain("现有海狸", t);
      Check(Math.Abs(t.Value - .8f) < .0001, "口渴独立补偿。");
      hunger.Clear(); h.Value = .5f;
      Check(hunger.Count == 0 && h.Value == .5f && thirst.Count == 1, "关闭饥饿只清理自身记录，不回滚或改更新开关。");
      hunger.Maintain("现有海狸", h); h.Value = .4f; hunger.Maintain("现有海狸", h);
      Check(Math.Abs(h.Value - .5f) < .0001, "重新启用捕获新基线。");
      hunger.Prune(new HashSet<string>());
      Check(hunger.Count == 0, "移除对象清理引用。");
      foreach (float unsafeValue in new[] { 0f, -.2f })
        Check(!hunger.Maintain("危险对象", new Need { Value = unsafeValue }) && hunger.Count == 0, "不伪报危急对象已安全锁定。");
      bool rejected = false;
      try { hunger.Maintain("异常", new Need { Value = float.NaN }); } catch (InvalidOperationException) { rejected = true; }
      Check(rejected && hunger.Count == 0, "异常需求不捕获。");
      h = new Need(); hunger.Maintain("效果失败", h); h.Value = .1f; h.Reject = true; rejected = false;
      try { hunger.Maintain("效果失败", h); } catch (InvalidOperationException) { rejected = true; }
      Check(rejected, "效果回读不符不能伪造成功。");
      return "通过：" + _checks + " 项单仓库命令及需求维持断言（模拟接口，非实机验收）。";
    }
  }
}
