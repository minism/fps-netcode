using UnityEngine;

public interface ISimulationAdjuster {
  float AdjustedInterval { get; }
}

public class NoopAdjuster : ISimulationAdjuster {
  public float AdjustedInterval { get; } = 1.0f;
}

public abstract class BaseSimulation {
  // The current world tick of the simulation.
  public uint WorldTick { get; protected set; } = 0;

  // The interval for world update ticks (inverse of tickrate).
  // This is currently always constant, but will eventually need to fluctuate on the client
  // during re-sync events with the server.
  // Note that this may be independent of network transmission rates (which is confusingly
  // often called the 'tick rate' in the gaming community).
  protected float tickInterval = Settings.SimulationTickInterval;

  // Managers.
  protected PlayerManager playerManager;
  protected NetworkObjectManager networkObjectManager;

  // Simulation adjuster delegate which can be reassigned.
  protected ISimulationAdjuster simulationAdjuster = new NoopAdjuster();

  private float accumulator;

  private InterpolationController interpController;

  protected BaseSimulation(PlayerManager playerManager, NetworkObjectManager networkObjectManager) {
    this.playerManager = playerManager;
    this.networkObjectManager = networkObjectManager;

    // TODO: Handle this dependency better.
    interpController = GameObject.FindObjectOfType<InterpolationController>();
  }

  public void Update(float dt) {
    accumulator += dt;
    var adjustedTickInterval = tickInterval * simulationAdjuster.AdjustedInterval;
    while (accumulator >= adjustedTickInterval) {
      accumulator -= adjustedTickInterval;

      interpController.ExplicitFixedUpdate(tickInterval);

      // Although we can run the simulation at different speeds, the actual tick processing is
      // *always* done with the original unmodified rate for physics accuracy.
      // This has a time-warping effect.
      Tick(tickInterval);
    }
    interpController.ExplicitUpdate(dt);
    PostUpdate();
  }

  protected abstract void Tick(float dt);

  protected virtual void PostUpdate() { }

  protected void SimulateWorld(float dt) {
    playerManager.GetPlayers().ForEach(p => p.Controller.Simulate(dt));
  }
}
