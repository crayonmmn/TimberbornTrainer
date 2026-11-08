using System;
using TimberbornTrainer.Modules;

namespace TimberbornTrainer.Tests {
  public static class InventoryBrowserTests {
    private static int _checks;
    private static void Check(bool value, string message) { ++_checks; if (!value) throw new Exception(message); }
    private sealed class Catalog : IInventoryCatalog {
      public int Discovers, Reads, Clears, Focuses;
      public bool Invalid, ThrowRead, ThrowDiscover;
      public InventoryTarget Target = new InventoryTarget { Id = "真实身份", OwnerId = "实体", Abilities = new InventoryAbilities(true, "只读阶段") };
      public InventoryDiscovery Discover(InventorySource source) {
        ++Discovers; if (ThrowDiscover) throw new Exception();
        return new InventoryDiscovery { Targets = new[] { Target }, Message = "发现" };
      }
      public InventorySnapshot Read(string id) {
        ++Reads; if (ThrowRead) throw new Exception();
        return new InventorySnapshot { Valid = !Invalid, Target = Target, Message = "读取" };
      }
      public void Clear() { ++Clears; }
      public bool Focus(string id) { ++Focuses; return true; }
    }
    public static string Run() {
      _checks = 0;
      Check(InventoryIdentity.Target("a", "b:c", "d") != InventoryIdentity.Target("a", "b", "c:d"), "身份不因分隔符碰撞。");
      Check(InventoryIdentity.Target("场景1", "实体", "输入") != InventoryIdentity.Target("场景1", "实体", "输出"), "同建筑多库存独立身份。");
      Check(InventoryIdentity.Target("场景1", "实体", "输入") != InventoryIdentity.Target("场景2", "实体", "输入"), "场景重建隔离。");
      bool rejected = false; try { InventoryIdentity.Owner("", "实体"); } catch (ArgumentException) { rejected = true; }
      Check(rejected, "缺失原生身份不能用显示序号替代。");
      var abilities = new InventoryAbilities(true, "未审核");
      Check(abilities.Read.Available && !abilities.Add.Available && !abilities.Subtract.Available && !abilities.Set.Available, "四项独立能力，只读。");
      Check(abilities.Add.Reason == "未审核" && abilities.Subtract.Reason == "未审核" && abilities.Set.Reason == "未审核", "每项写入都有原因。");
      var catalog = new Catalog(); var browser = new InventoryBrowser(catalog);
      Check(catalog.Discovers == 0 && catalog.Reads == 0, "创建面板模型不访问游戏库存。");
      browser.Switch(InventorySource.Settlement);
      Check(catalog.Discovers == 1 && catalog.Reads == 1 && browser.Snapshot.Valid, "打开默认只读概览按需读取一次。");
      browser.Switch(InventorySource.Building);
      Check(catalog.Discovers == 1 && catalog.Reads == 1 && browser.Targets.Length == 0 && browser.Snapshot == null, "切换建筑模式不扫描、不保留前一目标。");
      browser.Discover();
      Check(catalog.Discovers == 2 && catalog.Reads == 1 && browser.Targets.Length == 1, "主动打开建筑列表才发现，不自动读所有库存。");
      browser.Select("显示序号1"); Check(catalog.Reads == 1 && browser.Snapshot == null, "无法用未知序号选中对象。");
      browser.Select("真实身份"); Check(catalog.Reads == 2 && browser.Snapshot.Valid, "选择真实目标才读取。");
      browser.Focus("真实身份"); Check(catalog.Focuses == 1 && catalog.Reads == 2, "定位不扫描库存。");
      Check(!browser.Focus("未知") && catalog.Focuses == 1, "不能定位列表外目标。");
      browser.Discover(); Check(browser.Snapshot.Valid && catalog.Reads == 3, "主动重列时用稳定身份恢复选择。");
      catalog.Invalid = true; browser.Select("真实身份");
      Check(browser.Snapshot == null && browser.Targets.Length == 0, "对象失效清理选择和列表。");
      catalog.Invalid = false; browser.Discover(); catalog.ThrowRead = true; browser.Select("真实身份");
      Check(browser.Targets.Length == 0 && browser.Snapshot == null, "读异常释放对象引用。");
      catalog.ThrowRead = false; catalog.ThrowDiscover = true; browser.Discover();
      Check(browser.Targets.Length == 0 && browser.Message.Contains("失败"), "发现异常不展示陈旧数据。");
      catalog.ThrowDiscover = false; browser.Switch(InventorySource.DistrictCenter);
      Check(browser.Targets.Length == 1 && browser.Snapshot == null, "地区中心按需发现，等待明确选择。");
      browser.Dispose(); browser.Dispose();
      Check(browser.Targets.Length == 0 && browser.Snapshot == null && !browser.Opened, "重复卸载安全。");
      int calls = catalog.Discovers + catalog.Reads + catalog.Focuses;
      browser.Discover(); browser.Select("真实身份"); browser.Focus("真实身份");
      Check(calls == catalog.Discovers + catalog.Reads + catalog.Focuses, "卸载后旧模型不能访问场景。");
      return "库存浏览模型：" + _checks + " 项断言通过。";
    }
  }
}
