using UnityEngine;
using System.Collections;

public abstract class BaseSimulation {
  // The current world tick of the simulation.
  public uint WorldTick { get; protected set; } = 0;

  // Fixed timing accumulator.
  protected float accumulator;

  // The player manager.
  protected PlayerManager playerManager;

  protected BaseSimulation(PlayerManager playerManager) {
    this.playerManager = playerManager;
  }

  protected void SimulateWorld(float dt) {
    playerManager.GetPlayers().ForEach(p => p.Controller.Simulate(dt));
  }
}
