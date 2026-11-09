using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Timberborn.GameCycleSystem;
using Timberborn.HazardousWeatherSystem;
using Timberborn.SingletonSystem;
using Timberborn.TimeSystem;
using Timberborn.WeatherSystem;
using TimberbornTrainer.Core;
using UnityEngine;

namespace TimberbornTrainer.Modules {
  public sealed class WeatherModule : IPostLoadableSingleton, IUnloadableSingleton, IWeatherDurationAccess {
    private TrainerSession _session;
    private GameCycleService _cycle;
    private HazardousWeatherService _hazard;
    private TemperateWeatherDurationService _temperate;
    private SpeedManager _speed;
    private bool _ready;
    private readonly MethodInfo _setDuration;
    private readonly FieldInfo _cycleDays, _durations;
    public WeatherDurationCommands Duration { get; }
    public WeatherDurationResult LastResult { get; private set; }
    public bool Available => _ready && _session?.State != null && _session.State.EffectiveEnabled &&
      _speed.CurrentSpeed == 0 && Time.timeScale == 0 && _setDuration != null && _cycleDays != null && _durations != null;

    public WeatherModule(TrainerSession session, GameCycleService cycle, HazardousWeatherService hazard,
        TemperateWeatherDurationService temperate, SpeedManager speed) {
      _session = session; _cycle = cycle; _hazard = hazard; _temperate = temperate; _speed = speed;
      _setDuration = typeof(HazardousWeatherService).GetProperty("HazardousWeatherDuration")?.GetSetMethod(true);
      _cycleDays = typeof(GameCycleService).GetField("_cycleDurationInDays", BindingFlags.Instance | BindingFlags.NonPublic);
      _durations = typeof(GameCycleService).GetField("_cycleDurations", BindingFlags.Instance | BindingFlags.NonPublic);
      if (_cycleDays != null && (_cycleDays.FieldType != typeof(int) || _cycleDays.IsInitOnly)) _cycleDays = null;
      Duration = new WeatherDurationCommands(this, () => Available);
    }
    public void PostLoad() { _ready = true; }
    public WeatherDurationResult SetDuration(string input) {
      var result = Duration.Execute(input);
      if (!Duration.Busy) LastResult = result;
      return result;
    }
    public WeatherDurationSnapshot Read() {
      if (!_ready || _session?.State == null || !_session.State.Compatible || _cycleDays == null || _durations == null)
        throw new InvalidOperationException("场景或版本/访问适配器尚未就绪。");
      var selected = _hazard.CurrentCycleHazardousWeather;
      string name = selected is DroughtWeather ? "干旱" : selected is BadtideWeather ? "恶水" : null;
      if (name == null) throw new InvalidOperationException("未识别的灾害类型保持只读。");
      var durations = _durations.GetValue(_cycle) as IEnumerable<ICycleDuration>;
      if (durations == null) throw new InvalidOperationException("无法读取原生周期时长来源。");
      var items = durations.ToArray();
      if (items.Length != 2 || items.Count(d => ReferenceEquals(d, _hazard)) != 1 ||
          items.Count(d => ReferenceEquals(d, _temperate)) != 1)
        throw new InvalidOperationException("存在其他周期服务，当前适配器不修改。");
      int sum = checked(items.Sum(d => d.DurationInDays));
      return new WeatherDurationSnapshot(_cycle.Cycle, _cycle.CycleDay, _temperate.TemperateWeatherDuration,
        _hazard.HazardousWeatherDuration, (int)_cycleDays.GetValue(_cycle), sum, name);
    }
    public void Apply(WeatherDurationSnapshot before, int duration, int cycleDays) {
      if (!Available) throw new InvalidOperationException("执行状态已变化。");
      var current = Read();
      if (duration < 1 || duration > 1000 || (long)current.CycleDays - current.HazardDays + duration != cycleDays ||
          cycleDays < current.Day || current.CycleDays != current.RegisteredDays ||
          (long)current.TemperateDays + current.HazardDays != current.CycleDays)
        throw new InvalidOperationException("时长或周期约束无效。");
      if (current.Cycle != before.Cycle || current.Day != before.Day || current.HazardDays != before.HazardDays ||
          current.TemperateDays != before.TemperateDays || current.HazardName != before.HazardName ||
          current.CycleDays != before.CycleDays || current.RegisteredDays != before.RegisteredDays)
        throw new InvalidOperationException("周期已变化。");
      // Verified version-specific setter only assigns the integer; neither assignment posts events.
      // Keep the scheduler cache and saved native duration consistent in the same synchronous call.
      try {
        _setDuration.Invoke(_hazard, new object[] { duration });
        _cycleDays.SetValue(_cycle, cycleDays);
      } catch {
        // These two primitive assignments have no native event callbacks; restore both on access failure.
        try {
          if (_hazard.HazardousWeatherDuration != before.HazardDays)
            _setDuration.Invoke(_hazard, new object[] { before.HazardDays });
        } finally {
          if ((int)_cycleDays.GetValue(_cycle) != before.CycleDays) _cycleDays.SetValue(_cycle, before.CycleDays);
        }
        throw;
      }
    }
    public string Describe() {
      try {
        var s = Read();
        return "当前：" + (s.Day > s.TemperateDays ? s.HazardName : "正常") + "；周期 " + s.Cycle +
          " 第 " + s.Day + " 天；本周期灾害：" + s.HazardName + "，总时长 " + s.HazardDays + " 天。";
      } catch { return "天气周期暂不可读取或不符合当前版本适配条件。"; }
    }
    public void Unload() {
      _ready = false; LastResult = null;
      _session = null; _cycle = null; _hazard = null; _temperate = null; _speed = null;
    }
  }
}
