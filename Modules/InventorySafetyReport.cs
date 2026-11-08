using System;

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
}
