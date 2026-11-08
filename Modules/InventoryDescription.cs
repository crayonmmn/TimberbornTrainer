using System;

namespace TimberbornTrainer.Modules {
  public enum InventorySource { Settlement, DistrictCenter, Building }
  public enum CapacityState { Finite, Unlimited, Unknown, Aggregate }

  public sealed class InventoryAbility {
    public bool Available { get; }
    public string Reason { get; }
    public InventoryAbility(bool available, string reason) { Available = available; Reason = reason ?? ""; }
  }

  // Read-only contract: deliberately contains no mutation methods or native object references.
  public sealed class InventoryAbilities {
    public InventoryAbility Read { get; }
    public InventoryAbility Add { get; }
    public InventoryAbility Subtract { get; }
    public InventoryAbility Set { get; }
    public InventoryAbilities(bool readable, string reason) {
      Read = new InventoryAbility(readable, readable ? "按需读取原生数据" : reason);
      Add = new InventoryAbility(false, reason);
      Subtract = new InventoryAbility(false, reason);
      Set = new InventoryAbility(false, reason);
    }
  }

  public sealed class InventoryTarget {
    public string Id { get; set; }
    public string OwnerId { get; set; }
    public string OwnerName { get; set; }
    public string BuildingType { get; set; }
    public string DisplayName { get; set; }
    public string Purpose { get; set; }
    public InventorySource Source { get; set; }
    public InventoryAbilities Abilities { get; set; }
  }

  public sealed class InventoryResource {
    public string GoodId { get; set; }
    public string DisplayName { get; set; }
    public bool Registered { get; set; }
    public bool Allowed { get; set; }
    public long Amount { get; set; }
    public long? ReservedStock { get; set; }
    public long? AvailableStock { get; set; }
    public long? ReservedCapacity { get; set; }
    public long? FreeCapacity { get; set; }
    public long? ResourceLimit { get; set; }
    public long? TotalCapacity { get; set; }
    public CapacityState CapacityState { get; set; }
    public string Details { get; set; }
  }

  public sealed class InventorySnapshot {
    public InventoryTarget Target { get; set; }
    public InventoryResource[] Resources { get; set; } = Array.Empty<InventoryResource>();
    public bool Valid { get; set; }
    public string Message { get; set; }
  }

  public sealed class InventoryDiscovery {
    public InventoryTarget[] Targets { get; set; } = Array.Empty<InventoryTarget>();
    public string Message { get; set; }
  }

  public interface IInventoryCatalog {
    InventoryDiscovery Discover(InventorySource source);
    InventorySnapshot Read(string targetId);
    bool Focus(string targetId);
    void Clear();
  }

  public static class InventoryIdentity {
    // Length prefixes avoid collisions without relying on user-visible names or order.
    public static string Owner(string scene, string entity) => Part(scene) + Part(entity);
    public static string Target(string scene, string entity, string component) => Owner(scene, entity) + Part(component);
    private static string Part(string value) {
      if (string.IsNullOrEmpty(value)) throw new ArgumentException("库存身份缺失。");
      return value.Length + ":" + value;
    }
  }
}
