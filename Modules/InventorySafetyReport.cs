using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TimberbornTrainer.Modules {
  public enum InventoryIdentityStatus { Valid, Invalid, Unknown }

  // Observation only. Contains no native reference, lease or execution permission.
  public sealed class InventoryIdentityReport {
    public InventoryIdentityStatus Status { get; }
    public string Code { get; }
    public string Reason { get; }
    public string Scene { get; }
    public string District { get; }
    public string Resource { get; }
    public string Observation { get; }
    public bool IsExecutionAuthorized => false;
    internal InventoryIdentityReport(InventoryIdentityStatus status, string code, string reason,
        string scene, string district, string resource) {
      Status = status; Code = code; Reason = reason; Scene = scene ?? "";
      District = district ?? "未知"; Resource = resource ?? "";
      Observation = Guid.NewGuid().ToString("N");
    }
  }

  public sealed class InventoryReservationRow {
    public string Resource { get; }
    public long Amount { get; }
    public long Unreserved { get; }
    public long Reserved => Amount - Unreserved;
    public long CapacityReservation { get; }
    public long FreeCapacity { get; }
    public long Limit { get; }
    public bool Declared { get; }
    public bool Registered { get; }
    internal InventoryReservationRow(string resource, long amount, long unreserved, long capacityReservation,
        long freeCapacity, long limit, bool declared, bool registered) {
      Resource = resource; Amount = amount; Unreserved = unreserved; CapacityReservation = capacityReservation;
      FreeCapacity = freeCapacity; Limit = limit; Declared = declared; Registered = registered;
    }
  }
  public sealed class InventorySafetyReport {
    public InventoryIdentityReport Identity { get; }
    public IReadOnlyList<InventoryReservationRow> Rows { get; }
    public bool ObservationConsistent { get; }
    public long? ReservedStock { get; }
    public long? ReservedCapacity { get; }
    public string Reason { get; }
    public DateTime ObservedUtc { get; } = DateTime.UtcNow;
    public bool ObservedZeroReservations => ObservationConsistent && ReservedStock == 0 && ReservedCapacity == 0;
    // Public observations are not an atomic native snapshot or an exhaustive private-registry proof.
    public bool UnknownReservationState => true;
    public bool CandidateEligible => false;
    public bool IsExecutionAuthorized => false;
    internal InventorySafetyReport(InventoryIdentityReport identity, IEnumerable<InventoryReservationRow> rows,
        bool consistent, long? reserved, long? capacityReserved, string reason) {
      Identity = identity; Rows = new ReadOnlyCollection<InventoryReservationRow>((rows ?? Enumerable.Empty<InventoryReservationRow>()).ToArray());
      ObservationConsistent = consistent; ReservedStock = reserved; ReservedCapacity = capacityReserved; Reason = reason;
    }
  }
  public interface IInventorySafetyDiagnostics {
    InventorySafetyReport DiagnoseSafety(string targetId, string goodId);
  }
}
