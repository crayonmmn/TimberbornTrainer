using System;
using System.Linq;
using Timberborn.EntitySystem;
using Timberborn.InventorySystem;
using Timberborn.Stockpiles;
using Timberborn.GameDistricts;
using TimberbornTrainer.Core;
using TimberbornTrainer.Modules;

namespace TimberbornTrainer.Tests {
  public static class MapSelectionTests {
    private static int _checks;
    private static void Check(bool ok, string message) { ++_checks; if (!ok) throw new Exception(message); }
    private static EntityComponent Building(EntityRegistry registry) {
      var e = new EntityComponent(); e.Add(new Timberborn.Buildings.Building()); e.Add(new Timberborn.BlockSystem.BlockObject());
      e.Add(new Inventories()); e.Add(new LabeledEntity()); e.Add(new Timberborn.SelectionSystem.SelectableObject()); registry.Values.Add(e); return e;
    }
    private static Inventory Warehouse(EntityComponent e, DistrictCenter district, string good) {
      e.Add(new DistrictBuilding { District = district });
      var i = e.Add(new Inventory { ComponentName = "Stockpile", Incoming = 0, Reserved = 0 }); i.Allow(good, 0);
      e.GetComponent<Inventories>().AllInventories.Add(i); e.Add(new Stockpile { Inventory = i });
      var a = e.Add(new SingleGoodAllower { AllowedGood = good }); a.Initialize(i); return i;
    }
    public static string Run() {
      _checks = 0;
      var entities = new EntityRegistry(); var centers = new DistrictCenterRegistry(); var district = Building(entities).Add(new DistrictCenter()); centers.Values.Add(district);
      var owner = Building(entities); var a = Warehouse(owner, district, "Water");
      var other = Building(entities); var b = Warehouse(other, district, "ModFood");
      var goods = new Timberborn.Goods.TestGoods(); var selection = new Timberborn.SelectionSystem.EntitySelectionService();
      var catalog = new NativeInventoryCatalog(entities, centers, goods, new Timberborn.ResourceCountingSystem.ResourceCountingService(), selection); catalog.PostLoad();
      var state = new TrainerState(TrainerState.TargetVersion, true, "6000.5.11f1"); state.SetEnabled(true); UnityEngine.Time.timeScale = 0;
      var editor = new NativeInventoryEditor(catalog, new TrainerSession { State = state }, new Timberborn.TimeSystem.SpeedManager { CurrentSpeed = 0 });
      Check(!catalog.ReadSelectedWarehouse().Valid, "未选择时不给出可写目标。");
      selection.SelectedObject = owner.GetComponent<Timberborn.SelectionSystem.SelectableObject>(); var s = catalog.ReadSelectedWarehouse(); string old = s.Target.Id;
      Check(s.Valid && s.Resources.Single().GoodId == "Water" && s.Resources.Single().Amount == 0, "实际选择适配器读取空仓库的原生配置，而非由存货推断。");
      Check(entities.Enumerations == 0 && goods.Reads == 0 && catalog.LastScan == null, "选中仓库不扫描实体或枚举全资源定义。");
      var r = editor.Execute(old, "Water", InventoryWriteOperation.Add, "2");
      Check(r.State == InventoryWriteState.Succeeded && a.Values["Water"] == 2 && r.Before == 0 && r.After == 2, "复用实际编辑器对空仓库配置资源增量并回读。");
      selection.SelectedObject = null; r = editor.Execute(old, "Water", InventoryWriteOperation.Add, "1");
      Check(r.State == InventoryWriteState.Rejected && a.WriteCalls == 1, "取消选择后，即使旧目标ID仍在，也不能写入。");
      Check(!catalog.ReadSelectedWarehouse().Valid && !catalog.Read(old).Valid, "清除地图目标引用后旧目录不授权。");
      selection.SelectedObject = other.GetComponent<Timberborn.SelectionSystem.SelectableObject>(); s = catalog.ReadSelectedWarehouse();
      Check(s.Valid && s.Target.Id != old && s.Resources.Single().GoodId == "ModFood", "切换仓库跟随单物品配置及真实身份。");
      r = editor.Execute(s.Target.Id, "ModFood", InventoryWriteOperation.Add, "4");
      Check(r.State == InventoryWriteState.Succeeded && b.Values["ModFood"] == 4 && a.Values["Water"] == 2, "标准原生组件配模拟Mod资源可用，不混仓。");
      b.Reserved = 3; r = editor.Execute(s.Target.Id, "ModFood", InventoryWriteOperation.Remove, "2");
      Check(r.State == InventoryWriteState.Rejected && b.Values["ModFood"] == 4, "移除不越过非零原生库存预留。");
      r = editor.Execute(s.Target.Id, "ModFood", InventoryWriteOperation.Remove, "1");
      Check(r.State == InventoryWriteState.Succeeded && b.Values["ModFood"] == 3, "允许范围内减少并回读。");
      r = editor.Execute(s.Target.Id, "ModFood", InventoryWriteOperation.Add, "1000");
      Check(r.State == InventoryWriteState.Rejected, "保持容量限制。");
      other.GetComponent<SingleGoodAllower>().AllowedGood = null;
      Check(!catalog.ReadSelectedWarehouse().Valid, "即使有存货，未配置资源也不猜测。");
      other.GetComponent<SingleGoodAllower>().AllowedGood = "Unregistered";
      Check(catalog.ReadSelectedWarehouse().Message.Contains("Unregistered"), "未知配置资源保留ID和原因。");
      other.GetComponent<SingleGoodAllower>().AllowedGood = "ModFood"; s = catalog.ReadSelectedWarehouse();
      b.Allow("Water", 0); other.GetComponent<SingleGoodAllower>().AllowedGood = "Water";
      Check(editor.Execute(s.Target.Id, "ModFood", InventoryWriteOperation.Add, "1").State == InventoryWriteState.Rejected, "配置改变后旧资源请求在原生执行器内被拒绝。");
      goods.MissingName = true; s = catalog.ReadSelectedWarehouse();
      Check(s.Resources.Single().GoodId == "Water" && s.Resources.Single().DisplayName.Contains("Water"), "显示名称缺失不丢失原生ID。");
      other.Deleted = true; Check(!catalog.ReadSelectedWarehouse().Valid, "拆除或失效目标清理。");
      var road = Building(entities); var ci = road.Add(new Inventory { ComponentName = "ConstructionSite" });
      road.GetComponent<Inventories>().AllInventories.Add(ci); road.Add(new Timberborn.ConstructionSites.ConstructionSite { Inventory = ci });
      selection.SelectedObject = road.GetComponent<Timberborn.SelectionSystem.SelectableObject>();
      Check(!catalog.ReadSelectedWarehouse().Valid, "道路/住宅施工库存无仓储绑定，不自动识别为可编辑仓库。");
      selection.SelectedObject = owner.GetComponent<Timberborn.SelectionSystem.SelectableObject>(); owner.Add(new SingleGoodAllower { AllowedGood = "Water" });
      Check(!catalog.ReadSelectedWarehouse().Valid, "不能唯一确认配置的多容器对象拒绝，不开发多资源选择器。");
      catalog.Unload(); Check(!catalog.ReadSelectedWarehouse().Valid, "场景卸载后选择适配器无效。");
      return "地图仓储实际适配器/原生接口替身：" + _checks + " 项通过，未实机验证。";
    }
  }
}
