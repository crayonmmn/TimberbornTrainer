using System;
using System.Linq;
using System.Collections.Generic;
using Timberborn.BlockSystem;
using Timberborn.ConstructionSites;
using Timberborn.DistributionSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.InventorySystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.Stockpiles;

namespace TimberbornTrainer.Modules {
  public sealed partial class NativeInventoryCatalog : IInventorySafetyDiagnostics {
    public InventorySafetyReport DiagnoseSafety(string targetId, string goodId) {
      _timing.Record("UI.Diagnose.Enter", _observedTicks);
      try { return ObserveSafety(targetId, goodId); }
      finally { _timing.Record("UI.Diagnose.Exit", _observedTicks); }
    }
    private InventorySafetyReport ObserveSafety(string targetId, string goodId) {
      var identity = DiagnoseIdentity(targetId, goodId);
      try {
        NativeTarget target;
        if (targetId == null || !_targets.TryGetValue(targetId, out target) || !Valid(target) || !target.Inventory)
          return new InventorySafetyReport(identity, null, false, null, null, "没有有效的独立原生库存；聚合统计不适用。");
        var inventory = target.Inventory;
        var stock = inventory.Stock.ToArray();
        var capacityReservations = inventory.ReservedCapacity().ToArray();
        var allowed = new HashSet<string>(inventory.AllowedGoods.Select(g => g.StorableGood.GoodId), StringComparer.Ordinal);
        var registered = _goods.Goods.ToArray();
        var ids = stock.Select(g => g.GoodId).Concat(capacityReservations.Select(g => g.GoodId))
          .Concat(allowed).Concat(registered).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (ids.Length > 4096 || ids.Any(string.IsNullOrEmpty))
          return new InventorySafetyReport(identity, null, false, null, null, "资源域缺失或超过诊断上限，预留状态未知。");
        bool consistent = stock.All(g => g.Amount >= 0) && capacityReservations.All(g => g.Amount >= 0)
          && stock.Select(g => g.GoodId).Distinct().Count() == stock.Length
          && capacityReservations.Select(g => g.GoodId).Distinct().Count() == capacityReservations.Length;
        var rows = new List<InventoryReservationRow>();
        long reserved = 0, reservedCapacity = 0, amountSum = 0;
        foreach (string id in ids) {
          long amount = inventory.AmountInStock(id), unreserved = inventory.UnreservedAmountInStock(id);
          long incoming = inventory.ReservedCapacity(id), limit = inventory.LimitedAmount(id), free = inventory.UnreservedCapacity(id);
          bool known = _goods.HasGood(id) && _goods.GetGood(id) != null;
          long capturedAmount = stock.Where(g => g.GoodId == id).Sum(g => (long)g.Amount);
          long capturedReservation = capacityReservations.Where(g => g.GoodId == id).Sum(g => (long)g.Amount);
          if (!known || amount < 0 || unreserved < 0 || unreserved > amount || incoming < 0 || free < 0 || limit < 0
              || amount != capturedAmount || incoming != capturedReservation) consistent = false;
          reserved += amount - unreserved; reservedCapacity += incoming; amountSum += amount;
          rows.Add(new InventoryReservationRow(id, amount, unreserved, incoming, free, limit, allowed.Contains(id), known));
        }
        if (amountSum != inventory.TotalAmountInStock || amountSum > inventory.Capacity || inventory.Capacity < 0
            || reservedCapacity != capacityReservations.Sum(g => (long)g.Amount)) consistent = false;
        // Detect visible changes, but never claim that a matching second read proves atomicity.
        foreach (var row in rows) {
          if (inventory.AmountInStock(row.Resource) != row.Amount || inventory.UnreservedAmountInStock(row.Resource) != row.Unreserved
              || inventory.ReservedCapacity(row.Resource) != row.CapacityReservation) consistent = false;
        }
        var after = DiagnoseIdentity(targetId, goodId);
        if (!Valid(target) || after.Code != identity.Code || after.District != identity.District || after.Scene != identity.Scene) consistent = false;
        return new InventorySafetyReport(after, rows, consistent, consistent ? reserved : (long?)null,
          consistent ? reservedCapacity : (long?)null,
          "仅为按需观察；公共资源域与两次读数不能证明原子性或隐藏预留完整性。安全执行点未证明，候选写入资格关闭。"
          + (!consistent ? " 本次读数不自洽或发生变化，预留总量未知。" : ""));
      } catch (Exception) {
        return new InventorySafetyReport(identity, null, false, null, null, "原生预留读取异常，状态未知；没有修改库存或自动重试。");
      }
    }
    public InventoryIdentityReport DiagnoseIdentity(string targetId, string goodId) {
      try { return CheckNativeIdentity(targetId, goodId); }
      catch (Exception) {
        return IdentityResult(InventoryIdentityStatus.Unknown, "InspectionException", "原生身份读取异常，不能确认目标。", goodId);
      }
    }
    private InventoryIdentityReport IdentityResult(InventoryIdentityStatus status, string code,
        string reason, string good, string district = null) =>
      new InventoryIdentityReport(status, code, reason, _scene, district, good);

    private InventoryIdentityReport CheckNativeIdentity(string targetId, string goodId) {
      NativeTarget target;
      if (!_ready || _entities == null || _scene == null)
        return IdentityResult(InventoryIdentityStatus.Invalid, "SceneUnavailable", "当前场景未就绪或已卸载。", goodId);
      if (targetId == null || !_targets.TryGetValue(targetId, out target) || target.Scene != _scene)
        return IdentityResult(InventoryIdentityStatus.Invalid, "TargetExpired", "目标不属于当前场景目录，请重新扫描和选择。", goodId);
      var owner = _entities.GetEntity(target.EntityId);
      if (!owner || !ReferenceEquals(owner, target.Owner) || !OwnerValid(owner))
        return IdentityResult(InventoryIdentityStatus.Invalid, "EntityChanged", "原生注册实体已删除、替换或尚未初始化。", goodId);
      var inventory = target.Inventory;
      var inventories = owner.GetComponent<Inventories>();
      if (!inventory || !inventories || !ReferenceEquals(inventory.GetComponent<EntityComponent>(), owner)
          || inventory.ComponentName != target.Component
          || inventories.AllInventories.Count(i => ReferenceEquals(i, inventory)) != 1
          || inventories.AllInventories.Count(i => i && i.ComponentName == target.Component) != 1)
        return IdentityResult(InventoryIdentityStatus.Invalid, "InventoryChanged", "库存组件引用、归属或唯一组件名已变化。", goodId);
      if (owner.GetComponent<DistrictCenter>() || owner.GetComponent<DistrictCrossing>() || owner.GetComponent<DistrictCrossingInventory>())
        return IdentityResult(InventoryIdentityStatus.Invalid, "ExcludedOwner", "地区中心及地区交汇点不是首批普通仓储候选。", goodId);
      if (!owner.GetComponent<BlockObject>().IsFinished || !inventory.Enabled)
        return IdentityResult(InventoryIdentityStatus.Invalid, "NotFinished", "建筑未完工或库存已停用。", goodId);
      var stockpile = owner.GetComponent<Stockpile>();
      var construction = owner.GetComponent<ConstructionSite>();
      var output = owner.GetComponent<SimpleOutputInventory>();
      if (!stockpile || !stockpile.Enabled || !ReferenceEquals(stockpile.Inventory, inventory)
          || (construction && ReferenceEquals(construction.Inventory, inventory))
          || (output && ReferenceEquals(output.Inventory, inventory)))
        return IdentityResult(InventoryIdentityStatus.Invalid, "NotWarehouse", "目标不是明确绑定的已完工普通仓储；施工、生产及未知用途只读。", goodId);
      if (inventory.GetType() != typeof(Inventory) || stockpile.GetType() != typeof(Stockpile))
        return IdentityResult(InventoryIdentityStatus.Unknown, "CustomImplementation", "库存或仓储为派生实现，尚无安全依据。", goodId);
      var buildingDistrict = owner.GetComponent<DistrictBuilding>();
      var district = buildingDistrict && buildingDistrict.District
        ? buildingDistrict.District.GetComponent<EntityComponent>() : null;
      if (!district || !target.DistrictOwner)
        return IdentityResult(InventoryIdentityStatus.Unknown, "DistrictUnknown", "无法确认原生地区归属，请保持只读。", goodId);
      if (!ReferenceEquals(district, target.DistrictOwner) || !ReferenceEquals(_entities.GetEntity(district.EntityId), district)
          || district.Deleted || !district.Initialized || !_districts.FinishedDistrictCenters.Contains(buildingDistrict.District))
        return IdentityResult(InventoryIdentityStatus.Invalid, "DistrictChanged", "地区归属或地区中心注册状态已变化，请重新扫描。", goodId);
      if (string.IsNullOrEmpty(goodId) || !_goods.HasGood(goodId) || _goods.GetGood(goodId) == null)
        return IdentityResult(InventoryIdentityStatus.Invalid, "ResourceUnknown", "所选资源不在当前原生注册表中。", goodId, district.EntityId.ToString());
      return IdentityResult(InventoryIdentityStatus.Valid, "ObservedNativeWarehouse",
        "本次观察的注册实体、原引用、仓储绑定及地区一致；不证明调度安全或未来有效性，不授予写入。",
        goodId, district.EntityId.ToString());
    }
  }
}
