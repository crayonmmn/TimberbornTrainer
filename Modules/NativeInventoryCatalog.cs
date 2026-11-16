using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Timberborn.Buildings;
using Timberborn.BlockSystem;
using Timberborn.ConstructionSites;
using Timberborn.DistributionSystem;
using Timberborn.EntityNaming;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.ResourceCountingSystem;
using Timberborn.SelectionSystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.SingletonSystem;
using Timberborn.Stockpiles;

namespace TimberbornTrainer.Modules {
  public sealed partial class NativeInventoryCatalog : IInventoryCatalog, IPostLoadableSingleton, IUnloadableSingleton {
    private static readonly string WriteBlocked = global::TimberbornTrainer.Localization.TextToken.Key("t0283");
    private EntityRegistry _entities;
    private DistrictCenterRegistry _districts;
    private IGoodService _goods;
    private ResourceCountingService _counts;
    private EntitySelectionService _selection;
    private string _scene;
    private bool _ready;
    private readonly Dictionary<string, NativeTarget> _targets = new Dictionary<string, NativeTarget>();
    private sealed class NativeTarget {
      public InventoryTarget Description;
      public EntityComponent Owner;
      public Inventory Inventory;
      public string Scene, Component;
      public Guid EntityId;
      public bool RequireMapSelection;
      public EntityComponent DistrictOwner;
    }

    public NativeInventoryCatalog(EntityRegistry entities, DistrictCenterRegistry districts,
        IGoodService goods, ResourceCountingService counts, EntitySelectionService selection) {
      _entities = entities; _districts = districts; _goods = goods; _counts = counts; _selection = selection;
    }
    public void PostLoad() {
      if (_entities == null || _ready) return;
      _scene = Guid.NewGuid().ToString("N"); _ready = true;
    }
    public InventoryScanReport LastScan { get; private set; }
    private int _scanCount;
    private InventorySource _viewSource;
    private string[] _registeredGoods = Array.Empty<string>();
    private readonly HashSet<EntityComponent> _districtOwners = new HashSet<EntityComponent>();
    private readonly Dictionary<string, int> _excluded = new Dictionary<string, int>();
    private int _excludedObjects, _excludedInventories;
    public void Clear() {
      _targets.Clear(); _districtOwners.Clear(); _registeredGoods = Array.Empty<string>();
      _excluded.Clear(); LastScan = null;
      _excludedObjects = 0; _excludedInventories = 0;
    }
    public void Unload() {
      ReleaseSelectedWarehouse();
      _timing.Clear(); _observedTicks = 0;
      _ready = false; Clear(); _scene = null; _scanCount = 0;
      _entities = null; _districts = null; _goods = null; _counts = null; _selection = null;
    }
    public InventoryScanReport Scan() {
      if (!_ready) return new InventoryScanReport { Message = global::TimberbornTrainer.Localization.TextToken.Key("t0139") };
      var report = new InventoryScanReport { Sequence = ++_scanCount };
      var watch = Stopwatch.StartNew();
      Clear();
      try {
        _registeredGoods = _goods.Goods.Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
        foreach (string id in _registeredGoods) {
          if (_goods.GetGood(id) == null) throw new InvalidOperationException(global::TimberbornTrainer.Localization.TextToken.Key("t0463"));
        }
        report.RegisteredResources = _registeredGoods.Length;
        foreach (var center in _districts.FinishedDistrictCenters.ToArray()) {
          if (center && OwnerValid(center.GetComponent<EntityComponent>())) _districtOwners.Add(center.GetComponent<EntityComponent>());
        }
        report.DistrictCenters = _districtOwners.Count;
        foreach (var entity in _entities.Entities.ToArray()) {
          ++report.InspectedEntities;
          try {
            if (OwnerValid(entity)) ++report.RecognizedBuildings;
            AddBuilding(entity);
          } catch {
            foreach (string id in _targets.Where(p => ReferenceEquals(p.Value.Owner, entity)).Select(p => p.Key).ToArray()) _targets.Remove(id);
            _districtOwners.Remove(entity);
            Exclude(global::TimberbornTrainer.Localization.TextToken.Key("t0349"));
          }
        }
        report.DistrictCenters = _districtOwners.Count;
        report.ReadableInventories = _targets.Count;
        report.InventoryBuildings = _targets.Values.Select(t => t.Description.OwnerId).Distinct().Count();
        var d = new InventoryTarget {
          Id = InventoryIdentity.Target(_scene, "settlement", "statistics"),
          OwnerId = InventoryIdentity.Owner(_scene, "settlement"), OwnerName = global::TimberbornTrainer.Localization.TextToken.Key("t0236"),
          BuildingType = global::TimberbornTrainer.Localization.TextToken.Key("t0470"), DisplayName = global::TimberbornTrainer.Localization.TextToken.Key("t0239"), Source = InventorySource.Settlement,
          Purpose = global::TimberbornTrainer.Localization.TextToken.Key("t0436"),
          Abilities = new InventoryAbilities(true, global::TimberbornTrainer.Localization.TextToken.Key("t0235"))
        };
        _targets.Add(d.Id, new NativeTarget { Description = d });
        report.Completed = true; report.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0331");
        report.ExcludedObjects = _excludedObjects; report.ExcludedInventories = _excludedInventories;
        report.ExclusionReasons = _excluded.Select(p => p.Key + global::TimberbornTrainer.Localization.TextToken.Key("t0093") + p.Value).OrderBy(s => s).ToArray();
      } catch {
        Clear(); report.Message = global::TimberbornTrainer.Localization.TextToken.Key("t0329");
      } finally { watch.Stop(); report.ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds; }
      LastScan = report;
      return report;
    }
    private void Exclude(string reason, bool inventory = false) {
      int count; _excluded.TryGetValue(reason, out count); _excluded[reason] = count + 1;
      if (inventory) ++_excludedInventories; else ++_excludedObjects;
    }
    public InventoryDiscovery Discover(InventorySource source) {
      if (source != InventorySource.Settlement && source != InventorySource.DistrictCenter && source != InventorySource.Building)
        throw new ArgumentOutOfRangeException(nameof(source));
      _viewSource = source;
      if (!_ready || LastScan == null || !LastScan.Completed)
        return new InventoryDiscovery { Message = global::TimberbornTrainer.Localization.TextToken.Key("t0339") };
      var targets = _targets.Values.Where(t => source == InventorySource.Settlement
        ? t.Description.Source == InventorySource.Settlement
        : t.Description.Source != InventorySource.Settlement && (source == InventorySource.Building || _districtOwners.Contains(t.Owner)));
      return new InventoryDiscovery {
        Targets = targets.Select(t => Describe(t.Description, source)).OrderBy(t => t.OwnerName)
          .ThenBy(t => t.DisplayName).ThenBy(t => t.Id, StringComparer.Ordinal).ToArray(),
        Message = global::TimberbornTrainer.Localization.TextToken.Key("t0350")
      };
    }
    private static InventoryTarget Describe(InventoryTarget d, InventorySource source) => new InventoryTarget {
      Id = d.Id, OwnerId = d.OwnerId, OwnerName = d.OwnerName, BuildingType = d.BuildingType,
      DisplayName = d.DisplayName, Purpose = d.Purpose, Source = source, Abilities = d.Abilities
    };
    private static bool OwnerValid(EntityComponent owner) => owner && owner.Initialized && !owner.Deleted
      && owner.GetComponent<BlockObject>() && owner.GetComponent<Building>() && owner.GetComponent<Building>().Spec != null;
    private void AddBuilding(EntityComponent owner) {
      if (!owner) return;
      var inventories = owner.GetComponent<Inventories>();
      if (!inventories) return;
      if (!OwnerValid(owner)) { Exclude(global::TimberbornTrainer.Localization.TextToken.Key("t0328")); return; }
      if (owner.GetComponent<DistrictCrossingInventory>() || owner.GetComponent<DistrictCrossing>()) {
        Exclude(global::TimberbornTrainer.Localization.TextToken.Key("t0168")); return;
      }
      var labeled = owner.GetComponent<LabeledEntity>();
      var named = owner.GetComponent<NamedEntity>();
      var center = owner.GetComponent<DistrictCenter>();
      string type = labeled ? labeled.DisplayName : global::TimberbornTrainer.Localization.TextToken.Key("t0368");
      string name = center ? center.DistrictName : named ? named.EntityName : type;
      if (string.IsNullOrEmpty(name)) name = type;
      var membership = owner.GetComponent<DistrictBuilding>();
      if (!center && membership && membership.District) name = membership.District.DistrictName + " · " + name;
      name += global::TimberbornTrainer.Localization.TextToken.Key("t0100") + owner.GetComponent<BlockObject>().Coordinates + global::TimberbornTrainer.Localization.TextToken.Key("t0103");
      var all = inventories.AllInventories.ToArray();
      var names = all.Where(i => i && !string.IsNullOrEmpty(i.ComponentName)).GroupBy(i => i.ComponentName)
        .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
      foreach (var inventory in all) {
        if (!inventory) { Exclude(global::TimberbornTrainer.Localization.TextToken.Key("t0258"), true); continue; }
        if (string.IsNullOrEmpty(inventory.ComponentName) || names[inventory.ComponentName] != 1) {
          Exclude(global::TimberbornTrainer.Localization.TextToken.Key("t0257"), true); continue;
        }
        string id = InventoryIdentity.Target(_scene, owner.EntityId.ToString(), inventory.ComponentName);
        if (_targets.ContainsKey(id)) { Exclude(global::TimberbornTrainer.Localization.TextToken.Key("t0429"), true); continue; }
        string purpose = Purpose(owner, inventory);
        var d = new InventoryTarget {
          Id = id, OwnerId = InventoryIdentity.Owner(_scene, owner.EntityId.ToString()), OwnerName = name,
          BuildingType = type, DisplayName = purpose + global::TimberbornTrainer.Localization.TextToken.Key("t0106") + inventory.ComponentName + global::TimberbornTrainer.Localization.TextToken.Key("t0107"),
          Source = InventorySource.Building, Purpose = purpose + (inventory.Enabled ? global::TimberbornTrainer.Localization.TextToken.Key("t0057") : global::TimberbornTrainer.Localization.TextToken.Key("t0058")),
          Abilities = new InventoryAbilities(true, WriteBlocked)
        };
        var districtBuilding = owner.GetComponent<DistrictBuilding>();
        _targets.Add(id, new NativeTarget { Owner = owner, Inventory = inventory, Description = d,
          Scene = _scene, EntityId = owner.EntityId, Component = inventory.ComponentName,
          DistrictOwner = districtBuilding && districtBuilding.District
            ? districtBuilding.District.GetComponent<EntityComponent>() : null });
      }
    }
    private static string Purpose(EntityComponent owner, Inventory inventory) {
      var construction = owner.GetComponent<ConstructionSite>();
      if (construction && construction.Inventory == inventory) return owner.GetComponent<BlockObject>().IsFinished
        ? global::TimberbornTrainer.Localization.TextToken.Key("t0346") : global::TimberbornTrainer.Localization.TextToken.Key("t0345");
      var stockpile = owner.GetComponent<Stockpile>();
      if (stockpile && stockpile.Inventory == inventory) return global::TimberbornTrainer.Localization.TextToken.Key("t0126");
      var output = owner.GetComponent<SimpleOutputInventory>();
      if (output && output.Inventory == inventory) return owner.GetComponent<DistrictCenter>() ? global::TimberbornTrainer.Localization.TextToken.Key("t0170") : global::TimberbornTrainer.Localization.TextToken.Key("t0214");
      if (inventory.IsInput && inventory.IsOutput) return global::TimberbornTrainer.Localization.TextToken.Key("t0434");
      if (inventory.IsInput) return global::TimberbornTrainer.Localization.TextToken.Key("t0433");
      if (inventory.IsOutput) return global::TimberbornTrainer.Localization.TextToken.Key("t0432");
      return global::TimberbornTrainer.Localization.TextToken.Key("t0414");
    }
    private bool Valid(NativeTarget target) {
      if (!_ready) return false;
      if (target.Description.Source == InventorySource.Settlement) return true;
      if (!OwnerValid(target.Owner) || !target.Inventory
          || target.Owner.GetComponent<DistrictCrossingInventory>() || target.Owner.GetComponent<DistrictCrossing>()) return false;
      if (_viewSource == InventorySource.DistrictCenter) {
        var center = target.Owner.GetComponent<DistrictCenter>();
        if (!center || !_districts.FinishedDistrictCenters.Contains(center)) return false;
      }
      var inventories = target.Owner.GetComponent<Inventories>();
      return inventories && inventories.AllInventories.Contains(target.Inventory)
        && InventoryIdentity.Target(_scene, target.Owner.EntityId.ToString(), target.Inventory.ComponentName) == target.Description.Id;
    }
    public InventorySnapshot Read(string targetId) {
      NativeTarget target;
      if (!_ready || targetId == null || !_targets.TryGetValue(targetId, out target) || !Valid(target)) {
        if (targetId != null) _targets.Remove(targetId); return new InventorySnapshot { Message = global::TimberbornTrainer.Localization.TextToken.Key("t0280") };
      }
      try {
        var snapshot = target.Description.Source == InventorySource.Settlement ? ReadSettlement(target) : ReadNative(target);
        if (snapshot.Valid) {
          snapshot.Target = Describe(target.Description, _viewSource);
          if (target.Inventory) snapshot.Target.Purpose = Purpose(target.Owner, target.Inventory)
            + (target.Inventory.Enabled ? global::TimberbornTrainer.Localization.TextToken.Key("t0057") : global::TimberbornTrainer.Localization.TextToken.Key("t0058"));
        }
        return snapshot;
      } catch { _targets.Remove(targetId); throw; }
    }
    private InventorySnapshot ReadSettlement(NativeTarget target) {
      var rows = new List<InventoryResource>();
      foreach (string id in _registeredGoods.Where(id => _goods.HasGood(id))) {
        var count = _counts.GetGlobalResourceCount(id);
        rows.Add(new InventoryResource {
          GoodId = id, DisplayName = GoodName(id), Registered = true, Allowed = false,
          Amount = count.AllStock, AvailableStock = count.AvailableStock, CapacityState = CapacityState.Aggregate,
          Details = global::TimberbornTrainer.Localization.TextToken.Key("t0125") + count.StockpiledStock + global::TimberbornTrainer.Localization.TextToken.Key("t0069") + count.BufferedOutputStock
            + global::TimberbornTrainer.Localization.TextToken.Key("t0087") + count.CarriedToStockpilesStock + global::TimberbornTrainer.Localization.TextToken.Key("t0047") + count.StockUnderProcessing
            + global::TimberbornTrainer.Localization.TextToken.Key("t0070") + count.BufferedInput + global::TimberbornTrainer.Localization.TextToken.Key("t0088") + count.CarriedToProcessors
            + global::TimberbornTrainer.Localization.TextToken.Key("t0097")
        });
      }
      return new InventorySnapshot { Target = target.Description, Resources = rows.ToArray(), Valid = true,
        Message = global::TimberbornTrainer.Localization.TextToken.Key("t0237") };
    }
    private InventorySnapshot ReadNative(NativeTarget target) {
      var inventory = target.Inventory;
      var allowed = new HashSet<string>(inventory.AllowedGoods.Select(g => g.StorableGood.GoodId), StringComparer.Ordinal);
      var registered = new HashSet<string>(_registeredGoods.Where(id => _goods.HasGood(id)), StringComparer.Ordinal);
      // Include unsupported leftovers for observation; never pretend they are allowed goods.
      var ids = allowed.Concat(inventory.Stock.Select(g => g.GoodId)).Distinct().OrderBy(g => g, StringComparer.Ordinal).ToArray();
      var rows = new List<InventoryResource>();
      foreach (string id in ids) {
        long amount = inventory.AmountInStock(id);
        bool registeredNow = _goods.HasGood(id) && _goods.GetGood(id) != null;
        bool inDirectory = registered.Contains(id);
        bool storable = allowed.Contains(id) && registeredNow && inDirectory;
        rows.Add(new InventoryResource {
          GoodId = id, DisplayName = GoodName(id), Registered = registeredNow, Allowed = storable,
          Amount = amount, AvailableStock = inventory.UnreservedAmountInStock(id),
          ReservedStock = amount - inventory.UnreservedAmountInStock(id), ReservedCapacity = inventory.ReservedCapacity(id),
          FreeCapacity = storable ? inventory.UnreservedCapacity(id) : (long?)null,
          ResourceLimit = storable ? inventory.LimitedAmount(id) : (long?)null, TotalCapacity = inventory.Capacity,
          CapacityState = inventory.Capacity == int.MaxValue ? CapacityState.Unknown : CapacityState.Finite,
          Details = !registeredNow ? global::TimberbornTrainer.Localization.TextToken.Key("t0466")
            : !inDirectory ? global::TimberbornTrainer.Localization.TextToken.Key("t0475")
            : !allowed.Contains(id) ? global::TimberbornTrainer.Localization.TextToken.Key("t0474")
            : global::TimberbornTrainer.Localization.TextToken.Key("t0472")
        });
      }
      if (!Valid(target)) { _targets.Remove(target.Description.Id); return new InventorySnapshot { Message = global::TimberbornTrainer.Localization.TextToken.Key("t0185") }; }
      return new InventorySnapshot { Target = target.Description, Resources = rows.ToArray(), Valid = true,
        Message = rows.Count == 0 ? global::TimberbornTrainer.Localization.TextToken.Key("t0407")
          : global::TimberbornTrainer.Localization.TextToken.Key("t0405") };
    }
    private string GoodName(string id) {
      try {
        if (!_goods.HasGood(id) || _goods.GetGood(id) == null) return global::TimberbornTrainer.Localization.TextToken.Key("t0381") + id + global::TimberbornTrainer.Localization.TextToken.Key("t0103");
        string name = _goods.GetGood(id).DisplayName.Value;
        return string.IsNullOrWhiteSpace(name) ? global::TimberbornTrainer.Localization.TextToken.Key("t0394") + id + global::TimberbornTrainer.Localization.TextToken.Key("t0103") : name;
      } catch { return global::TimberbornTrainer.Localization.TextToken.Key("t0393") + id + global::TimberbornTrainer.Localization.TextToken.Key("t0103"); }
    }
    public bool Focus(string targetId) {
      NativeTarget target;
      if (!_ready || targetId == null || !_targets.TryGetValue(targetId, out target)) return false;
      if (!Valid(target)) { _targets.Remove(targetId); return false; }
      if (target.Description.Source == InventorySource.Settlement || !target.Owner.GetComponent<SelectableObject>()) return false;
      _selection.SelectAndFocusOn(target.Owner);
      return true;
    }
  }
}
