using System;
using System.Collections.Generic;
using System.Linq;
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
  public sealed class NativeInventoryCatalog : IInventoryCatalog, IPostLoadableSingleton, IUnloadableSingleton {
    private const string WriteBlocked = "本阶段仅开放读取，新增适配器写入尚未审核与授权。";
    private EntityRegistry _entities;
    private DistrictCenterRegistry _districts;
    private IGoodService _goods;
    private ResourceCountingService _counts;
    private EntitySelectionService _selection;
    private string _scene;
    private bool _ready;
    private readonly Dictionary<string, NativeTarget> _targets = new Dictionary<string, NativeTarget>();
    private readonly List<string> _warnings = new List<string>();
    private sealed class NativeTarget {
      public InventoryTarget Description;
      public EntityComponent Owner;
      public Inventory Inventory;
    }

    public NativeInventoryCatalog(EntityRegistry entities, DistrictCenterRegistry districts,
        IGoodService goods, ResourceCountingService counts, EntitySelectionService selection) {
      _entities = entities; _districts = districts; _goods = goods; _counts = counts; _selection = selection;
    }
    public void PostLoad() {
      if (_entities == null || _ready) return;
      _scene = Guid.NewGuid().ToString("N"); _ready = true;
    }
    public void Clear() { _targets.Clear(); _warnings.Clear(); }
    public void Unload() {
      _ready = false; Clear(); _scene = null;
      _entities = null; _districts = null; _goods = null; _counts = null; _selection = null;
    }
    public InventoryDiscovery Discover(InventorySource source) {
      Clear();
      if (!_ready) return new InventoryDiscovery { Message = "场景尚未完成初始化，或已经卸载；请稍后主动刷新。" };
      if (source == InventorySource.Settlement) {
        var d = new InventoryTarget {
          Id = InventoryIdentity.Target(_scene, "settlement", "statistics"),
          OwnerId = InventoryIdentity.Owner(_scene, "settlement"), OwnerName = "聚居地",
          BuildingType = "资源统计", DisplayName = "聚居地资源统计", Source = source,
          Purpose = "原生统计汇总，包含仓储、生产缓冲与在途物资，不是独立仓库",
          Abilities = new InventoryAbilities(true, "聚合统计只读，没有对应的单个可写库存。")
        };
        _targets.Add(d.Id, new NativeTarget { Description = d });
      } else if (source == InventorySource.DistrictCenter) {
        foreach (var center in _districts.FinishedDistrictCenters.ToArray()) {
          if (center) AddBuilding(center.GetComponent<EntityComponent>(), source, center.DistrictName);
        }
      } else if (source == InventorySource.Building) {
        // The only settlement-wide inventory discovery path; called by explicit list requests.
        foreach (var entity in _entities.Entities.ToArray()) {
          AddBuilding(entity, source, null);
        }
      } else throw new ArgumentOutOfRangeException(nameof(source));
      return new InventoryDiscovery {
        Targets = _targets.Values.Select(t => t.Description).OrderBy(t => t.OwnerName)
          .ThenBy(t => t.DisplayName).ThenBy(t => t.Id, StringComparer.Ordinal).ToArray(),
        Message = source == InventorySource.Settlement ? "原生统计可能到下一次模拟更新才同步。"
          : "已按需发现库存。全部只读；地区交汇点已排除。单击选择，双击定位建筑。"
            + (_warnings.Count == 0 ? "" : "\n" + string.Join("\n", _warnings.Distinct()))
      };
    }
    private static bool OwnerValid(EntityComponent owner) => owner && owner.Initialized && !owner.Deleted
      && owner.GetComponent<BlockObject>();
    private void AddBuilding(EntityComponent owner, InventorySource source, string districtName) {
      if (!OwnerValid(owner) || owner.GetComponent<DistrictCrossingInventory>() || owner.GetComponent<DistrictCrossing>()) return;
      var inventories = owner.GetComponent<Inventories>();
      if (!inventories) return;
      var labeled = owner.GetComponent<LabeledEntity>();
      var named = owner.GetComponent<NamedEntity>();
      string type = labeled ? labeled.DisplayName : "未命名建筑类型";
      string name = !string.IsNullOrEmpty(districtName) ? districtName : named ? named.EntityName : type;
      if (string.IsNullOrEmpty(name)) name = type;
      name += "（" + owner.GetComponent<BlockObject>().Coordinates + "）";
      var all = inventories.AllInventories.ToArray();
      foreach (var inventory in all) {
        // Duplicate/missing component names cannot provide an unambiguous native identity.
        if (!inventory) continue;
        if (string.IsNullOrEmpty(inventory.ComponentName)
            || all.Count(i => i && i.ComponentName == inventory.ComponentName) != 1) {
          _warnings.Add(name + "：跳过组件名缺失或重复的库存，无法建立可靠身份。"); continue;
        }
        string id = InventoryIdentity.Target(_scene, owner.EntityId.ToString(), inventory.ComponentName);
        if (_targets.ContainsKey(id)) continue;
        string purpose = Purpose(owner, inventory);
        var d = new InventoryTarget {
          Id = id, OwnerId = InventoryIdentity.Owner(_scene, owner.EntityId.ToString()), OwnerName = name,
          BuildingType = type, DisplayName = purpose + "［" + inventory.ComponentName + "］",
          Source = source, Purpose = purpose + (inventory.Enabled ? "；库存已启用" : "；库存已停用，仅观察"),
          Abilities = new InventoryAbilities(true, WriteBlocked)
        };
        _targets.Add(id, new NativeTarget { Owner = owner, Inventory = inventory, Description = d });
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
      if (target.Description.Source == InventorySource.DistrictCenter) {
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
        Clear(); return new InventorySnapshot { Message = "库存对象已失效。" };
      }
      return target.Description.Source == InventorySource.Settlement ? ReadSettlement(target) : ReadNative(target);
    }
    private InventorySnapshot ReadSettlement(NativeTarget target) {
      var rows = new List<InventoryResource>();
      foreach (string id in _goods.Goods.ToArray().Distinct().OrderBy(g => g, StringComparer.Ordinal)) {
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
      var registered = new HashSet<string>(_goods.Goods, StringComparer.Ordinal);
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
      if (!Valid(target)) { Clear(); return new InventorySnapshot { Message = "读取期间库存失效，请重新选择。" }; }
      return new InventorySnapshot { Target = target.Description, Resources = rows.ToArray(), Valid = true,
        Message = "已按需读取真实库存。声明支持的资源仍可能因当前设置、容量或预留而没有可用空位。所有写入未开放。" };
    }
    private string GoodName(string id) => _goods.HasGood(id) ? _goods.GetGood(id).DisplayName.Value : "未注册资源（" + id + "）";
    public bool Focus(string targetId) {
      NativeTarget target;
      if (!_ready || targetId == null || !_targets.TryGetValue(targetId, out target)) return false;
      if (!Valid(target)) { Clear(); return false; }
      if (target.Description.Source == InventorySource.Settlement || !target.Owner.GetComponent<SelectableObject>()) return false;
      _selection.SelectAndFocusOn(target.Owner);
      return true;
    }
  }
}
