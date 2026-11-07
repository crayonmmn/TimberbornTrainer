using System;
using TimberbornTrainer.Core;
using TimberbornTrainer.Modules;

namespace TimberbornTrainer.Tests {
  public static class ConstructionTests {
    private static int _checks;
    private static void Check(bool value, string message) { ++_checks; if (!value) throw new Exception(message); }
    private sealed class Site : IConstructionSiteState {
      public bool Unfinished { get; set; } = true;
      public bool IsOn { get; set; } = true;
      public bool MaterialsFull { get; set; } = true;
      public float TotalHours { get; set; } = 2;
      public float ProgressHours { get; set; }
      public bool NativeFinishBlocked;
      public int Writes, FinishEvents;
      public void IncreaseTime(float hours) {
        ++Writes;
        ProgressHours = Math.Min(TotalHours, ProgressHours + hours);
        if (!NativeFinishBlocked) { Unfinished = false; ++FinishEvents; }
      }
    }
    public static string Run() {
      _checks = 0;
      // Four-combination policy/instant-adapter tests only, NOT implementation of free materials.
      foreach (bool instant in new[] { false, true }) foreach (bool free in new[] { false, true }) {
        var mode = ConstructionRules.Mode(instant, free);
        Check(ConstructionRules.RequestsInstant(mode) == instant, "瞬间开关独立性。");
        Check(ConstructionRules.RequestsFree(mode) == free, "免材料请求独立性。");
        var site = new Site();
        Check(ConstructionEngine.TryAdvance(site, true, mode) == instant, "时间推进只受瞬间请求影响。");
        Check(site.Writes == (instant ? 1 : 0), "每次只调用一次原生时间入口。");
        site = new Site { MaterialsFull = false };
        Check(!ConstructionEngine.TryAdvance(site, true, mode) && site.Writes == 0 && !site.MaterialsFull,
          "没有材料时不能用未实现的免材料偏好偷偷绕过检查。");
        site = new Site();
        Check(!ConstructionEngine.TryAdvance(site, false, mode) && site.Writes == 0, "全局关闭不能推进。");
      }
      var blocked = new Site { NativeFinishBlocked = true };
      Check(ConstructionEngine.TryAdvance(blocked, true, ConstructionMode.InstantOnly), "阻挡时可以补足时间。");
      Check(blocked.Unfinished && blocked.FinishEvents == 0 && blocked.ProgressHours == 2, "完工决定必须留给原生接口。");
      Check(!ConstructionEngine.TryAdvance(blocked, true, ConstructionMode.InstantOnly) && blocked.Writes == 1,
        "已补足时间时不重复派发进度事件。");
      var paused = new Site { IsOn = false };
      Check(!ConstructionEngine.TryAdvance(paused, true, ConstructionMode.InstantOnly) && paused.Writes == 0, "暂停/无支撑工地不得推进。");
      var finished = new Site { Unfinished = false };
      Check(!ConstructionEngine.TryAdvance(finished, true, ConstructionMode.InstantOnly) && finished.Writes == 0, "不重复处理成品。");
      var partial = new Site { ProgressHours = .75f };
      Check(ConstructionEngine.TryAdvance(partial, true, ConstructionMode.InstantOnly) && partial.ProgressHours == 2 && partial.FinishEvents == 1,
        "只补足剩余时间并由原生入口发出完工事件。");
      foreach (float total in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) {
        var invalid = new Site { TotalHours = total };
        bool rejected = false;
        try { ConstructionEngine.TryAdvance(invalid, true, ConstructionMode.InstantOnly); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && invalid.Writes == 0, "异常工时不应写入。");
      }
      var state = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1");
      state.Apply(new Config { Enabled = true, InstantBuilding = true, FreeBuilding = true });
      Check(state.Settings.InstantBuilding && state.Settings.FreeBuilding, "配置分别保留两项偏好。");
      state.RestoreDefaults();
      Check(!state.EffectiveEnabled && !state.Settings.InstantBuilding && !state.Settings.FreeBuilding, "默认恢复停用持续偏好。");
      return "通过：" + _checks + " 项施工策略/原生入口模拟断言；免材料未接入，四组合实机未测。";
    }
  }
}
