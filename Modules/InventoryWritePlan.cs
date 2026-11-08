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
      if (permission == InventoryPermission.Allowed) return new InventoryWriteValidation("OK", "预检查通过，尚未执行。");
      return Reject(permission == InventoryPermission.Denied ? "CapabilityDenied" : "CapabilityUnknown",
        operation + (permission == InventoryPermission.Denied ? "能力不可用。" : "能力未知。"));
    }
    public static InventoryWriteValidation Validate(InventoryWriteRequest request, InventoryWriteSnapshot snapshot) {
      if (request == null || snapshot == null) return Reject("MissingInput", "请求或库存快照缺失。");
      if (!Enum.IsDefined(typeof(InventoryWriteOperation), request.Operation)) return Reject("UnknownOperation", "操作类型未知。");
      var expected = request.Target; var actual = snapshot.Identity;
      if (!Complete(expected) || !Complete(actual)) return Reject("MissingIdentity", "场景、实体、组件实例、地区或资源身份缺失。");
      if (expected.Scene != actual.Scene) return Reject("SceneChanged", "场景已切换，请重新选择。");
      if (expected.Entity != actual.Entity || expected.OwnerInstance != actual.OwnerInstance) return Reject("EntityChanged", "建筑实体已变化或已重建。");
      if (expected.Inventory != actual.Inventory || expected.Component != actual.Component) return Reject("InventoryChanged", "库存身份不一致。");
      if (expected.ComponentInstance != actual.ComponentInstance) return Reject("ComponentReplaced", "库存组件已替换。");
      if (expected.Use != actual.Use) return Reject("UseChanged", "库存用途已变化。");
      if (expected.District != actual.District) return Reject("DistrictChanged", "地区归属已变化。");
      if (expected.Resource != actual.Resource) return Reject("ResourceChanged", "资源身份不一致。");
      if (actual.Use != InventoryUse.Warehouse) return Reject("UnsupportedUse", "仅明确识别的普通仓储可进行写入预检查。");
      if (!Present(snapshot.SnapshotId)) return Reject("UnknownSnapshot", "快照有效性标识缺失。");
      if (snapshot.Valid != true) return Reject("InvalidTarget", "目标已失效或有效性未知。");
      if (snapshot.Finished != true || snapshot.Enabled != true || snapshot.VerifiedNativeWarehouse != true)
        return Reject("UnsafeWarehouse", "未确认已完工、启用的原生普通仓储身份。");
      if (snapshot.ResourceRegistered != true) return Reject("UnknownResource", "资源未注册或注册状态未知。");
      if (snapshot.Capabilities == null) return Reject("CapabilityUnknown", "操作能力缺失。");
      var read = Capability(snapshot.Capabilities.Read, "读取");
      return !read.Accepted ? read : Capability(snapshot.Capabilities.For(request.Operation), request.Operation.ToString());
    }
  }

  public sealed class InventoryWritePlan {
    public InventoryWriteRequest Request { get; }
    public InventoryWriteIdentity Target => Request?.Target;
    public string TargetSummary => Target == null ? "目标缺失" : Target.Scene + " / " + Target.Entity + " / "
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
        throw new ArgumentOutOfRangeException(nameof(state), "计划不能表示执行结果。");
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
      if (n < 0 || n > int.MaxValue) return reject("InvalidQuantity", "数量必须在非负原生整数范围内。");
      if ((request.Operation == InventoryWriteOperation.Add || request.Operation == InventoryWriteOperation.Remove) && n == 0)
        return reject("PositiveQuantityRequired", "增加和减少数量必须大于零。");
      if (request.Operation == InventoryWriteOperation.Clear && n != 0) return reject("ClearTargetMustBeZero", "清空的目标数量必须为零。");
      if (!snapshot.Amount.HasValue || !snapshot.Unreserved.HasValue) return reject("UnknownStock", "实际数量或未预留数量未知。");
      long before = snapshot.Amount.Value, unreserved = snapshot.Unreserved.Value;
      if (before < 0 || before > int.MaxValue || unreserved < 0 || unreserved > before)
        return reject("InvalidStock", "库存或预留数量不自洽。");
      if (snapshot.FreeCapacity.HasValue && (snapshot.FreeCapacity < 0 || snapshot.FreeCapacity > int.MaxValue))
        return reject("InvalidCapacity", "未预留容量超出原生范围。");
      if (snapshot.TotalAmount.HasValue && (snapshot.TotalAmount < before || snapshot.TotalAmount > int.MaxValue))
        return reject("InvalidTotal", "库存共享总量不自洽。");
      long target;
      switch (request.Operation) {
        case InventoryWriteOperation.Add: target = before + n; break;
        case InventoryWriteOperation.Remove: target = before - n; break;
        case InventoryWriteOperation.Set: target = n; break;
        default: target = 0; break;
      }
      if (target < 0) return reject("InsufficientStock", "实际库存不足，整单拒绝。");
      if (target > int.MaxValue) return reject("Overflow", "目标数量超过原生整数范围。");
      long delta = target - before;
      if (delta > 0) {
        var capability = InventoryWriteTargetValidator.Capability(snapshot.Capabilities.Add, "增加");
        if (!capability.Accepted) return new InventoryWritePlan(request, snapshot, InventoryWriteState.Rejected, capability);
        if (snapshot.ResourceAllowed != true) return reject("ResourceNotAllowed", "资源不允许进入该库存或允许状态未知。");
        if (!snapshot.FreeCapacity.HasValue || !snapshot.TotalAmount.HasValue) return reject("UnknownCapacity", "未预留容量或共享总量未知。");
        if (delta > snapshot.FreeCapacity.Value) return reject("InsufficientCapacity", "未预留容量不足，整单拒绝。");
        if (snapshot.TotalAmount.Value + delta > int.MaxValue) return reject("Overflow", "共享总量超过原生整数范围。");
      } else if (delta < 0) {
        var capability = InventoryWriteTargetValidator.Capability(snapshot.Capabilities.Remove, "减少");
        if (!capability.Accepted) return new InventoryWritePlan(request, snapshot, InventoryWriteState.Rejected, capability);
        if (-delta > unreserved) return reject("ReservedStock", "不能移除已预留物品，整单拒绝。");
      }
      return new InventoryWritePlan(request, snapshot, delta == 0 ? InventoryWriteState.NoOp : InventoryWriteState.Planned,
        new InventoryWriteValidation("OK", delta == 0 ? "无需修改，尚未执行任何操作。" : "计划通过；执行前必须读取实时状态重新校验。"), target, delta);
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
      new InventoryWriteResult(InventoryWriteState.Indeterminate, message ?? "结果无法确认；不得自动重试或回滚。", after);
  }

  // Future implementation must resolve native references, verify their actual ownership/roles,
  // and mint fresh instance evidence inside the same safe execution context as any mutation.
  // Reading through this contract or obtaining a plan still grants no execution authority.
  public interface IInventoryWritePreflightSource {
    InventoryWriteSnapshot ResolveAndReadFresh(InventoryWriteRequest intent);
  }
}
