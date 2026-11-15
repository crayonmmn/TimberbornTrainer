using System;
using System.Linq;
using Timberborn.SingletonSystem;
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
      public void Add(int amount) { Value += amount; ++Adds; } public void Subtract(int amount) { Value -= amount; ++Removes; }
    }
    public static string Run(string directory) {
      _checks = 0; var state = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1");
      var session = new TrainerSession { State = state, Store = new ConfigStore(directory) }; var points = new Points();
      var layout = new UILayout(); var catalog = new NativeInventoryCatalog(); var events = new EventBus();
      var editor = new NativeInventoryEditor(catalog, () => state.EffectiveEnabled);
      var panel = new MainPanel(session, layout, new ResourceModule { Science = new ScienceCommands(points, () => state.EffectiveEnabled) },
        new BuildingModule(), new BeaverModule(), catalog, editor, events);
      panel.Load(); var root = layout.Root;
      var togglePanel = root.Tree().OfType<Button>().Single(b => b.text == "修改工具"); togglePanel.Click();
      var science = root.Q<Button>("TrainerScienceEntry"); var inventory = root.Q<Button>("TrainerInventoryEntry");
      Check(science.Visible && inventory.Visible && events.Count == 1, "实际Load注册独立入口，库存打开后仅一份选择订阅。");
      Check(root.Q<Label>("MapInventoryTarget").text.Contains("请在地图") && !root.Q<Button>("MapInventoryAdd").enabledInHierarchy,
        "未选择时给出地图选择提示，没有可写旧目标。");
      Check(root.Q("InventorySelector") == null && root.Q("InventorySource") == null && root.Q("InventoryResource") == null,
        "默认库存页面不再注册手动地区/建筑/资源选择器。");
      catalog.Pick("a", events);
      Check(root.Q<Button>("MapInventoryAdd").enabledInHierarchy && root.Q<Button>("MapInventoryRemove").enabledInHierarchy,
        "有效仓库在全局关闭时仍允许点击查看拒绝原因");
      foreach (var name in new[] { "MapInventoryAdd", "MapInventoryRemove" }) {
        root.Q<Button>(name).Click();
        Check(catalog.A.Writes == 0 && root.Q<Label>("MapInventoryResult").text.Contains("请先开启"),
          "全局关闭的实际增减UI回调给出明确提示且零写入");
      }
      root.Q<Toggle>("TrainerEnabled").value = true;
      root.Q<TextField>("MapInventoryAmount").value = "1";
      root.Q<Button>("MapInventoryAdd").Click();root.Q<Button>("MapInventoryRemove").Click();
      Check(catalog.A.Amount == 10 && catalog.A.Writes == 2,"全局开启后实际UI增减即时生效，无旧状态缓存");
      root.Q<Toggle>("TrainerEnabled").value = false;
      root.Q<Button>("MapInventoryAdd").Click();
      Check(catalog.A.Writes == 2 && root.Q<Label>("MapInventoryResult").text.Contains("请先开启"),"关闭后即时恢复安全提示");
      catalog.Pick(null, events);
      science.Click(); Check(events.Count == 0 && root.Q("TrainerScienceArea").Visible, "科研页面保持独立，并停止地图选择监听。");
      Check(root.Q<Label>("ScienceCurrentCount").text.Contains("100") && !root.Q<Button>("ScienceAddButton").enabledInHierarchy, "全局关闭仍可读科研。");
      root.Q<Toggle>("TrainerEnabled").value = true;
      root.Q<TextField>("ScienceAddInput").value = "10"; root.Q<Button>("ScienceAddButton").Click();
      root.Q<TextField>("ScienceSubtractInput").value = "5"; root.Q<Button>("ScienceSubtractButton").Click();
      Check(points.Value == 105 && points.Adds == 1 && points.Removes == 1 && root.Q<Label>("TrainerStatus").text.Contains("105"), "实际科研增减回调及反馈保持正常。");
      root.Q<TextField>("ScienceSetInput").value = "120";root.Q<Button>("ScienceSetButton").Click();
      Check(points.Value == 120 && root.Q<Label>("ScienceCurrentCount").text.Contains("120"),"科研设置及真实读取UI回调保持原样");
      inventory.Click(); Check(events.Count == 1, "返回库存只注册一次。");
      catalog.A.Amount = 0; catalog.Pick("a", events);
      Check(root.Q<Label>("MapInventoryTarget").text.Contains("ModFood") && root.Q<Label>("MapInventoryStock").text.Contains("当前数量：0"),
        "地图事件立即更新空仓库及配置资源，不需扫描。");
      var amount = root.Q<TextField>("MapInventoryAmount"); amount.value = "2"; root.Q<Button>("MapInventoryAdd").Click();
      Check(catalog.A.Amount == 2 && editor.LastTarget == "a" && editor.LastResource == "ModFood" && root.Q<Label>("MapInventoryResult").text.Contains("真实回读：2"),
        "实际地图UI复用已有命令增加和回读。");
      amount.value = "1"; root.Q<Button>("MapInventoryRemove").Click();
      Check(catalog.A.Amount == 1 && root.Q<Label>("MapInventoryStock").text.Contains("当前数量：1"), "减少后数据与反馈及时更新。");
      catalog.Pick("b", events); amount.value = "3"; root.Q<Button>("MapInventoryAdd").Click();
      Check(editor.LastTarget == "b" && editor.LastResource == "Water" && catalog.B.Amount == 13 && catalog.A.Amount == 1, "切仓后不误写前仓。");
      catalog.Pick(null, events); int writes = catalog.B.Writes; root.Q<Button>("MapInventoryAdd").Click();
      Check(catalog.B.Writes == writes && !root.Q<Button>("MapInventoryAdd").enabledInHierarchy, "取消选择事件立即关闭旧目标操作。");
      catalog.Pick("input", events); Check(!root.Q<Button>("MapInventoryAdd").enabledInHierarchy, "施工库存选择不会变成可编辑仓库。");
      catalog.Configured["a"] = null; catalog.Pick("a", events);
      Check(root.Q<Label>("MapInventoryTarget").text.Contains("配置物品") && !root.Q<Button>("MapInventoryAdd").enabledInHierarchy, "未配置仓库有原生配置提示，不能按旧存货猜测。");
      catalog.Configured["a"] = "Water"; UnityEngine.Time.unscaledTime = 100; panel.UpdateSingleton();
      Check(root.Q<Label>("MapInventoryTarget").text.Contains("Water") && catalog.LastScan == null, "同一仓库配置改变由仅访问当前对象的低频刷新发现，无全地图扫描。");
      amount.value = "-1"; root.Q<Button>("MapInventoryAdd").Click();
      Check(root.Q<Label>("MapInventoryResult").text.Contains("整数"), "非法输入保留明确反馈。");
      amount.value = "1000"; root.Q<Button>("MapInventoryAdd").Click();
      Check(root.Q<Label>("MapInventoryResult").text.Contains("整单未执行"), "容量不足不截断或绕过规则。");
      catalog.A.Alive = false; amount.value = "1"; root.Q<Button>("MapInventoryAdd").Click();
      Check(!root.Q<Button>("MapInventoryAdd").enabledInHierarchy && root.Q<Label>("MapInventoryResult").text.Contains("旧目标"), "点击前重新读取并拒绝失效目标。");
      root.Tree().OfType<Button>().Single(b => b.text == "关闭面板").Click(); Check(events.Count == 0 && catalog.Releases > 0, "关闭面板释放订阅和目标引用。");
      int reads = catalog.Reads; catalog.Pick("b", events); panel.UpdateSingleton(); Check(catalog.Reads == reads, "隐藏面板不监听或轮询选择。");
      togglePanel.Click(); Check(events.Count == 1 && root.Q<Label>("MapInventoryTarget").text.Contains("Water"), "重新打开只绑定一次并读取真实当前选择。");
      science.Click(); Check(events.Count == 0 && root.Q("TrainerScienceArea").Visible && root.Tree().Count(e => e.name == "ScienceAddButton") == 1,
        "地图交互不会覆盖或重复科研入口。");
      root.Tree().OfType<Button>().Single(b => b.text == "建造与生产").Click(); Check(!science.Visible && events.Count == 0, "其他模块不保留库存监听。");
      Check(new[] { "资源管理", "建造与生产", "海狸管理" }.All(name => root.Tree().OfType<Button>().Count(b => b.text == name) == 1), "三个保留模块各有唯一实际导航入口。");
      Check(!root.Tree().OfType<Button>().Any(b => b.text == "天气管理" || b.text == "灾害管理"), "实际导航不再注册已删除模块。");
      panel.Unload(); Check(events.Count == 0, "卸载后没有选择事件订阅。");
      foreach (bool missing in new[] { false, true }) {
        var nextState = new TrainerState("未来游戏版本", true, "其他Unity", f => missing && f == TrainerFeature.Science ? "科研点接口兼容性警告" : null);
        var nextLayout = new UILayout(); var nextCatalog = new NativeInventoryCatalog(); var nextEvents = new EventBus();
        var next = new MainPanel(new TrainerSession { State = nextState, Store = new ConfigStore(directory) }, nextLayout,
          new ResourceModule { Science = new ScienceCommands(new Points(), () => nextState.EffectiveEnabled) },
          new BuildingModule(), new BeaverModule(), nextCatalog, new NativeInventoryEditor(nextCatalog, () => nextState.EffectiveEnabled), nextEvents);
        next.Load(); var tree = nextLayout.Root;
        tree.Tree().OfType<Button>().Single(b => b.text == "修改工具").Click();
        Check(tree.Q<Toggle>("TrainerEnabled").enabledInHierarchy, "实际全局开关不因未知版本变为不可交互");
        tree.Q<Toggle>("TrainerEnabled").value = true;
        Check(nextState.EffectiveEnabled && tree.Q<Label>("TrainerCompatibilityNotice").text.Contains("未来游戏版本"), "实际UI可启用且显示当前未验证版本");
        tree.Q<Button>("TrainerScienceEntry").Click();
        Check(tree.Q<Button>("ScienceAddButton").enabledInHierarchy, "接口警告不禁用科研操作按钮");
        Check((tree.Q<Label>("TrainerInterfaceWarningScience") != null) == missing, "缺失接口显示具体警告");
        var add = tree.Q<TextField>("ScienceAddInput"); add.value = "1";
        tree.Q<Button>("ScienceAddButton").Click();
        Check(tree.Q<Label>("ScienceCurrentCount").text.Contains("101"), "有无接口警告均允许实际科研回调和读取刷新");
        tree.Q<Button>("TrainerInventoryEntry").Click(); tree.Q<Button>("TrainerScienceEntry").Click();
        Check(tree.Tree().Count(e => e.name == "TrainerCompatibilityNotice") == 1, "页面切换不重复添加兼容性警告");
        next.Unload();
      }
      return "地图库存/科研实际UI注册回调：" + _checks + " 项通过（界面与场景替身，非实机）。";
    }
  }
}
