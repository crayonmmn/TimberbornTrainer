using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.Beavers;
using Timberborn.Characters;
using Timberborn.Effects;
using Timberborn.EntitySystem;
using Timberborn.NeedSystem;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using TimberbornTrainer.Core;
using UnityEngine;

namespace TimberbornTrainer.Modules {
  public sealed class BeaverModule : ITickableSingleton, IPostLoadableSingleton, IUnloadableSingleton {
    private TrainerSession _session;
    private CharacterPopulation _population;
    private TrainerState _state;
    private bool _ready;
    private readonly NeedLock<Beaver> _hunger = new NeedLock<Beaver>();
    private readonly NeedLock<Beaver> _thirst = new NeedLock<Beaver>();
    public bool RuntimeError { get; private set; }
    public int HungerCount => _hunger.Count;
    public int ThirstCount => _thirst.Count;
    public int UnsafeNeeds { get; private set; }
    public BeaverModule(TrainerSession session, CharacterPopulation population) { _session = session; _population = population; }
    public void PostLoad() {
      if (_ready || RuntimeError || _session == null || _population == null) return;
      _state = _session.State;
      if (_state == null) {
        RuntimeError = true;
        Debug.LogError("海狸修改工具：配置状态尚未就绪，本场景需求维持已停用。");
        return;
      }
      _state.Changed += SettingsChanged;
      _ready = true;
      SettingsChanged();
      // PostLoad only attaches configuration events. Entity access waits for simulation Tick.
    }
    private void SettingsChanged() {
      if (_state == null) return;
      var settings = _state.Settings;
      if (!_state.EffectiveEnabled || !settings.LockHunger) _hunger.Clear();
      if (!_state.EffectiveEnabled || !settings.LockThirst) _thirst.Clear();
      if (!_state.EffectiveEnabled || (!settings.LockHunger && !settings.LockThirst)) UnsafeNeeds = 0;
    }
    public void Tick() {
      if (!_ready || RuntimeError || _state == null || _population == null) return;
      SettingsChanged();
      if (!_state.EffectiveEnabled) return;
      var settings = _state.Settings;
      if (!settings.LockHunger && !settings.LockThirst) return;
      try {
        // Native population owns membership. Keep only a local snapshot, never a second roster.
        var characters = _population.Characters.ToArray();
        var present = new HashSet<Beaver>();
        UnsafeNeeds = 0;
        foreach (var character in characters) {
          if (!_ready || _state == null || !_state.EffectiveEnabled) return;
          settings = _state.Settings;
          Beaver beaver;
          NeedManager needs;
          if (!TryGetReady(character, out beaver, out needs)) continue;
          present.Add(beaver);
          if (settings.LockHunger && needs.HasNeed("Hunger") &&
              !_hunger.Maintain(beaver, new NativeNeed(needs, "Hunger"))) ++UnsafeNeeds;
          // A native effect can invoke callbacks; recheck before the second operation.
          if (!_ready || _state == null || !_state.EffectiveEnabled) return;
          settings = _state.Settings;
          var previousBeaver = beaver;
          if (!TryGetReady(character, out beaver, out needs)) { present.Remove(previousBeaver); continue; }
          if (settings.LockThirst && needs.HasNeed("Thirst") &&
              !_thirst.Maintain(beaver, new NativeNeed(needs, "Thirst"))) ++UnsafeNeeds;
        }
        _hunger.Prune(present); _thirst.Prune(present);
      } catch (Exception e) {
        RuntimeError = true; _hunger.Clear(); _thirst.Clear(); UnsafeNeeds = 0;
        Debug.LogError("海狸修改工具：需求维持出现异常（" + e.GetType().Name + "），已暂停本场景锁定，不会自动重试。");
      }
    }
    private static bool TryGetReady(Character character, out Beaver beaver, out NeedManager needs) {
      beaver = null; needs = null;
      if (!character || !character.Alive) return false;
      var entity = character.GetComponent<EntityComponent>();
      // CharacterCreatedEvent can precede the end of entity initialization.
      if (!entity || !entity.Initialized || entity.Deleted) return false;
      beaver = character.GetComponent<Beaver>();
      if (!beaver) return false;
      needs = character.GetComponent<NeedManager>();
      return needs;
    }
    public void Unload() {
      _ready = false;
      if (_state != null) _state.Changed -= SettingsChanged;
      _hunger.Clear(); _thirst.Clear(); UnsafeNeeds = 0;
      _state = null; _population = null; _session = null;
    }
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

