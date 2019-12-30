using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Client world simulation including prediction and state rewind.
// Inputs are state frames from the server.
// Outputs are player command frames to the server.
public class ServerSimulation : BaseSimulation {
  // Debugging.
  public float debugPhysicsErrorChance;

  // Player stuff.
  private PlayerManager playerManager;

  // Delegate for processing input packets.
  private PlayerInputProcessor playerInputProcessor;

  // Snapshot buffers for input and state used for prediction & replay.
  //private PlayerInputs[] localPlayerInputsSnapshots = new PlayerInputs[1024];
  //private PlayerState[] localPlayerStateSnapshots = new PlayerState[1024];

  // The latest world tick that has been simulated.
  private uint lastSimulatedWorldTick = 0;

  // I/O interface for world states.
  public interface Handler {
    void BroadcastWorldState(NetCommand.WorldState state);
  }
  private Handler handler;

  public ServerSimulation(
      float debugPhysicsErrorChance,
      PlayerManager playerManager,
      Handler handler) : base(playerManager) {
    this.debugPhysicsErrorChance = debugPhysicsErrorChance;
    this.playerManager = playerManager;
    this.handler = handler;
    playerInputProcessor = new PlayerInputProcessor();
  }

  public void Update(float dt) {
    // Fixed timestep loop.
    accumulator += dt;
    while (accumulator >= Time.fixedDeltaTime) {
      accumulator -= Time.fixedDeltaTime;

      // Apply inputs to each player.
      var tickInputs = playerInputProcessor.DequeueInputsForTick(WorldTick);
      foreach (var tickInput in tickInputs) {
        var player = tickInput.Player;
        player.Controller.SetPlayerInputs(tickInput.Inputs);
      }
      if (tickInputs.Count < 1 && playerManager.GetPlayers().Count > 0) {
        Debug.LogWarning("No inputs for player!");
      }

      // TODO: Check for missing inputs and notify player here.

      // Advance the world simulation.
      SimulateWorld(Time.fixedDeltaTime);
      if (Random.value < debugPhysicsErrorChance) {
        Debug.Log("Injecting random physics error.");
        playerManager.GetPlayers().ForEach(
            p => p.GameObject.transform.Translate(new Vector3(1, 0, 0)));
      }
      ++WorldTick;

      // Broadcast the world state.
      // The new world state tick is N+1, given input world tick N.
      var worldStateCmd = new NetCommand.WorldState {
        WorldTick = WorldTick,
        PlayerStates = playerManager.GetPlayers().Select(
            p => p.Controller.ToNetworkState()).ToArray(),
      };
      handler.BroadcastWorldState(worldStateCmd);

      // Monitoring.
      DebugUI.ShowValue("tick", WorldTick);
    }
  }

  public void EnqueuePlayerInput(WithPeer<NetCommand.PlayerInput> input) {
    var player = playerManager.GetPlayerForPeer(input.Peer);
    playerInputProcessor.EnqueueInput(input.Value, player, WorldTick);
  }
}
