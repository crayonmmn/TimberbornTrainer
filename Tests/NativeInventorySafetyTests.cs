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
      f = new Fixture(); f.Inventory.Incoming = 0; f.Inventory.Reserved = 0;
      var report = f.Catalog.DiagnoseSafety(f.Target, "Water");
      Check(report.ObservedZeroReservations && report.Rows.Count == 2, "按注册域覆盖零预留，包含另一资源。");
      Check(report.UnknownReservationState && !report.CandidateEligible && !report.IsExecutionAuthorized,
        "观察零预留仍不能证明同步性与隐藏状态，写入关闭。");
      f.Inventory.ReservedByGood["Water"] = 2;
      report = f.Catalog.DiagnoseSafety(f.Target, "Water");
      Check(!report.ObservedZeroReservations && report.ReservedStock == 2, "单资源物品预留。");
      f.Inventory.Allow("ModFood", 5); f.Inventory.ReservedByGood["ModFood"] = 3;
      report = f.Catalog.DiagnoseSafety(f.Target, "Water");
      Check(report.ReservedStock == 5 && report.Rows.Single(r => r.Resource == "ModFood").Reserved == 3, "跨资源物品预留不可漏读。");
      f.Inventory.IncomingByGood["ModFood"] = 4;
      report = f.Catalog.DiagnoseSafety(f.Target, "Water");
      Check(report.ReservedCapacity == 4 && !report.CandidateEligible, "全部容量预留包含另一资源。");
      f.Inventory.IncomingByGood["Unregistered"] = 1;
      report = f.Catalog.DiagnoseSafety(f.Target, "Water");
      Check(!report.ObservationConsistent && report.ReservedCapacity == null, "未知资源容量预留不能当零。");
      f = new Fixture(); f.Inventory.Incoming = 0; f.Inventory.Reserved = 0; f.Inventory.ReservedByGood["Water"] = 99;
      Check(!f.Catalog.DiagnoseSafety(f.Target, "Water").ObservationConsistent, "负未预留量不钳制。");
      f.Inventory.Fail = true; report = f.Catalog.DiagnoseSafety(f.Target, "Water");
      Check(!report.ObservationConsistent && report.ReservedStock == null && report.Rows.Count == 0, "读取异常保留未知，无重试。");
      f = new Fixture(); f.Catalog.Unload();
      Check(!f.Catalog.DiagnoseSafety(f.Target, "Water").CandidateEligible, "场景卸载后诊断仍不能授予资格。");
      var trace = new InventoryTimingTrace();
      trace.Record("Disabled", 0); Check(!trace.Summary().Contains("Disabled"), "时序默认关闭。");
      trace.Start("Scene"); for (int i = 0; i < 300; ++i) trace.Record("Bounded", i);
      Check(trace.Stop().Contains("256/256"), "时序数量上限。");
      trace.Clear(); Check(!trace.Summary().Contains("Bounded"), "卸载清除场景时序。");
      trace.Record("AfterUnload", 0); Check(!trace.Summary().Contains("AfterUnload"), "卸载后不再记录。");
      f = new Fixture();
      var state = new TimberbornTrainer.Core.TrainerState("1.1.2.8-ffc2de0-sw", true, "6000.5.11f1"); state.SetEnabled(true);
      var session = new TimberbornTrainer.Core.TrainerSession { State = state };
      var speed = new Timberborn.TimeSystem.SpeedManager { CurrentSpeed = 0 };
      UnityEngine.Time.timeScale = 0;
      var editor = new NativeInventoryEditor(f.Catalog, session, speed);
      var edit = editor.Execute(f.Target, "Water", InventoryWriteOperation.Add, "5");
      Check(edit.State == InventoryWriteState.Succeeded && f.Inventory.Values["Water"] == 15 && f.Inventory.WriteCalls == 1,
        "实际适配器源码执行Give；已有物品/容量预留不要求归零（替身）。");
      speed.CurrentSpeed = 1; edit = editor.Execute(f.Target, "Water", InventoryWriteOperation.Add, "1");
      Check(edit.State == InventoryWriteState.Rejected && f.Inventory.WriteCalls == 1, "原生未暂停拒绝。");
      speed.CurrentSpeed = 0; state.SetEnabled(false); edit = editor.Execute(f.Target, "Water", InventoryWriteOperation.Add, "1");
      Check(edit.State == InventoryWriteState.Rejected && f.Inventory.WriteCalls == 1, "实时全局关闭拒绝。"); state.SetEnabled(true);
      f.Inventory.AfterWrite = () => { throw new Exception("模拟通知异常"); };
      edit = editor.Execute(f.Target, "Water", InventoryWriteOperation.Remove, "2");
      Check(edit.State == InventoryWriteState.Indeterminate && edit.After == 13 && f.Inventory.WriteCalls == 2,
        "真实适配器捕获变更后异常，回读且不重试（替身）。");
      InventoryEditResult nested = null;
      f.Inventory.AfterWrite = () => { nested = editor.Execute(f.Target, "Water", InventoryWriteOperation.Add, "1"); };
      edit = editor.Execute(f.Target, "Water", InventoryWriteOperation.Add, "1");
      Check(nested != null && nested.State == InventoryWriteState.Rejected && f.Inventory.WriteCalls == 3, "同步事件重入被拒绝。");
      f.Owner.Deleted = true; edit = editor.Execute(f.Target, "Water", InventoryWriteOperation.Add, "1");
      Check(edit.State == InventoryWriteState.Rejected && f.Inventory.WriteCalls == 3, "删除旧对象零写调用。");
      editor.Unload(); edit = editor.Execute(f.Target, "Water", InventoryWriteOperation.Add, "1");
      Check(edit.State == InventoryWriteState.Rejected && f.Inventory.WriteCalls == 3, "执行器卸载拒绝旧命令。");
      return "原生库存身份／预留替身测试：" + _checks + " 项断言通过。不是实机证明。";
    }
  }
}
