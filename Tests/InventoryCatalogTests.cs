// Test doubles compile the actual NativeInventoryCatalog; never load game assemblies into the test host.
using System;
using System.Collections.Generic;
using System.Linq;
using TimberbornTrainer.Modules;
using Timberborn.EntitySystem;
using Timberborn.InventorySystem;

namespace InventoryTestDoubles {
  public class Component {
    public EntityComponent Owner;
    public bool Destroyed, Enabled = true;
    public static implicit operator bool(Component component) => component != null && !component.Destroyed;
    public T GetComponent<T>() where T : class => (Owner ?? this as EntityComponent)?.Components.OfType<T>().FirstOrDefault();
  }
}
namespace Timberborn.SingletonSystem {
  public interface IPostLoadableSingleton { void PostLoad(); }
  public interface IUnloadableSingleton { void Unload(); }
}
namespace Timberborn.EntitySystem {
  public class EntityComponent : InventoryTestDoubles.Component {
    public Guid EntityId = Guid.NewGuid(); public bool Initialized = true, Deleted;
    public readonly List<InventoryTestDoubles.Component> Components = new List<InventoryTestDoubles.Component>();
    public EntityComponent() { Owner = this; Components.Add(this); }
    public T Add<T>(T component) where T : InventoryTestDoubles.Component { component.Owner = this; Components.Add(component); return component; }
  }
  public class EntityRegistry {
    public readonly List<EntityComponent> Values = new List<EntityComponent>(); public int Enumerations;
    public IEnumerable<EntityComponent> Entities { get { ++Enumerations; return Values; } }
    public EntityComponent GetEntity(Guid id) => Values.FirstOrDefault(e => e.EntityId == id);
  }
  public class LabeledEntity : InventoryTestDoubles.Component { public string DisplayName = "模组仓库"; }
}
namespace Timberborn.Buildings { public class BuildingSpec { } public class Building : InventoryTestDoubles.Component { public BuildingSpec Spec = new BuildingSpec(); } }
namespace Timberborn.BlockSystem { public class BlockObject : InventoryTestDoubles.Component { public string Coordinates = "1,2,3"; public bool IsFinished = true; } }
namespace Timberborn.EntityNaming { public class NamedEntity : InventoryTestDoubles.Component { public string EntityName = "自定义建筑名称"; } }
namespace Timberborn.GameDistricts {
  public class DistrictBuilding : InventoryTestDoubles.Component { public DistrictCenter District; }
  public class DistrictCenter : InventoryTestDoubles.Component { public string DistrictName = "地区甲"; }
  public class DistrictCenterRegistry {
    public readonly List<DistrictCenter> Values = new List<DistrictCenter>(); public int Enumerations;
    public IEnumerable<DistrictCenter> FinishedDistrictCenters { get { ++Enumerations; return Values; } }
  }
}
namespace Timberborn.Goods {
  public struct StorableGood { public string GoodId; }
  public struct StorableGoodAmount { public StorableGood StorableGood; }
  public struct GoodAmount { public string GoodId; public int Amount; }
  public class Localized { public string Value; }
  public class GoodSpec { public Localized DisplayName; }
  public interface IGoodService { IEnumerable<string> Goods { get; } bool HasGood(string id); GoodSpec GetGood(string id); }
  public class TestGoods : IGoodService {
    public readonly List<string> Values = new List<string> { "Water", "ModFood" }; public int Reads; public bool Fail;
    public IEnumerable<string> Goods { get { ++Reads; if (Fail) throw new Exception(); return Values; } }
    public bool HasGood(string id) => Values.Contains(id);
    public GoodSpec GetGood(string id) => new GoodSpec { DisplayName = new Localized { Value = "本地化" + id } };
  }
}
namespace Timberborn.InventorySystem {
  public class Inventories : InventoryTestDoubles.Component { public List<Inventory> AllInventories = new List<Inventory>(); }
  public class Inventory : InventoryTestDoubles.Component {
    public string ComponentName = "Stockpile"; public int Capacity = 100, Limit = 30, Incoming = 2, Reserved = 3, Reads;
    public bool IsInput = true, IsOutput = true, Fail;
    public readonly List<Timberborn.Goods.StorableGoodAmount> AllowedGoods = new List<Timberborn.Goods.StorableGoodAmount>();
    public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
    public IEnumerable<Timberborn.Goods.GoodAmount> Stock => Values.Select(v => new Timberborn.Goods.GoodAmount { GoodId = v.Key, Amount = v.Value });
    public int AmountInStock(string id) { ++Reads; if (Fail) throw new Exception(); return Values.TryGetValue(id, out int n) ? n : 0; }
    public int UnreservedAmountInStock(string id) => AmountInStock(id) - Reserved;
    public int ReservedCapacity(string id) => Incoming;
    public int UnreservedCapacity(string id) => Math.Max(0, Math.Min(Capacity - Values.Values.Sum(), Limit - AmountInStock(id)) - Incoming);
    public int LimitedAmount(string id) => Limit;
    // No Give/Take members: production catalog cannot accidentally compile a write against these doubles.
    public void Allow(string id, int n = 10) {
      AllowedGoods.Add(new Timberborn.Goods.StorableGoodAmount { StorableGood = new Timberborn.Goods.StorableGood { GoodId = id } }); Values[id] = n;
    }
  }
}
namespace Timberborn.ConstructionSites { public class ConstructionSite : InventoryTestDoubles.Component { public Inventory Inventory; } }
namespace Timberborn.Stockpiles { public class Stockpile : InventoryTestDoubles.Component { public Inventory Inventory; } }
namespace Timberborn.SimpleOutputBuildings { public class SimpleOutputInventory : InventoryTestDoubles.Component { public Inventory Inventory; } }
namespace Timberborn.DistributionSystem {
  public class DistrictCrossing : InventoryTestDoubles.Component { }
  public class DistrictCrossingInventory : InventoryTestDoubles.Component { }
}
namespace Timberborn.ResourceCountingSystem {
  public class ResourceCount {
    public int AllStock = 42, AvailableStock = 30, StockpiledStock = 10, BufferedOutputStock = 10,
      CarriedToStockpilesStock = 10, StockUnderProcessing = 4, BufferedInput = 4, CarriedToProcessors = 4;
  }
  public class ResourceCountingService { public int Reads; public ResourceCount GetGlobalResourceCount(string id) { ++Reads; return new ResourceCount(); } }
}
namespace Timberborn.SelectionSystem {
  public class SelectableObject : InventoryTestDoubles.Component { }
  public class EntitySelectionService {
    public EntityComponent Selected; public int Calls;
    public void SelectAndFocusOn(EntityComponent owner) { Selected = owner; ++Calls; }
  }
}
namespace TimberbornTrainer.Tests {
  public static class InventoryCatalogTests {
    private static int _checks;
    private static void Check(bool value, string message) { ++_checks; if (!value) throw new Exception(message); }
    private static EntityComponent Building(EntityRegistry entities) {
      var entity = new EntityComponent(); entity.Add(new Timberborn.BlockSystem.BlockObject()); entity.Add(new Timberborn.Buildings.Building());
      entity.Add(new LabeledEntity()); entity.Add(new Inventories());
      entity.Add(new Timberborn.SelectionSystem.SelectableObject()); entities.Values.Add(entity); return entity;
    }
    private static Inventory Stock(EntityComponent owner, string component, string good) {
      var stock = owner.Add(new Inventory { ComponentName = component }); stock.Allow(good);
      owner.GetComponent<Inventories>().AllInventories.Add(stock); return stock;
    }
    private static bool NoWrites(InventoryTarget target) => target.Abilities.Read.Available && !target.Abilities.Add.Available
      && !target.Abilities.Subtract.Available && !target.Abilities.Set.Available;
    private sealed class ModBuilding : Timberborn.Buildings.Building { }
    public static string Run() {
      _checks = 0;
      var entities = new EntityRegistry(); var districts = new Timberborn.GameDistricts.DistrictCenterRegistry();
      var goods = new Timberborn.Goods.TestGoods(); var counts = new Timberborn.ResourceCountingSystem.ResourceCountingService();
      var selection = new Timberborn.SelectionSystem.EntitySelectionService();
      var catalog = new NativeInventoryCatalog(entities, districts, goods, counts, selection);
      Check(!catalog.Scan().Completed && entities.Enumerations == 0, "初始化前拒绝扫描。");
      catalog.PostLoad(); catalog.PostLoad();
      foreach (var source in new[] { InventorySource.Settlement, InventorySource.DistrictCenter, InventorySource.Building }) {
        Check(catalog.Discover(source).Targets.Length == 0 && catalog.LastScan == null, "首次打开来源不暗中扫描：" + source);
      }
      Check(entities.Enumerations == 0 && districts.Enumerations == 0 && goods.Reads == 0, "初始化及列表不访问实体、地区和资源全集。");
      var a = Building(entities); var input = Stock(a, "Input", "ModFood"); input.IsOutput = false;
      var output = Stock(a, "Output", "Water"); output.IsInput = false;
      var construction = Stock(a, "Construction", "Water"); a.Add(new Timberborn.ConstructionSites.ConstructionSite { Inventory = construction });
      var b = Building(entities); b.Components.Remove(b.GetComponent<Timberborn.Buildings.Building>()); b.Add(new ModBuilding());
      b.GetComponent<LabeledEntity>().DisplayName = "松树"; // A real native building with a misleading label remains a building.
      var warehouse = Stock(b, "CustomGiantStockpile", "ModFood"); b.Add(new Timberborn.Stockpiles.Stockpile { Inventory = warehouse });
      var center = Building(entities); var district = center.Add(new Timberborn.GameDistricts.DistrictCenter()); districts.Values.Add(district);
      var centerStock = Stock(center, "SimpleOutputInventory", "Water"); centerStock.Capacity = int.MaxValue; centerStock.Limit = 20;
      center.Add(new Timberborn.SimpleOutputBuildings.SimpleOutputInventory { Inventory = centerStock });
      var crossing = Building(entities); Stock(crossing, "Transport", "Water"); crossing.Add(new Timberborn.DistributionSystem.DistrictCrossing());
      var mirror = Building(entities); Stock(mirror, "Mirror", "Water"); mirror.Add(new Timberborn.DistributionSystem.DistrictCrossingInventory());
      var unready = Building(entities); Stock(unready, "Unready", "Water"); unready.Initialized = false;
      var deleted = Building(entities); Stock(deleted, "Deleted", "Water"); deleted.Deleted = true;
      var pine = Building(entities); Stock(pine, "NaturalInventory", "Water"); pine.Components.Remove(pine.GetComponent<Timberborn.Buildings.Building>());
      pine.GetComponent<LabeledEntity>().DisplayName = "小型仓库"; // Labels must not decide entity identity.
      var plant = Building(entities); Stock(plant, "PlantInventory", "Water"); plant.Components.Remove(plant.GetComponent<Timberborn.Buildings.Building>());
      var malformed = Building(entities); Stock(malformed, "Unknown", "Water"); malformed.GetComponent<Timberborn.Buildings.Building>().Spec = null;
      Building(entities); // Native building with no initialized inventories.
      var scan = catalog.Scan();
      Check(scan.Completed && scan.Sequence == 1 && scan.ElapsedMilliseconds >= 0, "首次扫描成功且记录真实计时与序号。");
      Check(scan.RegisteredResources == 2 && scan.InspectedEntities == 11, "资源数与检查实体数准确。");
      Check(scan.RecognizedBuildings == 6 && scan.InventoryBuildings == 3 && scan.ReadableInventories == 5, "建筑、库存建筑与多库存分别统计。");
      Check(scan.DistrictCenters == 1 && scan.ExcludedObjects == 7 && scan.ExcludedInventories == 0, "地区中心及安全排除计数。");
      Check(scan.ExclusionReasons.Any(r => r.Contains("缺少有效原生建筑身份")) && scan.ExclusionReasons.Any(r => r.Contains("运输镜像")), "排除原因可审查。");
      Check(entities.Enumerations == 1 && goods.Reads == 1 && districts.Enumerations == 1, "一次用户扫描各全集读取一次。");
      Check(input.Reads == 0 && warehouse.Reads == 0 && centerStock.Reads == 0, "扫描目录不读取全体库存数量。");
      var all = catalog.Discover(InventorySource.Building).Targets;
      Check(all.Length == 5 && all.All(NoWrites), "全部有效库存仍只读。");
      Check(!all.Any(t => t.OwnerId.EndsWith(pine.EntityId.ToString()) || t.OwnerId.EndsWith(plant.EntityId.ToString())), "具有 BlockObject 和库存的自然物体均排除。");
      Check(!all.Any(t => t.OwnerId.EndsWith(malformed.EntityId.ToString())), "缺失原生建筑规格采取保守排除。");
      Check(all.Any(t => t.OwnerId.EndsWith(b.EntityId.ToString())), "原生 Building 派生的模组风格建筑被识别，不看显示名称（模拟）。");
      var multi = all.Where(t => t.OwnerId.EndsWith(a.EntityId.ToString())).ToArray();
      Check(multi.Length == 3 && multi.Select(t => t.Id).Distinct().Count() == 3, "单建筑三库存独立身份。");
      Check(multi.Any(t => t.Purpose.Contains("施工")) && multi.Any(t => t.Purpose.Contains("输入")) && multi.Any(t => t.Purpose.Contains("输出")), "库存用途分别描述。");
      catalog.Discover(InventorySource.Building); catalog.Discover(InventorySource.DistrictCenter); catalog.Discover(InventorySource.Settlement);
      Check(entities.Enumerations == 1 && goods.Reads == 1 && districts.Enumerations == 1 && catalog.LastScan.Sequence == 1, "打开列表及来源切换仅访问缓存。");
      var settlement = catalog.Discover(InventorySource.Settlement).Targets.Single(); var snapshot = catalog.Read(settlement.Id);
      Check(snapshot.Valid && snapshot.Resources.Length == 2 && snapshot.Resources[0].Amount == 42 && counts.Reads == 2, "按需读取原生聚合统计。");
      Check(snapshot.Resources.All(r => r.CapacityState == CapacityState.Aggregate && r.TotalCapacity == null && r.ReservedStock == null), "不伪造聚合容量和预留。");
      Check(!catalog.Focus(settlement.Id), "聚合目标不支持建筑定位。");
      var centers = catalog.Discover(InventorySource.DistrictCenter).Targets;
      Check(centers.Length == 1 && centers[0].Source == InventorySource.DistrictCenter, "地区来源只使用扫描时有效中心目录。");
      snapshot = catalog.Read(centers[0].Id);
      Check(snapshot.Valid && snapshot.Target.Source == InventorySource.DistrictCenter && snapshot.Resources[0].Amount == 10, "读取自身库存并保留来源描述。");
      Check(snapshot.Resources[0].ResourceLimit == 20 && snapshot.Resources[0].CapacityState == CapacityState.Unknown, "整数总上限不猜为无限，保留每资源限额。");
      centerStock.Values["Water"] = 80; snapshot = catalog.Read(centers[0].Id);
      Check(snapshot.Resources[0].Amount == 80 && snapshot.Resources[0].FreeCapacity == 0, "缓存不是库存数量，读取真实越限物资。");
      catalog.Discover(InventorySource.Building);
      var custom = all.Single(t => t.DisplayName.Contains("CustomGiantStockpile")); snapshot = catalog.Read(custom.Id);
      Check(snapshot.Resources.Length == 1 && snapshot.Resources[0].GoodId == "ModFood", "按目标库存过滤原生注册资源。");
      Check(snapshot.Resources[0].Amount == 10 && snapshot.Resources[0].ReservedStock == 3 && snapshot.Resources[0].AvailableStock == 7
        && snapshot.Resources[0].ReservedCapacity == 2 && snapshot.Resources[0].FreeCapacity == 18, "真实数量、预留和空位分别读取。");
      warehouse.Values["ModFood"] = 15; snapshot = catalog.Read(custom.Id);
      Check(snapshot.Resources[0].Amount == 15 && entities.Enumerations == 1, "按需读到变化，不使用目录缓存代替真实库存。");
      warehouse.Values["RemovedModGood"] = 5; snapshot = catalog.Read(custom.Id);
      var leftover = snapshot.Resources.Single(r => r.GoodId == "RemovedModGood");
      Check(!leftover.Allowed && !leftover.Registered && leftover.Amount == 5 && leftover.FreeCapacity == null, "未知残留只观察。");
      warehouse.Limit = 0; snapshot = catalog.Read(custom.Id);
      Check(snapshot.Resources.Single(r => r.GoodId == "ModFood").ResourceLimit == 0, "当前资源限额不是声明支持列表。"); warehouse.Limit = 30;
      Check(catalog.Focus(custom.Id) && selection.Selected == b, "使用真实所属建筑原生定位。");
      entities.Values.Reverse(); var secondScan = catalog.Scan();
      Check(secondScan.Sequence == 2 && secondScan.Completed && secondScan.ElapsedMilliseconds >= 0 && entities.Enumerations == 2, "重复主动扫描记录次数与耗时。");
      Check(catalog.Discover(InventorySource.Building).Targets.Any(t => t.Id == custom.Id), "重新扫描不因实体排序变化丢失稳定身份。");
      goods.Values.Add("NewModResource");
      catalog.Discover(InventorySource.Settlement);
      Check(!catalog.Read(settlement.Id).Resources.Any(r => r.GoodId == "NewModResource"), "资源目录仅在主动扫描时更新。");
      catalog.Scan(); catalog.Discover(InventorySource.Settlement);
      Check(catalog.Read(settlement.Id).Resources.Any(r => r.GoodId == "NewModResource") && catalog.LastScan.RegisteredResources == 3, "重新扫描发现新注册资源。");
      catalog.Discover(InventorySource.Building); warehouse.Enabled = false;
      snapshot = catalog.Read(custom.Id);
      Check(snapshot.Valid && snapshot.Target.Purpose.Contains("已停用"), "停用库存仍只读观察，状态说明按当前值读取而非扫描缓存。");
      warehouse.Fail = true; bool failed = false; try { catalog.Read(custom.Id); } catch { failed = true; }
      Check(failed && !catalog.Read(custom.Id).Valid, "读异常释放对应库存引用。"); warehouse.Fail = false;
      Check(catalog.Read(multi[0].Id).Valid, "单库存失效不清除所有其他扫描结果。");
      catalog.Scan(); catalog.Discover(InventorySource.Building);
      b.GetComponent<Inventories>().AllInventories.Remove(warehouse);
      Check(!catalog.Read(custom.Id).Valid, "执行读取前验证真实组件归属，不信任缓存。");
      var ambiguous = Building(entities); Stock(ambiguous, "Duplicated", "Water"); Stock(ambiguous, "Duplicated", "ModFood");
      var ambiguityScan = catalog.Scan();
      Check(ambiguityScan.ExcludedInventories == 2 && ambiguityScan.ExclusionReasons.Any(r => r.Contains("组件名缺失或重复")), "重复原生库存身份统计并说明。");
      var center2 = Building(entities); var district2 = center2.Add(new Timberborn.GameDistricts.DistrictCenter { DistrictName = "地区乙" });
      var water2 = Stock(center2, "Output", "Water"); water2.Values["Water"] = 7; districts.Values.Add(district2);
      Check(catalog.Discover(InventorySource.DistrictCenter).Targets.Length == 1, "新增中心不会触发后台刷新。");
      catalog.Scan(); centers = catalog.Discover(InventorySource.DistrictCenter).Targets;
      Check(centers.Length == 2 && catalog.LastScan.DistrictCenters == 2, "重扫后加入新增地区中心。");
      Check(centers.Select(t => catalog.Read(t.Id).Resources[0].Amount).OrderBy(n => n).SequenceEqual(new long[] { 7, 80 }), "两中心读取真实独立库存。");
      districts.Values.Remove(district2);
      Check(!catalog.Read(centers.Single(t => t.OwnerId.EndsWith(center2.EntityId.ToString())).Id).Valid, "使用缓存中心前重新检查原生完成列表。");
      catalog.Discover(InventorySource.Building); var targetA = multi[0].Id;
      a.GetComponent<Timberborn.Buildings.Building>().Spec = null;
      Check(!catalog.Read(targetA).Valid, "对象失去有效建筑身份后不能沿用缓存。");
      goods.Fail = true; var failure = catalog.Scan();
      Check(!failure.Completed && failure.Sequence > secondScan.Sequence && failure.ElapsedMilliseconds >= 0, "失败扫描也记录次数和耗时。");
      Check(catalog.Discover(InventorySource.Building).Targets.Length == 0 && !catalog.Read(custom.Id).Valid, "失败扫描不会保留陈旧目录。"); goods.Fail = false;
      catalog.Scan(); var live = catalog.Discover(InventorySource.Building).Targets.First(); string oldId = live.Id;
      catalog.Unload(); catalog.Unload();
      Check(catalog.LastScan == null && catalog.Discover(InventorySource.Building).Targets.Length == 0 && !catalog.Read(oldId).Valid, "场景卸载清理全部缓存与报告。");
      int enumerations = entities.Enumerations; catalog.PostLoad(); catalog.Scan();
      Check(entities.Enumerations == enumerations, "旧实例不可重新接入场景。");
      foreach (string field in new[] { "_entities", "_districts", "_goods", "_counts", "_selection", "_scene" })
        Check(typeof(NativeInventoryCatalog).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(catalog) == null, "释放场景引用：" + field);
      var next = new NativeInventoryCatalog(entities, districts, goods, counts, selection); next.PostLoad();
      Check(next.LastScan == null && next.Discover(InventorySource.Building).Targets.Length == 0, "新场景必须重新主动扫描。");
      Check(next.Scan().Sequence == 1 && !next.Discover(InventorySource.Building).Targets.Any(t => t.Id == oldId), "新场景计数及身份重新建立。");
      next.Unload();
      return "原生库存身份、扫描及缓存（实际适配器＋替身）：" + _checks + " 项断言通过。";
    }
    private static string GetScene(InventoryTarget target) {
      int colon = target.Id.IndexOf(':'); int length = int.Parse(target.Id.Substring(0, colon)); return target.Id.Substring(colon + 1, length);
    }
  }
}
