using System.Linq;
using Timberborn.BlockSystem;
using Timberborn.ConstructionSites;
using Timberborn.EntitySystem;
using Timberborn.SingletonSystem;
using TimberbornTrainer.Core;
using UnityEngine;

namespace TimberbornTrainer.Modules {
  public sealed class BuildingModule : IUnloadableSingleton {
    private TrainerSession _session;
    private EntityRegistry _entities;
    private EventBus _events;
    private bool _unloaded;
    private readonly ConstructionCommands _commands;
    public ConstructionResult LastResult { get; private set; }
    public bool Busy => _commands.Busy;
    public bool RuntimeError => LastResult != null && (LastResult.Errors > 0 || LastResult.Unconfirmed > 0);

    public BuildingModule(TrainerSession session, EntityRegistry entities, EventBus events) {
      _session = session; _entities = entities; _events = events;
      _commands = new ConstructionCommands(() => !_unloaded && _session?.State != null && _session.State.EffectiveEnabled);
    }

    public ConstructionResult CompleteAll() {
      var result = _commands.Execute(() => {
        // Finishing can mutate the registry: snapshot before inspecting or finishing any entity.
        var entities = _entities.Entities.ToArray();
        return entities.Select(entity => (IConstructionTarget)new NativeSite(_entities, _events, entity)).ToArray();
      }, e => Debug.LogError("海狸修改工具：批量完工异常（" + e.GetType().Name +
        "）；工地可能已变化，不自动重试或回滚。"));
      if (!Busy) LastResult = result;
      return result;
    }

    public void Unload() {
      _unloaded = true;
      _session = null; _entities = null; _events = null; LastResult = null;
    }

    private sealed class NativeSite : IConstructionTarget {
      private readonly EntityRegistry _registry;
      private readonly EventBus _events;
      private readonly EntityComponent _entity;
      private ConstructionSite _site;
      private BlockObject _block;
      public NativeSite(EntityRegistry registry, EventBus events, EntityComponent entity) {
        _registry = registry; _events = events; _entity = entity;
      }
      public ConstructionTargetState Inspect() {
        if (!_entity || _entity.Deleted || !_entity.Initialized ||
            !ReferenceEquals(_registry.GetEntity(_entity.EntityId), _entity)) return ConstructionTargetState.Invalid;
        _site = _entity.GetComponent<ConstructionSite>();
        _block = _entity.GetComponent<BlockObject>();
        if (!_site || !_block || !_block.IsUnfinished) return ConstructionTargetState.NotApplicable;
        if (!_site.Enabled || !_site.Inventory || !_site.Inventory.Enabled) return ConstructionTargetState.Invalid;
        return _site.IsOn ? ConstructionTargetState.Ready : ConstructionTargetState.Blocked;
      }
      public ConstructionCompletion FinishNow() {
        if (Inspect() != ConstructionTargetState.Ready) return ConstructionCompletion.Skipped;
        var block = _block;
        var observer = new CompletionObserver(block);
        _events.Register(observer);
        try {
          _site.FinishNow();
          // DeleteOnFinish sites may disappear after the native finished-state event.
          if (observer.Finished || (block && block.IsFinished)) return ConstructionCompletion.Completed;
          if (block && !_entity.Deleted && block.IsUnfinished) return ConstructionCompletion.Unfinished;
          return ConstructionCompletion.Unconfirmed;
        } finally { _events.Unregister(observer); }
      }
    }

    private sealed class CompletionObserver {
      private readonly BlockObject _block;
      public bool Finished { get; private set; }
      public CompletionObserver(BlockObject block) { _block = block; }
      [OnEvent]
      public void OnFinished(EnteredFinishedStateEvent e) {
        if (ReferenceEquals(e.BlockObject, _block)) Finished = true;
      }
    }
  }
}
