using System;
using System.IO;
using Bindito.Core;
using Timberborn.ModManagerScene;
using Timberborn.SingletonSystem;
using Timberborn.Versioning;
using TimberbornTrainer.UI;
using TimberbornTrainer.Modules;
using UnityEngine;
using Timberborn.Localization;
using TimberbornTrainer.Localization;

namespace TimberbornTrainer.Core {
  public sealed class TrainerMod : IModStarter {
    public void StartMod(IModEnvironment modEnvironment) {
      // Language files are local data, not executable dependencies.
      RuntimeText.Configure(Path.Combine(modEnvironment.ModPath, "Languages"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TimberbornTrainer", "language.json"));
      Debug.Log(global::TimberbornTrainer.Localization.RuntimeText.Render(global::TimberbornTrainer.Localization.TextToken.Key("t0195")));
    }
  }

  [Context("Game")]
  public sealed class TrainerConfigurator : Configurator {
    protected override void Configure() {
      Bind<TrainerSession>().AsSingleton();
      Bind<ResourceModule>().AsSingleton();
      Bind<NativeInventoryCatalog>().AsSingleton();
      Bind<NativeInventoryEditor>().AsSingleton();
      Bind<BuildingModule>().AsSingleton();
      Bind<BeaverModule>().AsSingleton();
      Bind<MainPanel>().AsSingleton();
    }
  }

  public sealed class TrainerSession : ILoadableSingleton {
    public TrainerState State { get; private set; }
    public ConfigStore Store { get; private set; }
    public string StartupMessage { get; private set; } = "";
    private bool _compatibilityWarningLogged;
    private readonly ILocalizationService _localization;
    public TrainerSession(ILocalizationService localization) { _localization = localization; }

    public void Load() {
      try { RuntimeText.SelectInitial(_localization.CurrentLanguage); } catch { RuntimeText.SelectInitial(null); }
      string version;
      try { version = GameVersions.CurrentVersion.Full; }
      catch { version = global::TimberbornTrainer.Localization.TextToken.Key("t0376"); }
      State = new TrainerState(version, Application.platform == RuntimePlatform.WindowsPlayer && Environment.Is64BitProcess,
                               Application.unityVersion, RuntimeApiChecks.Capture());
      if (!State.VerifiedVersion && !_compatibilityWarningLogged) {
        _compatibilityWarningLogged = true;
        Debug.LogWarning(global::TimberbornTrainer.Localization.RuntimeText.Render(State.CompatibilityMessage));
      }
      Store = new ConfigStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                          "TimberbornTrainer"));
      try {
        var settings = Store.Load();
        // Never arm persistent gameplay changes simply by loading a file or a new settlement.
        settings.Enabled = false;
        State.Apply(settings);
      } catch (Exception) {
        StartupMessage = global::TimberbornTrainer.Localization.TextToken.Key("t0184");
        Debug.LogWarning(global::TimberbornTrainer.Localization.RuntimeText.Render(global::TimberbornTrainer.Localization.TextToken.Key("t0194")));
      }
    }
  }
}
