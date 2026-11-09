using System;
using System.Collections.Generic;
using TimberbornTrainer.Core;
using TimberbornTrainer.Modules;

namespace TimberbornTrainer.Tests {
  public static class ConstructionTests {
    private static int _checks;
    private static void Check(bool value, string message) { ++_checks; if (!value) throw new Exception(message); }
    private sealed class Site : IConstructionTarget {
      public ConstructionTargetState State = ConstructionTargetState.Ready;
      public ConstructionCompletion Outcome = ConstructionCompletion.Completed;
      public int Calls;
      public Action OnFinish;
      public ConstructionTargetState Inspect() => State;
      public ConstructionCompletion FinishNow() { ++Calls; OnFinish?.Invoke(); return Outcome; }
    }
    public static string Run() {
      _checks = 0;
      bool enabled = true;
      var commands = new ConstructionCommands(() => enabled);
      var complete = new Site();
      var blocked = new Site { State = ConstructionTargetState.Blocked };
      var invalid = new Site { State = ConstructionTargetState.Invalid };
      var unrelated = new Site { State = ConstructionTargetState.NotApplicable };
      var unfinished = new Site { Outcome = ConstructionCompletion.Unfinished };
      var unconfirmed = new Site { Outcome = ConstructionCompletion.Unconfirmed };
      var skipped = new Site { Outcome = ConstructionCompletion.Skipped };
      int snapshots = 0;
      var result = commands.Execute(() => { ++snapshots; return new IConstructionTarget[] {
        complete, blocked, invalid, unrelated, unfinished, unconfirmed, skipped, null }; });
      Check(snapshots == 1, "单次命令只取得一次快照。");
      Check(result.Completed == 1 && complete.Calls == 1, "确认完工仅记一次。");
      Check(result.Unfinished == 2 && unfinished.Calls == 1, "受阻与原生未完工均不冒称成功。");
      Check(blocked.Calls == 0, "建筑自身暂停/阻挡不强行完成。");
      Check(result.Skipped == 3 && invalid.Calls == 0, "失效、调用前失效与空目标分别跳过。");
      Check(unrelated.Calls == 0, "非工地或已完成建筑不重复执行。");
      Check(result.Unconfirmed == 1 && result.Errors == 0, "仅消失而无完工证据不能算成功。");
      Check(!commands.Busy && result.Message.Contains("不会自动重试"), "执行结束释放保护并提示不确定结果。");

      enabled = false;
      result = commands.Execute(() => { ++snapshots; return new IConstructionTarget[] { complete }; });
      Check(snapshots == 1 && complete.Calls == 1 && result.Completed == 0, "关闭全局后不扫描、不修改。");
      enabled = true;
      var createdLater = new Site();
      var live = new List<IConstructionTarget>();
      var first = new Site { OnFinish = () => live.Add(createdLater) };
      live.Add(first);
      result = commands.Execute(() => live.ToArray());
      Check(result.Completed == 1 && createdLater.Calls == 0, "执行时新增工地不进入本次快照。");
      Check(first.Calls == 1 && !commands.Busy, "返回后无持续施工任务。");
      first.State = ConstructionTargetState.NotApplicable;
      result = commands.Execute(() => live.ToArray());
      Check(createdLater.Calls == 1 && first.Calls == 1, "后续工地仅由下一次明确点击处理。");

      int logs = 0;
      var throws = new Site { OnFinish = () => throw new InvalidOperationException("native event after mutation") };
      var afterError = new Site();
      result = commands.Execute(() => new IConstructionTarget[] { throws, afterError }, e => ++logs);
      Check(result.Errors == 1 && logs == 1 && throws.Calls == 1, "原生异常仅记异常，不重试、不反向操作。");
      Check(afterError.Calls == 1 && result.Completed == 1, "单项异常不阻止其他工地。");
      result = commands.Execute(() => new IConstructionTarget[] { throws, afterError }, e => throw new Exception("logger"));
      Check(result.Errors == 1 && result.Completed == 1, "日志错误不伪造成功且不打断其余工地。");

      var deletedLater = new Site();
      var deletes = new Site { OnFinish = () => deletedLater.State = ConstructionTargetState.Invalid };
      result = commands.Execute(() => new IConstructionTarget[] { deletes, deletedLater });
      Check(deletedLater.Calls == 0 && result.Skipped == 1, "前一个工地引发失效时重新检查后续目标。");
      var next = new Site();
      var stops = new Site { OnFinish = () => enabled = false };
      result = commands.Execute(() => new IConstructionTarget[] { stops, next });
      Check(next.Calls == 0 && result.Completed == 1 && result.Message.Contains("已停止"), "场景/全局失效后停止剩余处理。");
      Check(!commands.Busy, "停止后释放执行保护。");
      enabled = true;

      ConstructionResult nested = null;
      int nestedSnapshots = 0;
      var reentrant = new Site { OnFinish = () => nested = commands.Execute(() => {
        ++nestedSnapshots; return new IConstructionTarget[] { next }; }) };
      result = commands.Execute(() => new IConstructionTarget[] { reentrant });
      Check(nestedSnapshots == 0 && next.Calls == 0 && nested.Message.Contains("已有批量完工"), "重入命令不枚举、不重复执行。");
      Check(reentrant.Calls == 1 && result.Completed == 1 && !commands.Busy, "重入不会破坏外层结果或执行保护。");
      result = commands.Execute(() => throw new InvalidOperationException("snapshot"));
      Check(result.Errors == 1 && !commands.Busy, "快照获取失败也释放保护。");
      result = commands.Execute(() => Array.Empty<IConstructionTarget>());
      Check(result.Message.Contains("没有符合条件") && result.Completed == 0, "无工地有明确提示。");

      var legacy = ConfigStore.Parse("{\"SchemaVersion\":1,\"Enabled\":true,\"InstantBuilding\":true,\"FreeBuilding\":true,\"LockHunger\":true}");
      Check(legacy.InstantBuilding && legacy.FreeBuilding, "旧JSON字段仍可解析。");
      var state = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1");
      state.Apply(legacy);
      Check(!state.Settings.InstantBuilding && !state.Settings.FreeBuilding, "旧true偏好在有效配置中统一停用。");
      Check(state.EffectiveEnabled && state.Settings.LockHunger && legacy.InstantBuilding, "保留其他配置且不修改调用者对象。");
      Check(next.Calls == 0, "应用配置不触发一次性命令。");
      state.RestoreDefaults();
      Check(!state.EffectiveEnabled && !state.Settings.InstantBuilding && next.Calls == 0, "恢复默认不触发或撤销完工。");
      var wrongVersion = new TrainerState("different", true, "6000.5.11f1");
      wrongVersion.Apply(legacy);
      var guarded = new ConstructionCommands(() => wrongVersion.EffectiveEnabled);
      result = guarded.Execute(() => { ++snapshots; return new IConstructionTarget[] { next }; });
      Check(snapshots == 1 && next.Calls == 0, "版本不匹配不能执行命令。");
      return "通过：" + _checks + " 项一次性施工命令/旧配置断言（模拟目标，不代表原生适配器实机通过）。";
    }
  }
}
