using System;
using System.IO;
using Bindito.Core;
using Timberborn.ModManagerScene;
using Timberborn.SingletonSystem;
using Timberborn.Versioning;
using TimberbornTrainer.UI;
using TimberbornTrainer.Modules;
using UnityEngine;

namespace TimberbornTrainer.Core {
  public sealed class TrainerMod : IModStarter {
    public void StartMod(IModEnvironment modEnvironment) {
      // Startup only: no game services, patches, or gameplay modifications here.
      Debug.Log("海狸修改工具：基础框架已启动，等待游戏场景初始化。");
    }
  }

  [Context("Game")]
  public sealed class TrainerConfigurator : Configurator {
    protected override void Configure() {
      Bind<TrainerSession>().AsSingleton();
      Bind<ResourceModule>().AsSingleton();
      Bind<BuildingModule>().AsSingleton();
      Bind<MainPanel>().AsSingleton();
    }
  }

  public sealed class TrainerSession : ILoadableSingleton {
    public TrainerState State { get; private set; }
    public ConfigStore Store { get; private set; }
    public string StartupMessage { get; private set; } = "";

    public void Load() {
      string version;
      try { version = GameVersions.CurrentVersion.Full; }
      catch { version = "未知"; }
      State = new TrainerState(version, Application.platform == RuntimePlatform.WindowsPlayer && Environment.Is64BitProcess,
                               Application.unityVersion);
      Store = new ConfigStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                          "TimberbornTrainer"));
      try {
        var settings = Store.Load();
        // Never arm persistent gameplay changes simply by loading a file or a new settlement.
        settings.Enabled = false;
        State.Apply(settings);
      } catch (Exception) {
        StartupMessage = "读取配置失败，已使用默认设置；原文件未被覆盖。";
        Debug.LogWarning("海狸修改工具：读取配置失败，原文件保持不变。");
      }
    }
  }
}
