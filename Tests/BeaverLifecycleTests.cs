// Test doubles only. This assembly runs the real BeaverModule without loading Unity/game code.
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
  public class Object {
    public bool Destroyed;
    public static implicit operator bool(Object value) => value != null && !value.Destroyed;
  }
  public static class Debug { public static void LogError(string message) { } }
}
namespace Timberborn.SingletonSystem {
  public interface IPostLoadableSingleton { void PostLoad(); }
  public interface IUnloadableSingleton { void Unload(); }
}
namespace Timberborn.TickSystem { public interface ITickableSingleton { void Tick(); } }
namespace Timberborn.EntitySystem {
  public class EntityComponent : UnityEngine.Object { public bool Initialized = true, Deleted; }
}
namespace Timberborn.Beavers { public class Beaver : UnityEngine.Object { } }
namespace Timberborn.Effects {
  public struct InstantEffect {
    public string NeedId;
    public float Points;
    public int Count;
    public InstantEffect(string id, float points, int count) { NeedId = id; Points = points; Count = count; }
  }
}
namespace Timberborn.Characters {
  public class Character : UnityEngine.Object {
    public bool Alive = true;
    public readonly Dictionary<Type, object> Components = new Dictionary<Type, object>();
    public T GetComponent<T>() where T : class => Components.TryGetValue(typeof(T), out var value) ? value as T : null;
  }
  public class CharacterPopulation {
    public readonly List<Character> NativeCharacters = new List<Character>();
    public int Reads;
    public IReadOnlyList<Character> Characters { get { ++Reads; return NativeCharacters; } }
  }
}
namespace Timberborn.NeedSystem {
  public class NeedManager : UnityEngine.Object {
    public readonly Dictionary<string, float> Values = new Dictionary<string, float> { ["Hunger"] = .6f, ["Thirst"] = .8f };
    public int Writes, Reads;
    public Action Callback;
    public bool FailRead;
    public bool HasNeed(string id) => Values.ContainsKey(id);
    public float GetNeedPoints(string id) { ++Reads; if (FailRead) throw new InvalidOperationException(); return Values[id]; }
    public void ApplyEffect(in Timberborn.Effects.InstantEffect effect) {
      ++Writes; Values[effect.NeedId] += effect.Points * effect.Count; Callback?.Invoke();
    }
  }
}
namespace TimberbornTrainer.Core { public class TrainerSession { public TrainerState State { get; set; } } }

namespace TimberbornTrainer.Tests {
  public static class BeaverLifecycleTests {
    private static int _checks;
    private static void Check(bool value, string message) { ++_checks; if (!value) throw new Exception(message); }
    private static TrainerState State() {
      var state = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1");
      state.Apply(new Config { Enabled = true, LockHunger = true, LockThirst = true });
      return state;
    }
    private static Character Character(bool initialized = true) {
      var c = new Character();
      c.Components[typeof(EntityComponent)] = new EntityComponent { Initialized = initialized };
      c.Components[typeof(Beaver)] = new Beaver();
      c.Components[typeof(NeedManager)] = new NeedManager();
      return c;
    }
    private static int Subscriptions(TrainerState state) =>
      (typeof(TrainerState).GetField("Changed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(state) as Delegate)?.GetInvocationList().Length ?? 0;
    public static string Run() {
      _checks = 0;
      var state = State(); var session = new TrainerSession { State = state }; var population = new CharacterPopulation();
      var adult = Character(); var newborn = Character(false); var dead = Character(); dead.Alive = false;
      var deleted = Character(); deleted.GetComponent<EntityComponent>().Deleted = true;
      var notBeaver = Character(); notBeaver.Components.Remove(typeof(Beaver));
      var noNeeds = Character(); noNeeds.Components.Remove(typeof(NeedManager));
      var noEntity = Character(); noEntity.Components.Remove(typeof(EntityComponent));
      var destroyed = Character(); destroyed.Destroyed = true;
      population.NativeCharacters.AddRange(new[] { adult, newborn, dead, deleted, notBeaver, noNeeds, noEntity, destroyed, null });
      var module = new BeaverModule(session, population);
      module.Tick();
      Check(population.Reads == 0 && module.HungerCount == 0, "PostLoad 前不得枚举或处理。");
      module.PostLoad(); module.PostLoad();
      Check(population.Reads == 0 && Subscriptions(state) == 1, "PostLoad 不碰实体，重复调用不重复订阅。");
      module.Tick();
      Check(module.HungerCount == 1 && module.ThirstCount == 1, "仅接纳初始化完成的存活海狸。");
      Check(newborn.GetComponent<NeedManager>().Reads == 0 && dead.GetComponent<NeedManager>().Reads == 0,
        "出生初始化间隙及死亡对象不得访问需求。");
      newborn.GetComponent<EntityComponent>().Initialized = true; module.Tick();
      Check(module.HungerCount == 2 && module.ThirstCount == 2, "后续 Tick 纳入已完成初始化的新生对象。");
      var needs = adult.GetComponent<NeedManager>();
      needs.Values["Hunger"] = .4f; needs.Values["Thirst"] = .5f; module.Tick();
      Check(Math.Abs(needs.Values["Hunger"] - .6f) < .0001f && Math.Abs(needs.Values["Thirst"] - .8f) < .0001f,
        "既有需求效果与回读保留。");
      var config = state.Settings; config.LockHunger = false; state.Apply(config);
      Check(module.HungerCount == 0 && module.ThirstCount == 2, "独立关闭立即清理对应基线。");
      needs.Values["Hunger"] = .3f; module.Tick();
      Check(needs.Values["Hunger"] == .3f, "关闭后不补偿自然变化，也不回滚数值。");
      state.SetEnabled(false);
      int reads = population.Reads, writes = needs.Writes; module.Tick();
      Check(module.HungerCount == 0 && module.ThirstCount == 0 && population.Reads == reads && needs.Writes == writes,
        "全局关闭清理基线并停止处理。");
      module.Unload(); module.Unload(); module.Tick();
      Check(Subscriptions(state) == 0 && population.Reads == reads, "重复卸载安全，旧场景不再读取或订阅。");
      var flags = BindingFlags.Instance | BindingFlags.NonPublic;
      Check(new[] { "_session", "_population", "_state" }.All(name => typeof(BeaverModule).GetField(name, flags).GetValue(module) == null),
        "卸载释放场景服务与状态引用。");
      state.SetEnabled(true); module.PostLoad(); module.Tick();
      Check(population.Reads == reads && Subscriptions(state) == 0, "卸载后的旧实例不能重新接入旧场景。");
      var secondState = State(); var secondPopulation = new CharacterPopulation(); var secondAdult = Character();
      secondAdult.GetComponent<NeedManager>().Values["Hunger"] = .9f;
      secondPopulation.NativeCharacters.Add(secondAdult);
      var second = new BeaverModule(new TrainerSession { State = secondState }, secondPopulation);
      second.PostLoad(); second.Tick();
      Check(second.HungerCount == 1 && module.HungerCount == 0 && Subscriptions(secondState) == 1,
        "重新进入使用新实例、新场景及新基线。");
      secondAdult.GetComponent<NeedManager>().Values["Hunger"] = .7f; second.Tick();
      Check(Math.Abs(secondAdult.GetComponent<NeedManager>().Values["Hunger"] - .9f) < .0001f, "新场景不继承旧需求基线。");

      var later = Character(); var removed = Character(); secondPopulation.NativeCharacters.Add(removed); second.Tick();
      var secondNeeds = secondAdult.GetComponent<NeedManager>(); secondNeeds.Values["Hunger"] = .5f;
      secondNeeds.Callback = () => { removed.Alive = false; secondPopulation.NativeCharacters.Remove(removed); secondPopulation.NativeCharacters.Add(later); };
      second.Tick(); secondNeeds.Callback = null;
      Check(!second.RuntimeError && second.HungerCount == 1 && later.GetComponent<NeedManager>().Reads == 0,
        "原生回调改变集合时使用快照，新对象留待后续 Tick。");
      second.Tick();
      Check(second.HungerCount == 2, "后续发现新增对象且删除死亡基线。");
      secondNeeds.Values["Hunger"] = .4f;
      secondNeeds.Callback = () => secondState.SetEnabled(false);
      int thirstReads = secondNeeds.Reads; second.Tick(); secondNeeds.Callback = null;
      Check(second.HungerCount == 0 && second.ThirstCount == 0 && !second.RuntimeError,
        "原生回调关闭全局后不能重新建立基线或继续另一项需求处理。");
      Check(secondNeeds.Reads == thirstReads + 2, "回调关闭后只有本次饥饿读取和回读，没有口渴读取。");
      secondState.SetEnabled(true); second.Tick(); secondNeeds.FailRead = true; second.Tick();
      Check(second.RuntimeError && second.HungerCount == 0 && second.ThirstCount == 0, "异常后清理并停用本场景维持。");
      reads = secondPopulation.Reads; second.Tick();
      Check(secondPopulation.Reads == reads, "异常不自动重复执行。");
      second.Unload(); Check(Subscriptions(secondState) == 0, "异常后仍能卸载清理订阅。");
      var missing = new BeaverModule(new TrainerSession(), new CharacterPopulation()); missing.PostLoad(); missing.Tick(); missing.Unload();
      Check(missing.RuntimeError, "状态未就绪只停用模块，不提前访问实体。");
      CheckIndependentNeeds();
      CheckMembershipChanges();
      return "通过：" + _checks + " 项实际 BeaverModule 生命周期断言（游戏接口替代实现，非实机验收）。";
    }
    private static void CheckIndependentNeeds() {
      for (int mask = 0; mask < 4; ++mask) {
        var state = State();
        var config = new Config { Enabled = true, LockHunger = (mask & 1) != 0, LockThirst = (mask & 2) != 0 };
        state.Apply(config);
        var population = new CharacterPopulation(); var beaver = Character(); population.NativeCharacters.Add(beaver);
        var needs = beaver.GetComponent<NeedManager>(); needs.Values["Sleep"] = .33f;
        var module = new BeaverModule(new TrainerSession { State = state }, population); module.PostLoad(); module.Tick();
        Check(module.HungerCount == (config.LockHunger ? 1 : 0) && module.ThirstCount == (config.LockThirst ? 1 : 0), "四种维生组合独立捕获真实业务基线。");
        needs.Values["Hunger"] = .2f; needs.Values["Thirst"] = .25f; module.Tick();
        Check(Math.Abs(needs.Values["Hunger"] - (config.LockHunger ? .6f : .2f)) < .0001f &&
          Math.Abs(needs.Values["Thirst"] - (config.LockThirst ? .8f : .25f)) < .0001f,
          "真实BeaverModule仅补偿已启用需求，关闭项保留模拟原生下降。");
        Check(needs.Values["Sleep"] == .33f && needs.Writes == (config.LockHunger ? 1 : 0) + (config.LockThirst ? 1 : 0),
          "效果次数与独立开关一致，不影响其他需求。");
        config.LockHunger = config.LockThirst = false; state.Apply(config);
        int writes = needs.Writes, reads = needs.Reads; needs.Values["Hunger"] = needs.Values["Thirst"] = .1f; module.Tick();
        Check(needs.Writes == writes && needs.Reads == reads && module.HungerCount == 0 && module.ThirstCount == 0,
          "关闭后不继续读写需求，也不保留永久锁定基线。");
        config.LockHunger = config.LockThirst = true; state.Apply(config); module.PostLoad(); module.Tick();
        needs.Values["Hunger"] = needs.Values["Thirst"] = .05f; module.Tick();
        Check(Math.Abs(needs.Values["Hunger"] - .1f) < .0001f && Math.Abs(needs.Values["Thirst"] - .1f) < .0001f && Subscriptions(state) == 1,
          "反复启停只订阅一次，重启捕获新的较低基线，不恢复旧值。");
        module.Unload(); Check(Subscriptions(state) == 0, "每种开关组合结束都清理事件订阅。");
      }
    }
    private static void CheckMembershipChanges() {
      var state = State(); var population = new CharacterPopulation(); var adult = Character(); population.NativeCharacters.Add(adult);
      var module = new BeaverModule(new TrainerSession { State = state }, population); module.PostLoad(); module.Tick();
      var joined = Character(); var needs = joined.GetComponent<NeedManager>();
      needs.Values["Hunger"] = .45f; needs.Values["Thirst"] = .55f; population.NativeCharacters.Add(joined); module.Tick();
      needs.Values["Hunger"] = needs.Values["Thirst"] = .1f; module.Tick();
      Check(module.HungerCount == 2 && Math.Abs(needs.Values["Hunger"] - .45f) < .0001f && Math.Abs(needs.Values["Thirst"] - .55f) < .0001f,
        "新加入的初始化完成海狸自动捕获自身基线并真正获得补偿。");
      adult.Alive = false; int deadReads = adult.GetComponent<NeedManager>().Reads; module.Tick();
      Check(module.HungerCount == 1 && module.ThirstCount == 1 && adult.GetComponent<NeedManager>().Reads == deadReads,
        "死亡对象不再访问需求，清理两种基线。");
      population.NativeCharacters.Remove(joined); module.Tick();
      Check(module.HungerCount == 0 && module.ThirstCount == 0, "仍有引用但离开原生人口集合也释放基线。");
      needs.Values["Hunger"] = .3f; population.NativeCharacters.Add(joined); module.Tick();
      needs.Values["Hunger"] = .1f; module.Tick();
      Check(Math.Abs(needs.Values["Hunger"] - .3f) < .0001f, "移除后重新加入不沿用旧基线。");
      needs.Values["Hunger"] = needs.Values["Thirst"] = .1f;
      needs.Callback = () => state.Apply(new Config { Enabled = true, LockHunger = false, LockThirst = true });
      module.Tick(); needs.Callback = null;
      Check(!module.RuntimeError && module.HungerCount == 0 && module.ThirstCount == 1,
        "效果回调独立关闭饥饿时，不重建已关闭基线，口渴仍保持。");
      int writes = needs.Writes; needs.Values["Hunger"] = .05f; needs.Values["Thirst"] = .2f; module.Tick();
      Check(needs.Values["Hunger"] == .05f && Math.Abs(needs.Values["Thirst"] - .55f) < .0001f && needs.Writes == writes + 1 && Subscriptions(state) == 1,
        "后续Tick只维持口渴，没有重复订阅或饥饿残留处理。");
      module.Unload(); Check(Subscriptions(state) == 0, "人口变化与回调测试后仍可正常卸载。");
    }
  }
}
