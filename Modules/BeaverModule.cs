using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Beavers;
using Timberborn.Effects;
using Timberborn.NeedSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using TimberbornTrainer.Core;
using UnityEngine;

namespace TimberbornTrainer.Modules {
  public sealed class BeaverModule : ITickableSingleton, ILoadableSingleton, IUnloadableSingleton {
    private readonly TrainerSession _session;
    private readonly BeaverCollection _collection;
    private readonly NeedLock<Beaver> _hunger = new NeedLock<Beaver>();
    private readonly NeedLock<Beaver> _thirst = new NeedLock<Beaver>();
    public bool RuntimeError { get; private set; }
    public int HungerCount => _hunger.Count;
    public int ThirstCount => _thirst.Count;
    public int UnsafeNeeds { get; private set; }
    public BeaverModule(TrainerSession session, BeaverCollection collection) { _session = session; _collection = collection; }
    public void Load() { _session.State.Changed += SettingsChanged; }
    private void SettingsChanged() {
      var settings = _session.State.Settings;
      if (!_session.State.EffectiveEnabled || !settings.LockHunger) _hunger.Clear();
      if (!_session.State.EffectiveEnabled || !settings.LockThirst) _thirst.Clear();
    }
    public void Tick() {
      if (RuntimeError || _session.State == null) return;
      SettingsChanged();
      if (!_session.State.EffectiveEnabled) return;
      var settings = _session.State.Settings;
      if (!settings.LockHunger && !settings.LockThirst) return;
      try {
        var beavers = _collection.Beavers.Where(b => b).ToArray();
        var present = new HashSet<Beaver>(beavers);
        _hunger.Prune(present); _thirst.Prune(present);
        UnsafeNeeds = 0;
        foreach (var beaver in beavers) {
          var needs = beaver.GetComponent<NeedManager>();
          if (!needs) continue;
          if (settings.LockHunger && needs.HasNeed("Hunger") &&
              !_hunger.Maintain(beaver, new NativeNeed(needs, "Hunger"))) ++UnsafeNeeds;
          if (settings.LockThirst && needs.HasNeed("Thirst") &&
              !_thirst.Maintain(beaver, new NativeNeed(needs, "Thirst"))) ++UnsafeNeeds;
        }
      } catch (Exception e) {
        RuntimeError = true; _hunger.Clear(); _thirst.Clear();
        Debug.LogError("海狸修改工具：需求维持出现异常（" + e.GetType().Name + "），已暂停本场景锁定，不会自动重试。");
      }
    }
    public void Unload() { if (_session.State != null) _session.State.Changed -= SettingsChanged; _hunger.Clear(); _thirst.Clear(); }
    private sealed class NativeNeed : INeedValue {
      private readonly NeedManager _needs;
      private readonly string _id;
      public NativeNeed(NeedManager needs, string id) { _needs = needs; _id = id; }
      public float Read() => _needs.GetNeedPoints(_id);
      public void AddEffect(float points) {
        var effect = new InstantEffect(_id, points, 1);
        _needs.ApplyEffect(in effect);
      }
    }
  }
}

