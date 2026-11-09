// Execute actual BeaverModule/NeedMaintenance with native-shaped range/update/appraisal behavior.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TimberbornTrainer.Core;
using TimberbornTrainer.Modules;
using Timberborn.Characters;
using Timberborn.EntitySystem;
using Timberborn.Beavers;
using Timberborn.NeedSystem;

namespace UnityEngine {
  public class Object { public bool Destroyed; public static implicit operator bool(Object v) => v != null && !v.Destroyed; }
  public static class Debug { public static void LogError(string message) { } }
}
namespace Timberborn.SingletonSystem {
  public interface IPostLoadableSingleton { void PostLoad(); } public interface IUnloadableSingleton { void Unload(); }
}
namespace Timberborn.TickSystem { public interface ITickableSingleton { void Tick(); } }
namespace Timberborn.EntitySystem { public class EntityComponent : UnityEngine.Object { public bool Initialized = true, Deleted; } }
namespace Timberborn.Beavers { public class Beaver : UnityEngine.Object { } }
namespace Timberborn.Characters {
  public class Character : UnityEngine.Object {
    public bool Alive = true; public readonly Dictionary<Type, object> Components = new Dictionary<Type, object>();
    public T GetComponent<T>() where T : class => Components.TryGetValue(typeof(T), out var v) ? v as T : null;
  }
  public class CharacterPopulation {
    public readonly List<Character> NativeCharacters = new List<Character>(); public int Reads;
    public IReadOnlyList<Character> Characters { get { ++Reads; return NativeCharacters; } }
  }
}
namespace Timberborn.NeedSystem {
  public sealed class NeedSpec { public float MinimumValue = -3, MaximumValue = 1, StartingValue = 1; }
  public sealed class Need {
    private bool _updateEnabled = true;
    public bool Enabled = true; public bool Updating => _updateEnabled;
    public void EnableUpdate() { _updateEnabled = true; } public void DisableUpdate() { _updateEnabled = false; }
  }
  public sealed class NeedManager : UnityEngine.Object {
    public readonly Dictionary<string, float> Values = new Dictionary<string, float> { ["Hunger"] = .6f, ["Thirst"] = .8f, ["Sleep"] = .7f };
    private readonly Dictionary<string, Need> _needs = new Dictionary<string, Need>();
    public readonly NeedSpec Spec = new NeedSpec(); public int Writes, Reads, FoodActions, WaterActions; public bool FailRead;
    public Action Callback;
    private Need GetNeed(string id) { if (!_needs.TryGetValue(id, out var n)) _needs.Add(id, n = new Need()); return n; }
    public Need Native(string id) => GetNeed(id);
    public bool HasNeed(string id) => Values.ContainsKey(id);
    public NeedSpec GetNeedSpec(string id) => Spec;
    public float GetNeedPoints(string id) { ++Reads; if (FailRead) throw new InvalidOperationException(); return Values[id]; }
    public void EnableUpdate(string id) => GetNeed(id).EnableUpdate(); public void DisableUpdate(string id) => GetNeed(id).DisableUpdate();
    public void ResetNeed(string id) { ++Writes; Values[id] = Math.Max(Spec.MinimumValue, Math.Min(Spec.MaximumValue, Spec.StartingValue)); Callback?.Invoke(); }
    public bool CanConsume(string id) => GetNeed(id).Enabled && Math.Floor((Spec.MaximumValue - Values[id]) / .2f) >= 1;
    public void Advance(float days) {
      foreach (var id in Values.Keys.ToArray()) if (GetNeed(id).Updating && GetNeed(id).Enabled)
        Values[id] = Math.Max(Spec.MinimumValue, Math.Min(Spec.MaximumValue, Values[id] - days * (id == "Hunger" ? .8f : .7f)));
    }
    public void TryMeals() {
      if (CanConsume("Hunger")) { ++FoodActions; Values["Hunger"] = Math.Min(1, Values["Hunger"] + .2f); }
      if (CanConsume("Thirst")) { ++WaterActions; Values["Thirst"] = Math.Min(1, Values["Thirst"] + .2f); }
    }
  }
}
namespace TimberbornTrainer.Core { public class TrainerSession { public TrainerState State { get; set; } } }
namespace TimberbornTrainer.Tests {
  public static class BeaverLifecycleTests {
    private static int _checks;
    private static void Check(bool ok, string message) { ++_checks; if (!ok) throw new Exception(message); }
    private static TrainerState State(bool h = true, bool t = true) {
      var s = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1"); s.Apply(new Config { Enabled = true, LockHunger = h, LockThirst = t }); return s;
    }
    private static Character Character(bool initialized = true) {
      var c = new Character(); c.Components[typeof(EntityComponent)] = new EntityComponent { Initialized = initialized };
      c.Components[typeof(Beaver)] = new Beaver(); c.Components[typeof(NeedManager)] = new NeedManager(); return c;
    }
    private static int Subscriptions(TrainerState s) => (typeof(TrainerState).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s) as Delegate)?.GetInvocationList().Length ?? 0;
    public static string Run() {
      _checks = 0;
      for (int mask = 0; mask < 4; ++mask) {
        bool h = (mask & 1) != 0, t = (mask & 2) != 0; var s = State(h, t); var p = new CharacterPopulation(); var c = Character(); p.NativeCharacters.Add(c);
        var n = c.GetComponent<NeedManager>(); var m = new BeaverModule(new TrainerSession { State = s }, p);
        m.Tick(); Check(p.Reads == 0, "初始化前不处理。"); m.PostLoad(); m.PostLoad();
        Check(p.Reads == 0 && Subscriptions(s) == 1, "PostLoad只订阅一次，不访问实体。"); m.Tick();
        Check(n.Values["Hunger"] == (h ? 1 : .6f) && n.Values["Thirst"] == (t ? 1 : .8f), "启用项立即达到原生正常上限，不捕获低基线。");
        Check(n.Native("Hunger").Updating == !h && n.Native("Thirst").Updating == !t, "只停用对应单项原生扣减，不禁用需求或行为系统。");
        int writes = n.Writes;
        for (int i = 0; i < 10000; ++i) { n.Advance(.005f); m.Tick(); n.TryMeals(); }
        Check((!h || n.Values["Hunger"] == 1) && (!t || n.Values["Thirst"] == 1) && n.Values.Values.All(v => v <= 1 && v >= -3), "累计模拟50天仍在原生范围内，没有隐藏储备。");
        Check((h ? n.FoodActions == 0 : n.FoodActions > 0) && (t ? n.WaterActions == 0 : n.WaterActions > 0), "按原生非浪费评估条件，满值锁定项不产生新进食/饮水动作。");
        Check(n.Writes == writes && n.Values["Sleep"] < .7f, "无需累计补差，其他需求继续自然变化。");
        s.SetEnabled(false); Check(n.Native("Hunger").Updating && n.Native("Thirst").Updating && m.HungerCount == 0 && m.ThirstCount == 0, "关闭恢复单项更新并释放持有状态。");
        float old = n.Values["Hunger"]; n.Advance(.1f); m.Tick(); Check(n.Values["Hunger"] < old || old == -3, "关闭后原生变化继续，不补回。");
        s.SetEnabled(true); m.Tick(); Check((!h || n.Values["Hunger"] == 1) && (!t || n.Values["Thirst"] == 1), "重新启用恢复正常上限，不继承低值或累计量。");
        m.Unload(); m.Unload(); Check(Subscriptions(s) == 0 && n.Native("Hunger").Updating && n.Native("Thirst").Updating, "卸载恢复并取消订阅。");
      }
      var state = State(); var pop = new CharacterPopulation(); var adult = Character(); var child = Character(false); var dead = Character(); dead.Alive = false;
      var deleted = Character(); deleted.GetComponent<EntityComponent>().Deleted = true;
      var noNeeds = Character(); noNeeds.Components.Remove(typeof(NeedManager)); var noBeaver = Character(); noBeaver.Components.Remove(typeof(Beaver));
      pop.NativeCharacters.AddRange(new[] { adult, child, dead, deleted, noNeeds, noBeaver, null });
      var module = new BeaverModule(new TrainerSession { State = state }, pop); module.PostLoad(); module.Tick();
      Check(module.HungerCount == 1 && child.GetComponent<NeedManager>().Reads == 0 && dead.GetComponent<NeedManager>().Reads == 0, "忽略未初始化、死亡、删除和缺组件对象。");
      child.GetComponent<EntityComponent>().Initialized = true; module.Tick(); Check(module.HungerCount == 2 && child.GetComponent<NeedManager>().Values["Hunger"] == 1, "新生完成初始化后纳入。");
      pop.NativeCharacters.Remove(child); module.Tick(); Check(module.HungerCount == 1 && child.GetComponent<NeedManager>().Native("Hunger").Updating, "移除时恢复原更新状态且不保留旧对象。");
      adult.Alive = false; module.Tick(); Check(module.HungerCount == 0 && adult.GetComponent<NeedManager>().Native("Hunger").Updating, "死亡对象释放持有状态。"); module.Unload();
      var ownedState = State(); var ownedPop = new CharacterPopulation(); var owned = Character(); ownedPop.NativeCharacters.Add(owned);
      var ownedNeeds = owned.GetComponent<NeedManager>(); ownedNeeds.DisableUpdate("Hunger");
      var ownedModule = new BeaverModule(new TrainerSession { State = ownedState }, ownedPop); ownedModule.PostLoad(); ownedModule.Tick(); ownedState.SetEnabled(false);
      Check(!ownedNeeds.Native("Hunger").Updating && ownedNeeds.Native("Thirst").Updating, "保留启用前其他系统已有的单项停更状态，不误开启。"); ownedModule.Unload();
      var callbackState = State(); var callbackPop = new CharacterPopulation(); var callbackChar = Character(); callbackPop.NativeCharacters.Add(callbackChar);
      var callbackNeeds = callbackChar.GetComponent<NeedManager>(); var callbackModule = new BeaverModule(new TrainerSession { State = callbackState }, callbackPop);
      callbackNeeds.Callback = () => callbackState.SetEnabled(false); callbackModule.PostLoad(); callbackModule.Tick(); callbackNeeds.Callback = null;
      Check(callbackModule.HungerCount == 0 && callbackModule.ThirstCount == 0 && callbackNeeds.Native("Hunger").Updating && callbackNeeds.Values["Thirst"] == .8f,
        "原生重置事件内关闭全局也恢复更新，不继续处理另一项。"); callbackModule.Unload();
      var disableState = State(); var disablePop = new CharacterPopulation(); var disableChar = Character(); disablePop.NativeCharacters.Add(disableChar);
      var disableNeeds = disableChar.GetComponent<NeedManager>(); var disableModule = new BeaverModule(new TrainerSession { State = disableState }, disablePop); disableModule.PostLoad(); disableModule.Tick();
      disableNeeds.Native("Hunger").Enabled = false; disableModule.Tick();
      Check(disableNeeds.Native("Hunger").Updating && disableModule.HungerCount == 0, "需求被其他系统禁用也归还更新状态，避免日后重新启用时永久停更。"); disableModule.Unload();
      var errorState = State(); var errorPop = new CharacterPopulation(); var errorChar = Character(); errorPop.NativeCharacters.Add(errorChar);
      var errorModule = new BeaverModule(new TrainerSession { State = errorState }, errorPop); errorModule.PostLoad(); errorModule.Tick(); errorChar.GetComponent<NeedManager>().FailRead = true; errorModule.Tick();
      Check(errorModule.RuntimeError && errorChar.GetComponent<NeedManager>().Native("Hunger").Updating && errorModule.HungerCount == 0, "读取异常停用并恢复本模块持有的更新开关。"); errorModule.Unload();
      var loadedState = State(); loadedState.SetEnabled(false); var loadedPop = new CharacterPopulation(); var loaded = Character(); loadedPop.NativeCharacters.Add(loaded);
      loaded.GetComponent<NeedManager>().Values["Hunger"] = 1; var loadedModule = new BeaverModule(new TrainerSession { State = loadedState }, loadedPop); loadedModule.PostLoad(); loadedModule.Tick();
      loaded.GetComponent<NeedManager>().Advance(.1f); Check(loaded.GetComponent<NeedManager>().Values["Hunger"] < 1 && loadedPop.Reads == 0, "新场景加载全局关闭、更新默认启用，正常值不是永久储备。"); loadedModule.Unload();
      return "维生实际模块/原生范围与更新行为替身：" + _checks + " 项通过；非实机行为证明。";
    }
  }
}
