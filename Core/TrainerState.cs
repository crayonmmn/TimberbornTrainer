using System;

namespace TimberbornTrainer.Core {
  public sealed class TrainerState {
    private Config _config = new Config();
    public Config Settings => _config.Copy();
    public event Action Changed;
    public bool EffectiveEnabled => Compatible && _config.Enabled;
    public bool Compatible { get; }
    public string GameVersion { get; }
    public string CompatibilityMessage { get; }
    public const string TargetVersion = "1.1.2.8-ffc2de0-sw";
    public const string ModVersion = "0.3.0-inventory-dev";

    public TrainerState(string version, bool windows64, string unityVersion) {
      GameVersion = version ?? "未知";
      Compatible = windows64 && version == TargetVersion && unityVersion == "6000.5.11f1";
      CompatibilityMessage = Compatible ? "版本检查通过；库存、需求维持与施工均为实验功能" : "运行环境不匹配，已禁止启用";
    }

    public void Apply(Config settings) {
      if (settings == null) throw new ArgumentNullException(nameof(settings));
      settings.Validate();
      _config = settings.Copy();
      _config.InstantBuilding = false;
      _config.FreeBuilding = false;
      Changed?.Invoke();
    }

    public void SetEnabled(bool value) {
      if (value && !Compatible) throw new InvalidOperationException("此运行环境不受支持。");
      var copy = Settings;
      copy.Enabled = value;
      Apply(copy);
    }

    public void RestoreDefaults() => Apply(new Config());
  }
}
