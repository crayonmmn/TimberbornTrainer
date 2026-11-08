using System;
using TimberbornTrainer.Modules;

namespace TimberbornTrainer.Tests {
  public static class InventoryBrowserTests {
    private static int _checks;
    private static void Check(bool value, string message) { ++_checks; if (!value) throw new Exception(message); }
    private sealed class Catalog : IInventoryCatalog {
      public int Discovers, Reads, Clears, Scans;
      public bool Invalid, ThrowRead, Missing, RemoveGood, Reorder, FailScan;
      public InventoryScanReport LastScan { get; private set; }
      public readonly InventoryTarget Target = new InventoryTarget { Id = "真实身份", OwnerId = "实体", Abilities = new InventoryAbilities(true, "只读阶段") };
      public InventoryScanReport Scan() => LastScan = new InventoryScanReport { Completed = !FailScan, Sequence = ++Scans, ElapsedMilliseconds = 1.5, Message = "扫描失败" };
      public InventoryDiscovery Discover(InventorySource source) {
        ++Discovers;
        return new InventoryDiscovery { Targets = LastScan == null || !LastScan.Completed || Missing ? Array.Empty<InventoryTarget>() : new[] { Target }, Message = "请扫描" };
      }
      public InventorySnapshot Read(string id) {
        ++Reads; if (ThrowRead) throw new Exception();
        var water = new InventoryResource { GoodId = "Water", Amount = Reads };
        var food = new InventoryResource { GoodId = "ModFood" };
        return new InventorySnapshot { Valid = !Invalid, Target = Target, Message = "读取",
          Resources = RemoveGood ? new[] { food } : Reorder ? new[] { water, food } : new[] { food, water } };
      }
      public void Clear() { ++Clears; LastScan = null; }
      public bool Focus(string id) => !Invalid;
    }
    public static string Run() {
      _checks = 0;
      Check(InventoryIdentity.Target("a", "b:c", "d") != InventoryIdentity.Target("a", "b", "c:d"), "身份不因分隔符碰撞。");
      Check(InventoryIdentity.Target("场景1", "实体", "输入") != InventoryIdentity.Target("场景1", "实体", "输出"), "多库存独立身份。");
      Check(InventoryIdentity.Target("场景1", "实体", "输入") != InventoryIdentity.Target("场景2", "实体", "输入"), "场景隔离。");
      bool rejected = false; try { InventoryIdentity.Owner("", "实体"); } catch (ArgumentException) { rejected = true; }
      Check(rejected, "不能用缺失身份代替实体标识。");
      var ability = new InventoryAbilities(true, "未审核");
      Check(ability.Read.Available && !ability.Add.Available && !ability.Subtract.Available && !ability.Set.Available, "全部新目标仍只读。");
      Check(ability.Add.Reason == "未审核" && ability.Subtract.Reason == "未审核" && ability.Set.Reason == "未审核", "独立不可用原因。");
      var catalog = new Catalog(); var browser = new InventoryBrowser(catalog);
      Check(catalog.Scans == 0 && catalog.Discovers == 0 && catalog.Reads == 0, "构造不扫描不读取。");
      browser.Switch(InventorySource.Settlement);
      Check(catalog.Scans == 0 && catalog.Reads == 0 && browser.Targets.Length == 0, "首次打开显示扫描入口，不自动扫描。");
      browser.Scan();
      Check(catalog.Scans == 1 && browser.Snapshot.Valid && browser.LastScan.Sequence == 1, "首次主动扫描建立目录并读取默认概览。");
      browser.Switch(InventorySource.Building);
      Check(catalog.Scans == 1 && browser.Snapshot == null && browser.Targets.Length == 1 && catalog.Clears == 0, "切换来源读取目录，不全图重扫、不清缓存。");
      browser.Discover(); browser.Discover();
      Check(catalog.Scans == 1 && catalog.Reads == 1, "反复打开列表不扫描、不读取所有库存。");
      browser.Select("真实身份"); browser.SelectGood("Water");
      Check(browser.SelectedGoodId == "Water", "按原生资源标识选择。");
      catalog.Reorder = true; browser.Select("真实身份");
      Check(browser.SelectedGoodId == "Water" && browser.Snapshot.Target.Id == "真实身份", "库存刷新保留资源身份，不依赖索引。");
      browser.Discover(); Check(browser.SelectedGoodId == "Water", "重开目录保留目标与资源。");
      browser.Scan();
      Check(catalog.Scans == 2 && browser.SelectedGoodId == "Water" && browser.Snapshot.Target.Id == "真实身份", "重复主动扫描保留有效目标及资源。");
      Check(browser.LastScan.ElapsedMilliseconds == 1.5, "展示实际报告的耗时。");
      catalog.RemoveGood = true; browser.Select("真实身份");
      Check(browser.Snapshot.Valid && browser.SelectedGoodId == null && browser.Message.Contains("资源已移除"), "仅资源失效时清除资源，保留库存目标并解释。");
      browser.Select("真实身份");
      Check(browser.SelectedGoodId == null, "后续刷新不偷偷选择另一种资源。");
      browser.SelectGood("ModFood"); Check(browser.SelectedGoodId == "ModFood", "失效后允许用户明确重新选择。");
      catalog.Missing = true; browser.Scan();
      Check(browser.Snapshot == null && browser.SelectedGoodId == null && browser.Message.Contains("原库存目标已失效"), "重扫后目标移除，清除对应选择并说明。");
      catalog.Missing = false; browser.Discover(); browser.Select("真实身份"); catalog.Invalid = true; browser.Select("真实身份");
      Check(browser.Snapshot == null && browser.Targets.Length == 0, "读取前实际目标失效清除选择。");
      catalog.Invalid = false; browser.Discover(); catalog.ThrowRead = true; browser.Select("真实身份");
      Check(browser.Snapshot == null && browser.Message.Contains("读取异常"), "读异常无陈旧快照。");
      catalog.ThrowRead = false; catalog.FailScan = true; browser.Scan();
      Check(browser.Snapshot == null && browser.Targets.Length == 0, "扫描失败不展示旧选择。");
      catalog.FailScan = false; browser.Scan(); browser.Switch(InventorySource.DistrictCenter);
      Check(browser.Snapshot == null && browser.Targets.Length == 1, "切换地区不自动选取其他目标。");
      browser.Dispose(); browser.Dispose();
      Check(browser.Targets.Length == 0 && browser.Snapshot == null && browser.LastScan == null && !browser.Opened, "卸载释放目录与选择。");
      int calls = catalog.Scans + catalog.Discovers + catalog.Reads;
      browser.Scan(); browser.Switch(InventorySource.Building); browser.Discover(); browser.Select("真实身份");
      Check(calls == catalog.Scans + catalog.Discovers + catalog.Reads, "卸载旧模型不再访问场景。");
      return "库存扫描与选择模型：" + _checks + " 项断言通过。";
    }
  }
}