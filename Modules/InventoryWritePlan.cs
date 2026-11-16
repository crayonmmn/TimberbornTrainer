using System;

namespace TimberbornTrainer.Modules {
  public enum InventoryWriteOperation { Add, Remove, Set, Clear }
  public enum InventoryWriteState { Planned, NoOp, Rejected, Succeeded, Indeterminate }
  public enum InventoryPermission { Unknown, Denied, Allowed }
  public enum InventoryUse { Unknown, Warehouse, Construction, ProductionInput, ProductionOutput, DistrictCenter, Crossing, Custom }

  // Instance tokens are opaque comparison evidence supplied by a future native resolver.
  // They are not entity names, coordinates, native references, or execution credentials.
  public sealed class InventoryWriteIdentity {
    public string Scene { get; }
    public string Entity { get; }
    public string OwnerInstance { get; }
    public string Inventory { get; }
    public string Component { get; }
    public string ComponentInstance { get; }
    public InventoryUse Use { get; }
    public string District { get; }
    public string Resource { get; }
    public InventoryWriteIdentity(string scene, string entity, string ownerInstance, string inventory,
        string component, string componentInstance, InventoryUse use, string district, string resource) {
      Scene = scene; Entity = entity; OwnerInstance = ownerInstance; Inventory = inventory;
      Component = component; ComponentInstance = componentInstance; Use = use; District = district; Resource = resource;
    }
  }

  public sealed class InventoryWriteCapabilities {
    public InventoryPermission Read { get; }
    public InventoryPermission Add { get; }
    public InventoryPermission Remove { get; }
    public InventoryPermission Set { get; }
    public InventoryPermission Clear { get; }
    public InventoryWriteCapabilities(InventoryPermission read, InventoryPermission add, InventoryPermission remove,
        InventoryPermission set, InventoryPermission clear) {
      Read = read; Add = add; Remove = remove; Set = set; Clear = clear;
    }
    internal InventoryPermission For(InventoryWriteOperation operation) {
      switch (operation) {
        case InventoryWriteOperation.Add: return Add;
        case InventoryWriteOperation.Remove: return Remove;
        case InventoryWriteOperation.Set: return Set;
        case InventoryWriteOperation.Clear: return Clear;
        default: return InventoryPermission.Unknown;
      }
    }
  }

  // Null is unknown, never a substitute for zero or false. No native objects are held.
  public sealed class InventoryWriteSnapshot {
    public InventoryWriteIdentity Identity { get; }
    public string SnapshotId { get; }
    public bool? Valid { get; }
    public bool? Finished { get; }
    public bool? Enabled { get; }
    public bool? VerifiedNativeWarehouse { get; }
    public bool? ResourceRegistered { get; }
    public bool? ResourceAllowed { get; }
    public long? Amount { get; }
    public long? Unreserved { get; }
    public long? FreeCapacity { get; }
    public long? TotalAmount { get; }
    public InventoryWriteCapabilities Capabilities { get; }
    public InventoryWriteSnapshot(InventoryWriteIdentity identity, string snapshotId, bool? valid, bool? finished,
        bool? enabled, bool? verifiedNativeWarehouse, bool? registered, bool? allowed, long? amount,
        long? unreserved, long? freeCapacity, long? totalAmount, InventoryWriteCapabilities capabilities) {
      Identity = identity; SnapshotId = snapshotId; Valid = valid; Finished = finished; Enabled = enabled;
      VerifiedNativeWarehouse = verifiedNativeWarehouse; ResourceRegistered = registered; ResourceAllowed = allowed;
      Amount = amount; Unreserved = unreserved; FreeCapacity = freeCapacity; TotalAmount = totalAmount; Capabilities = capabilities;
    }
  }

  public sealed class InventoryWriteRequest {
    public InventoryWriteIdentity Target { get; }
    public InventoryWriteOperation Operation { get; }
    public long Quantity { get; }
    public InventoryWriteRequest(InventoryWriteIdentity target, InventoryWriteOperation operation, long quantity) {
      Target = target; Operation = operation; Quantity = quantity;
    }
  }

  public sealed class InventoryWriteValidation {
    public bool Accepted => Code == "OK";
    public string Code { get; }
    public string Reason { get; }
    internal InventoryWriteValidation(string code, string reason) { Code = code; Reason = reason; }
  }

  public static class InventoryWriteTargetValidator {
    internal static InventoryWriteValidation Reject(string code, string reason) => new InventoryWriteValidation(code, reason);
    private static bool Present(string value) => !string.IsNullOrWhiteSpace(value);
    private static bool Complete(InventoryWriteIdentity id) => id != null && Present(id.Scene) && Present(id.Entity)
      && Present(id.OwnerInstance) && Present(id.Inventory) && Present(id.Component) && Present(id.ComponentInstance)
      && Present(id.District) && Present(id.Resource);
    internal static InventoryWriteValidation Capability(InventoryPermission permission, string operation) {
      if (permission == InventoryPermission.Allowed) return new InventoryWriteValidation("OK", global::TimberbornTrainer.Localization.TextToken.Key("t0422"));
      return Reject(permission == InventoryPermission.Denied ? "CapabilityDenied" : "CapabilityUnknown",
        operation + (permission == InventoryPermission.Denied ? global::TimberbornTrainer.Localization.TextToken.Key("t0285") : global::TimberbornTrainer.Localization.TextToken.Key("t0286")));
    }
    public static InventoryWriteValidation Validate(InventoryWriteRequest request, InventoryWriteSnapshot snapshot) {
      if (request == null || snapshot == null) return Reject("MissingInput", global::TimberbornTrainer.Localization.TextToken.Key("t0304"));
      if (!Enum.IsDefined(typeof(InventoryWriteOperation), request.Operation)) return Reject("UnknownOperation", global::TimberbornTrainer.Localization.TextToken.Key("t0131"));
      var expected = request.Target; var actual = snapshot.Identity;
      if (!Complete(expected) || !Complete(actual)) return Reject("MissingIdentity", global::TimberbornTrainer.Localization.TextToken.Key("t0136"));
      if (expected.Scene != actual.Scene) return Reject("SceneChanged", global::TimberbornTrainer.Localization.TextToken.Key("t0140"));
      if (expected.Entity != actual.Entity || expected.OwnerInstance != actual.OwnerInstance) return Reject("EntityChanged", global::TimberbornTrainer.Localization.TextToken.Key("t0213"));
      if (expected.Inventory != actual.Inventory || expected.Component != actual.Component) return Reject("InventoryChanged", global::TimberbornTrainer.Localization.TextToken.Key("t0252"));
      if (expected.ComponentInstance != actual.ComponentInstance) return Reject("ComponentReplaced", global::TimberbornTrainer.Localization.TextToken.Key("t0259"));
      if (expected.Use != actual.Use) return Reject("UseChanged", global::TimberbornTrainer.Localization.TextToken.Key("t0256"));
      if (expected.District != actual.District) return Reject("DistrictChanged", global::TimberbornTrainer.Localization.TextToken.Key("t0167"));
      if (expected.Resource != actual.Resource) return Reject("ResourceChanged", global::TimberbornTrainer.Localization.TextToken.Key("t0469"));
      if (actual.Use != InventoryUse.Warehouse) return Reject("UnsupportedUse", global::TimberbornTrainer.Localization.TextToken.Key("t0228"));
      if (!Present(snapshot.SnapshotId)) return Reject("UnknownSnapshot", global::TimberbornTrainer.Localization.TextToken.Key("t0261"));
      if (snapshot.Valid != true) return Reject("InvalidTarget", global::TimberbornTrainer.Localization.TextToken.Key("t0282"));
      if (snapshot.Finished != true || snapshot.Enabled != true || snapshot.VerifiedNativeWarehouse != true)
        return Reject("UnsafeWarehouse", global::TimberbornTrainer.Localization.TextToken.Key("t0369"));
      if (snapshot.ResourceRegistered != true) return Reject("UnknownResource", global::TimberbornTrainer.Localization.TextToken.Key("t0471"));
      if (snapshot.Capabilities == null) return Reject("CapabilityUnknown", global::TimberbornTrainer.Localization.TextToken.Key("t0132"));
      var read = Capability(snapshot.Capabilities.Read, global::TimberbornTrainer.Localization.TextToken.Key("t0177"));
      return !read.Accepted ? read : Capability(snapshot.Capabilities.For(request.Operation), request.Operation.ToString());
    }
  }

  public sealed class InventoryWritePlan {
    public InventoryWriteRequest Request { get; }
    public InventoryWriteIdentity Target => Request?.Target;
    public string TargetSummary => Target == null ? global::TimberbornTrainer.Localization.TextToken.Key("t0278") : Target.Scene + " / " + Target.Entity + " / "
      + Target.Component + " / " + Target.ComponentInstance + " / " + Target.District + " / " + Target.Resource;
    public string SnapshotId { get; }
    public InventoryWriteState State { get; }
    public InventoryWriteValidation Validation { get; }
    public long? Before { get; }
    public long? ExpectedAfter { get; }
    public long Delta { get; }
    public long PlannedAdd => Delta > 0 ? Delta : 0;
    public long PlannedRemove => Delta < 0 ? -Delta : 0;
    public bool RequiresLiveRevalidation => true;
    public bool IsExecutionAuthorized => false;
    internal InventoryWritePlan(InventoryWriteRequest request, InventoryWriteSnapshot snapshot,
        InventoryWriteState state, InventoryWriteValidation validation, long? expected = null, long delta = 0) {
      if (state != InventoryWriteState.Planned && state != InventoryWriteState.NoOp && state != InventoryWriteState.Rejected)
        throw new ArgumentOutOfRangeException(nameof(state), global::TimberbornTrainer.Localization.TextToken.Key("t0206"));
      Request = request; SnapshotId = snapshot?.SnapshotId; Before = snapshot?.Amount;
      State = state; Validation = validation; ExpectedAfter = expected; Delta = delta;
    }
  }

  public static class InventoryWritePlanner {
    public static InventoryWritePlan Create(InventoryWriteRequest request, InventoryWriteSnapshot snapshot) {
      var valid = InventoryWriteTargetValidator.Validate(request, snapshot);
      if (!valid.Accepted) return new InventoryWritePlan(request, snapshot, InventoryWriteState.Rejected, valid);
      Func<string, string, InventoryWritePlan> reject = (code, reason) => new InventoryWritePlan(request, snapshot,
        InventoryWriteState.Rejected, InventoryWriteTargetValidator.Reject(code, reason));
      long n = request.Quantity;
      if (n < 0 || n > int.MaxValue) return reject("InvalidQuantity", global::TimberbornTrainer.Localization.TextToken.Key("t0355"));
      if ((request.Operation == InventoryWriteOperation.Add || request.Operation == InventoryWriteOperation.Remove) && n == 0)
        return reject("PositiveQuantityRequired", global::TimberbornTrainer.Localization.TextToken.Key("t0448"));
      if (request.Operation == InventoryWriteOperation.Clear && n != 0) return reject("ClearTargetMustBeZero", global::TimberbornTrainer.Localization.TextToken.Key("t0301"));
      if (!snapshot.Amount.HasValue || !snapshot.Unreserved.HasValue) return reject("UnknownStock", global::TimberbornTrainer.Localization.TextToken.Key("t0348"));
      long before = snapshot.Amount.Value, unreserved = snapshot.Unreserved.Value;
      if (before < 0 || before > int.MaxValue || unreserved < 0 || unreserved > before)
        return reject("InvalidStock", global::TimberbornTrainer.Localization.TextToken.Key("t0251"));
      if (snapshot.FreeCapacity.HasValue && (snapshot.FreeCapacity < 0 || snapshot.FreeCapacity > int.MaxValue))
        return reject("InvalidCapacity", global::TimberbornTrainer.Localization.TextToken.Key("t0374"));
      if (snapshot.TotalAmount.HasValue && (snapshot.TotalAmount < before || snapshot.TotalAmount > int.MaxValue))
        return reject("InvalidTotal", global::TimberbornTrainer.Localization.TextToken.Key("t0249"));
      long target;
      switch (request.Operation) {
        case InventoryWriteOperation.Add: target = before + n; break;
        case InventoryWriteOperation.Remove: target = before - n; break;
        case InventoryWriteOperation.Set: target = n; break;
        default: target = 0; break;
      }
      if (target < 0) return reject("InsufficientStock", global::TimberbornTrainer.Localization.TextToken.Key("t0347"));
      if (target > int.MaxValue) return reject("Overflow", global::TimberbornTrainer.Localization.TextToken.Key("t0279"));
      long delta = target - before;
      if (delta > 0) {
        var capability = InventoryWriteTargetValidator.Capability(snapshot.Capabilities.Add, global::TimberbornTrainer.Localization.TextToken.Key("t0446"));
        if (!capability.Accepted) return new InventoryWritePlan(request, snapshot, InventoryWriteState.Rejected, capability);
        if (snapshot.ResourceAllowed != true) return reject("ResourceNotAllowed", global::TimberbornTrainer.Localization.TextToken.Key("t0467"));
        if (!snapshot.FreeCapacity.HasValue || !snapshot.TotalAmount.HasValue) return reject("UnknownCapacity", global::TimberbornTrainer.Localization.TextToken.Key("t0375"));
        if (delta > snapshot.FreeCapacity.Value) return reject("InsufficientCapacity", global::TimberbornTrainer.Localization.TextToken.Key("t0372"));
        if (snapshot.TotalAmount.Value + delta > int.MaxValue) return reject("Overflow", global::TimberbornTrainer.Localization.TextToken.Key("t0189"));
      } else if (delta < 0) {
        var capability = InventoryWriteTargetValidator.Capability(snapshot.Capabilities.Remove, global::TimberbornTrainer.Localization.TextToken.Key("t0208"));
        if (!capability.Accepted) return new InventoryWritePlan(request, snapshot, InventoryWriteState.Rejected, capability);
        if (-delta > unreserved) return reject("ReservedStock", global::TimberbornTrainer.Localization.TextToken.Key("t0120"));
      }
      return new InventoryWritePlan(request, snapshot, delta == 0 ? InventoryWriteState.NoOp : InventoryWriteState.Planned,
        new InventoryWriteValidation("OK", delta == 0 ? global::TimberbornTrainer.Localization.TextToken.Key("t0391") : global::TimberbornTrainer.Localization.TextToken.Key("t0207")), target, delta);
    }
  }

  // There is deliberately no executor, mutation method, or Succeeded factory in this stage.
  public sealed class InventoryWriteResult {
    public InventoryWriteState State { get; }
    public string Message { get; }
    public long? After { get; }
    public bool ConfirmedSuccess => State == InventoryWriteState.Succeeded;
    public bool IsRejected => State == InventoryWriteState.Rejected;
    public bool AutomaticRetryAllowed => false;
    public bool AutomaticRollbackAllowed => false;
    private InventoryWriteResult(InventoryWriteState state, string message, long? after) { State = state; Message = message; After = after; }
    public static InventoryWriteResult FromPlan(InventoryWritePlan plan) {
      if (plan == null) throw new ArgumentNullException(nameof(plan));
      return new InventoryWriteResult(plan.State, plan.Validation.Reason, null);
    }
    public static InventoryWriteResult UnconfirmedObservation(string message, long? after = null) =>
      new InventoryWriteResult(InventoryWriteState.Indeterminate, message ?? global::TimberbornTrainer.Localization.TextToken.Key("t0220"), after);
  }

  // Future implementation must resolve native references, verify their actual ownership/roles,
  // and mint fresh instance evidence inside the same safe execution context as any mutation.
  // Reading through this contract or obtaining a plan still grants no execution authority.
  public interface IInventoryWritePreflightSource {
    InventoryWriteSnapshot ResolveAndReadFresh(InventoryWriteRequest intent);
  }
}
