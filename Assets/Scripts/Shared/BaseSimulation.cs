using UnityEngine;

public abstract class BaseSimulation {
  // The current world tick of the simulation.
  public uint WorldTick { get; protected set; } = 0;

  // The interval for world update ticks (inverse of tickrate).
  // This is currently always constant, but will eventually need to fluctuate on the client
  // during re-sync events with the server.
  protected float tickInterval = Time.fixedDeltaTime;

  // The player manager.
  protected PlayerManager playerManager;

  private float accumulator;

  protected BaseSimulation(PlayerManager playerManager) {
    this.playerManager = playerManager;
  }

  public void Update(float dt) {
    accumulator += dt;
    while (accumulator >= tickInterval) {
      accumulator -= tickInterval;
      Tick();
    }
    PostUpdate();
  }

  protected abstract void Tick();

  protected virtual void PostUpdate() { }

  protected void SimulateWorld(float dt) {
    playerManager.GetPlayers().ForEach(p => p.Controller.Simulate(dt));
  }
}
