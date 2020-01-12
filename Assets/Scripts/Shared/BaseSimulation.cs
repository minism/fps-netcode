using UnityEngine;

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

  private float accumulator;

  protected BaseSimulation(PlayerManager playerManager, NetworkObjectManager networkObjectManager) {
    this.playerManager = playerManager;
    this.networkObjectManager = networkObjectManager;
  }

  public void Update(float dt) {
    accumulator += dt;
    while (accumulator >= tickInterval) {
      accumulator -= tickInterval;
      Tick(tickInterval);
    }
    PostUpdate();
  }

  protected abstract void Tick(float dt);

  protected virtual void PostUpdate() { }

  protected void SimulateWorld(float dt) {
    playerManager.GetPlayers().ForEach(p => p.Controller.Simulate(dt));
  }
}
