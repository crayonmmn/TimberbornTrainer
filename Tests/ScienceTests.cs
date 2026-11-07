using System;
using TimberbornTrainer.Core;
using TimberbornTrainer.Modules;

namespace TimberbornTrainer.Tests {
  public static class ScienceTests {
    private static int _assertions;
    private static void Check(bool value, string description) {
      ++_assertions;
      if (!value) throw new Exception(description);
    }
    private sealed class FakePoints : ISciencePoints {
      public int Value, Reads, Adds, Subtracts, FailReadAt;
      public bool IgnoreWrite, ThrowBeforeWrite, ThrowAfterWrite;
      public int Read() { if (++Reads == FailReadAt) throw new Exception(); return Value; }
      public void Add(int amount) { ++Adds; if (ThrowBeforeWrite) throw new Exception(); if (!IgnoreWrite) Value += amount; if (ThrowAfterWrite) throw new Exception(); }
      public void Subtract(int amount) { ++Subtracts; if (ThrowBeforeWrite) throw new Exception(); if (!IgnoreWrite) Value -= amount; if (ThrowAfterWrite) throw new Exception(); }
    }
    public static string Run() {
      _assertions = 0;
      var state = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1");
      var points = new FakePoints { Value = 20 };
      var commands = new ScienceCommands(points, () => state.EffectiveEnabled);
      int current;
      Check(commands.TryRead(out current) && current == 20, "全局关闭仍应允许真实读取。");
      Check(!commands.Execute(ScienceOperation.Add, "3").Success && points.Adds == 0 && points.Value == 20, "全局关闭必须禁止写入。");
      state.SetEnabled(true);
      var result = commands.Execute(ScienceOperation.Add, "100");
      Check(result.Success && result.Before == 20 && result.After == 120 && result.Delta == 100 && points.Adds == 1, "增加与真实回读。");
      result = commands.Execute(ScienceOperation.Subtract, "30");
      Check(result.Success && points.Value == 90 && result.Delta == -30 && points.Subtracts == 1, "减少与回读。");
      Check(commands.Execute(ScienceOperation.Set, "200").Success && points.Value == 200 && points.Adds == 2, "设置目标应调用增加。");
      Check(commands.Execute(ScienceOperation.Set, "10").Success && points.Value == 10 && points.Subtracts == 2, "设置目标应调用减少。");
      int writes = points.Adds + points.Subtracts;
      foreach (var operation in new[] { ScienceOperation.Add, ScienceOperation.Subtract })
        Check(commands.Execute(operation, "0").Success && points.Value == 10, "零增减应成功且不变。");
      Check(commands.Execute(ScienceOperation.Set, "10").Success && writes == points.Adds + points.Subtracts, "相同目标与零操作不应调用原生写入。");
      Check(commands.Execute(ScienceOperation.Set, "0").Success && points.Value == 0, "允许设置为零。");
      foreach (string invalid in new[] { "-1", "+1", "abc", "1.5", "NaN", "Infinity", "1e3", "", "1000000001", "2147483648", "999999999999999999999999999999999999999999", null }) {
        int calls = points.Adds + points.Subtracts;
        Check(!commands.Execute(ScienceOperation.Add, invalid).Success && points.Adds + points.Subtracts == calls, "非法输入必须不调用写入。");
      }
      Check(!commands.Execute(ScienceOperation.Subtract, "1").Success && points.Value == 0, "余额不足不得调用原生减点接口。");
      Check(!commands.Execute((ScienceOperation)99, "1").Success, "未知操作应拒绝。");
      Check(commands.Execute(ScienceOperation.Set, "1000000000").Success, "上限应可设置。");
      Check(!commands.Execute(ScienceOperation.Add, "1").Success && points.Value == 1000000000, "总量上限保护。");
      points.Value = int.MaxValue;
      Check(!commands.Execute(ScienceOperation.Add, "1000000000").Success && points.Value == int.MaxValue, "必须防止整数溢出。");
      Check(commands.Execute(ScienceOperation.Set, "0").Success && points.Value == 0, "可安全减少超上限的已有余额。");
      Check(commands.Execute(ScienceOperation.Add, " 12 ").Success && points.Value == 12, "允许首尾空格。");
      state.RestoreDefaults();
      Check(points.Value == 12 && !commands.Execute(ScienceOperation.Set, "1").Success, "恢复默认不回滚科研点，且关闭修改。");
      state.Apply(new Config { Enabled = false });
      Check(points.Value == 12, "配置应用不能执行一次性命令。");
      var mismatchState = new TrainerState("其他版本", true, "6000.5.11f1");
      mismatchState.Apply(new Config { Enabled = true });
      Check(!new ScienceCommands(points, () => mismatchState.EffectiveEnabled).Execute(ScienceOperation.Add, "1").Success, "配置不得绕过版本限制。");

      var unavailable = new FakePoints { FailReadAt = 1 };
      var bad = new ScienceCommands(unavailable, () => true);
      result = bad.Execute(ScienceOperation.Add, "1");
      Check(!result.Success && !result.Before.HasValue && unavailable.Adds == 0 && bad.RuntimeError, "读取失败必须先停止修改。");
      var readback = new FakePoints { Value = 5, FailReadAt = 2 };
      bad = new ScienceCommands(readback, () => true);
      result = bad.Execute(ScienceOperation.Add, "1");
      Check(!result.Success && result.Before == 5 && !result.After.HasValue && readback.Value == 6 && readback.Adds == 1, "回读失败不能伪造成功或重复调用。");
      var ignored = new FakePoints { Value = 5, IgnoreWrite = true };
      bad = new ScienceCommands(ignored, () => true);
      result = bad.Execute(ScienceOperation.Add, "2");
      Check(!result.Success && result.After == 5 && bad.RuntimeError, "忽略写入应检测为实际不符。");
      bad.TryRead(out current);
      Check(bad.RuntimeError, "后台刷新不得掩盖写入异常。");
      ignored.IgnoreWrite = false;
      Check(bad.Execute(ScienceOperation.Add, "2").Success && !bad.RuntimeError, "后续经确认的成功操作可清除异常状态。");
      var throwing = new FakePoints { Value = 5, ThrowBeforeWrite = true };
      result = new ScienceCommands(throwing, () => true).Execute(ScienceOperation.Add, "1");
      Check(!result.Success && result.After == 5 && throwing.Adds == 1, "原生异常不能报告成功或重试。");
      throwing = new FakePoints { Value = 5, ThrowAfterWrite = true };
      result = new ScienceCommands(throwing, () => true).Execute(ScienceOperation.Add, "1");
      Check(!result.Success && result.After == 6 && result.Delta == 1 && throwing.Adds == 1, "部分生效后异常必须显示实际回读。");
      var negative = new ScienceCommands(new FakePoints { Value = -1 }, () => true);
      Check(!negative.TryRead(out current) && negative.RuntimeError, "异常的游戏原始值不能被当作合法余额。");
      return "通过：" + _assertions + " 项科研命令断言（模拟接口，非游戏内测试）。";
    }
  }
}
