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
      var result = new ScienceResult { Message = global::TimberbornTrainer.Localization.TextToken.Key("t0135") };
      if (!_enabled()) { result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0322"); return result; }
      if (operation != ScienceOperation.Set && operation != ScienceOperation.Add && operation != ScienceOperation.Subtract) {
        result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0123"); return result;
      }
      int amount;
      if (input == null || input.Length > 32 || !int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out amount)
          || amount < 0 || amount > InputLimit) {
        result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0307");
        return result;
      }
      int before;
      if (!TryRead(out before)) { result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0384"); return result; }
      result.Before = before;
      long expected = operation == ScienceOperation.Set ? amount
        : operation == ScienceOperation.Add ? (long)before + amount : (long)before - amount;
      if (expected < 0) { result.After = before; result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0211"); return result; }
      if (operation != ScienceOperation.Subtract && expected > InputLimit) {
        result.After = before; result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0397"); return result;
      }
      var delta = expected - before;
      try {
        if (delta > 0) _points.Add(checked((int)delta));
        else if (delta < 0) _points.Subtract(checked((int)-delta));
      } catch {
        _commandError = true;
        try { result.After = _points.Read(); } catch { }
        result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0245");
        return result;
      }
      int after;
      if (!TryRead(out after)) {
        result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0217");
        return result;
      }
      result.After = after;
      if (after != expected) {
        _commandError = true;
        result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0418");
        return result;
      }
      result.Success = true;
      _commandError = false;
      result.Message = delta == 0 ? global::TimberbornTrainer.Localization.TextToken.Key("t0145") : global::TimberbornTrainer.Localization.TextToken.Key("t0147");
      return result;
    }
  }
}
