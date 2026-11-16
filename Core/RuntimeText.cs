using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TimberbornTrainer.Localization {
  public static class RuntimeText {
    private static Dictionary<string, string> _english = new Dictionary<string, string>();
    private static Dictionary<string, string> _chinese = new Dictionary<string, string>();
    private static string _preference;
    private static string _manual;
    public static string Language { get; private set; } = "en-US";
    public static string LoadWarning { get; private set; }
    public static event Action Changed;
    public static void Configure(string languageDirectory, string preference) {
      _preference = preference; _manual = null; LoadWarning = null;
      _english = ReadPack(Path.Combine(languageDirectory ?? "", "en-US.json"));
      _chinese = ReadPack(Path.Combine(languageDirectory ?? "", "zh-CN.json"));
      try {
        if (File.Exists(preference) && new FileInfo(preference).Length <= 4096) {
          var p = JObject.Parse(File.ReadAllText(preference, new UTF8Encoding(false, true)));
          var value = (string)p["Language"];
          if (value == "en-US" || value == "zh-CN") _manual = value;
        }
      } catch { /* A corrupt preference never prevents startup. */ }
      TextToken.Display = Render;
      SetLanguage(_manual ?? "en-US");
    }
    private static Dictionary<string, string> ReadPack(string path) {
      try {
        if (new FileInfo(path).Length > 1024 * 1024) throw new IOException();
        var obj = JObject.Parse(File.ReadAllText(path, new UTF8Encoding(false, true)),
          new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in obj.Properties()) {
          if (p.Value.Type != JTokenType.String) throw new IOException();
          result.Add(p.Name, (string)p.Value);
        }
        return result;
      } catch { LoadWarning = "Language files could not be fully loaded; fallback text is in use."; return new Dictionary<string, string>(); }
    }
    private static void SetLanguage(string value) {
      if (Language == value) return;
      Language = value;
      Changed?.Invoke();
    }
    public static bool Select(string language) {
      if (language != "en-US" && language != "zh-CN") return false;
      bool saved = false;
      if (string.IsNullOrWhiteSpace(_preference)) { _manual = language; SetLanguage(language); LoadWarning = "Language changed for this session; no preference path is available."; return false; }
      string temporary = _preference + ".tmp";
      try {
        Directory.CreateDirectory(Path.GetDirectoryName(_preference));
        File.WriteAllText(temporary, new JObject { ["Language"] = language }.ToString(Formatting.None), new UTF8Encoding(false));
        if (File.Exists(_preference)) File.Replace(temporary, _preference, null); else File.Move(temporary, _preference);
        saved = true;
      } catch { LoadWarning = "Language changed for this session, but the preference could not be saved."; }
      finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
      _manual = language;
      SetLanguage(language);
      return saved;
    }
    public static string Value(string id) {
      string value;
      if (Language == "zh-CN" && _chinese.TryGetValue(id, out value) && !string.IsNullOrEmpty(value)) return value;
      if (_english.TryGetValue(id, out value) && !string.IsNullOrEmpty(value)) return value;
      if (_chinese.TryGetValue(id, out value) && !string.IsNullOrEmpty(value)) return value;
      return "[Text unavailable: " + id + "]";
    }
    public static string Render(string value) {
      if (value == null || value.IndexOf('\u001e') < 0) return value;
      var output = new StringBuilder();
      for (int i = 0; i < value.Length;) {
        if (value[i] != '\u001e') { output.Append(value[i++]); continue; }
        int end = value.IndexOf('\u001f', i + 1);
        if (end < 0) { output.Append("[Text unavailable]"); break; }
        output.Append(Value(value.Substring(i + 1, end - i - 1))); i = end + 1;
      }
      return output.ToString();
    }
    public static string Format(string id, params object[] args) {
      try { return string.Format(CultureInfo.InvariantCulture, Value(id), args); }
      catch (FormatException) { return "[Invalid translation format: " + id + "]"; }
    }
  }
}
