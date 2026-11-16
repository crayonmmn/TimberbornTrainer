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
      if (Busy) { result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0412"); return result; }
      if (!_enabled()) { result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0324"); return result; }
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
      result.Message = (interrupted ? global::TimberbornTrainer.Localization.TextToken.Key("t0137") : total == 0 ?
        global::TimberbornTrainer.Localization.TextToken.Key("t0265") : global::TimberbornTrainer.Localization.TextToken.Key("t0116")) +
        global::TimberbornTrainer.Localization.TextToken.Key("t0027") + result.Completed + global::TimberbornTrainer.Localization.TextToken.Key("t0073") + result.Unfinished +
        global::TimberbornTrainer.Localization.TextToken.Key("t0065") + result.Skipped + global::TimberbornTrainer.Localization.TextToken.Key("t0080") + result.Errors + global::TimberbornTrainer.Localization.TextToken.Key("t0049") + result.Unconfirmed + global::TimberbornTrainer.Localization.TextToken.Key("t0096");
      if (result.Errors + result.Unconfirmed > 0)
        result.Message += global::TimberbornTrainer.Localization.TextToken.Key("t0021");
      return result;
    }

    private static void Report(Action<Exception> logError, Exception error) {
      // A logger failure must not interrupt other sites or turn a native error into success.
      try { logError?.Invoke(error); } catch { }
    }
  }
}
