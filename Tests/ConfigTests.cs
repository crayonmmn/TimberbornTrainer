using System;
using System.IO;
using TimberbornTrainer.Core;

namespace TimberbornTrainer.Tests {
  public static class ConfigTests {
    private static int _assertions;
    private static void Check(bool condition, string message) {
      ++_assertions;
      if (!condition) throw new Exception(message);
    }
    private static void Reject(Action action, string message) {
      bool rejected = false;
      try { action(); } catch { rejected = true; }
      Check(rejected, message);
    }
    public static string Run(string directory) {
      _assertions = 0;
      var store = new ConfigStore(directory);
      Check(!Directory.Exists(directory), "测试必须使用新的隔离目录。");
      var defaults = store.Load();
      Check(!defaults.Enabled && defaults.ProductionMultiplier == 1, "默认值错误。");
      Check(!Directory.Exists(directory), "读取缺省配置不应创建目录。");
      var config = new Config { Enabled = true, LockHunger = true, ProductionMultiplier = 100,
                                DroughtOverrideEnabled = true, DroughtDurationDays = 20 };
      store.Save(config);
      Check(store.Load().ProductionMultiplier == 100 && store.Load().LockHunger, "保存读取失败。");
      store.Export("测试.json", config);
      Check(store.Import("测试.json").DroughtDurationDays == 20, "导入导出失败。");
      var oldFile = File.ReadAllText(store.ConfigPath);
      Reject(() => store.Save(new Config { ProductionMultiplier = 101 }), "应拒绝过大倍率。");
      Check(File.ReadAllText(store.ConfigPath) == oldFile, "无效配置不应覆盖原文件。");
      config.ProductionMultiplier = 2;
      store.Save(config);
      Check(store.Load().ProductionMultiplier == 2, "原子替换保存失败。");
      Reject(() => store.Export("../逃逸.json", config), "应拒绝目录穿越。");
      Reject(() => store.Import("config.json"), "应拒绝配置文件保留名称。");
      Reject(() => ConfigStore.Parse("{}"), "应要求配置版本。");
      Reject(() => ConfigStore.Parse("{\"SchemaVersion\":2}"), "应拒绝未来配置版本。");
      Reject(() => ConfigStore.Parse("{\"SchemaVersion\":1,\"ProductionMultiplier\":0}"), "应拒绝过小倍率。");
      Reject(() => ConfigStore.Parse("{\"SchemaVersion\":1,\"ProductionMultiplier\":\"2\"}"), "应拒绝字符串倍率。");
      Reject(() => ConfigStore.Parse("{\"SchemaVersion\":1,\"Enabled\":1}"), "应拒绝非布尔开关。");
      Reject(() => ConfigStore.Parse("{\"SchemaVersion\":1,\"Enabled\":true,\"Enabled\":false}"), "应拒绝重复字段。");
      Reject(() => ConfigStore.Parse("{\"SchemaVersion\":1,\"$type\":\"危险类型\"}"), "应拒绝类型元数据。");
      Reject(() => ConfigStore.Parse("{\"SchemaVersion\":1} {}"), "应拒绝多余 JSON。");
      Reject(() => ConfigStore.Parse(new string(' ', ConfigStore.MaxBytes + 1)), "应限制文件大小。");
      Reject(() => new Config { ProductionMultiplier = float.NaN }.Validate(), "应拒绝非数值。");
      Reject(() => new Config { WeatherDurationDays = float.PositiveInfinity }.Validate(), "应拒绝无穷值。");
      Reject(() => new Config { BadtideDurationDays = -1 }.Validate(), "应拒绝负时长。");
      Reject(() => new Config { DroughtDurationDays = 1001 }.Validate(), "应拒绝过长时长。");
      File.WriteAllText(store.ConfigPath, "损坏配置");
      Reject(() => store.Load(), "应报告损坏配置。");
      Check(File.ReadAllText(store.ConfigPath) == "损坏配置", "读取失败不得覆盖原文件。");
      var state = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1");
      int changes = 0;
      state.Changed += () => changes++;
      state.Apply(config);
      Check(state.EffectiveEnabled, "匹配环境应可启用框架状态。");
      var detached = state.Settings;
      detached.ProductionMultiplier = 77;
      Check(state.Settings.ProductionMultiplier == 2, "状态不应泄露可变配置引用。");
      config.LockThirst = true;
      Check(!state.Settings.LockThirst, "应用配置应复制输入。");
      state.SetEnabled(false);
      Check(!state.EffectiveEnabled, "全局停用失败。");
      state.RestoreDefaults();
      var reset = state.Settings;
      Check(!reset.Enabled && !reset.LockHunger && !reset.LockThirst && !reset.InstantBuilding && !reset.FreeBuilding &&
            !reset.WeatherOverrideEnabled && !reset.DroughtOverrideEnabled && !reset.BadtideOverrideEnabled &&
            reset.ProductionMultiplier == 1 && reset.DroughtDurationDays == 1 && reset.BadtideDurationDays == 1,
            "恢复默认必须清除所有持续性设置。");
      Check(changes == 3, "状态事件数量错误。");
      var mismatch = new TrainerState("1.1.2.9", true, "6000.5.11f1");
      Reject(() => mismatch.SetEnabled(true), "错误游戏版本应禁止启用。");
      mismatch.Apply(config);
      Check(!mismatch.EffectiveEnabled, "导入配置不得绕过环境检查。");
      Check(!new TrainerState(TrainerState.TargetVersion, false, "6000.5.11f1").Compatible, "错误平台应被拒绝。");
      Check(!new TrainerState(TrainerState.TargetVersion, true, "6000.5.12f1").Compatible, "错误引擎版本应被拒绝。");
      return "通过：" + _assertions + " 项断言（配置、状态、范围、文件保护和版本检查）。";
    }
  }
}
