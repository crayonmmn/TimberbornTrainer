using System.Collections.Generic;
using System.Linq;
using TimberbornTrainer.Localization;
namespace TimberbornTrainer.Localization {
  public static class LocalizedAssertions {
    // Compare displayed wording, not the internal language-neutral representation.
    public static bool ContainsLocalized(this string value, string part) => TextToken.Display(value).Contains(TextToken.Display(part));
    public static bool ContainsLocalized<T>(this IEnumerable<T> values, T part) => values.Contains(part);
  }
}
