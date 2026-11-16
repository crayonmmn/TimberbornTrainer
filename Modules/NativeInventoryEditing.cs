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
    private string PermissionError() {
      if (_unloaded) return global::TimberbornTrainer.Localization.TextToken.Key("t0142");
      if (_session?.State == null) return global::TimberbornTrainer.Localization.TextToken.Key("t0138");
      if (!_session.State.Compatible) return global::TimberbornTrainer.Localization.TextToken.Key("t0443");
      if (!_session.State.EffectiveEnabled) return global::TimberbornTrainer.Localization.TextToken.Key("t0309");
      if (_speed == null || _speed.CurrentSpeed != 0 || UnityEngine.Time.timeScale != 0)
        return global::TimberbornTrainer.Localization.TextToken.Key("t0325");
      return null;
    }
    public string ReadOnlyReason(string targetId, string resource) {
      if (_unloaded) return global::TimberbornTrainer.Localization.TextToken.Key("t0141");
      try {
        var stock = _catalog.ResolveForEdit(targetId, resource);
        return stock != null && stock.Usable ? null :
          global::TimberbornTrainer.Localization.TextToken.Key("t0160");
      } catch { return global::TimberbornTrainer.Localization.TextToken.Key("t0386"); }
    }
    public InventoryEditResult Execute(string targetId, string resource, InventoryWriteOperation operation, string input) {
      if (_busy) return new InventoryEditResult { Message = global::TimberbornTrainer.Localization.TextToken.Key("t0411") };
      _busy = true;
      try {
        string denial = null, permissionState = global::TimberbornTrainer.Localization.TextToken.Key("t0367");
        var r = InventoryEditCommands.Execute(() => _catalog.ResolveForEdit(targetId, resource), () => {
          denial = PermissionError();
          permissionState = global::TimberbornTrainer.Localization.TextToken.Key("t0320") + (_session?.State?.EffectiveEnabled == true) + global::TimberbornTrainer.Localization.TextToken.Key("t0086") +
            (_speed == null ? global::TimberbornTrainer.Localization.TextToken.Key("t0376") : _speed.CurrentSpeed.ToString()) + global::TimberbornTrainer.Localization.TextToken.Key("t0066") + UnityEngine.Time.timeScale + global::TimberbornTrainer.Localization.TextToken.Key("t0078") + _unloaded;
          return denial == null;
        }, operation, input);
        if (denial != null && !r.NativeCallStarted) r.Message = denial;
        UnityEngine.Debug.Log(global::TimberbornTrainer.Localization.RuntimeText.Render(global::TimberbornTrainer.Localization.TextToken.Key("t0196") + operation + global::TimberbornTrainer.Localization.TextToken.Key("t0091") + r.State
          + global::TimberbornTrainer.Localization.TextToken.Key("t0044") + r.NativeCallStarted + global::TimberbornTrainer.Localization.TextToken.Key("t0061") + r.Before + global::TimberbornTrainer.Localization.TextToken.Key("t0046") + r.After
          + global::TimberbornTrainer.Localization.TextToken.Key("t0063") + r.RequestedDelta + global::TimberbornTrainer.Localization.TextToken.Key("t0040") + permissionState + global::TimberbornTrainer.Localization.TextToken.Key("t0045") + r.Message + global::TimberbornTrainer.Localization.TextToken.Key("t0098")));
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
      private Inventory Current() { if (!Usable) throw new InvalidOperationException(global::TimberbornTrainer.Localization.TextToken.Key("t0254")); return _target.Inventory; }
      public int Read() => Current().AmountInStock(_resource);
      public int UnreservedStock() => Current().UnreservedAmountInStock(_resource);
      public int FreeCapacity() {
        var inventory = Current();
        return inventory.AllowedGoods.Any(g => g.StorableGood.GoodId == _resource) && inventory.LimitedAmount(_resource) > 0
          ? Math.Min(inventory.UnreservedCapacity(_resource), int.MaxValue - inventory.TotalAmountInStock) : 0;
      }
      public void Give(int amount) {
        var inventory = Current();
        if (amount <= 0 || amount > FreeCapacity()) throw new InvalidOperationException(global::TimberbornTrainer.Localization.TextToken.Key("t0437"));
        inventory.GiveExisting(new GoodAmount(_resource, amount));
      }
      public void Take(int amount) {
        var inventory = Current();
        if (amount <= 0 || amount > inventory.UnreservedAmountInStock(_resource)) throw new InvalidOperationException(global::TimberbornTrainer.Localization.TextToken.Key("t0438"));
        inventory.TakeExisting(new GoodAmount(_resource, amount));
      }
    }
  }
}
