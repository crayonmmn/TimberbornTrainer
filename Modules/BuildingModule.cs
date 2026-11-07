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
          ConstructionEngine.TryAdvance(new NativeSite(site, _time), true, ConstructionMode.InstantOnly);
        }
      } catch (Exception) {
        RuntimeError = true;
        Debug.LogError("海狸修改工具：瞬间施工出现异常，已暂停本场景建造模块；请检查工地状态，不会自动重试。");
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
