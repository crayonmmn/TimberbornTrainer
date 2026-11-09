using System;
using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.TimeSystem;
using Timberborn.SingletonSystem;
using TimberbornTrainer.Core;

namespace TimberbornTrainer.Modules {
  public sealed class NativeInventoryEditor : IInventoryEditor, IInventoryEditPreview, IUnloadableSingleton {
    private readonly NativeInventoryCatalog _catalog;
    private readonly TrainerSession _session;
    private readonly SpeedManager _speed;
    private bool _busy, _unloaded;
    public NativeInventoryEditor(NativeInventoryCatalog catalog, TrainerSession session, SpeedManager speed) {
      _catalog = catalog; _session = session; _speed = speed;
    }
    private bool Permitted() => !_unloaded && _session.State != null && _session.State.EffectiveEnabled
      && _speed.CurrentSpeed == 0 && UnityEngine.Time.timeScale == 0;
    public string ReadOnlyReason(string targetId, string resource) {
      if (_unloaded) return "场景已卸载，请重新进入聚居地。";
      try {
        var stock = _catalog.ResolveForEdit(targetId, resource);
        return stock != null && stock.Usable ? null :
          "当前目标已失效或不属于可编辑库存。仅支持已完工普通仓储、地区中心自身输出；施工、生产、未知及尚未适配的自定义库存只读。";
      } catch { return "无法确认库存用途、资源或对象身份，请主动刷新。"; }
    }
    public InventoryEditResult Execute(string targetId, string resource, InventoryWriteOperation operation, string input) {
      if (_busy) return new InventoryEditResult { Message = "已有库存命令执行中，拒绝重入。" };
      _busy = true;
      try {
        var r = InventoryEditCommands.Execute(() => _catalog.ResolveForEdit(targetId, resource), Permitted, operation, input);
        UnityEngine.Debug.Log("海狸修改工具：库存命令 " + operation + "；状态=" + r.State
          + "；调用开始=" + r.NativeCallStarted + "；前=" + r.Before + "；后=" + r.After
          + "；请求差额=" + r.RequestedDelta + "。无自动重试。");
        return r;
      } finally { _busy = false; }
    }
    public void Unload() { _unloaded = true; }
  }

  public sealed partial class NativeInventoryCatalog {
    internal IWarehouseStock ResolveForEdit(string id, string resource) {
      NativeTarget target;
      if (!_ready || id == null || !_targets.TryGetValue(id, out target) || target.Inventory == null) return null;
      // Captures the actual object pair; every read and the one write re-checks the same pair.
      return new EditableInventory(this, target, resource);
    }
    private bool Editable(NativeTarget target, string resource) {
      if (target.RequireMapSelection && !MapTargetCurrent(target, resource)) return false;
      if (!_ready || target.Scene != _scene || !OwnerValid(target.Owner) || !target.Inventory
          || !_goods.HasGood(resource) || _goods.GetGood(resource) == null
          || !ReferenceEquals(_entities.GetEntity(target.EntityId), target.Owner)) return false;
      NativeTarget current;
      if (!_targets.TryGetValue(target.Description.Id, out current) || !ReferenceEquals(current, target)) return false;
      // Ordinary stockpiles use the already reviewed live native identity resolver.
      var center = target.Owner.GetComponent<DistrictCenter>();
      if (!center) return CheckNativeIdentity(target.Description.Id, resource).Status == InventoryIdentityStatus.Valid;
      // Centers expose their own output only, never aggregate counts or crossing/transport buffers.
      var inventory = target.Inventory;
      var output = target.Owner.GetComponent<SimpleOutputInventory>();
      var all = target.Owner.GetComponent<Inventories>();
      return target.Owner.GetComponent<BlockObject>().IsFinished && inventory.Enabled && output && output.Enabled
        && _districts.FinishedDistrictCenters.Contains(center) && ReferenceEquals(output.Inventory, inventory)
        && inventory.GetType() == typeof(Inventory) && inventory.ComponentName == target.Component
        && ReferenceEquals(inventory.GetComponent<EntityComponent>(), target.Owner) && all
        && all.AllInventories.Count(i => ReferenceEquals(i, inventory)) == 1
        && all.AllInventories.Count(i => i && i.ComponentName == target.Component) == 1;
    }
    private sealed class EditableInventory : IWarehouseStock {
      private readonly NativeInventoryCatalog _catalog;
      private readonly NativeTarget _target;
      private readonly string _resource;
      internal EditableInventory(NativeInventoryCatalog catalog, NativeTarget target, string resource) {
        _catalog = catalog; _target = target; _resource = resource;
      }
      public bool Usable => _catalog.Editable(_target, _resource);
      private Inventory Current() { if (!Usable) throw new InvalidOperationException("库存身份已变化"); return _target.Inventory; }
      public int Read() => Current().AmountInStock(_resource);
      public int UnreservedStock() => Current().UnreservedAmountInStock(_resource);
      public int FreeCapacity() {
        var inventory = Current();
        return inventory.AllowedGoods.Any(g => g.StorableGood.GoodId == _resource) && inventory.LimitedAmount(_resource) > 0
          ? Math.Min(inventory.UnreservedCapacity(_resource), int.MaxValue - inventory.TotalAmountInStock) : 0;
      }
      public void Give(int amount) {
        var inventory = Current();
        if (amount <= 0 || amount > FreeCapacity()) throw new InvalidOperationException("原生未预留空位不足");
        inventory.GiveExisting(new GoodAmount(_resource, amount));
      }
      public void Take(int amount) {
        var inventory = Current();
        if (amount <= 0 || amount > inventory.UnreservedAmountInStock(_resource)) throw new InvalidOperationException("原生未预留库存不足");
        inventory.TakeExisting(new GoodAmount(_resource, amount));
      }
    }
  }
}
