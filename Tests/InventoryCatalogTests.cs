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
  }
  public class LabeledEntity : InventoryTestDoubles.Component { public string DisplayName = "模组仓库"; }
}
namespace Timberborn.BlockSystem { public class BlockObject : InventoryTestDoubles.Component { public string Coordinates = "1,2,3"; } }
namespace Timberborn.EntityNaming { public class NamedEntity : InventoryTestDoubles.Component { public string EntityName = "自定义建筑名称"; } }
namespace Timberborn.GameDistricts {
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
    public readonly List<string> Values = new List<string> { "Water", "ModFood" }; public int Reads;
    public IEnumerable<string> Goods { get { ++Reads; return Values; } }
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
      var entity = new EntityComponent(); entity.Add(new Timberborn.BlockSystem.BlockObject());
      entity.Add(new LabeledEntity()); entity.Add(new Inventories());
      entity.Add(new Timberborn.SelectionSystem.SelectableObject()); entities.Values.Add(entity); return entity;
    }
    private static Inventory Stock(EntityComponent owner, string component, string good) {
      var stock = owner.Add(new Inventory { ComponentName = component }); stock.Allow(good);
      owner.GetComponent<Inventories>().AllInventories.Add(stock); return stock;
    }
    private static bool NoWrites(InventoryTarget target) => target.Abilities.Read.Available && !target.Abilities.Add.Available
      && !target.Abilities.Subtract.Available && !target.Abilities.Set.Available;
    public static string Run() {
      _checks = 0;
      var entities = new EntityRegistry(); var districts = new Timberborn.GameDistricts.DistrictCenterRegistry();
      var goods = new Timberborn.Goods.TestGoods(); var counts = new Timberborn.ResourceCountingSystem.ResourceCountingService();
      var selection = new Timberborn.SelectionSystem.EntitySelectionService();
      var catalog = new NativeInventoryCatalog(entities, districts, goods, counts, selection);
      Check(catalog.Discover(InventorySource.Building).Targets.Length == 0 && entities.Enumerations == 0, "初始化前不枚举实体。");
      catalog.PostLoad(); catalog.PostLoad();
      Check(entities.Enumerations == 0 && districts.Enumerations == 0 && counts.Reads == 0, "PostLoad 不读取库存。");
      var d = catalog.Discover(InventorySource.Settlement);
      Check(d.Targets.Length == 1 && NoWrites(d.Targets[0]), "聚居地目标只读。");
      Check(entities.Enumerations == 0 && districts.Enumerations == 0 && goods.Reads == 0, "默认发现不扫描仓库或读取全部资源。");
      var snapshot = catalog.Read(d.Targets[0].Id);
      Check(snapshot.Valid && snapshot.Resources.Length == 2 && counts.Reads == 2, "选择时读取已注册资源的原生统计。");
      Check(snapshot.Resources[0].Amount == 42 && snapshot.Resources[0].AvailableStock == 30, "统计实际值与可用统计分开。");
      Check(snapshot.Resources.All(r => r.CapacityState == CapacityState.Aggregate && r.ReservedStock == null && r.TotalCapacity == null), "不伪造统计容量及预留。");
      Check(!catalog.Focus(d.Targets[0].Id) && selection.Calls == 0, "聚合目标不能镜头定位。");

      var a = Building(entities); a.Add(new Timberborn.EntityNaming.NamedEntity());
      var input = Stock(a, "Input", "ModFood"); input.IsOutput = false;
      var output = Stock(a, "Output", "Water"); output.IsInput = false;
      var construction = Stock(a, "Construction", "Water"); a.Add(new Timberborn.ConstructionSites.ConstructionSite { Inventory = construction });
      var b = Building(entities); var warehouse = Stock(b, "CustomGiantStockpile", "ModFood");
      b.Add(new Timberborn.Stockpiles.Stockpile { Inventory = warehouse });
      var center = Building(entities); var district = center.Add(new Timberborn.GameDistricts.DistrictCenter()); districts.Values.Add(district);
      var centerStock = Stock(center, "SimpleOutputInventory", "Water"); centerStock.Capacity = int.MaxValue; centerStock.Limit = 20;
      center.Add(new Timberborn.SimpleOutputBuildings.SimpleOutputInventory { Inventory = centerStock });
      var crossing = Building(entities); Stock(crossing, "Transport", "Water"); crossing.Add(new Timberborn.DistributionSystem.DistrictCrossing());
      var mirror = Building(entities); Stock(mirror, "Mirror", "Water"); mirror.Add(new Timberborn.DistributionSystem.DistrictCrossingInventory());
      var unready = Building(entities); Stock(unready, "Unready", "Water"); unready.Initialized = false;
      var deleted = Building(entities); Stock(deleted, "Deleted", "Water"); deleted.Deleted = true;

      d = catalog.Discover(InventorySource.DistrictCenter);
      Check(d.Targets.Length == 1 && entities.Enumerations == 0, "地区中心仅通过完成中心列表发现。");
      Check(d.Targets[0].OwnerName.Contains("地区甲") && d.Targets[0].Purpose.Contains("自身输出"), "地区名称和自身库存用途。");
      snapshot = catalog.Read(d.Targets[0].Id);
      Check(snapshot.Resources[0].Amount == 10 && snapshot.Resources[0].ResourceLimit == 20, "地区中心读取自身数量及原生每资源限额。");
      Check(snapshot.Resources[0].CapacityState == CapacityState.Unknown && snapshot.Resources[0].TotalCapacity == int.MaxValue, "整数上限不宣称无限容量。");
      centerStock.Values["Water"] = 80; snapshot = catalog.Read(d.Targets[0].Id);
      Check(snapshot.Resources[0].Amount == 80 && snapshot.Resources[0].FreeCapacity == 0, "开局越限物资如实显示，不伪造空位。");

      d = catalog.Discover(InventorySource.Building);
      Check(entities.Enumerations == 1 && d.Targets.Length == 5, "主动发现库存建筑，排除交汇点、镜像与未初始化/已删除实体。");
      Check(input.Reads == 0 && output.Reads == 0 && warehouse.Reads == 0, "列出建筑不读取所有库存数量。");
      var ownerId = InventoryIdentity.Owner(GetScene(d.Targets[0]), a.EntityId.ToString());
      var multi = d.Targets.Where(t => t.OwnerId == ownerId).ToArray();
      Check(multi.Length == 3 && multi.Select(t => t.Id).Distinct().Count() == 3, "单建筑三个库存分别拥有稳定身份。");
      Check(multi.Any(t => t.Purpose.Contains("输入")) && multi.Any(t => t.Purpose.Contains("输出")) && multi.Any(t => t.Purpose.Contains("施工")), "输入、输出、施工用途不混淆。");
      Check(d.Targets.All(NoWrites), "所有新适配目标写入均关闭。");
      var custom = d.Targets.Single(t => t.DisplayName.Contains("CustomGiantStockpile"));
      snapshot = catalog.Read(custom.Id);
      Check(snapshot.Resources.Length == 1 && snapshot.Resources[0].GoodId == "ModFood" && snapshot.Resources[0].Registered, "第三方风格建筑与资源通过原生组件发现，非名称白名单（模拟）。");
      Check(snapshot.Resources[0].ReservedStock == 3 && snapshot.Resources[0].AvailableStock == 7
        && snapshot.Resources[0].ReservedCapacity == 2 && snapshot.Resources[0].FreeCapacity == 18, "真实数量、两类预留和可用量独立读取。");
      Check(snapshot.Resources[0].CapacityState == CapacityState.Finite && snapshot.Resources[0].TotalCapacity == 100, "有限容量如实展示。");
      warehouse.Values["RemovedModGood"] = 5; snapshot = catalog.Read(custom.Id);
      var leftover = snapshot.Resources.Single(r => r.GoodId == "RemovedModGood");
      Check(!leftover.Registered && !leftover.Allowed && leftover.Amount == 5 && leftover.FreeCapacity == null, "未知残留资源不冒充可存入资源。");
      Check(snapshot.Resources.All(r => r.GoodId != "Water"), "注册资源不等于该库存允许资源。");
      warehouse.Limit = 0; snapshot = catalog.Read(custom.Id);
      Check(snapshot.Resources.Single(r => r.GoodId == "ModFood").Allowed
        && snapshot.Resources.Single(r => r.GoodId == "ModFood").ResourceLimit == 0
        && snapshot.Resources.Single(r => r.GoodId == "ModFood").FreeCapacity == 0, "声明支持和当前禁止存入分开展示。");
      warehouse.Limit = 30;
      Check(catalog.Focus(custom.Id) && selection.Selected == b && selection.Calls == 1, "定位真实所属建筑。");
      int reads = warehouse.Reads; entities.Values.Reverse(); var again = catalog.Discover(InventorySource.Building);
      Check(again.Targets.Any(t => t.Id == custom.Id) && warehouse.Reads == reads, "排序变化不改变身份，重列不读数量。");
      warehouse.Enabled = false; catalog.Discover(InventorySource.Building);
      Check(catalog.Read(custom.Id).Valid, "停用库存只观察，不因不活动而写入或伪造状态。");
      var browser = new InventoryBrowser(catalog); browser.Switch(InventorySource.Building); browser.Discover();
      warehouse.Fail = true; browser.Select(custom.Id);
      Check(browser.Snapshot == null && browser.Targets.Length == 0 && !catalog.Read(custom.Id).Valid, "实际适配器读异常清理浏览模型与原生映射。");
      warehouse.Fail = false; browser.Dispose(); catalog.Discover(InventorySource.Building);
      b.GetComponent<Inventories>().AllInventories.Remove(warehouse);
      Check(!catalog.Read(custom.Id).Valid, "库存被替换/移除立即拒绝旧引用。");
      Check(!catalog.Read(custom.Id).Valid, "失效后映射已清除。");
      catalog.Discover(InventorySource.DistrictCenter); districts.Values.Clear();
      Check(!catalog.Read(InventoryIdentity.Target(GetScene(d.Targets[0]), center.EntityId.ToString(), "SimpleOutputInventory")).Valid, "地区中心退出完成列表后不能沿用引用。");

      var ambiguous = Building(entities); Stock(ambiguous, "Duplicated", "Water"); Stock(ambiguous, "Duplicated", "ModFood");
      var ambiguityResult = catalog.Discover(InventorySource.Building);
      Check(!ambiguityResult.Targets.Any(t => t.OwnerId.EndsWith(ambiguous.EntityId.ToString()))
        && ambiguityResult.Message.Contains("组件名缺失或重复"), "不使用序号修补重复原生标识，明确说明跳过原因。");
      var center2 = Building(entities); var district2 = center2.Add(new Timberborn.GameDistricts.DistrictCenter { DistrictName = "地区乙" });
      var stock2 = Stock(center2, "SimpleOutputInventory", "Water"); stock2.Values["Water"] = 7;
      center2.Add(new Timberborn.SimpleOutputBuildings.SimpleOutputInventory { Inventory = stock2 });
      districts.Values.Add(district); districts.Values.Add(district2);
      var centers = catalog.Discover(InventorySource.DistrictCenter);
      var first = centers.Targets.Single(t => t.OwnerName.Contains("地区甲")); var second = centers.Targets.Single(t => t.OwnerName.Contains("地区乙"));
      Check(catalog.Read(first.Id).Resources[0].Amount == 80 && catalog.Read(second.Id).Resources[0].Amount == 7,
        "不同地区中心读取相互独立的实际库存，非地区汇总。");
      center2.Deleted = true;
      Check(!catalog.Focus(second.Id) && !catalog.Read(first.Id).Valid, "定位前对象失效时清理映射，不保留陈旧场景引用。");

      d = catalog.Discover(InventorySource.Building);
      var live = d.Targets.First(); var oldId = live.Id;
      catalog.Clear(); Check(!catalog.Read(oldId).Valid, "切换模式清除引用。");
      catalog.Unload(); catalog.Unload();
      int enumerations = entities.Enumerations; catalog.PostLoad(); catalog.Discover(InventorySource.Building);
      Check(entities.Enumerations == enumerations && !catalog.Read(oldId).Valid, "卸载实例不能重新接入场景。");
      foreach (string field in new[] { "_entities", "_districts", "_goods", "_counts", "_selection", "_scene" }) {
        Check(typeof(NativeInventoryCatalog).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(catalog) == null, "清理场景引用：" + field);
      }
      var next = new NativeInventoryCatalog(entities, districts, goods, counts, selection); next.PostLoad();
      Check(!next.Discover(InventorySource.Building).Targets.Any(t => t.Id == oldId), "新场景身份不会复用旧场景标识。");
      next.Unload();
      return "原生库存目录（实际适配器＋测试替身）：" + _checks + " 项断言通过。";
    }
    private static string GetScene(InventoryTarget target) {
      int colon = target.Id.IndexOf(':'); int length = int.Parse(target.Id.Substring(0, colon)); return target.Id.Substring(colon + 1, length);
    }
  }
}
