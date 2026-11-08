using System;
using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.ConstructionSites;
using Timberborn.DistributionSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.InventorySystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.Stockpiles;

namespace TimberbornTrainer.Modules {
  public sealed partial class NativeInventoryCatalog {
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
