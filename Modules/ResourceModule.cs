using Timberborn.ScienceSystem;
using TimberbornTrainer.Core;
using System;
using System.Linq;
using Timberborn.EntitySystem;
using Timberborn.BlockSystem;
using Timberborn.Stockpiles;
using Timberborn.Goods;


namespace TimberbornTrainer.Modules {
  public sealed class ResourceModule {
    public ScienceCommands Science { get; }
    public InventoryCommands Inventory { get; }
    private readonly EntityComponentRegistry _registry;
    private readonly IGoodService _goods;

    public ResourceModule(TrainerSession session, ScienceService scienceService, EntityComponentRegistry registry, IGoodService goods) {
      _registry = registry; _goods = goods;
      Science = new ScienceCommands(new NativeSciencePoints(scienceService),
        () => session.State != null && session.State.EffectiveEnabled);
      Inventory = new InventoryCommands(() => session.State != null && session.State.EffectiveEnabled);
    }
    public Stockpile[] Warehouses() => _registry.GetEnabled<Stockpile>().Where(s => s && s.Enabled &&
      s.GetComponent<BlockObject>().IsFinished && s.Inventory != null).ToArray();
    public string[] Goods(Stockpile site) => site && site.Inventory != null ? site.Inventory.AllowedGoods
      .Select(a => a.StorableGood.GoodId).Distinct().OrderBy(id => id).ToArray() : Array.Empty<string>();
    public string GoodName(string id) => _goods.GetGood(id).DisplayName.Value;
    public IWarehouseStock Stock(Stockpile site, string id) => new NativeStock(site, id);
    private sealed class NativeStock : IWarehouseStock {
      private readonly Stockpile _site;
      private readonly string _id;
      public NativeStock(Stockpile site, string id) { _site = site; _id = id; }
      public bool Usable => _site && _site.Enabled && _site.GetComponent<BlockObject>().IsFinished &&
        _site.Inventory != null && _site.Inventory.AllowedGoods.Any(a => a.StorableGood.GoodId == _id);
      public int Read() { if (!Usable) throw new InvalidOperationException(); return _site.Inventory.AmountInStock(_id); }
      public int FreeCapacity() => _site.Inventory.UnreservedCapacity(_id);
      public int UnreservedStock() => _site.Inventory.UnreservedAmountInStock(_id);
      public void Give(int amount) => _site.Inventory.GiveExisting(new GoodAmount(_id, amount));
      public void Take(int amount) => _site.Inventory.TakeExisting(new GoodAmount(_id, amount));
    }
    private sealed class NativeSciencePoints : ISciencePoints {
      private readonly ScienceService _service;
      public NativeSciencePoints(ScienceService service) { _service = service; }
      public int Read() => _service.SciencePoints;
      public void Add(int amount) => _service.AddPoints(amount);
      public void Subtract(int amount) => _service.SubtractPoints(amount);
    }
  }
}


