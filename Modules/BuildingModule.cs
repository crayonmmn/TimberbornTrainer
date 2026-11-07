using System;
using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.ConstructionSites;
using Timberborn.EntitySystem;
using Timberborn.TickSystem;
using TimberbornTrainer.Core;
using UnityEngine;

namespace TimberbornTrainer.Modules {
  public sealed class BuildingModule : ITickableSingleton {
    private readonly TrainerSession _session;
    private readonly EntityComponentRegistry _registry;
    private readonly ConstructionSiteBuildTimeCalculator _time;
    public bool RuntimeError { get; private set; }
    public bool FreeMaterialsImplemented => false;
    public int AdvancedSites { get; private set; }
    public int CompletedImmediately { get; private set; }

    public BuildingModule(TrainerSession session, EntityComponentRegistry registry, ConstructionSiteBuildTimeCalculator time) {
      _session = session; _registry = registry; _time = time;
    }
    public void Tick() {
      if (RuntimeError || _session.State == null || !_session.State.EffectiveEnabled) return;
      var config = _session.State.Settings;
      if (!config.InstantBuilding) return;
      try {
        // Completion can remove registry entries: never enumerate the live collection while mutating.
        var sites = _registry.GetEnabled<ConstructionSite>().ToArray();
        foreach (var site in sites) {
          if (!site || !site.Enabled) continue;
          // The free preference is intentionally not forwarded until a safe adapter exists.
          var block = site.GetComponent<BlockObject>();
          if (ConstructionEngine.TryAdvance(new NativeSite(site, _time), true, ConstructionMode.InstantOnly)) {
            ++AdvancedSites;
            // Read the native state after the call; advancing time is not proof of completion.
            if (block && !block.IsUnfinished) ++CompletedImmediately;
            Debug.Log("海狸修改工具：施工工时已推进；本场景推进次数=" + AdvancedSites +
              "，调用后原生完工次数=" + CompletedImmediately + "。未补充施工材料。");
          }
        }
      } catch (Exception e) {
        RuntimeError = true;
        Debug.LogError("海狸修改工具：瞬间施工出现异常（" + e.GetType().Name +
          "），已暂停本场景建造模块；请检查工地状态，不会自动重试。");
      }
    }
    private sealed class NativeSite : IConstructionSiteState {
      private readonly ConstructionSite _site;
      private readonly ConstructionSiteBuildTimeCalculator _time;
      public NativeSite(ConstructionSite site, ConstructionSiteBuildTimeCalculator time) { _site = site; _time = time; }
      public bool Unfinished => _site.GetComponent<BlockObject>().IsUnfinished;
      public bool IsOn => _site.IsOn;
      public bool MaterialsFull => _site.Inventory != null && _site.Inventory.IsFull;
      public float TotalHours => _time.GetConstructionTimeInHours(_site);
      public float ProgressHours => _site.BuildTimeProgressInHours;
      public void IncreaseTime(float hours) => _site.IncreaseBuildTime(hours);
    }
  }
}
