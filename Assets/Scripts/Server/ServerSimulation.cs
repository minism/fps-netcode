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

  // Snapshot buffers for input and state used for prediction & replay.
  private PlayerInputs[] localPlayerInputsSnapshots = new PlayerInputs[1024];
  private PlayerState[] localPlayerStateSnapshots = new PlayerState[1024];

  // Queue for incoming player input commands.
  // These are processed explicitly in a fixed update loop.
  private Queue<WithPeer<NetCommand.PlayerInput>> playerInputQueue =
      new Queue<WithPeer<NetCommand.PlayerInput>>();

  // The latest world tick that has been simulated.
  private uint lastSimulatedWorldTick = 0;

  // I/O interface for world states.
  public interface Handler {
    void BroadcastWorldState(NetCommand.WorldState state);
  }
  private Handler handler;

  // Exported monitoring statistics.
  public struct Stats {
    public int maxInputQueueSize;
    public int maxInputArraySize;
  }
  public Stats stats;

  public ServerSimulation(
      float debugPhysicsErrorChance,
      PlayerManager playerManager,
      Handler handler) : base(playerManager) {
    this.debugPhysicsErrorChance = debugPhysicsErrorChance;
    this.handler = handler;
    stats = new Stats();
  }

  public void Update(float dt) {
    // Process the player input queue.
    // TODO: An optimization would be to make this a priority queue, and process
    // matching world ticks in lock-step.  I don't actually know how often that
    // would happen though.
    while (playerInputQueue.Count > 0) {
      // Monitoring.
      if (playerInputQueue.Count > stats.maxInputQueueSize) {
        stats.maxInputQueueSize = playerInputQueue.Count;
      }

      var entry = playerInputQueue.Dequeue();
      var player = playerManager.GetPlayerForPeer(entry.Peer);
      var command = entry.Value;

      // Monitoring.
      if (command.Inputs.Length > stats.maxInputArraySize) {
        stats.maxInputArraySize = command.Inputs.Length;
      }
      if (command.StartWorldTick % 100 == 0) {
        //Debug.Log($"Beginning of tick {command.WorldTick} = {player.GameObject.transform.position}");
      }

      // Calculate the last tick in the incoming command.
      uint maxTick = command.StartWorldTick + (uint)command.Inputs.Length - 1;

      // Check if there are new inputs to simulate.
      if (maxTick >= lastSimulatedWorldTick) {
        uint start = lastSimulatedWorldTick > command.StartWorldTick
            ? lastSimulatedWorldTick - command.StartWorldTick : 0;
        for (int i = (int)start; i < command.Inputs.Length; ++i) {
          // Apply inputs to the associated player controller and simulate the world.
          player.Controller.SetPlayerInputs(command.Inputs[i]);
          SimulateWorld(Time.fixedDeltaTime);
          if (Random.value < debugPhysicsErrorChance) {
            Debug.Log("Injecting random physics error.");
            player.GameObject.transform.Translate(new Vector3(1, 0, 0));
          }
        }
      }

      // Update our latest simulated tick record.
      lastSimulatedWorldTick = maxTick + 1;

      // Broadcast the world state.
      // The new world state tick is N+1, given input world tick N.
      var worldStateCmd = new NetCommand.WorldState {
        WorldTick = lastSimulatedWorldTick,
        PlayerStates = playerManager.GetPlayers().Select(
            p => p.Controller.ToNetworkState()).ToArray(),
      };
      handler.BroadcastWorldState(worldStateCmd);

      // Monitoring.
      if (command.StartWorldTick % 100 == 0) {
        //Debug.Log($"Sending tick {worldStateCmd.WorldTick} = {worldStateCmd.PlayerStates[0].Position}");
      }
    }

    // Monitoring.
    DebugUI.ShowValue("max input packet queue", stats.maxInputQueueSize);
    DebugUI.ShowValue("max input array", stats.maxInputArraySize);
  }

  public void EnqueuePlayerInput(WithPeer<NetCommand.PlayerInput> input) {
    playerInputQueue.Enqueue(input);
  }
}
