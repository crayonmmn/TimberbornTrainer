using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TimberbornTrainer.Core;
using TimberbornTrainer.Localization;

namespace TimberbornTrainer.Tests {
  public static class RuntimeLocalizationTests {
    private static int _checks;
    private static void Check(bool ok, string message) { ++_checks; if (!ok) throw new Exception(message); }
    public static void Run(string source, string directory) {
      _checks = 0; Directory.CreateDirectory(directory);
      string packs = Path.Combine(directory, "Languages"), preference = Path.Combine(directory, "language.json");
      Directory.CreateDirectory(packs);
      foreach (var file in new[] { "en-US.json", "zh-CN.json" }) File.Copy(Path.Combine(source, file), Path.Combine(packs, file));
      var en = JObject.Parse(File.ReadAllText(Path.Combine(packs, "en-US.json")));
      var zh = JObject.Parse(File.ReadAllText(Path.Combine(packs, "zh-CN.json")));
      string id = zh.Properties().Single(p => (string)p.Value == "资源管理").Name;
      string token = TextToken.Key(id);
      Check(en.Properties().Select(p => p.Name).OrderBy(x => x).SequenceEqual(zh.Properties().Select(p => p.Name).OrderBy(x => x)), "Identical language keys");
      Check(en.Properties().All(p => p.Value.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)p.Value)), "Complete English resources");
      Check(zh.Properties().All(p => p.Value.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)p.Value)), "Complete Chinese resources");
      RuntimeText.Configure(packs, preference); Check(RuntimeText.Language == "en-US" && RuntimeText.Render(token) == "Resource Management", "Missing preference defaults to English");
      Check(!File.Exists(preference), "Default initialization does not persist a choice");
      File.WriteAllText(preference, "{\"Language\":\"zh-CN\"}");
      string oldChinese = File.ReadAllText(preference);
      RuntimeText.Configure(packs, preference); Check(RuntimeText.Language == "zh-CN" && RuntimeText.Render(token) == "资源管理", "Legacy Chinese preference restored");
      Check(File.ReadAllText(preference) == oldChinese, "Loading preserves legacy Chinese configuration");
      File.WriteAllText(preference, "{\"Language\":\"en-US\"}");
      RuntimeText.Configure(packs, preference); Check(RuntimeText.Language == "en-US", "Legacy English preference restored");
      Check(File.ReadAllText(preference) == "{\"Language\":\"en-US\"}", "Loading preserves legacy English configuration");
      Check(RuntimeText.Select("zh-CN") && RuntimeText.Render(token) == "资源管理", "Manual Chinese saved");
      RuntimeText.Configure(packs, preference); Check(RuntimeText.Language == "zh-CN", "Manual Chinese survives restart");
      Check(RuntimeText.Select("en-US") && RuntimeText.Render(token) == "Resource Management", "Manual English saved");
      RuntimeText.Configure(packs, preference); Check(RuntimeText.Language == "en-US", "Manual English survives restart");
      RuntimeText.Select("zh-CN"); File.WriteAllText(preference, "{ broken"); RuntimeText.Configure(packs, preference); Check(RuntimeText.Language == "en-US", "Corrupt preference falls back to English, not stale Chinese");
      File.WriteAllText(preference, "{\"Language\":\"xx\"}"); RuntimeText.Configure(packs, preference); Check(RuntimeText.Language == "en-US", "Invalid preference fallback");
      File.WriteAllText(preference, "{\"Language\":\"Auto\"}"); RuntimeText.Configure(packs, preference); Check(RuntimeText.Language == "en-US", "Old automatic value is invalid and defaults to English");
      File.WriteAllText(preference, "{}"); RuntimeText.Configure(packs, preference); Check(RuntimeText.Language == "en-US", "Missing language field defaults to English");
      RuntimeText.Select("zh-CN"); File.Delete(preference); RuntimeText.Configure(packs, preference); Check(RuntimeText.Language == "en-US", "Deleted preference does not retain prior session language");
      Check(!RuntimeText.Select("xx") && RuntimeText.Language == "en-US", "Reject invalid selection without changing language");
      zh.Remove(id); File.WriteAllText(Path.Combine(packs, "zh-CN.json"), zh.ToString());
      RuntimeText.Select("zh-CN"); RuntimeText.Configure(packs, preference); Check(RuntimeText.Language == "zh-CN" && RuntimeText.Render(token) == "Resource Management", "Missing Chinese key falls back to English");
      File.WriteAllText(Path.Combine(packs, "zh-CN.json"), "broken"); RuntimeText.Configure(packs, preference);
      Check(RuntimeText.Render(token) == "Resource Management" && RuntimeText.LoadWarning != null, "Corrupt language file cannot block startup");
      File.Copy(Path.Combine(source, "zh-CN.json"), Path.Combine(packs, "zh-CN.json"), true);
      RuntimeText.Select("en-US"); File.Delete(Path.Combine(packs, "en-US.json")); RuntimeText.Configure(packs, preference);
      Check(RuntimeText.Render(token) == "资源管理", "Missing English file uses available Chinese resource");
      File.Delete(Path.Combine(packs, "zh-CN.json")); RuntimeText.Configure(packs, preference);
      Check(RuntimeText.Render(token).StartsWith("[Text unavailable:"), "Both unavailable: safe English fallback");
      Check(RuntimeText.Render("native item 资源管理 (Good_Mod)") == "native item 资源管理 (Good_Mod)", "Never translate native names or IDs");
      Check(RuntimeText.Render("prefix\u001emissing") == "prefix[Text unavailable]", "Malformed token safe fallback");
      en["format"] = "Count {0}: {1}"; en["badformat"] = "Count {0";
      File.WriteAllText(Path.Combine(packs, "en-US.json"), en.ToString()); RuntimeText.Configure(packs, preference);
      Check(RuntimeText.Format("format", 12, "native资源") == "Count 12: native资源", "Dynamic parameters preserved");
      Check(RuntimeText.Format("badformat", 1).StartsWith("[Invalid translation format:"), "Bad format safe fallback");
      var state = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1"); state.SetEnabled(true);
      string before = Newtonsoft.Json.JsonConvert.SerializeObject(state.Settings); int changes = 0; state.Changed += () => ++changes;
      int notifications = 0; Action listener = () => ++notifications; RuntimeText.Changed += listener;
      RuntimeText.Select("zh-CN"); RuntimeText.Select("en-US"); RuntimeText.Select("en-US"); RuntimeText.Changed -= listener;
      Check(notifications == 2, "Only actual changes emit one notification");
      Check(changes == 0 && before == Newtonsoft.Json.JsonConvert.SerializeObject(state.Settings), "Language switch preserves gameplay state");
      Check(!RuntimeText.Select(null), "Null selection rejected");
      RuntimeText.Configure(source, Path.Combine(directory, "final-language.json"));
      string cached = token + " 12"; Check(RuntimeText.Render(cached) == "Resource Management 12", "Cached result English");
      RuntimeText.Select("zh-CN"); Check(RuntimeText.Render(cached) == "资源管理 12", "Cached result re-renders Chinese");
      Console.WriteLine("Runtime localization core: " + _checks + " passed (no game execution).");
    }
  }
}
