using System;
using System.Globalization;

namespace TimberbornTrainer.Modules {
  public interface IWarehouseStock {
    bool Usable { get; }
    int Read();
    int FreeCapacity();
    int UnreservedStock();
    void Give(int amount);
    void Take(int amount);
  }

  // Same command/result pattern as science; stock limits and reservations differ.
  public sealed class InventoryCommands {
    public const int InputLimit = 1000000;
    private readonly Func<bool> _enabled;
    public InventoryCommands(Func<bool> enabled) { _enabled = enabled; }
    public ScienceResult Execute(IWarehouseStock stock, ScienceOperation operation, string input) {
      var result = new ScienceResult { Message = global::TimberbornTrainer.Localization.TextToken.Key("t0255") };
      if (!_enabled()) { result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0323"); return result; }
      if (operation != ScienceOperation.Add && operation != ScienceOperation.Set && operation != ScienceOperation.Subtract) {
        result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0124"); return result;
      }
      int amount;
      if (input == null || input.Length > 32 || !int.TryParse(input.Trim(), NumberStyles.None,
          CultureInfo.InvariantCulture, out amount) || amount > InputLimit) {
        result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0308"); return result;
      }
      bool writeStarted = false;
      try {
        if (stock == null || !stock.Usable) { result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0315"); return result; }
        int before = stock.Read();
        if (before < 0) throw new InvalidOperationException();
        result.Before = before;
        long expected = operation == ScienceOperation.Set ? amount : operation == ScienceOperation.Add
          ? (long)before + amount : (long)before - amount;
        long delta = expected - before;
        if (expected < 0 || expected > InputLimit) { result.After = before; result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0275"); return result; }
        if (delta > 0 && delta > stock.FreeCapacity()) {
          result.After = before; result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0373"); return result;
        }
        if (delta < 0 && -delta > stock.UnreservedStock()) {
          result.After = before; result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0371"); return result;
        }
        if (delta != 0) {
          writeStarted = true;
          if (delta > 0) stock.Give(checked((int)delta));
          else stock.Take(checked((int)-delta));
        }
        result.After = stock.Read();
        result.Success = result.After == expected;
        result.Message = result.Success ? global::TimberbornTrainer.Localization.TextToken.Key("t0146")
          : global::TimberbornTrainer.Localization.TextToken.Key("t0451");
      } catch {
        if (writeStarted) {
          try { result.After = stock.Read(); } catch { }
          result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0427");
        } else result.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0181");
      }
      return result;
    }
  }
}
