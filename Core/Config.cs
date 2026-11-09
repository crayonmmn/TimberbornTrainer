using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TimberbornTrainer.Core {
  // Only persistent preferences belong here. Never serialize one-shot commands or savegame data.
  public sealed class Config {
    public int SchemaVersion { get; set; } = 1;
    public bool Enabled { get; set; } = false;
    // Deprecated compatibility fields; never authorize or schedule construction commands.
    public bool InstantBuilding { get; set; }
    public bool FreeBuilding { get; set; }
    public float ProductionMultiplier { get; set; } = 1;
    public bool LockHunger { get; set; }
    public bool LockThirst { get; set; }
    public bool WeatherOverrideEnabled { get; set; }
    public float WeatherDurationDays { get; set; } = 1;
    public bool DroughtOverrideEnabled { get; set; }
    public float DroughtDurationDays { get; set; } = 1;
    public bool BadtideOverrideEnabled { get; set; }
    public float BadtideDurationDays { get; set; } = 1;

    public void Validate() {
      if (SchemaVersion != 1) throw new ArgumentException("不支持此配置版本。");
      Check(ProductionMultiplier, 1, 100, "生产倍率");
      // Provisional input limit, not a claim about the game's supported weather range.
      Check(WeatherDurationDays, 0.1f, 1000, "天气持续时间");
      Check(DroughtDurationDays, 0.1f, 1000, "干旱持续时间");
      Check(BadtideDurationDays, 0.1f, 1000, "恶水持续时间");
    }

    private static void Check(float value, float min, float max, string name) {
      if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
        throw new ArgumentException(name + "超出允许范围。");
    }

    public Config Copy() => (Config)MemberwiseClone();
  }

  public sealed class ConfigStore {
    public const int MaxBytes = 65536;
    private readonly string _directory;
    public string DirectoryPath => _directory;
    public string ConfigPath => Path.Combine(_directory, "config.json");

    public ConfigStore(string directory) { _directory = Path.GetFullPath(directory); }

    public Config Load() => File.Exists(ConfigPath) ? Read(ConfigPath) : new Config();
    public void Save(Config config) => Write(ConfigPath, config);
    public Config Import(string name) => Read(ExchangePath(name));
    public void Export(string name, Config config) => Write(ExchangePath(name), config);

    public static Config Parse(string json) {
      if (json == null || Encoding.UTF8.GetByteCount(json) > MaxBytes)
        throw new ArgumentException("配置文件过大。");
      JObject document;
      using (var input = new StringReader(json))
      using (var reader = new JsonTextReader(input) { MaxDepth = 8, DateParseHandling = DateParseHandling.None }) {
        document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        if (reader.Read()) throw new ArgumentException("配置包含多余内容。");
      }
      if (document["SchemaVersion"]?.Type != JTokenType.Integer)
        throw new ArgumentException("配置缺少版本号。");
      foreach (var property in document.Properties()) {
        var info = typeof(Config).GetProperty(property.Name);
        if (info == null) throw new ArgumentException("配置包含未知字段。");
        var type = property.Value.Type;
        if (info.PropertyType == typeof(bool) && type != JTokenType.Boolean)
          throw new ArgumentException("配置开关必须为布尔值。");
        if (info.PropertyType == typeof(float) && type != JTokenType.Float && type != JTokenType.Integer)
          throw new ArgumentException("配置参数必须为数值。");
      }
      var config = document.ToObject<Config>(JsonSerializer.Create(new JsonSerializerSettings {
        TypeNameHandling = TypeNameHandling.None, MissingMemberHandling = MissingMemberHandling.Error
      }));
      config.Validate();
      return config;
    }

    private string ExchangePath(string name) {
      if (string.IsNullOrWhiteSpace(name) || name.Length > 80 ||
          name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("/") || name.Contains("\\") ||
          name == "." || name == ".." || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(name, "config.json", StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("请输入独立的 JSON 文件名，不要包含路径或使用 config.json。");
      return Path.Combine(_directory, "Exchange", name);
    }

    private static Config Read(string path) {
      using (var stream = File.OpenRead(path)) {
        if (stream.Length > MaxBytes) throw new ArgumentException("配置文件过大。");
        using (var reader = new StreamReader(stream, Encoding.UTF8, true)) return Parse(reader.ReadToEnd());
      }
    }

    private static void Write(string path, Config config) {
      config.Validate();
      var json = JsonConvert.SerializeObject(config, Formatting.Indented);
      Directory.CreateDirectory(Path.GetDirectoryName(path));
      var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
      try {
        File.WriteAllText(temporary, json, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
      } finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
  }
}
