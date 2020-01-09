using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// TODO: Probably share this.
struct ClientWorldState {
  public PlayerState[] playerStates;
}

// Client world simulation including prediction and state rewind.
// Inputs are state frames from the server.
// Outputs are player command frames to the server.
public class ClientSimulation : BaseSimulation {
  // Player stuff.
  private Player localPlayer;

  // Snapshot buffers for input and state used for prediction & replay.
  private PlayerInputs[] localPlayerInputsSnapshots = new PlayerInputs[1024];
  private ClientWorldState[] localWorldStateSnapshots = new ClientWorldState[1024];

  // Queue for incoming world states.
  private Queue<NetCommand.WorldState> worldStateQueue = new Queue<NetCommand.WorldState>();

  // The current world tick and last ack'd server world tick.
  private uint lastServerWorldTick = 0;

  // The estimated number of ticks we're running ahead of the server.
  // TODO: Implement a system for adjusting this on-the-fly, overwatch style.
  private uint estimatedTickLead;

  // I/O interface for player inputs.
  public interface Handler {
    PlayerInputs? SampleInputs();
    void SendInputs(NetCommand.PlayerInput command);
  }
  private Handler handler;

  // Exported monitoring statistics.
  public struct Stats {
    public int receivedStates;
    public int replayedStates;
  }
  public Stats stats;

  public ClientSimulation(
      Player localPlayer,
      PlayerManager playerManager,
      NetworkObjectManager networkObjectManager,
      Handler handler,
      float serverLatencySeconds,
      uint initialWorldTick) : base(playerManager, networkObjectManager) {
    // TODO: Redo player here for multiple players.
    this.localPlayer = localPlayer;
    this.handler = handler;

    // Set the last-acknowledged server tick.
    lastServerWorldTick = initialWorldTick;

    // Extrapolate based on latency what our client tick should be.
    estimatedTickLead = (uint)(serverLatencySeconds / Time.fixedDeltaTime);
    estimatedTickLead = (estimatedTickLead < 1 ? 1 : estimatedTickLead) + 1;
    Debug.Log("Initializing client with estimated tick lead of " + estimatedTickLead);
    WorldTick = initialWorldTick + estimatedTickLead;

    stats = new Stats();
  }

  public void EnqueueWorldState(NetCommand.WorldState state) {
    worldStateQueue.Enqueue(state);
  }

  // Process a single world tick update.
  protected override void Tick() {
    var inputs = handler.SampleInputs();
    if (!inputs.HasValue) {
      // We can't do any simulating until inputs are ready.
      return;
    }

    // Update our snapshot buffers.
    // TODO: The snapshot might only need pos/rot.
    uint bufidx = WorldTick % 1024;
    localPlayerInputsSnapshots[bufidx] = inputs.Value;
    localWorldStateSnapshots[bufidx].playerStates =
       playerManager.GetPlayers().Select(p => p.Controller.ToNetworkState()).ToArray();

    // Send a command for all inputs not yet acknowledged from the server.
    var unackedInputs = new List<PlayerInputs>();
    for (uint tick = lastServerWorldTick; tick <= WorldTick; ++tick) {
      unackedInputs.Add(localPlayerInputsSnapshots[tick % 1024]);
    }
    var command = new NetCommand.PlayerInput {
      StartWorldTick = lastServerWorldTick,
      Inputs = unackedInputs.ToArray(),
    };
    handler.SendInputs(command);

    // Prediction - Apply inputs to the associated player controller and simulate the world.
    localPlayer.Controller.SetPlayerInputs(inputs.Value);
    SimulateWorld(Time.fixedDeltaTime);

    ++WorldTick;
  }

  protected override void PostUpdate() {
    // Step through the incoming world state queue.
    // TODO: This is going to need to be structured pretty differently with other players.
    while (worldStateQueue.Count > 0) {
      // Lookup the historical state for the world tick we got.
      var incomingState = worldStateQueue.Dequeue();
      stats.receivedStates++;
      lastServerWorldTick = incomingState.WorldTick;

      bool headState = false;
      if (incomingState.WorldTick >= WorldTick) {
        headState = true;
      }
      if (incomingState.WorldTick > WorldTick) {
        Debug.LogError("Got a FUTURE tick somehow???");
      }

      // TODO: Fix this assumption.
      uint bufidx = incomingState.WorldTick % 1024;
      var stateSnapshot = localWorldStateSnapshots[bufidx];

      // Locate the data for our local player.
      PlayerState incomingPlayerState = new PlayerState();
      foreach (var playerState in incomingState.PlayerStates) {
        if (playerState.NetworkId == localPlayer.NetworkObject.NetworkId) {
          incomingPlayerState = playerState;
        } else {
          // Apply the state to other players.
          // TODO: This is definitely wrong, because this is a historical tick.  Not sure yet
          // how to resolve this.
          var obj = networkObjectManager.GetObject(playerState.NetworkId);
          obj.GetComponent<IPlayerController>().ApplyNetworkState(playerState);
        }
      }
      if (default(PlayerState).Equals(incomingPlayerState)) {
        Debug.LogError("No local player state found!");
      }

      // Compare the historical state to see how off it was.
      Vector3 error = Vector3.zero;
      if (stateSnapshot.playerStates != null) {
        foreach (var playerState in stateSnapshot.playerStates) {
          if (playerState.NetworkId == localPlayer.NetworkObject.NetworkId) {
            error = incomingPlayerState.Position - playerState.Position;
          }
        }
      }

      // TODO: Getting a huge amount of these. Next step to debug is to make a simple
      // Rigidbody based controller and see if the same issues are there, to determine
      // whether its an issue with my netcode or if KCC is really this non-deterministic.
      if (error.sqrMagnitude > 0.0001f) {
        if (!headState) {
          Debug.Log($"Rewind tick#{incomingState.WorldTick}, Error: {error.magnitude}, Range: {WorldTick - incomingState.WorldTick}");
          stats.replayedStates++;
        }

        // Rewind all player state to the correct state from the server.
        // TODO: Cleanup a lot of this when its merged with how rockets are spawned.
        foreach (var state in incomingState.PlayerStates) {
          var obj = networkObjectManager.GetObject(state.NetworkId);
          obj.GetComponent<IPlayerController>().ApplyNetworkState(state);
        }

        // Loop through and replay all captured input snapshots up to the current tick.
        uint replayTick = incomingState.WorldTick;
        while (replayTick < WorldTick) {
          // Grab the historical input.
          bufidx = replayTick % 1024;
          var inputSnapshot = localPlayerInputsSnapshots[bufidx];

          // Rewrite the historical sate snapshot.
          localWorldStateSnapshots[bufidx].playerStates =
             playerManager.GetPlayers().Select(p => p.Controller.ToNetworkState()).ToArray();

          // Apply inputs to the associated player controller and simulate the world.
          localPlayer.Controller.SetPlayerInputs(inputSnapshot);
          SimulateWorld(Time.fixedDeltaTime);

          ++replayTick;
        }
      }
    }

    // Show some debug monitoring values.
    DebugUI.ShowValue("cl rewinds", stats.replayedStates);
    DebugUI.ShowValue("cl tick", WorldTick);
    DebugUI.ShowValue("cl tick lead", estimatedTickLead);
  }
}
