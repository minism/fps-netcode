using System.Collections.Generic;
using UnityEngine;

// Client world simulation including prediction and state rewind.
// Inputs are state frames from the server.
// Outputs are player command frames to the server.
public class ClientSimulation {
  // Player stuff.
  private Player localPlayer;
  private PlayerManager playerManager;

  // Fixed timing accumulator.
  private float accumulator;

  // Snapshot buffers for input and state used for prediction & replay.
  private PlayerInputs[] localPlayerInputsSnapshots = new PlayerInputs[1024];
  private PlayerState[] localPlayerStateSnapshots = new PlayerState[1024];

  // Queue for incoming world states.
  private Queue<NetCommand.WorldState> worldStateQueue = new Queue<NetCommand.WorldState>();

  // The current world tick and last ack'd server world tick.
  private uint worldTick = 0;
  private uint lastServerWorldTick = 0;

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
      Handler handler) {
    // TODO: Redo player here for multiple players.
    this.localPlayer = localPlayer;
    this.playerManager = playerManager;
    this.handler = handler;
    stats = new Stats();
  }

  public void Update(float dt) {
    // Fixed timestep loop.
    accumulator += dt;
    while (accumulator >= Time.fixedDeltaTime) {
      accumulator -= Time.fixedDeltaTime;
      var inputs = handler.SampleInputs();
      if (!inputs.HasValue) {
        // We can't do any simulating until inputs are ready.
        continue;
      }

      // Update our snapshot buffers.
      // TODO: The snapshot might only need pos/rot.
      uint bufidx = worldTick % 1024;
      localPlayerInputsSnapshots[bufidx] = inputs.Value;
      localPlayerStateSnapshots[bufidx] = localPlayer.Controller.ToNetworkState();

      // Send a command for all inputs not yet acknowledged from the server.
      var unackedInputs = new List<PlayerInputs>();
      for (uint tick = lastServerWorldTick; tick <= worldTick; ++tick) {
        unackedInputs.Add(localPlayerInputsSnapshots[tick % 1024]);
      }
      var command = new NetCommand.PlayerInput {
        StartWorldTick = lastServerWorldTick,
        Inputs = unackedInputs.ToArray(),
      };
      handler.SendInputs(command);

      // Monitoring.
      if (worldTick % 100 == 0) {
        //Debug.Log($"Beginning of tick {command.WorldTick} = {localPlayer.GameObject.transform.position}");
      }

      // Prediction - Apply inputs to the associated player controller and simulate the world.
      localPlayer.Controller.SetPlayerInputs(inputs.Value);
      SimulateWorld(Time.fixedDeltaTime);

      // Monitoring.
      if (worldTick % 100 == 0) {
        //Debug.Log($"Moved for tick {worldTick+1} = {localPlayer.GameObject.transform.position}");
      }

      ++worldTick;
    }

    // Step through the incoming world state queue.
    // TODO: This is going to need to be structured pretty differently with other players.
    while (worldStateQueue.Count > 0) {
      // Lookup the historical state for the world tick we got.
      var incomingState = worldStateQueue.Dequeue();
      stats.receivedStates++;
      lastServerWorldTick = incomingState.WorldTick;

      bool headState = false;
      if (incomingState.WorldTick >= worldTick) {
        headState = true;
      }
      if (incomingState.WorldTick > worldTick) {
        Debug.LogError("Got a FUTURE tick somehow???");
      }

      // TODO: Fix this assumption.
      var incomingPlayerState = incomingState.PlayerStates[0];
      uint bufidx = incomingState.WorldTick % 1024;
      var stateSnapshot = localPlayerStateSnapshots[bufidx];

      // Compare the historical state to see how off it was.
      var error = incomingPlayerState.Position - stateSnapshot.Position;

      // TODO: Getting a huge amount of these. Next step to debug is to make a simple
      // Rigidbody based controller and see if the same issues are there, to determine
      // whether its an issue with my netcode or if KCC is really this non-deterministic.
      if (error.sqrMagnitude > 0.0001f) {
        if (!headState) {
          Debug.Log($"Rewind tick#{incomingState.WorldTick}: {incomingPlayerState.Position} - {stateSnapshot.Position}, Range: {worldTick - incomingState.WorldTick}");
          stats.replayedStates++;
        }

        // Rewind the player state to the correct state from the server.
        localPlayer.Controller.ApplyNetworkState(incomingPlayerState);

        // Loop through and replay all captured input snapshots up to the current tick.
        uint replayTick = incomingState.WorldTick;
        while (replayTick < worldTick) {
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

    // Update debug monitoring.
    DebugUI.ShowValue("recv states", stats.receivedStates);
    DebugUI.ShowValue("repl states", stats.replayedStates);
  }

  private void SimulateWorld(float dt) {
    //KinematicCharacterSystem.Simulate(dt, activeKinematicMotors, activePhysicsMovers);
    playerManager.GetPlayers().ForEach(p => p.Controller.Simulate(dt));
  }

  public void EnqueueWorldState(NetCommand.WorldState state) {
    worldStateQueue.Enqueue(state);
  }
}
