using System.Collections.Generic;
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

  // The server world tick for each local tick.
  private uint[] localPlayerWorldTickSnapshots = new uint[1024];

  // Queue for incoming world states.
  private Queue<NetCommand.WorldState> worldStateQueue = new Queue<NetCommand.WorldState>();

  // The last received server world tick.
  // TODO Not public
  public uint lastServerWorldTick = 0;

  // The last tick that the server has acknowledged our input for.
  private uint lastAckedInputTick = 0;

  // Delegate for adjusting the simulation speed based on incoming state data.
  private ClientSimulationAdjuster clientSimulationAdjuster;

  // Average of the excess size the of incoming world state queue, after tick processing.
  private Ice.MovingAverage excessWorldStateAvg = new Ice.MovingAverage(10);

  // I/O interface for player inputs.
  public interface Handler {
    PlayerInputs? SampleInputs();
    void SendInputs(NetCommand.PlayerInputCommand command);
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

    // Setup the simulation adjuster, this delegate will be responsible for time-warping the client
    // simulation whenever we are too far ahead or behind the server simulation.
    simulationAdjuster = clientSimulationAdjuster = new ClientSimulationAdjuster();

    // Set the last-acknowledged server tick.
    lastServerWorldTick = initialWorldTick;
    lastAckedInputTick = initialWorldTick;

    // Extrapolate based on latency what our client tick should start at.
    WorldTick = clientSimulationAdjuster.GuessClientTick(initialWorldTick, serverLatencyMs);
  }

  public void EnqueueWorldState(NetCommand.WorldState state) {
    worldStateQueue.Enqueue(state);
  }

  // Process a single world tick update.
  protected override void Tick(float dt) {
    // Grab inputs.
    var sampled = handler.SampleInputs();
    PlayerInputs inputs = sampled.HasValue ? sampled.Value : new PlayerInputs();

    // If the oldest server state is too stale, freeze the player.
    if (Settings.FreezeClientOnStaleServer &&
        WorldTick - lastServerWorldTick >= Settings.MaxStaleServerStateTicks) {
      this.Log("Server state is too old (is the network connection dead?)");
      inputs = new PlayerInputs();
    }

    // Update our snapshot buffers.
    uint bufidx = WorldTick % 1024;
    localPlayerInputsSnapshots[bufidx] = inputs;
    localPlayerStateSnapshots[bufidx] = localPlayer.Controller.ToNetworkState();
    localPlayerWorldTickSnapshots[bufidx] = lastServerWorldTick;

    // Send a command for all inputs not yet acknowledged from the server.
    var unackedInputs = new List<PlayerInputs>();
    var clientWorldTickDeltas = new List<short>();
    for (uint tick = lastAckedInputTick; tick <= WorldTick; ++tick) {
      unackedInputs.Add(localPlayerInputsSnapshots[tick % 1024]);
      clientWorldTickDeltas.Add((short)(tick - localPlayerWorldTickSnapshots[tick % 1024]));
    }
    var command = new NetCommand.PlayerInputCommand {
      StartWorldTick = lastAckedInputTick,
      Inputs = unackedInputs.ToArray(),
      ClientWorldTickDeltas = clientWorldTickDeltas.ToArray(),
    };
    handler.SendInputs(command);

    // Prediction - Apply inputs to the associated player controller and simulate the world.
    localPlayer.Controller.SetPlayerInputs(inputs);
    SimulateWorld(dt);
    ++WorldTick;

    // Notify camera.
    // TODO: Fix this - This is needed because the camera is not hooked into the same
    // world simulation "Simulate()" fixed loop, it can only used its own FixedUpdate, and so
    // it needs to be synced up a bit better.
    // Need some interface for entities that participate.
    GameObject.FindObjectOfType<CPMCameraController>().PlayerPositionUpdated();

    // Process a world state frame from the server if we have it.
    ProcessServerWorldState();
  }

  protected override void PostUpdate() {
    // Process the remaining world states if there are any, though we expect this to be empty?
    // TODO: This is going to need to be structured pretty differently with other players.
    excessWorldStateAvg.Push(worldStateQueue.Count);
    //while (worldStateQueue.Count > 0) {
    //  ProcessServerWorldState();
    //}
    // Show some debug monitoring values.
    DebugUI.ShowValue("cl reconciliations", replayedStates);
    DebugUI.ShowValue("incoming state excess", excessWorldStateAvg.Average());
    clientSimulationAdjuster.Monitoring();
  }

  private void ProcessServerWorldState() {
    if (worldStateQueue.Count < 1) {
      return;
    }

    var incomingState = worldStateQueue.Dequeue();
    lastServerWorldTick = incomingState.WorldTick;

    // During initialization the server will send zeroes for this field.
    if (incomingState.YourLatestInputTick > 0) {
      lastAckedInputTick = incomingState.YourLatestInputTick;

      // Calculate our actual tick lead on the server perspective. We add one because the world
      // state the server sends to use is always 1 higher than the latest input that has been
      // processed.
      int actualTickLead = (int)lastAckedInputTick - (int)lastServerWorldTick + 1;
      clientSimulationAdjuster.NotifyActualTickLead(actualTickLead);
    }

    // For debugging purposes, log the local lead we're running at
    var localWorldTickLead = WorldTick - lastServerWorldTick;
    DebugUI.ShowValue("local tick lead", localWorldTickLead);

    // Parse the player data and separate out our own incoming state.
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
      // This is unexpected.
      this.LogError("No local player state found!");
    }

    if (incomingState.WorldTick >= WorldTick) {
      // We're running behind the server at this point, which can happen
      // if the application is suspended for some reason, so just snap our 
      // state.
      // TODO: Look into interpolation here as well.
      this.Log("Got a future world state, snapping to latest state.");
      // TODO: We need to add local estimated latency here like we do during init.
      WorldTick = incomingState.WorldTick;
      localPlayer.Controller.ApplyNetworkState(incomingLocalPlayerState);
      return;
    }

    // Otherwise, continue with reconciliation procedure.

    // Lookup the historical state for the world tick we got.
    // TODO: This is nonsensical when we're behind server.
    uint bufidx = incomingState.WorldTick % 1024;
    var stateSnapshot = localPlayerStateSnapshots[bufidx];

    // Compare the historical state to see how off it was.
    var error = incomingLocalPlayerState.Position - stateSnapshot.Position;
    if (error.sqrMagnitude > 0.0001f) {
      this.Log($"Rewind tick#{incomingState.WorldTick}, Error: {error.magnitude}, Range: {WorldTick - incomingState.WorldTick}");
      replayedStates++;

      // TODO: If the error was too high, snap rather than interpolate.

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

      // TODO(important): After applying corrections, we need to interpolate the local
      // player view with the last known position.
      // https://www.codersblock.org/blog/client-side-prediction-in-unity-2018
    }
  }
}
