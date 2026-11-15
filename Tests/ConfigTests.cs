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
      var config = new Config { Enabled = true, LockHunger = true, ProductionMultiplier = 100 };
      store.Save(config);
      Check(store.Load().ProductionMultiplier == 100 && store.Load().LockHunger, "保存读取失败。");
      store.Export("测试.json", config);
      Check(store.Import("测试.json").LockHunger && store.Import("测试.json").ProductionMultiplier == 100, "导入导出失败。");
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
      Reject(() => ConfigStore.Parse("{\"SchemaVersion\":1,\"WeatherDurationDays\":1e100}"), "旧配置迁移仍拒绝无穷值。");
      Reject(() => ConfigStore.Parse("{\"SchemaVersion\":1,\"BadtideDurationDays\":-1}"), "旧配置迁移仍拒绝负时长。");
      Reject(() => ConfigStore.Parse("{\"SchemaVersion\":1,\"DroughtDurationDays\":1001}"), "旧配置迁移仍拒绝过长时长。");
      var migrated = ConfigStore.Parse("{\"SchemaVersion\":1,\"LockThirst\":true,\"WeatherOverrideEnabled\":true,\"DroughtDurationDays\":20}");
      Check(migrated.LockThirst && !Newtonsoft.Json.JsonConvert.SerializeObject(migrated).Contains("Drought"), "旧配置迁移保留海狸设置，不再导出已删除字段。");
      Check(typeof(Config).GetProperty("WeatherOverrideEnabled") == null && typeof(Config).GetProperty("BadtideDurationDays") == null, "配置模型中已真正删除天气参数。");
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
            reset.ProductionMultiplier == 1,
            "恢复默认必须清除所有持续性设置。");
      Check(changes == 3, "状态事件数量错误。");
      var mismatch = new TrainerState("1.1.2.9", true, "6000.5.11f1");
      mismatch.SetEnabled(true); Check(mismatch.EffectiveEnabled && !mismatch.VerifiedVersion, "未验证版本允许启用，但不得标记为已验证。");
      mismatch.Apply(config);
      Check(mismatch.EffectiveEnabled, "游戏版本警告不阻止有效配置启用。");
      Check(!new TrainerState(TrainerState.TargetVersion, false, "6000.5.11f1").Compatible, "错误平台应被拒绝。");
      Check(new TrainerState(TrainerState.TargetVersion, true, "6000.5.12f1").Compatible && !new TrainerState(TrainerState.TargetVersion, true, "6000.5.12f1").VerifiedVersion, "不同引擎版本只警告，不因版本号阻止启用。");
      // Isolated JSON configuration files, never a game save or the user's real configuration.
      var legacyStore = new ConfigStore(Path.Combine(directory, "legacy-construction"));
      legacyStore.Save(new Config { Enabled = true, InstantBuilding = true, FreeBuilding = true, LockHunger = true });
      var legacy = legacyStore.Load();
      Check(legacy.InstantBuilding && legacy.FreeBuilding && legacy.SchemaVersion == 1,
        "历史建造字段可经实际JSON文件保存并加载，版本不变。");
      var restored = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1");
      restored.Apply(legacy);
      Check(!restored.Settings.InstantBuilding && !restored.Settings.FreeBuilding && restored.Settings.LockHunger,
        "加载历史配置后应用时停用旧自动建造字段，保留无关偏好。");
      legacyStore.Save(restored.Settings);
      Check(!legacyStore.Load().InstantBuilding && !legacyStore.Load().FreeBuilding,
        "有效配置重新保存后旧自动建造值保持关闭。");
      legacyStore.Export("旧配置导入.json", legacy);
      restored.Apply(legacyStore.Import("旧配置导入.json"));
      Check(!restored.Settings.InstantBuilding && !restored.Settings.FreeBuilding && restored.Settings.LockHunger,
        "导入旧true字段也只更新兼容配置，不能重新启用自动施工。");
      return "通过：" + _assertions + " 项断言（配置、状态、范围、文件保护和版本检查）。";
    }
  }
}
