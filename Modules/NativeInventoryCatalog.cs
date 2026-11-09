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
    private const string WriteBlocked = "目录能力仅描述读取；库存编辑需独立命令执行时实时校验。";
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
      _timing.Clear(); _observedTicks = 0;
      _ready = false; Clear(); _scene = null; _scanCount = 0;
      _entities = null; _districts = null; _goods = null; _counts = null; _selection = null;
    }
    public InventoryScanReport Scan() {
      if (!_ready) return new InventoryScanReport { Message = "场景尚未完成初始化或已经卸载，不能扫描。" };
      var report = new InventoryScanReport { Sequence = ++_scanCount };
      var watch = Stopwatch.StartNew();
      Clear();
      try {
        _registeredGoods = _goods.Goods.Distinct().OrderBy(id => id, StringComparer.Ordinal).ToArray();
        foreach (string id in _registeredGoods) {
          if (_goods.GetGood(id) == null) throw new InvalidOperationException("注册资源定义缺失。");
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
            Exclude("实体或库存组件检查异常，未纳入目录");
          }
        }
        report.DistrictCenters = _districtOwners.Count;
        report.ReadableInventories = _targets.Count;
        report.InventoryBuildings = _targets.Values.Select(t => t.Description.OwnerId).Distinct().Count();
        var d = new InventoryTarget {
          Id = InventoryIdentity.Target(_scene, "settlement", "statistics"),
          OwnerId = InventoryIdentity.Owner(_scene, "settlement"), OwnerName = "聚居地",
          BuildingType = "资源统计", DisplayName = "聚居地资源统计", Source = InventorySource.Settlement,
          Purpose = "原生统计汇总，包含仓储、生产缓冲与在途物资，不是独立仓库",
          Abilities = new InventoryAbilities(true, "聚合统计只读，没有对应的单个可写库存。")
        };
        _targets.Add(d.Id, new NativeTarget { Description = d });
        report.Completed = true; report.Message = "扫描完成；目录仅在当前场景有效，实际库存仍按需读取。";
        report.ExcludedObjects = _excludedObjects; report.ExcludedInventories = _excludedInventories;
        report.ExclusionReasons = _excluded.Select(p => p.Key + "：" + p.Value).OrderBy(s => s).ToArray();
      } catch {
        Clear(); report.Message = "扫描失败，已清除目录和场景对象缓存，请主动重试。";
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
        return new InventoryDiscovery { Message = "尚无当前场景扫描目录，请点击“扫描资源与库存”。" };
      var targets = _targets.Values.Where(t => source == InventorySource.Settlement
        ? t.Description.Source == InventorySource.Settlement
        : t.Description.Source != InventorySource.Settlement && (source == InventorySource.Building || _districtOwners.Contains(t.Owner)));
      return new InventoryDiscovery {
        Targets = targets.Select(t => Describe(t.Description, source)).OrderBy(t => t.OwnerName)
          .ThenBy(t => t.DisplayName).ThenBy(t => t.Id, StringComparer.Ordinal).ToArray(),
        Message = "使用已有扫描目录，未重新枚举场景。新增或删除建筑后可主动重新扫描；选中时重新验证对象。"
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
      if (!OwnerValid(owner)) { Exclude("缺少有效原生建筑身份或实体未就绪（含自然物体与未知实体）"); return; }
      if (owner.GetComponent<DistrictCrossingInventory>() || owner.GetComponent<DistrictCrossing>()) {
        Exclude("地区交汇点或运输镜像实体，安全排除"); return;
      }
      var labeled = owner.GetComponent<LabeledEntity>();
      var named = owner.GetComponent<NamedEntity>();
      var center = owner.GetComponent<DistrictCenter>();
      string type = labeled ? labeled.DisplayName : "未命名建筑类型";
      string name = center ? center.DistrictName : named ? named.EntityName : type;
      if (string.IsNullOrEmpty(name)) name = type;
      name += "（" + owner.GetComponent<BlockObject>().Coordinates + "）";
      var all = inventories.AllInventories.ToArray();
      var names = all.Where(i => i && !string.IsNullOrEmpty(i.ComponentName)).GroupBy(i => i.ComponentName)
        .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
      foreach (var inventory in all) {
        if (!inventory) { Exclude("库存组件失效", true); continue; }
        if (string.IsNullOrEmpty(inventory.ComponentName) || names[inventory.ComponentName] != 1) {
          Exclude("库存组件名缺失或重复，无法建立稳定身份", true); continue;
        }
        string id = InventoryIdentity.Target(_scene, owner.EntityId.ToString(), inventory.ComponentName);
        if (_targets.ContainsKey(id)) { Exclude("原生库存身份重复", true); continue; }
        string purpose = Purpose(owner, inventory);
        var d = new InventoryTarget {
          Id = id, OwnerId = InventoryIdentity.Owner(_scene, owner.EntityId.ToString()), OwnerName = name,
          BuildingType = type, DisplayName = purpose + "［" + inventory.ComponentName + "］",
          Source = InventorySource.Building, Purpose = purpose + (inventory.Enabled ? "；库存已启用" : "；库存已停用，仅观察"),
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
      if (construction && construction.Inventory == inventory) return "施工材料库存";
      var stockpile = owner.GetComponent<Stockpile>();
      if (stockpile && stockpile.Inventory == inventory) return "仓储库存";
      var output = owner.GetComponent<SimpleOutputInventory>();
      if (output && output.Inventory == inventory) return owner.GetComponent<DistrictCenter>() ? "地区中心自身输出库存" : "建筑输出库存";
      if (inventory.IsInput && inventory.IsOutput) return "原生输入与输出库存";
      if (inventory.IsInput) return "原生输入库存";
      if (inventory.IsOutput) return "原生输出库存";
      return "用途未确认的原生库存";
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
        if (targetId != null) _targets.Remove(targetId); return new InventorySnapshot { Message = "目标已删除、替换或不再符合建筑身份，已清除该库存选择；请重新扫描。" };
      }
      try {
        var snapshot = target.Description.Source == InventorySource.Settlement ? ReadSettlement(target) : ReadNative(target);
        if (snapshot.Valid) {
          snapshot.Target = Describe(target.Description, _viewSource);
          if (target.Inventory) snapshot.Target.Purpose = Purpose(target.Owner, target.Inventory)
            + (target.Inventory.Enabled ? "；库存已启用" : "；库存已停用，仅观察");
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
          Details = "仓储 " + count.StockpiledStock + "；输出缓冲 " + count.BufferedOutputStock
            + "；运往仓储 " + count.CarriedToStockpilesStock + "；加工中 " + count.StockUnderProcessing
            + "；输入缓冲 " + count.BufferedInput + "；运往生产 " + count.CarriedToProcessors
            + "。可用数量是原生可用统计，不等于未预留库存；统计可能延迟到下一次模拟更新。"
        });
      }
      return new InventorySnapshot { Target = target.Description, Resources = rows.ToArray(), Valid = true,
        Message = "聚居地统计只读，不是可注入物资的全局仓库。预留量与统一容量不适用于此汇总。" };
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
        bool storable = allowed.Contains(id) && registered.Contains(id);
        rows.Add(new InventoryResource {
          GoodId = id, DisplayName = GoodName(id), Registered = registered.Contains(id), Allowed = storable,
          Amount = amount, AvailableStock = inventory.UnreservedAmountInStock(id),
          ReservedStock = amount - inventory.UnreservedAmountInStock(id), ReservedCapacity = inventory.ReservedCapacity(id),
          FreeCapacity = storable ? inventory.UnreservedCapacity(id) : (long?)null,
          ResourceLimit = storable ? inventory.LimitedAmount(id) : (long?)null, TotalCapacity = inventory.Capacity,
          CapacityState = inventory.Capacity == int.MaxValue ? CapacityState.Unknown : CapacityState.Finite,
          Details = !storable ? "未注册或未声明允许的残留资源，仅显示实际库存。"
            : "资源限额和可用空位按原生接口读取；总容量与资源限额共同约束。整数上限不代表已确认无限容量。"
        });
      }
      if (!Valid(target)) { _targets.Remove(target.Description.Id); return new InventorySnapshot { Message = "读取期间库存失效，请重新扫描并选择。" }; }
      return new InventorySnapshot { Target = target.Description, Resources = rows.ToArray(), Valid = true,
        Message = "已按需读取真实库存。声明支持的资源仍可能因当前设置、容量或预留而没有可用空位。库存编辑必须暂停并实时校验。" };
    }
    private string GoodName(string id) => _goods.HasGood(id) ? _goods.GetGood(id).DisplayName.Value : "未注册资源（" + id + "）";
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
