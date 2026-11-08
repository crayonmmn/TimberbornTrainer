using System;
using System.Linq;
using TimberbornTrainer.Modules;

namespace TimberbornTrainer.Tests {
  public static class InventoryWritePlanTests {
    private static int _checks;
    private static void Check(bool value, string message) { ++_checks; if (!value) throw new Exception(message); }
    private static InventoryWriteIdentity Id(string scene = "scene", string entity = "entity", string owner = "owner-1",
        string inventory = "composite", string component = "Stockpile", string instance = "component-1",
        InventoryUse use = InventoryUse.Warehouse, string district = "district-A", string good = "Water") =>
      new InventoryWriteIdentity(scene, entity, owner, inventory, component, instance, use, district, good);
    private sealed class Fixture {
      public InventoryWriteIdentity Identity = Id();
      public string Revision = "snapshot-1";
      public bool? Valid = true, Finished = true, Enabled = true, Native = true, Registered = true, Allowed = true;
      public long? Amount = 10, Unreserved = 10, Free = 90, Total = 10;
      public InventoryPermission Read = InventoryPermission.Allowed, Add = InventoryPermission.Allowed,
        Remove = InventoryPermission.Allowed, Set = InventoryPermission.Allowed, Clear = InventoryPermission.Allowed;
      public bool MissingCapabilities;
      public InventoryWriteSnapshot Snapshot() => new InventoryWriteSnapshot(Identity, Revision, Valid, Finished,
        Enabled, Native, Registered, Allowed, Amount, Unreserved, Free, Total, MissingCapabilities ? null :
        new InventoryWriteCapabilities(Read, Add, Remove, Set, Clear));
    }
    private static InventoryWritePlan Plan(Fixture f, InventoryWriteOperation op, long n, InventoryWriteIdentity expected = null) =>
      InventoryWritePlanner.Create(new InventoryWriteRequest(expected ?? f.Identity, op, n), f.Snapshot());
    private static void Reject(Fixture f, InventoryWriteOperation op, long n, string code, InventoryWriteIdentity expected = null) {
      var p = Plan(f, op, n, expected);
      Check(p.State == InventoryWriteState.Rejected && p.Validation.Code == code && !string.IsNullOrWhiteSpace(p.Validation.Reason)
        && p.Delta == 0 && p.ExpectedAfter == null, "拒绝原因/整单无计划量：" + code + "，实际" + p.Validation.Code);
    }
    public static string Run() {
      _checks = 0;
      var f = new Fixture();
      var p = Plan(f, InventoryWriteOperation.Add, 5);
      Check(p.State == InventoryWriteState.Planned && p.Before == 10 && p.ExpectedAfter == 15 && p.PlannedAdd == 5 && p.PlannedRemove == 0, "正常增加");
      p = Plan(f, InventoryWriteOperation.Remove, 3);
      Check(p.State == InventoryWriteState.Planned && p.ExpectedAfter == 7 && p.PlannedRemove == 3 && p.PlannedAdd == 0, "正常减少");
      p = Plan(f, InventoryWriteOperation.Set, 15);
      Check(p.State == InventoryWriteState.Planned && p.Delta == 5, "设置只计划差额增加");
      p = Plan(f, InventoryWriteOperation.Set, 7);
      Check(p.State == InventoryWriteState.Planned && p.Delta == -3, "设置只计划差额减少");
      p = Plan(f, InventoryWriteOperation.Clear, 0);
      Check(p.State == InventoryWriteState.Planned && p.ExpectedAfter == 0 && p.PlannedRemove == 10, "全量清空");
      f.Free = 4; Reject(f, InventoryWriteOperation.Add, 5, "InsufficientCapacity");
      Reject(f, InventoryWriteOperation.Set, 15, "InsufficientCapacity");
      f = new Fixture { Unreserved = 2 };
      Reject(f, InventoryWriteOperation.Remove, 3, "ReservedStock");
      Reject(f, InventoryWriteOperation.Set, 7, "ReservedStock");
      Reject(f, InventoryWriteOperation.Clear, 0, "ReservedStock");
      Check(Plan(f, InventoryWriteOperation.Remove, 2).Delta == -2, "仅规划未预留部分；不代表首批实机策略放行");
      Reject(f, InventoryWriteOperation.Remove, 11, "InsufficientStock");
      f = new Fixture();
      Check(Plan(f, InventoryWriteOperation.Set, 10).State == InventoryWriteState.NoOp, "设置相同值NoOp");
      f.Amount = f.Unreserved = f.Total = 0;
      Check(Plan(f, InventoryWriteOperation.Clear, 0).State == InventoryWriteState.NoOp, "空库存清空NoOp");
      foreach (var op in new[] { InventoryWriteOperation.Add, InventoryWriteOperation.Remove }) Reject(f, op, 0, "PositiveQuantityRequired");
      foreach (InventoryWriteOperation op in Enum.GetValues(typeof(InventoryWriteOperation))) Reject(f, op, -1, "InvalidQuantity");
      Reject(f, InventoryWriteOperation.Clear, 1, "ClearTargetMustBeZero");
      Reject(f, InventoryWriteOperation.Set, long.MaxValue, "InvalidQuantity");
      Reject(f, InventoryWriteOperation.Add, (long)int.MaxValue + 1, "InvalidQuantity");
      f = new Fixture { Amount = int.MaxValue, Unreserved = int.MaxValue, Total = int.MaxValue };
      Reject(f, InventoryWriteOperation.Add, 1, "Overflow");
      Check(Plan(f, InventoryWriteOperation.Clear, 0).PlannedRemove == int.MaxValue, "清空最大原生数量不溢出");
      f = new Fixture { Total = int.MaxValue };
      Reject(f, InventoryWriteOperation.Add, 1, "Overflow");
      f = new Fixture { Amount = 0, Unreserved = 0, Total = 0, Free = int.MaxValue };
      Check(Plan(f, InventoryWriteOperation.Add, int.MaxValue).ExpectedAfter == int.MaxValue, "原生整数上界可计划，不人为当成无限");
      f = new Fixture { Allowed = false };
      Reject(f, InventoryWriteOperation.Add, 1, "ResourceNotAllowed");
      Check(Plan(f, InventoryWriteOperation.Remove, 1).State == InventoryWriteState.Planned, "已注册旧库存可方向性减少，不能因不可存入而隐式允许增加");
      f.Allowed = null; Reject(f, InventoryWriteOperation.Set, 11, "ResourceNotAllowed");
      f = new Fixture { Free = null }; Reject(f, InventoryWriteOperation.Add, 1, "UnknownCapacity");
      Check(Plan(f, InventoryWriteOperation.Remove, 1).State == InventoryWriteState.Planned, "减少不依赖未知空位");
      f = new Fixture { Total = null }; Reject(f, InventoryWriteOperation.Add, 1, "UnknownCapacity");
      f = new Fixture { Unreserved = null }; Reject(f, InventoryWriteOperation.Clear, 0, "UnknownStock");
      Reject(f, InventoryWriteOperation.Add, 1, "UnknownStock");
      f = new Fixture { Amount = null }; Reject(f, InventoryWriteOperation.Set, 1, "UnknownStock");
      f = new Fixture { Unreserved = 11 }; Reject(f, InventoryWriteOperation.Remove, 1, "InvalidStock");
      f = new Fixture { Unreserved = -1 }; Reject(f, InventoryWriteOperation.Remove, 1, "InvalidStock");
      f = new Fixture { Amount = (long)int.MaxValue + 1 }; Reject(f, InventoryWriteOperation.Clear, 0, "InvalidStock");
      f = new Fixture { Free = -1 }; Reject(f, InventoryWriteOperation.Add, 1, "InvalidCapacity");
      f = new Fixture { Total = 9 }; Reject(f, InventoryWriteOperation.Add, 1, "InvalidTotal");
      f = new Fixture { MissingCapabilities = true }; Reject(f, InventoryWriteOperation.Add, 1, "CapabilityUnknown");
      f = new Fixture { Add = InventoryPermission.Unknown }; Reject(f, InventoryWriteOperation.Add, 1, "CapabilityUnknown");
      f = new Fixture { Read = InventoryPermission.Denied }; Reject(f, InventoryWriteOperation.Add, 1, "CapabilityDenied");
      f = new Fixture { Set = InventoryPermission.Denied }; Reject(f, InventoryWriteOperation.Set, 10, "CapabilityDenied");
      f = new Fixture { Clear = InventoryPermission.Unknown, Amount = 0, Unreserved = 0, Total = 0 };
      Reject(f, InventoryWriteOperation.Clear, 0, "CapabilityUnknown");
      f = new Fixture { Add = InventoryPermission.Denied, Free = 0 };
      Check(Plan(f, InventoryWriteOperation.Set, 7).Delta == -3, "满仓不能增加但可减量设置");
      Reject(f, InventoryWriteOperation.Set, 11, "CapabilityDenied");
      f = new Fixture { Remove = InventoryPermission.Denied };
      Reject(f, InventoryWriteOperation.Clear, 0, "CapabilityDenied");
      f = new Fixture { Registered = null }; Reject(f, InventoryWriteOperation.Remove, 1, "UnknownResource");
      f = new Fixture { Valid = false }; Reject(f, InventoryWriteOperation.Add, 1, "InvalidTarget");
      f.Valid = null; Reject(f, InventoryWriteOperation.Set, 10, "InvalidTarget");
      f = new Fixture { Finished = false }; Reject(f, InventoryWriteOperation.Add, 1, "UnsafeWarehouse");
      f = new Fixture { Enabled = null }; Reject(f, InventoryWriteOperation.Add, 1, "UnsafeWarehouse");
      f = new Fixture { Native = null }; Reject(f, InventoryWriteOperation.Add, 1, "UnsafeWarehouse");
      f = new Fixture { Revision = "" }; Reject(f, InventoryWriteOperation.Add, 1, "UnknownSnapshot");
      var expected = Id();
      foreach (var pair in new[] {
          Tuple.Create(Id(scene: "next-scene"), "SceneChanged"),
          Tuple.Create(Id(entity: "rebuilt"), "EntityChanged"),
          Tuple.Create(Id(owner: "owner-2"), "EntityChanged"),
          Tuple.Create(Id(inventory: "other-inventory"), "InventoryChanged"),
          Tuple.Create(Id(component: "ConstructionSite"), "InventoryChanged"),
          Tuple.Create(Id(instance: "component-2"), "ComponentReplaced"),
          Tuple.Create(Id(use: InventoryUse.ProductionOutput), "UseChanged"),
          Tuple.Create(Id(district: "district-B"), "DistrictChanged"),
          Tuple.Create(Id(good: "ModWater"), "ResourceChanged"),
          Tuple.Create(Id(instance: ""), "MissingIdentity"),
          Tuple.Create(Id(district: null), "MissingIdentity") }) {
        Reject(new Fixture { Identity = pair.Item1 }, InventoryWriteOperation.Add, 1, pair.Item2, expected);
      }
      foreach (InventoryUse use in Enum.GetValues(typeof(InventoryUse))) {
        if (use != InventoryUse.Warehouse) Reject(new Fixture { Identity = Id(use: use) }, InventoryWriteOperation.Clear, 0, "UnsupportedUse");
      }
      Reject(new Fixture(), (InventoryWriteOperation)999, 1, "UnknownOperation");
      Check(InventoryWritePlanner.Create(null, null).Validation.Code == "MissingInput", "空输入拒绝");
      f = new Fixture(); var snapshot = f.Snapshot(); var request = new InventoryWriteRequest(Id(), InventoryWriteOperation.Set, 15);
      p = InventoryWritePlanner.Create(request, snapshot);
      f.Amount = f.Unreserved = f.Total = 20; f.Revision = "snapshot-2";
      var livePlan = InventoryWritePlanner.Create(request, f.Snapshot());
      Check(p.Delta == 5 && livePlan.Delta == -5 && p.SnapshotId == "snapshot-1" && livePlan.SnapshotId == "snapshot-2", "实时重规划不能沿用UI缓存的方向");
      Check(p.RequiresLiveRevalidation && !p.IsExecutionAuthorized && !livePlan.IsExecutionAuthorized, "新旧计划均不是执行授权");
      Check(snapshot.Amount == 10 && p.TargetSummary.Contains("district-A") && p.TargetSummary.Contains("Stockpile"), "快照不可变且身份摘要可追溯");
      var result = InventoryWriteResult.FromPlan(p);
      Check(result.State == InventoryWriteState.Planned && !result.ConfirmedSuccess && result.After == null, "计划不能生成实际成功或伪造回读");
      var unknown = InventoryWriteResult.UnconfirmedObservation(null);
      Check(unknown.State == InventoryWriteState.Indeterminate && !unknown.IsRejected && !unknown.ConfirmedSuccess && unknown.After == null, "未知结果不等于拒绝或成功");
      Check(!unknown.AutomaticRetryAllowed && !unknown.AutomaticRollbackAllowed, "未知结果禁止自动重试及反向回滚");
      var observed = InventoryWriteResult.UnconfirmedObservation("事件异常，可能已生效", 15);
      Check(observed.State == InventoryWriteState.Indeterminate && !observed.ConfirmedSuccess && observed.After == 15, "观察到目标数值也不伪装执行成功");
      bool blocked = false;
      try { new InventoryWritePlan(request, snapshot, InventoryWriteState.Succeeded, new InventoryWriteValidation("OK", "")); }
      catch (ArgumentOutOfRangeException) { blocked = true; }
      Check(blocked, "计划构造器不接受执行成功状态");
      foreach (var type in new[] { typeof(InventoryWriteSnapshot), typeof(InventoryWriteIdentity), typeof(InventoryWriteCapabilities),
          typeof(InventoryWriteRequest), typeof(InventoryWritePlan), typeof(InventoryWriteResult) }) {
        Check(type.GetProperties().All(prop => !prop.CanWrite), type.Name + "无可变属性");
      }
      Check(typeof(IInventoryWritePreflightSource).GetMethods().Length == 1 &&
        typeof(IInventoryWritePreflightSource).GetMethods()[0].ReturnType == typeof(InventoryWriteSnapshot), "未来契约仅解析读取，没有执行入口");
      Check(typeof(InventoryWritePlanner).Assembly.GetReferencedAssemblies().All(a =>
        !a.Name.StartsWith("Unity", StringComparison.Ordinal) && !a.Name.StartsWith("Timberborn.", StringComparison.Ordinal)
        && !a.Name.StartsWith("Bindito", StringComparison.Ordinal) && a.Name != "Newtonsoft.Json"), "纯逻辑测试程序集实际依赖不含游戏、Unity、Bindito或JSON库");
      return "纯逻辑写入计划与身份安全：" + _checks + " 项断言通过（无游戏依赖、无写入执行器）。";
    }
  }
}
