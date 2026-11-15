using System;
using TimberbornTrainer.Core;

namespace TimberbornTrainer.Tests {
  public static class CompatibilityTests {
    private static int _checks;
    private static void Check(bool value, string message) { ++_checks; if (!value) throw new Exception(message); }
    private sealed class ScienceApi {
      public static int Calls;
      public int SciencePoints => 0;
      public void AddPoints(int amount) { ++Calls; }
      public void SubtractPoints(int amount) { ++Calls; }
    }
    private sealed class WrongApi {
      public long SciencePoints => 0;
      public void AddPoints(int amount) { }
      public void SubtractPoints(int amount) { }
    }
    // Signature fixture only; never stands in for native lifecycle or game-version testing.
    private sealed class ApiSurface {
      private bool _updateEnabled = false;
      public bool Enabled => _updateEnabled;
      public int SciencePoints => 0;
      public float CurrentSpeed => 0;
      public bool IsFinished => true;
      public bool IsUnfinished => false;
      public float MaximumValue => 1;
      public float StartingValue => 1;
      public void AddPoints(int n) { }
      public void SubtractPoints(int n) { }
      public int AmountInStock(string id) => 0;
      public int UnreservedAmountInStock(string id) => 0;
      public int UnreservedCapacity(string id) => 1;
      public void GiveExisting(ApiSurface amount) { }
      public void TakeExisting(ApiSurface amount) { }
      public void FinishNow() { }
      private ApiSurface GetNeed(string id) => this;
      public ApiSurface GetNeedSpec(string id) => this;
      public void ResetNeed(string id) { }
      public float GetNeedPoints(string id) => 1;
      public bool HasNeed(string id) => true;
    }
    public static string Run() {
      _checks = 0;
      var verified = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1");
      Check(verified.VerifiedVersion && !verified.CompatibilityMessage.Contains("警告"), "已验证版本不显示兼容性警告");
      foreach (var version in new[] { "1.1.2.9", "未来版本", null }) {
        var state = new TrainerState(version, true, "其他Unity");
        state.SetEnabled(true);
        Check(state.EffectiveEnabled && !state.VerifiedVersion, "未验证版本允许启用，但不冒称已验证");
        Check(state.CompatibilityMessage.Contains(TrainerState.TargetVersion) && state.CompatibilityMessage.Contains(version ?? "未知")
          && state.CompatibilityMessage.Contains("仍将尝试启动"), "警告明确已验证及当前检测版本");
      }
      ScienceApi.Calls = 0;
      var complete = RuntimeApiChecks.Capture(_ => typeof(ApiSurface));
      foreach (TrainerFeature f in Enum.GetValues(typeof(TrainerFeature))) Check(complete(f) == null, "完整签名替身通过对应功能检查：" + f);
      var api = RuntimeApiChecks.Capture(name => name == "Timberborn.ScienceSystem.ScienceService" ? typeof(ScienceApi) : null);
      Check(api(TrainerFeature.Science) == null && ScienceApi.Calls == 0, "签名检查不调用游戏接口或修改状态");
      var partial = new TrainerState("未来版本", true, "未知", api); partial.SetEnabled(true);
      Check(partial.EffectiveEnabled, "部分接口缺失不关闭Trainer和科研功能");
      foreach (var feature in new[] { TrainerFeature.Inventory, TrainerFeature.Construction, TrainerFeature.Needs }) {
        Check(partial.EffectiveEnabled && partial.InterfaceWarning(feature).Contains("警告"), "缺失接口只产生警告，不限制功能");
      }
      Check(RuntimeApiChecks.Capture(_ => typeof(WrongApi))(TrainerFeature.Science) != null, "存在同名但类型不符的属性不能通过检查");
      var broken = new TrainerState("未来版本", true, "未知", RuntimeApiChecks.Capture(_ => throw new TypeLoadException("missing")));
      broken.SetEnabled(true);
      Check(broken.EffectiveEnabled && broken.InterfaceWarning(TrainerFeature.Construction).Contains("missing"), "类型检查异常仍只警告，不禁止使用");
      int calls = 0;
      var command = new TimberbornTrainer.Modules.ConstructionCommands(() => partial.EffectiveEnabled);
      command.Execute(() => { ++calls; return Array.Empty<TimberbornTrainer.Modules.IConstructionTarget>(); });
      Check(calls == 1, "接口检查警告不阻止用户尝试一次性命令");
      partial.SetEnabled(false); Check(!partial.EffectiveEnabled, "警告模式仍保留全局关闭安全限制");
      return "版本及接口仅警告（纯逻辑及签名替身，非跨版本实机）：" + _checks + " 项通过。";
    }
  }
}
