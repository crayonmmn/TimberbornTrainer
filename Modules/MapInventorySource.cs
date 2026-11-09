namespace TimberbornTrainer.Modules {
  public interface IMapInventorySource {
    InventorySnapshot ReadSelectedWarehouse();
    void ReleaseSelectedWarehouse();
  }
}
