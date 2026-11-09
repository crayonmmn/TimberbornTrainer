using System;
using System.Collections.Generic;
using System.Linq;
using TimberbornTrainer.Core;
using TimberbornTrainer.Modules;

// Native interface substitutes: exercise the actual reflection adapter, not the running game.
namespace UnityEngine { public static class Time { public static float timeScale; } }
namespace Timberborn.SingletonSystem {
  public interface IPostLoadableSingleton { void PostLoad(); }
  public interface IUnloadableSingleton { void Unload(); }
}
namespace Timberborn.TimeSystem { public sealed class SpeedManager { public int CurrentSpeed; } }
namespace Timberborn.GameCycleSystem {
  public interface ICycleDuration { int DurationInDays { get; } }
  public sealed class GameCycleService {
    private int _cycleDurationInDays;
    private readonly IEnumerable<ICycleDuration> _cycleDurations;
    public int Cycle = 2, CycleDay = 1;
    public int Total { get => _cycleDurationInDays; set => _cycleDurationInDays = value; }
    public GameCycleService(params ICycleDuration[] sources) {
      _cycleDurations = sources; _cycleDurationInDays = sources.Sum(s => s.DurationInDays);
    }
    public int Registered => _cycleDurations.Sum(s => s.DurationInDays);
  }
}
namespace Timberborn.HazardousWeatherSystem {
  public sealed class DroughtWeather { }
  public sealed class BadtideWeather { }
  public sealed class HazardousWeatherService : Timberborn.GameCycleSystem.ICycleDuration {
    private int _duration = 4;
    public int Writes;
    public bool FailOnceAfterWrite;
    public int HazardousWeatherDuration { get => _duration; private set {
      ++Writes; _duration = value;
      if (FailOnceAfterWrite) { FailOnceAfterWrite = false; throw new Exception("access failure"); }
    } }
    public int DurationInDays => HazardousWeatherDuration;
    public object CurrentCycleHazardousWeather = new DroughtWeather();
  }
}
namespace Timberborn.WeatherSystem {
  public sealed class TemperateWeatherDurationService : Timberborn.GameCycleSystem.ICycleDuration {
    public int TemperateWeatherDuration = 5;
    public int DurationInDays => TemperateWeatherDuration;
  }
}
namespace TimberbornTrainer.Core { public sealed class TrainerSession { public TrainerState State; } }

namespace TimberbornTrainer.Tests {
  public static class WeatherTests {
    private static int _checks;
    private static void Check(bool ok, string message) { ++_checks; if (!ok) throw new Exception(message); }
    private sealed class Fixture {
      public TrainerState State = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1");
      public Timberborn.HazardousWeatherSystem.HazardousWeatherService Hazard = new Timberborn.HazardousWeatherSystem.HazardousWeatherService();
      public Timberborn.WeatherSystem.TemperateWeatherDurationService Temperate = new Timberborn.WeatherSystem.TemperateWeatherDurationService();
      public Timberborn.TimeSystem.SpeedManager Speed = new Timberborn.TimeSystem.SpeedManager();
      public Timberborn.GameCycleSystem.GameCycleService Cycle;
      public WeatherModule Module;
      public Fixture(bool extra = false) {
        State.SetEnabled(true); UnityEngine.Time.timeScale = 0;
        Cycle = extra ? new Timberborn.GameCycleSystem.GameCycleService(Hazard, Temperate, new ExtraDuration())
          : new Timberborn.GameCycleSystem.GameCycleService(Hazard, Temperate);
        Module = new WeatherModule(new TrainerSession { State = State }, Cycle, Hazard, Temperate, Speed);
      }
    }
    private sealed class ExtraDuration : Timberborn.GameCycleSystem.ICycleDuration { public int DurationInDays => 1; }
    private sealed class FailingAccess : IWeatherDurationAccess {
      public int Calls;
      public WeatherDurationSnapshot Read() => new WeatherDurationSnapshot(2, 1, 5, Calls == 0 ? 4 : 6, Calls == 0 ? 9 : 11, Calls == 0 ? 9 : 11, "干旱");
      public void Apply(WeatherDurationSnapshot b, int duration, int cycleDays) { ++Calls; throw new Exception("after change"); }
    }
    public static string Run() {
      _checks = 0;
      var f = new Fixture();
      Check(!f.Module.SetDuration("7").Success && f.Hazard.Writes == 0, "PostLoad前不能修改。");
      f.Module.PostLoad(); var r = f.Module.SetDuration("7");
      Check(r.Success && f.Hazard.HazardousWeatherDuration == 7 && f.Cycle.Total == 12 && f.Cycle.Registered == 12,
        "实际适配器反射同步原生时长与缓存，非仅修改倒计时。");
      Check(f.Cycle.Cycle == 2 && f.Cycle.CycleDay == 1 && f.Temperate.TemperateWeatherDuration == 5,
        "不推进周期、日期或改变正常季节长度。");
      Check(f.Module.SetDuration("7").Success && f.Hazard.Writes == 1, "相同时长不重复写入。");
      f = new Fixture(); f.Hazard.CurrentCycleHazardousWeather = new Timberborn.HazardousWeatherSystem.BadtideWeather();
      f.Module.PostLoad(); r = f.Module.SetDuration("6");
      Check(r.Success && r.After.HazardName == "恶水" && f.Module.Describe().Contains("当前：正常"), "正常期可改本周期已选恶水时长，不切换类型或天气阶段。");
      f = new Fixture(); f.Module.PostLoad(); f.Cycle.CycleDay = 8;
      Check(!f.Module.SetDuration("2").Success && f.Cycle.Total == 9 && f.Hazard.Writes == 0, "不能把周期缩短到已过去日期。");
      Check(f.Module.SetDuration("3").Success && f.Cycle.Total == 8 && f.Module.Describe().Contains("当前：干旱"), "可缩至当前最后一天，类型保持不变。");
      foreach (var input in new[] { "", "0", "-1", "1.5", "NaN", "1001", "2147483648" }) {
        f = new Fixture(); f.Module.PostLoad();
        Check(!f.Module.SetDuration(input).Success && f.Hazard.Writes == 0 && f.Cycle.Total == 9, "拒绝非法或越界天数：" + input);
      }
      f = new Fixture(); f.Module.PostLoad(); f.State.SetEnabled(false);
      Check(!f.Module.SetDuration("7").Success && f.Hazard.Writes == 0, "全局关闭不修改。");
      f.State.SetEnabled(true); f.Speed.CurrentSpeed = 1;
      Check(!f.Module.SetDuration("7").Success && f.Hazard.Writes == 0, "原生速度未暂停不修改。");
      f.Speed.CurrentSpeed = 0; UnityEngine.Time.timeScale = 1;
      Check(!f.Module.SetDuration("7").Success && f.Hazard.Writes == 0, "Unity时间未暂停不修改。");
      f = new Fixture(); f.Module.PostLoad(); f.Cycle.Total = 99;
      Check(!f.Module.SetDuration("7").Success && f.Hazard.Writes == 0, "总长缓存与注册源不一致拒绝。");
      f = new Fixture(true); f.Module.PostLoad();
      Check(!f.Module.SetDuration("7").Success && f.Hazard.Writes == 0, "额外周期类型不静默覆盖。");
      f = new Fixture(); f.Module.PostLoad(); f.Hazard.CurrentCycleHazardousWeather = new object();
      Check(!f.Module.SetDuration("7").Success && f.Hazard.Writes == 0, "自定义未知灾害不擅自修改。");
      f = new Fixture(); f.Module.PostLoad(); var old = f.Module.Read(); f.Cycle.CycleDay = 2;
      bool rejected = false;
      try { f.Module.Apply(old, 7, 12); } catch (InvalidOperationException) { rejected = true; }
      Check(rejected && f.Hazard.Writes == 0 && f.Cycle.Total == 9, "执行前重新读取，拒绝旧周期快照。");
      f = new Fixture(); f.Module.PostLoad(); f.Hazard.FailOnceAfterWrite = true;
      r = f.Module.SetDuration("7");
      Check(!r.Success && f.Hazard.HazardousWeatherDuration == 4 && f.Cycle.Total == 9 && f.Hazard.Writes == 2,
        "对无事件的两项原生整数赋值注入异常，恢复旧值，不重试原请求或报告成功。");
      f.Module.Unload();
      Check(!f.Module.Available && !f.Module.SetDuration("7").Success, "卸载后清理引用并拒绝操作。");
      var fail = new FailingAccess(); var command = new WeatherDurationCommands(fail, () => true);
      r = command.Execute("6");
      Check(!r.Success && r.After.HazardDays == 6 && fail.Calls == 1 && !command.Busy,
        "异常后尝试回读，不盲目重试或伪造成功。");
      return "本周期灾害时长：" + _checks + " 项实际适配器/替身及命令断言通过；不代表实机通过。";
    }
  }
}
