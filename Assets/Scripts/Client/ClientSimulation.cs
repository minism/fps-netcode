using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Client world simulation including prediction and state rewind.
// Inputs are state frames from the server.
// Outputs are player command frames to the server.
public class ClientSimulation : BaseSimulation {
  // Player stuff.
  private Player localPlayer;

  // Snapshot buffers for input and state used for prediction & replay.
  private PlayerInputs[] localPlayerInputsSnapshots = new PlayerInputs[1024];
  private PlayerState[] localPlayerStateSnapshots = new PlayerState[1024];

  // Queue for incoming world states.
  private Queue<NetCommand.WorldState> worldStateQueue = new Queue<NetCommand.WorldState>();

  // The current world tick and last ack'd server world tick.
  private uint lastServerWorldTick = 0;

  // The estimated number of ticks we're running ahead of the server.
  private uint estimatedTickLead;

  // I/O interface for player inputs.
  public interface Handler {
    PlayerInputs? SampleInputs();
    void SendInputs(NetCommand.PlayerInput command);
  }
  private Handler handler;

  // Monitoring statistics.
  private int replayedStates;

  public ClientSimulation(
      Player localPlayer,
      PlayerManager playerManager,
      NetworkObjectManager networkObjectManager,
      Handler handler,
      int serverLatencyMs,
      uint initialWorldTick) : base(playerManager, networkObjectManager) {
    this.localPlayer = localPlayer;
    this.handler = handler;

    // Set the last-acknowledged server tick.
    lastServerWorldTick = initialWorldTick;

    // Extrapolate based on latency what our client tick should be.
    float serverLatencySeconds = serverLatencyMs / 1000f;
    estimatedTickLead = (uint)(serverLatencySeconds * 1.5 / Time.fixedDeltaTime) + 4;
    WorldTick = initialWorldTick + estimatedTickLead;
    Debug.Log($"Initializing client with estimated tick lead of {estimatedTickLead}, ping: {serverLatencyMs}");
  }

  public void EnqueueWorldState(NetCommand.WorldState state) {
    worldStateQueue.Enqueue(state);
  }

  // Adjust the simulation to a new tick offset from the server.
  public void Adjust(int actualTickLead, int tickOffset) {
    Debug.Log($"Adjusting client simulation by {tickOffset}");

    // Update our estimate, this is only used for monitoring though.
    //estimatedTickLead = (uint) (actualTickLead + tickOffset);

    // TODO: This should smoothly transition over time.
    if (tickOffset >= 0) {
      WorldTick += (uint)tickOffset;
    } else {
      WorldTick -= (uint)Mathf.Abs(tickOffset);
    }
  }

  // Process a single world tick update.
  protected override void Tick(float dt) {
    var sampled = handler.SampleInputs();
    PlayerInputs inputs = sampled.HasValue ? sampled.Value : new PlayerInputs();

    // Update our snapshot buffers.
    uint bufidx = WorldTick % 1024;
    localPlayerInputsSnapshots[bufidx] = inputs;
    localPlayerStateSnapshots[bufidx] = localPlayer.Controller.ToNetworkState();

    // Send a command for all inputs not yet acknowledged from the server.
    var unackedInputs = new List<PlayerInputs>();
    // TODO: lastServerWorldTick is technically not the same as lastAckedInputTick, fix this.
    for (uint tick = lastServerWorldTick; tick <= WorldTick; ++tick) {
      unackedInputs.Add(localPlayerInputsSnapshots[tick % 1024]);
    }
    var command = new NetCommand.PlayerInput {
      StartWorldTick = lastServerWorldTick,
      Inputs = unackedInputs.ToArray(),
    };
    handler.SendInputs(command);

    // Prediction - Apply inputs to the associated player controller and simulate the world.
    localPlayer.Controller.SetPlayerInputs(inputs);
    SimulateWorld(dt);
    ++WorldTick;

    // Process a world state frame from the server if we have it.
    ProcessServerWorldState();
  }

  protected override void PostUpdate() {
    // Step through the incoming world state queue.
    // TODO: This is going to need to be structured pretty differently with other players.
    while (worldStateQueue.Count > 0) {
      ProcessServerWorldState();
    }

    // Show some debug monitoring values.
    DebugUI.ShowValue("cl rewinds", replayedStates);
    DebugUI.ShowValue("cl tick", WorldTick);
    DebugUI.ShowValue("cl est. tick lead", estimatedTickLead);
    DebugUI.ShowValue("cl rec. tick lead", WorldTick - lastServerWorldTick);
  }

  private void ProcessServerWorldState() {
    if (worldStateQueue.Count < 1) {
      return;
    }

    var incomingState = worldStateQueue.Dequeue();
    lastServerWorldTick = incomingState.WorldTick;

    bool headState = false;
    if (incomingState.WorldTick >= WorldTick) {
      headState = true;
    }
    if (incomingState.WorldTick > WorldTick) {
      Debug.LogError("Got a FUTURE tick somehow???");
    }

    // Lookup the historical state for the world tick we got.
    uint bufidx = incomingState.WorldTick % 1024;
    var stateSnapshot = localPlayerStateSnapshots[bufidx];

    // Locate the data for our local player.
    PlayerState incomingLocalPlayerState = new PlayerState();
    foreach (var playerState in incomingState.PlayerStates) {
      if (playerState.NetworkId == localPlayer.NetworkObject.NetworkId) {
        incomingLocalPlayerState = playerState;
      } else {
        // Apply the state immediately to other players.
        // TODO: Is this right even though this is a historical tick?  Interp should happen here.
        var obj = networkObjectManager.GetObject(playerState.NetworkId);
        obj.GetComponent<IPlayerController>().ApplyNetworkState(playerState);
      }
    }
    if (default(PlayerState).Equals(incomingLocalPlayerState)) {
      Debug.LogError("No local player state found!");
    }

    // Compare the historical state to see how off it was.
    var error = incomingLocalPlayerState.Position - stateSnapshot.Position;
    if (error.sqrMagnitude > 0.0001f) {
      if (!headState) {
        Debug.Log($"Rewind tick#{incomingState.WorldTick}, Error: {error.magnitude}, Range: {WorldTick - incomingState.WorldTick}");
        replayedStates++;
      }

      // Rewind local player state to the correct state from the server.
      // TODO: Cleanup a lot of this when its merged with how rockets are spawned.
      localPlayer.Controller.ApplyNetworkState(incomingLocalPlayerState);

      // Loop through and replay all captured input snapshots up to the current tick.
      uint replayTick = incomingState.WorldTick;
      while (replayTick < WorldTick) {
        // Grab the historical input.
        bufidx = replayTick % 1024;
        var inputSnapshot = localPlayerInputsSnapshots[bufidx];

        // Rewrite the historical sate snapshot.
        localPlayerStateSnapshots[bufidx] = localPlayer.Controller.ToNetworkState();

        // Apply inputs to the associated player controller and simulate the world.
        localPlayer.Controller.SetPlayerInputs(inputSnapshot);
        SimulateWorld(Time.fixedDeltaTime);

        ++replayTick;
      }
    }
  }
}
