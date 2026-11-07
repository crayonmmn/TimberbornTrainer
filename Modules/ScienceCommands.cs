using System;
using System.Globalization;

namespace TimberbornTrainer.Modules {
  public interface ISciencePoints {
    int Read();
    void Add(int amount);
    void Subtract(int amount);
  }

  public enum ScienceOperation { Set, Add, Subtract }

  public sealed class ScienceResult {
    public bool Success { get; internal set; }
    public int? Before { get; internal set; }
    public int? After { get; internal set; }
    public long? Delta => Before.HasValue && After.HasValue ? (long?)After.Value - Before.Value : null;
    public string Message { get; internal set; }
  }

  // No Unity dependency: test command validation and failure behavior without game execution.
  public sealed class ScienceCommands {
    public const int InputLimit = 1000000000;
    private readonly ISciencePoints _points;
    private readonly Func<bool> _enabled;
    private bool _readError, _commandError;
    public bool RuntimeError => _readError || _commandError;

    public ScienceCommands(ISciencePoints points, Func<bool> enabled) {
      _points = points ?? throw new ArgumentNullException(nameof(points));
      _enabled = enabled ?? throw new ArgumentNullException(nameof(enabled));
    }

    public bool TryRead(out int current) {
      try {
        current = _points.Read();
        if (current < 0) throw new InvalidOperationException();
        _readError = false;
        return true;
      } catch { current = 0; _readError = true; return false; }
    }

    public ScienceResult Execute(ScienceOperation operation, string input) {
      var result = new ScienceResult { Message = "操作失败。" };
      if (!_enabled()) { result.Message = "全局修改器已关闭或版本不匹配，禁止修改科研点数。"; return result; }
      if (operation != ScienceOperation.Set && operation != ScienceOperation.Add && operation != ScienceOperation.Subtract) {
        result.Message = "不支持的科研操作。"; return result;
      }
      int amount;
      if (input == null || input.Length > 32 || !int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out amount)
          || amount < 0 || amount > InputLimit) {
        result.Message = "请输入零至十亿之间的整数，不接受负数、小数或其他字符。";
        return result;
      }
      int before;
      if (!TryRead(out before)) { result.Message = "无法读取游戏真实科研点数，未执行修改。"; return result; }
      result.Before = before;
      long expected = operation == ScienceOperation.Set ? amount
        : operation == ScienceOperation.Add ? (long)before + amount : (long)before - amount;
      if (expected < 0) { result.After = before; result.Message = "减少数量超过当前科研点数，未执行修改。"; return result; }
      if (operation != ScienceOperation.Subtract && expected > InputLimit) {
        result.After = before; result.Message = "修改后的科研点数将超过十亿上限，未执行修改。"; return result;
      }
      var delta = expected - before;
      try {
        if (delta > 0) _points.Add(checked((int)delta));
        else if (delta < 0) _points.Subtract(checked((int)-delta));
      } catch {
        _commandError = true;
        try { result.After = _points.Read(); } catch { }
        result.Message = "科研接口调用失败，可能已部分生效。请核对真实点数，不要直接重复操作。";
        return result;
      }
      int after;
      if (!TryRead(out after)) {
        result.Message = "接口已调用，但无法读取修改后数值，不能确认成功；请勿重复执行。";
        return result;
      }
      result.After = after;
      if (after != expected) {
        _commandError = true;
        result.Message = "游戏实际点数与预期不一致，不能确认成功。请核对游戏界面。";
        return result;
      }
      result.Success = true;
      _commandError = false;
      result.Message = delta == 0 ? "成功：数值未变化，未调用增减接口。" : "成功：已回读游戏真实科研点数。";
      return result;
    }
  }
}
