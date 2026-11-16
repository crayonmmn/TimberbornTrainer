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
          return new InventorySafetyReport(identity, null, false, null, null, global::TimberbornTrainer.Localization.TextToken.Key("t0267"));
        var inventory = target.Inventory;
        var stock = inventory.Stock.ToArray();
        var capacityReservations = inventory.ReservedCapacity().ToArray();
        var allowed = new HashSet<string>(inventory.AllowedGoods.Select(g => g.StorableGood.GoodId), StringComparer.Ordinal);
        var registered = _goods.Goods.ToArray();
        var ids = stock.Select(g => g.GoodId).Concat(capacityReservations.Select(g => g.GoodId))
          .Concat(allowed).Concat(registered).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (ids.Length > 4096 || ids.Any(string.IsNullOrEmpty))
          return new InventorySafetyReport(identity, null, false, null, null, global::TimberbornTrainer.Localization.TextToken.Key("t0476"));
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
          global::TimberbornTrainer.Localization.TextToken.Key("t0229")
          + (!consistent ? global::TimberbornTrainer.Localization.TextToken.Key("t0020") : ""));
      } catch (Exception) {
        return new InventorySafetyReport(identity, null, false, null, null, global::TimberbornTrainer.Localization.TextToken.Key("t0440"));
      }
    }
    public InventoryIdentityReport DiagnoseIdentity(string targetId, string goodId) {
      try { return CheckNativeIdentity(targetId, goodId); }
      catch (Exception) {
        return IdentityResult(InventoryIdentityStatus.Unknown, "InspectionException", global::TimberbornTrainer.Localization.TextToken.Key("t0431"), goodId);
      }
    }
    private InventoryIdentityReport IdentityResult(InventoryIdentityStatus status, string code,
        string reason, string good, string district = null) =>
      new InventoryIdentityReport(status, code, reason, _scene, district, good);

    private InventoryIdentityReport CheckNativeIdentity(string targetId, string goodId) {
      NativeTarget target;
      if (!_ready || _entities == null || _scene == null)
        return IdentityResult(InventoryIdentityStatus.Invalid, "SceneUnavailable", global::TimberbornTrainer.Localization.TextToken.Key("t0154"), goodId);
      if (targetId == null || !_targets.TryGetValue(targetId, out target) || target.Scene != _scene)
        return IdentityResult(InventoryIdentityStatus.Invalid, "TargetExpired", global::TimberbornTrainer.Localization.TextToken.Key("t0272"), goodId);
      var owner = _entities.GetEntity(target.EntityId);
      if (!owner || !ReferenceEquals(owner, target.Owner) || !OwnerValid(owner))
        return IdentityResult(InventoryIdentityStatus.Invalid, "EntityChanged", global::TimberbornTrainer.Localization.TextToken.Key("t0441"), goodId);
      var inventory = target.Inventory;
      var inventories = owner.GetComponent<Inventories>();
      if (!inventory || !inventories || !ReferenceEquals(inventory.GetComponent<EntityComponent>(), owner)
          || inventory.ComponentName != target.Component
          || inventories.AllInventories.Count(i => ReferenceEquals(i, inventory)) != 1
          || inventories.AllInventories.Count(i => i && i.ComponentName == target.Component) != 1)
        return IdentityResult(InventoryIdentityStatus.Invalid, "InventoryChanged", global::TimberbornTrainer.Localization.TextToken.Key("t0260"), goodId);
      if (owner.GetComponent<DistrictCenter>() || owner.GetComponent<DistrictCrossing>() || owner.GetComponent<DistrictCrossingInventory>())
        return IdentityResult(InventoryIdentityStatus.Invalid, "ExcludedOwner", global::TimberbornTrainer.Localization.TextToken.Key("t0169"), goodId);
      if (!owner.GetComponent<BlockObject>().IsFinished || !inventory.Enabled)
        return IdentityResult(InventoryIdentityStatus.Invalid, "NotFinished", global::TimberbornTrainer.Localization.TextToken.Key("t0215"), goodId);
      var stockpile = owner.GetComponent<Stockpile>();
      var construction = owner.GetComponent<ConstructionSite>();
      var output = owner.GetComponent<SimpleOutputInventory>();
      if (!stockpile || !stockpile.Enabled || !ReferenceEquals(stockpile.Inventory, inventory)
          || (construction && ReferenceEquals(construction.Inventory, inventory))
          || (output && ReferenceEquals(output.Inventory, inventory)))
        return IdentityResult(InventoryIdentityStatus.Invalid, "NotWarehouse", global::TimberbornTrainer.Localization.TextToken.Key("t0271"), goodId);
      if (inventory.GetType() != typeof(Inventory) || stockpile.GetType() != typeof(Stockpile))
        return IdentityResult(InventoryIdentityStatus.Unknown, "CustomImplementation", global::TimberbornTrainer.Localization.TextToken.Key("t0250"), goodId);
      var buildingDistrict = owner.GetComponent<DistrictBuilding>();
      var district = buildingDistrict && buildingDistrict.District
        ? buildingDistrict.District.GetComponent<EntityComponent>() : null;
      if (!district || !target.DistrictOwner)
        return IdentityResult(InventoryIdentityStatus.Unknown, "DistrictUnknown", global::TimberbornTrainer.Localization.TextToken.Key("t0387"), goodId);
      if (!ReferenceEquals(district, target.DistrictOwner) || !ReferenceEquals(_entities.GetEntity(district.EntityId), district)
          || district.Deleted || !district.Initialized || !_districts.FinishedDistrictCenters.Contains(buildingDistrict.District))
        return IdentityResult(InventoryIdentityStatus.Invalid, "DistrictChanged", global::TimberbornTrainer.Localization.TextToken.Key("t0166"), goodId);
      if (string.IsNullOrEmpty(goodId) || !_goods.HasGood(goodId) || _goods.GetGood(goodId) == null)
        return IdentityResult(InventoryIdentityStatus.Invalid, "ResourceUnknown", global::TimberbornTrainer.Localization.TextToken.Key("t0358"), goodId, district.EntityId.ToString());
      return IdentityResult(InventoryIdentityStatus.Valid, "ObservedNativeWarehouse",
        global::TimberbornTrainer.Localization.TextToken.Key("t0114"),
        goodId, district.EntityId.ToString());
    }
  }
}
