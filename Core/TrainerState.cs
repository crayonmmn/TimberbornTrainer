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
    public const string ModVersion = "0.5.0";

    public TrainerState(string version, bool windows64, string unityVersion, Func<TrainerFeature, string> interfaceWarning = null) {
      GameVersion = version ?? global::TimberbornTrainer.Localization.TextToken.Key("t0376");
      // Compatible is the supported platform gate; version verification is informational only.
      Compatible = windows64;
      VerifiedVersion = version == TargetVersion && unityVersion == "6000.5.11f1";
      _interfaceWarning = interfaceWarning;
      CompatibilityMessage = !Compatible ? global::TimberbornTrainer.Localization.TextToken.Key("t0161") : VerifiedVersion ? global::TimberbornTrainer.Localization.TextToken.Key("t0417") :
        global::TimberbornTrainer.Localization.TextToken.Key("t0416")
        + global::TimberbornTrainer.Localization.TextToken.Key("t0398")
        + global::TimberbornTrainer.Localization.TextToken.Key("t0319") + TargetVersion
        + global::TimberbornTrainer.Localization.TextToken.Key("t0102") + GameVersion + global::TimberbornTrainer.Localization.TextToken.Key("t0101") + unityVersion + global::TimberbornTrainer.Localization.TextToken.Key("t0103");
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
      if (value && !Compatible) throw new InvalidOperationException(global::TimberbornTrainer.Localization.TextToken.Key("t0148"));
      var copy = Settings;
      copy.Enabled = value;
      Apply(copy);
    }

    public void RestoreDefaults() => Apply(new Config());
  }
}
