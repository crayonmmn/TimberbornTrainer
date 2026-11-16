using System;
using System.Collections.Generic;
using System.Linq;

namespace TimberbornTrainer.Localization {
  // Language-neutral messages survive concatenation and cached command results.
  public static class TextToken {
    public static string Key(string id) => "\u001e" + id + "\u001f";
    public static Func<string, string> Display = value => value;
  }
}
