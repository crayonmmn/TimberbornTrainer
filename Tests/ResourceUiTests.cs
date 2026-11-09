using System;
using System.Linq;
using Timberborn.UILayoutSystem;
using TimberbornTrainer.Core;
using TimberbornTrainer.Modules;
using TimberbornTrainer.UI;
using UnityEngine.UIElements;

namespace TimberbornTrainer.Tests {
  public static class ResourceUiTests {
    private static int _checks;
    private static void Check(bool ok, string message) { ++_checks; if (!ok) throw new Exception(message); }
    private sealed class Points : ISciencePoints {
      public int Value = 100, Adds, Removes;
      public int Read() => Value;
      public void Add(int amount) { Value += amount; ++Adds; }
      public void Subtract(int amount) { Value -= amount; ++Removes; }
    }
    public static string Run(string directory) {
      _checks = 0;
      var state = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1");
      var session = new TrainerSession { State = state, Store = new ConfigStore(directory) };
      var points = new Points(); var layout = new UILayout(); var catalog = new NativeInventoryCatalog();
      var editor = new NativeInventoryEditor(catalog, () => state.EffectiveEnabled);
      var panel = new MainPanel(session, layout, new ResourceModule { Science = new ScienceCommands(points, () => state.EffectiveEnabled) },
        new BuildingModule(), new BeaverModule(), catalog, editor, new WeatherModule());
      // Compile and execute the real Load/SelectModule/click callbacks and both real resource UIs.
      panel.Load(); var root = layout.Root;
      root.Tree().OfType<Button>().Single(b => b.text == "修改工具").Click();
      var scienceEntry = root.Q<Button>("TrainerScienceEntry"); var inventoryEntry = root.Q<Button>("TrainerInventoryEntry");
      Check(scienceEntry != null && inventoryEntry != null && scienceEntry.Visible && scienceEntry.enabledInHierarchy,
        "实际MainPanel.Load注册两个可见入口，全局关闭不隐藏科研入口。");
      Check(!(scienceEntry.parent is ScrollView) && scienceEntry.parent.parent == root.Q("TrainerPanel"),
        "资源入口位于滚动正文外，不会随长库存内容埋没。");
      scienceEntry.Click();
      Check(root.Q("TrainerScienceArea").Visible && !root.Q("TrainerInventoryArea").Visible, "实际入口切换到独立科研区域。");
      Check(!root.Q("TrainerInventoryArea").Tree().Contains(root.Q("TrainerScienceArea")), "科研区域不再嵌入库存内部。");
      Check(root.Q<Label>("ScienceCurrentCount").text.Contains("100") && !root.Q<Button>("ScienceAddButton").enabledInHierarchy,
        "未启用全局时仍能读点数，但不能增加。");
      root.Q<Toggle>("TrainerEnabled").value = true;
      root.Q<TextField>("ScienceAddInput").value = "25"; root.Q<Button>("ScienceAddButton").Click();
      Check(points.Value == 125 && points.Adds == 1 && root.Q<Label>("ScienceCurrentCount").text.Contains("125"), "实际科研按钮调用已有命令并刷新真实回读。");
      root.Q<TextField>("ScienceSubtractInput").value = "10"; root.Q<Button>("ScienceSubtractButton").Click();
      Check(points.Value == 115 && points.Removes == 1 && root.Q<Label>("TrainerStatus").text.Contains("修改后：115"), "科研减少与结果反馈实际接通。");
      Check(catalog.LastScan == null && catalog.Reads == 0, "打开科研入口不要求扫描或读取库存。");
      inventoryEntry.Click();
      Check(!root.Q("TrainerScienceArea").Visible && root.Q("TrainerInventoryArea").Visible, "库存与科研共存且互不覆盖。");
      var change = root.Q<Button>("InventoryChangeTarget");
      Check(!root.Q("InventorySelector").Visible, "选择器默认收起，不占用资源操作滚动区。");
      change.Click();
      Check(root.Q("InventorySelector").Visible && catalog.LastScan == null, "更换目标只展开，不隐式扫描。");
      root.Q<Button>("InventoryScan").Click();
      Check(catalog.LastScan.Sequence == 1 && !root.Q<Button>("InventoryAdd").enabledInHierarchy, "聚居地聚合统计只读，不能注入物品。");
      var source = root.Q<DropdownField>("InventorySource"); source.index = 2;
      root.Q<Button>("InventoryOwner:a").Click();
      Check(!root.Q("InventorySelector").Visible && root.Q("InventorySelectionSummary").tooltip == "a", "选中建筑后自动收起，摘要保留真实目标标识。");
      root.Tree().OfType<Button>().Single(b => b.text == "定位目标").Click();
      Check(!root.Q("InventorySelector").Visible && root.Q("InventorySelectionSummary").tooltip == "a" && catalog.LastScan.Sequence == 1,
        "收起后仍能定位当前目标，不必展开列表或重新扫描。");
      Check(root.Q<DropdownField>("InventoryTarget").choices.Count == 2 && root.Q<DropdownField>("InventoryResource").choices.Any(s => s.Contains("ModFood")),
        "选择建筑后分别列出多库存及动态资源标识，不固定原版资源。");
      var amount = root.Q<TextField>("InventoryAmount"); amount.value = "10"; root.Q<Button>("InventoryAdd").Click();
      Check(catalog.A.Amount == 20 && editor.LastTarget == "a" && editor.LastResource == "ModFood" && root.Q<Label>("InventoryStock").text.Contains("真实数量：20"),
        "实际库存UI增量命令路由正确并刷新库存读数。");
      scienceEntry.Click(); inventoryEntry.Click();
      Check(root.Q("InventorySelectionSummary").tooltip == "a" && !root.Q("InventorySelector").Visible &&
        root.Q<DropdownField>("InventoryResource").value.Contains("ModFood"), "资源子页切换保留当前有效目标和资源，并保持收起。");
      amount.value = "5"; root.Q<Button>("InventoryRemove").Click();
      Check(catalog.A.Amount == 15 && root.Q<Label>("InventoryEditResult").text.Contains("修改前：20") &&
        root.Q<Label>("InventoryEditResult").text.Contains("真实回读：15"), "减少后保留前后反馈，不被刷新清除。");
      amount.value = "-1"; root.Q<Button>("InventoryAdd").Click();
      Check(catalog.A.Writes == 2 && root.Q<Label>("InventoryEditResult").text.Contains("整数"), "非法输入明确拒绝且零额外写入。");
      amount.value = "1000"; root.Q<Button>("InventoryAdd").Click();
      Check(catalog.A.Writes == 2 && root.Q<Label>("InventoryEditResult").text.Contains("整单未执行"), "容量不足保留原约束并明确反馈。");
      root.Tree().OfType<Button>().Single(b => b.text == "主动刷新").Click();
      Check(catalog.LastScan.Sequence == 1 && root.Q<DropdownField>("InventoryResource").value.Contains("ModFood"), "主动刷新保留资源且不全图扫描。");
      change.Click(); root.Q<DropdownField>("InventoryTarget").index = 1;
      Check(!root.Q<Button>("InventoryAdd").enabledInHierarchy && !root.Q<Button>("InventoryRemove").enabledInHierarchy,
        "同建筑施工库存仍只读，不因目标已选择而获写入按钮。");
      Check(!root.Q("InventorySelector").Visible, "明确选择具体库存后自动收起。");
      change.Click(); root.Q<DropdownField>("InventoryTarget").index = 0;
      root.Q<DropdownField>("InventoryResource").index = 1; amount.value = "1"; root.Q<Button>("InventoryAdd").Click();
      Check(editor.LastResource == "Water" && editor.LastTarget == "a", "切换资源后增量命令携带新原生资源ID。");
      Check(!root.Q("InventorySelector").Visible, "切换资源和调整数量不自动展开选择器。");
      change.Click();
      root.Q<Button>("InventoryOwner:b").Click();
      Check(root.Q<DropdownField>("InventoryResource").choices.Count == 1 && root.Q<DropdownField>("InventoryResource").value.Contains("Water"), "切换建筑刷新允许资源列表。");
      change.Click(); source.index = 1; root.Q<Button>("InventoryOwner:center").Click(); amount.value = "2"; root.Q<Button>("InventoryAdd").Click();
      Check(editor.LastTarget == "center" && catalog.Center.Amount == 12 && catalog.B.Amount == 10, "地区中心真实目标与其他建筑分离（接口替身）。");
      change.Click(); source.index = 2; root.Q<Button>("InventoryOwner:a").Click(); int writes = catalog.A.Writes;
      catalog.A.Alive = false; amount.value = "1"; root.Q<Button>("InventoryAdd").Click();
      Check(catalog.A.Writes == writes && !root.Q<Button>("InventoryAdd").enabledInHierarchy &&
        root.Q<Label>("InventoryEditResult").text.Contains("失效"), "目标失效安全失败并清除可编辑UI状态。");
      Check(!root.Q("InventorySelector").Visible && root.Q<Label>("InventorySelectionSummary").text.Contains("没有有效目标"),
        "失效时明确提示更换目标，不擅自展开或替换选择。");
      scienceEntry.Click();
      Check(points.Value == 115 && root.Tree().Count(e => e.name == "ScienceAddButton") == 1, "库存异常不移除或重复创建科研服务入口。");
      root.Q<Toggle>("TrainerEnabled").value = false;
      Check(!root.Q<Button>("ScienceAddButton").enabledInHierarchy && root.Q<Label>("ScienceCurrentCount").text.Contains("115"), "全局关闭停止命令，科研读取保持可用。");
      root.Tree().OfType<Button>().Single(b => b.text == "建造与生产").Click();
      Check(!scienceEntry.Visible, "其他模块不叠加资源子导航。");
      root.Tree().OfType<Button>().Single(b => b.text == "资源管理").Click();
      Check(scienceEntry.Visible && root.Q("TrainerScienceArea").Visible, "返回资源页恢复科研选择，入口不丢失。");
      int reads = catalog.Reads; UnityEngine.Time.unscaledTime = 100; panel.UpdateSingleton();
      Check(catalog.Reads == reads && catalog.LastScan.Sequence == 1, "通用UI刷新不读取库存或重新扫描。");
      panel.Unload();
      Check(catalog.LastScan == null, "实际Unload释放库存目录与UI场景引用。");
      return "资源UI实际注册/回调链：" + _checks + " 项断言通过（UI Toolkit树/事件替身，非Unity布局或游戏实测）。";
    }
  }
}
