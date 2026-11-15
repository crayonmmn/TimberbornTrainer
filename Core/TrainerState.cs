using System;

namespace TimberbornTrainer.Core {
  public sealed class TrainerState {
    private Config _config = new Config();
    public Config Settings => _config.Copy();
    public event Action Changed;
    public bool EffectiveEnabled => Compatible && _config.Enabled;
    public bool Compatible { get; }
    public bool VerifiedVersion { get; }
    private readonly Func<TrainerFeature, string> _interfaceWarning;
    public string GameVersion { get; }
    public string CompatibilityMessage { get; }
    public const string TargetVersion = "1.1.2.8-ffc2de0-sw";
    public const string ModVersion = "0.4.0";

    public TrainerState(string version, bool windows64, string unityVersion, Func<TrainerFeature, string> interfaceWarning = null) {
      GameVersion = version ?? "未知";
      // Compatible is the supported platform gate; version verification is informational only.
      Compatible = windows64;
      VerifiedVersion = version == TargetVersion && unityVersion == "6000.5.11f1";
      _interfaceWarning = interfaceWarning;
      CompatibilityMessage = !Compatible ? "当前平台不受支持（需要Windows 64位）。" : VerifiedVersion ? "游戏版本匹配" :
        "游戏版本兼容性警告\n当前游戏版本尚未经过 Timberborn Trainer 的完整验证。\n"
        + "修改器仍将尝试启动，但部分功能可能因游戏内部接口变化而无法正常工作，甚至出现异常。\n"
        + "请自行承担在未经验证版本中使用的风险。\n已验证版本：" + TargetVersion
        + "（Unity 6000.5.11f1）\n当前检测版本：" + GameVersion + "（Unity " + unityVersion + "）";
    }
    public string InterfaceWarning(TrainerFeature feature) => _interfaceWarning?.Invoke(feature);

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
