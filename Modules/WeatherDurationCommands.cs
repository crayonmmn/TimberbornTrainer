using System;
using System.Globalization;

namespace TimberbornTrainer.Modules {
  public sealed class WeatherDurationSnapshot {
    public int Cycle { get; }
    public int Day { get; }
    public int TemperateDays { get; }
    public int HazardDays { get; }
    public int CycleDays { get; }
    public int RegisteredDays { get; }
    public string HazardName { get; }
    public WeatherDurationSnapshot(int cycle, int day, int temperateDays, int hazardDays,
        int cycleDays, int registeredDays, string hazardName) {
      Cycle = cycle; Day = day; TemperateDays = temperateDays; HazardDays = hazardDays;
      CycleDays = cycleDays; RegisteredDays = registeredDays; HazardName = hazardName;
    }
  }

  public interface IWeatherDurationAccess {
    WeatherDurationSnapshot Read();
    void Apply(WeatherDurationSnapshot before, int duration, int cycleDays);
  }

  public sealed class WeatherDurationResult {
    public bool Success { get; internal set; }
    public WeatherDurationSnapshot Before { get; internal set; }
    public WeatherDurationSnapshot After { get; internal set; }
    public string Message { get; internal set; }
  }

  // A one-shot change to the selected cycle's total hazard duration, not a weather transition.
  public sealed class WeatherDurationCommands {
    private readonly IWeatherDurationAccess _access;
    private readonly Func<bool> _allowed;
    public bool Busy { get; private set; }
    public WeatherDurationCommands(IWeatherDurationAccess access, Func<bool> allowed) { _access = access; _allowed = allowed; }
    public WeatherDurationResult Execute(string input) {
      var r = new WeatherDurationResult();
      if (Busy || !_allowed()) { r.Message = "请先暂停游戏并开启全局修改器，且等待场景就绪。"; return r; }
      int days;
      if (input == null || input.Length > 16 || !int.TryParse(input.Trim(), NumberStyles.None,
          CultureInfo.InvariantCulture, out days) || days < 1 || days > 1000) {
        r.Message = "请输入一至一千的整数天数（本周期灾害总时长，不是剩余时间）。"; return r;
      }
      Busy = true;
      try {
        var b = r.Before = _access.Read();
        if (b.Cycle < 1 || b.Day < 1 || b.TemperateDays < 0 || b.HazardDays < 1 ||
            b.CycleDays != b.RegisteredDays || b.Day > b.CycleDays ||
            (long)b.TemperateDays + b.HazardDays != b.CycleDays) {
          r.Message = "周期状态不一致或有额外周期类型，未修改。"; return r;
        }
        long total = (long)b.CycleDays - b.HazardDays + days;
        if (total > int.MaxValue || total < b.Day) {
          r.Message = "新时长不能短于本周期已经到达的日期，也不能使周期溢出。"; return r;
        }
        if (days == b.HazardDays) {
          r.After = b; r.Success = true; r.Message = "时长未变化，未调用修改接口。"; return r;
        }
        if (!_allowed()) { r.Message = "场景或暂停/全局状态已变化，未修改。"; return r; }
        _access.Apply(b, days, (int)total);
        r.After = _access.Read();
        if (r.After.Cycle != b.Cycle || r.After.Day != b.Day || r.After.TemperateDays != b.TemperateDays ||
            r.After.HazardName != b.HazardName || r.After.HazardDays != days ||
            r.After.CycleDays != total || r.After.RegisteredDays != total) {
          r.Message = "修改后回读不一致，无法确认成功；请检查游戏状态，不要重复操作。"; return r;
        }
        r.Success = true;
        r.Message = "已回读确认：本周期" + b.HazardName + "总时长 " + b.HazardDays + " → " + days +
          " 天；周期总长 " + b.CycleDays + " → " + total + " 天。尚待实机验证。";
      } catch {
        try { r.After = _access.Read(); } catch { }
        r.Message = "原生周期访问异常，未确认修改结果；请检查实际状态，不自动重试。";
      } finally { Busy = false; }
      return r;
    }
  }
}
