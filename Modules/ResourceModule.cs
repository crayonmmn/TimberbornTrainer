using Timberborn.ScienceSystem;
using TimberbornTrainer.Core;

namespace TimberbornTrainer.Modules {
  // Only science is implemented. Inventory editing remains unavailable.
  public sealed class ResourceModule {
    public ScienceCommands Science { get; }
    public ResourceModule(TrainerSession session, ScienceService scienceService) {
      Science = new ScienceCommands(new NativeSciencePoints(scienceService),
        () => session.State != null && session.State.EffectiveEnabled);
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
