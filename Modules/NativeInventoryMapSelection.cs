using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Timberborn.BlockSystem;
using Timberborn.EntityNaming;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.InventorySystem;
using Timberborn.Stockpiles;

namespace TimberbornTrainer.Modules {
  public sealed partial class NativeInventoryCatalog : IMapInventorySource {
    private static readonly FieldInfo ConfiguredInventory = typeof(SingleGoodAllower).GetField("_inventory", BindingFlags.Instance | BindingFlags.NonPublic);
    private NativeTarget _mapTarget;
    private bool MapTargetCurrent(NativeTarget target, string resource) {
      if (!_ready || !_selection.SelectedObject || !ReferenceEquals(_selection.SelectedObject.GetComponent<EntityComponent>(), target.Owner)) return false;
      var allowers = new List<SingleGoodAllower>(); target.Owner.GetComponents(allowers); allowers.RemoveAll(a => !a || !a.Enabled);
      if (allowers.Count != 1) return false;
      var allower = allowers[0];
      return allower && allower.HasAllowedGood && allower.AllowedGood == resource && ConfiguredInventory != null &&
        ReferenceEquals(ConfiguredInventory.GetValue(allower), target.Inventory);
    }
    public void ReleaseSelectedWarehouse() {
      if (_mapTarget != null && _targets.TryGetValue(_mapTarget.Description.Id, out var current) && ReferenceEquals(current, _mapTarget))
        _targets.Remove(_mapTarget.Description.Id);
      _mapTarget = null;
    }
    public InventorySnapshot ReadSelectedWarehouse() {
      try {
        if (!_ready || !_selection.SelectedObject) return MapRejected("请在地图中选择一个仓库。");
        var owner = _selection.SelectedObject.GetComponent<EntityComponent>();
        if (!OwnerValid(owner) || !ReferenceEquals(_entities.GetEntity(owner.EntityId), owner)) return MapRejected("当前选择已失效或不是原生建筑，请在地图中选择一个仓库。");
        var stockpiles = new List<Stockpile>(); owner.GetComponents(stockpiles); stockpiles.RemoveAll(s => !s || !s.Enabled);
        if (stockpiles.Count != 1 || !owner.GetComponent<BlockObject>().IsFinished)
          return MapRejected("请选择已完工的普通仓库；道路、住宅施工库存和生产缓冲不是编辑目标。");
        var inventory = stockpiles[0].Inventory;
        var allowers = new List<SingleGoodAllower>(); owner.GetComponents(allowers); allowers.RemoveAll(a => !a || !a.Enabled);
        if (allowers.Count != 1 || ConfiguredInventory == null || !inventory || !inventory.Enabled ||
            !ReferenceEquals(ConfiguredInventory.GetValue(allowers[0]), inventory))
          return MapRejected("不能确认唯一的原生单物品存储配置，未开放编辑。");
        string good = allowers[0].AllowedGood;
        if (string.IsNullOrEmpty(good)) return MapRejected("仓库尚未配置物品，请先通过游戏原生仓库界面设置。");
        if (!_goods.HasGood(good) || _goods.GetGood(good) == null) return MapRejected("仓库配置资源ID：" + good + "，但当前原生注册表无法识别；未开放编辑。");
        if (!inventory.AllowedGoods.Any(a => a.StorableGood.GoodId == good))
          return MapRejected("资源ID：" + good + "，与当前库存允许规则不一致；请先检查原生配置。");
        string id = InventoryIdentity.Target(_scene, owner.EntityId.ToString(), inventory.ComponentName);
        if (_mapTarget == null || !ReferenceEquals(_mapTarget.Owner, owner) || !ReferenceEquals(_mapTarget.Inventory, inventory) || _mapTarget.Component != inventory.ComponentName) {
          ReleaseSelectedWarehouse();
          var label = owner.GetComponent<LabeledEntity>(); var named = owner.GetComponent<NamedEntity>(); var district = owner.GetComponent<DistrictBuilding>();
          var d = new InventoryTarget { Id = id, OwnerId = InventoryIdentity.Owner(_scene, owner.EntityId.ToString()),
            OwnerName = (district && district.District ? district.District.DistrictName + " · " : "") +
              (named ? named.EntityName : label ? label.DisplayName : "仓库") + "（" + owner.GetComponent<BlockObject>().Coordinates + "）",
            BuildingType = label ? label.DisplayName : "原生仓储", DisplayName = inventory.ComponentName,
            Purpose = "地图选中仓库的实际储存库存", Source = InventorySource.Building, Abilities = new InventoryAbilities(true, WriteBlocked) };
          _mapTarget = new NativeTarget { Description = d, Owner = owner, Inventory = inventory, Component = inventory.ComponentName,
            Scene = _scene, EntityId = owner.EntityId, DistrictOwner = district && district.District ? district.District.GetComponent<EntityComponent>() : null,
            RequireMapSelection = true };
          _targets[id] = _mapTarget;
        }
        _targets[id] = _mapTarget;
        if (!MapTargetCurrent(_mapTarget, good)) return MapRejected("仓库选择或配置已变化，请重新选择。");
        return new InventorySnapshot { Valid = true, Target = _mapTarget.Description, Message = "已从原生存储配置识别物品；空仓库也可操作。修改需暂停并启用全局。",
          Resources = new[] { new InventoryResource { GoodId = good, DisplayName = GoodName(good), Registered = true, Allowed = true,
            Amount = inventory.AmountInStock(good), AvailableStock = inventory.UnreservedAmountInStock(good),
            ReservedStock = (long)inventory.AmountInStock(good) - inventory.UnreservedAmountInStock(good),
            FreeCapacity = inventory.UnreservedCapacity(good), TotalCapacity = inventory.Capacity, ResourceLimit = inventory.LimitedAmount(good),
            CapacityState = inventory.Capacity == int.MaxValue ? CapacityState.Unknown : CapacityState.Finite } } };
      } catch { return MapRejected("读取地图选择或仓库配置失败，未保留可写目标；请重新选择。"); }
    }
    private InventorySnapshot MapRejected(string message) { ReleaseSelectedWarehouse(); return new InventorySnapshot { Message = message }; }
  }
}
