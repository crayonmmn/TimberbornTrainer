using System;

namespace TimberbornTrainer.Modules {
  public enum ConstructionMode { Normal, InstantOnly, FreeOnly, InstantAndFree }

  public static class ConstructionRules {
    public static ConstructionMode Mode(bool instant, bool free) => instant
      ? (free ? ConstructionMode.InstantAndFree : ConstructionMode.InstantOnly)
      : (free ? ConstructionMode.FreeOnly : ConstructionMode.Normal);
    // Independent requests. Free materials deliberately have no live adapter yet.
    public static bool RequestsInstant(ConstructionMode mode) => mode == ConstructionMode.InstantOnly || mode == ConstructionMode.InstantAndFree;
    public static bool RequestsFree(ConstructionMode mode) => mode == ConstructionMode.FreeOnly || mode == ConstructionMode.InstantAndFree;
  }

  public interface IConstructionSiteState {
    bool Unfinished { get; }
    bool IsOn { get; }
    bool MaterialsFull { get; }
    float TotalHours { get; }
    float ProgressHours { get; }
    void IncreaseTime(float hours);
  }

  public static class ConstructionEngine {
    // Never supplies goods or calls FinishNow/MarkAsFinished. Native IncreaseBuildTime
    // retains finish validators, occupant checks, blockers, callbacks and state transition.
    public static bool TryAdvance(IConstructionSiteState site, bool globallyEnabled, ConstructionMode mode) {
      if (!globallyEnabled || !ConstructionRules.RequestsInstant(mode)) return false;
      if (!site.Unfinished || !site.IsOn || !site.MaterialsFull) return false;
      float total = site.TotalHours, progress = site.ProgressHours;
      if (float.IsNaN(total) || float.IsInfinity(total) || total <= 0 ||
          float.IsNaN(progress) || float.IsInfinity(progress) || progress < 0)
        throw new InvalidOperationException("施工时间异常，已停止瞬间施工。");
      if (progress >= total) return false;
      site.IncreaseTime(total - progress);
      return true;
    }
  }
}
