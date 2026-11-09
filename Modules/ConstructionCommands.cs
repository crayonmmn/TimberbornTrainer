using System;

namespace TimberbornTrainer.Modules {
  public enum ConstructionTargetState { NotApplicable, Invalid, Blocked, Ready }
  public enum ConstructionCompletion { Completed, Unfinished, Skipped, Unconfirmed }

  public interface IConstructionTarget {
    ConstructionTargetState Inspect();
    ConstructionCompletion FinishNow();
  }

  public sealed class ConstructionResult {
    public int Completed { get; internal set; }
    public int Unfinished { get; internal set; }
    public int Skipped { get; internal set; }
    public int Errors { get; internal set; }
    public int Unconfirmed { get; internal set; }
    public string Message { get; internal set; }
  }

  // One explicit invocation, one snapshot, no retained targets or automatic retry.
  public sealed class ConstructionCommands {
    private readonly Func<bool> _enabled;
    public bool Busy { get; private set; }
    public ConstructionCommands(Func<bool> enabled) { _enabled = enabled; }

    public ConstructionResult Execute(Func<IConstructionTarget[]> snapshot, Action<Exception> logError = null) {
      var result = new ConstructionResult();
      if (Busy) { result.Message = "已有批量完工正在执行，本次未执行。"; return result; }
      if (!_enabled()) { result.Message = "全局修改器已关闭、版本不匹配或场景已卸载，未执行。"; return result; }
      Busy = true;
      bool interrupted = false;
      try {
        var targets = snapshot();
        foreach (var target in targets) {
          if (!_enabled()) { interrupted = true; break; }
          try {
            var state = target == null ? ConstructionTargetState.Invalid : target.Inspect();
            if (state == ConstructionTargetState.NotApplicable) continue;
            if (state == ConstructionTargetState.Invalid) { ++result.Skipped; continue; }
            if (state == ConstructionTargetState.Blocked) { ++result.Unfinished; continue; }
            if (!_enabled()) { interrupted = true; break; }
            switch (target.FinishNow()) {
              case ConstructionCompletion.Completed: ++result.Completed; break;
              case ConstructionCompletion.Unfinished: ++result.Unfinished; break;
              case ConstructionCompletion.Skipped: ++result.Skipped; break;
              default: ++result.Unconfirmed; break;
            }
          } catch (Exception e) { ++result.Errors; Report(logError, e); }
        }
      } catch (Exception e) { ++result.Errors; Report(logError, e); }
      finally { Busy = false; }
      int total = result.Completed + result.Unfinished + result.Skipped + result.Errors + result.Unconfirmed;
      result.Message = (interrupted ? "场景或全局状态变化，本次已停止。" : total == 0 ?
        "没有符合条件的未完工工地。" : "本次批量完工已结束。") +
        " 确认完成 " + result.Completed + "；未完成或受阻 " + result.Unfinished +
        "；失效或跳过 " + result.Skipped + "；异常 " + result.Errors + "；结果未确认 " + result.Unconfirmed + "。";
      if (result.Errors + result.Unconfirmed > 0)
        result.Message += " 部分工地可能已经变化，请检查现场；不会自动重试或回滚。";
      return result;
    }

    private static void Report(Action<Exception> logError, Exception error) {
      // A logger failure must not interrupt other sites or turn a native error into success.
      try { logError?.Invoke(error); } catch { }
    }
  }
}
