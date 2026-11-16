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
        if (!_ready || !_selection.SelectedObject) return MapRejected(global::TimberbornTrainer.Localization.TextToken.Key("t0317"));
        var owner = _selection.SelectedObject.GetComponent<EntityComponent>();
        if (!OwnerValid(owner) || !ReferenceEquals(_entities.GetEntity(owner.EntityId), owner)) return MapRejected(global::TimberbornTrainer.Localization.TextToken.Key("t0163"));
        var stockpiles = new List<Stockpile>(); owner.GetComponents(stockpiles); stockpiles.RemoveAll(s => !s || !s.Enabled);
        if (stockpiles.Count != 1 || !owner.GetComponent<BlockObject>().IsFinished)
          return MapRejected(global::TimberbornTrainer.Localization.TextToken.Key("t0316"));
        var inventory = stockpiles[0].Inventory;
        var allowers = new List<SingleGoodAllower>(); owner.GetComponents(allowers); allowers.RemoveAll(a => !a || !a.Enabled);
        if (allowers.Count != 1 || ConfiguredInventory == null || !inventory || !inventory.Enabled ||
            !ReferenceEquals(ConfiguredInventory.GetValue(allowers[0]), inventory))
          return MapRejected(global::TimberbornTrainer.Localization.TextToken.Key("t0119"));
        string good = allowers[0].AllowedGood;
        if (string.IsNullOrEmpty(good)) return MapRejected(global::TimberbornTrainer.Localization.TextToken.Key("t0129"));
        if (!_goods.HasGood(good) || _goods.GetGood(good) == null) return MapRejected(global::TimberbornTrainer.Localization.TextToken.Key("t0128") + good + global::TimberbornTrainer.Localization.TextToken.Key("t0030"));
        if (!inventory.AllowedGoods.Any(a => a.StorableGood.GoodId == good))
          return MapRejected(global::TimberbornTrainer.Localization.TextToken.Key("t0477") + good + global::TimberbornTrainer.Localization.TextToken.Key("t0037"));
        string id = InventoryIdentity.Target(_scene, owner.EntityId.ToString(), inventory.ComponentName);
        if (_mapTarget == null || !ReferenceEquals(_mapTarget.Owner, owner) || !ReferenceEquals(_mapTarget.Inventory, inventory) || _mapTarget.Component != inventory.ComponentName) {
          ReleaseSelectedWarehouse();
          var label = owner.GetComponent<LabeledEntity>(); var named = owner.GetComponent<NamedEntity>(); var district = owner.GetComponent<DistrictBuilding>();
          var d = new InventoryTarget { Id = id, OwnerId = InventoryIdentity.Owner(_scene, owner.EntityId.ToString()),
            OwnerName = (district && district.District ? district.District.DistrictName + " · " : "") +
              (named ? named.EntityName : label ? label.DisplayName : global::TimberbornTrainer.Localization.TextToken.Key("t0127")) + global::TimberbornTrainer.Localization.TextToken.Key("t0100") + owner.GetComponent<BlockObject>().Coordinates + global::TimberbornTrainer.Localization.TextToken.Key("t0103"),
            BuildingType = label ? label.DisplayName : global::TimberbornTrainer.Localization.TextToken.Key("t0423"), DisplayName = inventory.ComponentName,
            Purpose = global::TimberbornTrainer.Localization.TextToken.Key("t0173"), Source = InventorySource.Building, Abilities = new InventoryAbilities(true, WriteBlocked) };
          _mapTarget = new NativeTarget { Description = d, Owner = owner, Inventory = inventory, Component = inventory.ComponentName,
            Scene = _scene, EntityId = owner.EntityId, DistrictOwner = district && district.District ? district.District.GetComponent<EntityComponent>() : null,
            RequireMapSelection = true };
          _targets[id] = _mapTarget;
        }
        _targets[id] = _mapTarget;
        if (!MapTargetCurrent(_mapTarget, good)) return MapRejected(global::TimberbornTrainer.Localization.TextToken.Key("t0130"));
        return new InventorySnapshot { Valid = true, Target = _mapTarget.Description, Message = global::TimberbornTrainer.Localization.TextToken.Key("t0406"),
          Resources = new[] { new InventoryResource { GoodId = good, DisplayName = GoodName(good), Registered = true, Allowed = true,
            Amount = inventory.AmountInStock(good), AvailableStock = inventory.UnreservedAmountInStock(good),
            ReservedStock = (long)inventory.AmountInStock(good) - inventory.UnreservedAmountInStock(good),
            FreeCapacity = inventory.UnreservedCapacity(good), TotalCapacity = inventory.Capacity, ResourceLimit = inventory.LimitedAmount(good),
            CapacityState = inventory.Capacity == int.MaxValue ? CapacityState.Unknown : CapacityState.Finite } } };
      } catch { return MapRejected(global::TimberbornTrainer.Localization.TextToken.Key("t0180")); }
    }
    private InventorySnapshot MapRejected(string message) { ReleaseSelectedWarehouse(); return new InventorySnapshot { Message = message }; }
  }
}
