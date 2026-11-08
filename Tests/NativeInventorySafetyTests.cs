using System;
using System.Linq;
using Timberborn.EntitySystem;
using Timberborn.InventorySystem;
using Timberborn.GameDistricts;
using Timberborn.Stockpiles;
using Timberborn.Goods;
using TimberbornTrainer.Modules;

namespace TimberbornTrainer.Tests {
  public static class NativeInventorySafetyTests {
    private static int _checks;
    private static void Check(bool ok, string message) { ++_checks; if (!ok) throw new Exception(message); }
    private sealed class Fixture {
      public EntityRegistry Entities = new EntityRegistry();
      public DistrictCenterRegistry Districts = new DistrictCenterRegistry();
      public TestGoods Goods = new TestGoods();
      public EntityComponent Owner, DistrictOwner;
      public Inventory Inventory;
      public NativeInventoryCatalog Catalog;
      public string Target;
      public Fixture() {
        Owner = Building(); DistrictOwner = Building();
        var district = DistrictOwner.Add(new DistrictCenter()); Districts.Values.Add(district);
        Owner.Add(new DistrictBuilding { District = district });
        Inventory = Owner.Add(new Inventory()); Inventory.Allow("Water");
        Owner.GetComponent<Inventories>().AllInventories.Add(Inventory);
        Owner.Add(new Stockpile { Inventory = Inventory });
        Catalog = new NativeInventoryCatalog(Entities, Districts, Goods,
          new Timberborn.ResourceCountingSystem.ResourceCountingService(), new Timberborn.SelectionSystem.EntitySelectionService());
        Catalog.PostLoad(); Catalog.Scan(); Target = Catalog.Discover(InventorySource.Building).Targets.Single().Id;
      }
      private EntityComponent Building() {
        var e = new EntityComponent(); e.Add(new Timberborn.BlockSystem.BlockObject());
        e.Add(new Timberborn.Buildings.Building()); e.Add(new Inventories()); Entities.Values.Add(e); return e;
      }
      public InventoryIdentityReport Read() => Catalog.DiagnoseIdentity(Target, "Water");
    }
    public static string Run() {
      _checks = 0;
      var f = new Fixture(); int scans = f.Entities.Enumerations;
      Check(f.Read().Status == InventoryIdentityStatus.Valid && !f.Read().IsExecutionAuthorized, "真实解析通过仅为诊断，无执行权限。");
      Check(f.Entities.Enumerations == scans, "身份检查只解析单实体，不全图枚举。");
      f.Owner.Deleted = true; Check(f.Read().Code == "EntityChanged", "删除实体拒绝。");
      f = new Fixture(); f.Entities.Values.Remove(f.Owner); Check(f.Read().Code == "EntityChanged", "尚有引用但已注销拒绝。");
      f = new Fixture(); var replacement = new EntityComponent { EntityId = f.Owner.EntityId };
      f.Entities.Values.Remove(f.Owner); f.Entities.Values.Add(replacement); Check(f.Read().Code == "EntityChanged", "相同Id不同对象拒绝。");
      f = new Fixture(); f.Owner.EntityId = Guid.NewGuid(); Check(f.Read().Code == "EntityChanged", "同址重建或Id变化拒绝。");
      f = new Fixture(); f.Owner.GetComponent<Inventories>().AllInventories.Clear();
      var other = f.Owner.Add(new Inventory()); f.Owner.GetComponent<Inventories>().AllInventories.Add(other);
      Check(f.Read().Code == "InventoryChanged", "同名组件替换拒绝。");
      f = new Fixture(); f.Inventory.ComponentName = "Replaced"; Check(f.Read().Code == "InventoryChanged", "组件名变化拒绝。");
      f = new Fixture(); f.Inventory.Owner = f.DistrictOwner; Check(f.Read().Code == "InventoryChanged", "库存不属于原建筑拒绝。");
      f = new Fixture(); f.Owner.GetComponent<Inventories>().AllInventories.Add(f.Owner.Add(new Inventory()));
      Check(f.Read().Code == "InventoryChanged", "重复同名库存拒绝。");
      f = new Fixture(); f.Owner.GetComponent<Timberborn.BlockSystem.BlockObject>().IsFinished = false;
      Check(f.Read().Code == "NotFinished", "未完工拒绝。");
      f = new Fixture(); f.Inventory.Enabled = false; Check(f.Read().Code == "NotFinished", "停用库存拒绝。");
      f = new Fixture(); f.Owner.GetComponent<Stockpile>().Inventory = other;
      Check(f.Read().Code == "NotWarehouse", "生产或其他库存不因同一建筑获得资格。");
      f = new Fixture(); f.Owner.Add(new Timberborn.ConstructionSites.ConstructionSite { Inventory = f.Inventory });
      Check(f.Read().Code == "NotWarehouse", "冲突施工绑定拒绝。");
      f = new Fixture(); f.Owner.Add(new DistrictCenter()); Check(f.Read().Code == "ExcludedOwner", "地区中心拒绝。");
      f = new Fixture(); f.Owner.Add(new Timberborn.DistributionSystem.DistrictCrossing());
      Check(f.Read().Code == "ExcludedOwner", "地区交汇点拒绝。");
      f = new Fixture(); f.Owner.GetComponent<DistrictBuilding>().District = null;
      Check(f.Read().Code == "DistrictUnknown", "未知地区不当作零或默认地区。");
      f = new Fixture(); f.Owner.GetComponent<DistrictBuilding>().District = f.Owner.Add(new DistrictCenter());
      Check(f.Read().Code == "DistrictChanged", "地区归属变化拒绝。");
      f = new Fixture(); f.Goods.Values.Remove("Water"); Check(f.Read().Code == "ResourceUnknown", "资源失效拒绝。");
      f = new Fixture(); f.Catalog.Unload(); Check(f.Read().Code == "SceneUnavailable", "卸载后原引用不可诊断。");
      f = new Fixture(); string stale = f.Target; f.Catalog.Unload();
      Check(f.Catalog.DiagnoseIdentity(stale, "Water").Status == InventoryIdentityStatus.Invalid, "旧场景标识不能授权。");
      return "原生库存身份替身测试：" + _checks + " 项断言通过。不是实机证明。";
    }
  }
}
