using System;
using UnityEngine;
using LiteNetLib;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using KinematicCharacterController;

/// Primary logic controller for managing client game state.
public class ClientLogicController : BaseLogicController {
  private NetPeer serverPeer;
  private ClientPlayerInput localPlayerInput;
  private Player localPlayer;
  private PlayerSetupData playerSetupData;

  // Fixed timing accumulator.
  private float accumulator;

  // The current world tick and last ack'd server world tick.
  private uint worldTick = 0;
  private uint lastServerWorldTick = 0;

  // Queue for incoming world states from the server.
  private Queue<NetCommand.WorldState> worldStateQueue = new Queue<NetCommand.WorldState>();

  // Snapshot buffers for input and state used for prediction & replay.
  private PlayerInputs[] localPlayerInputsSnapshots = new PlayerInputs[1024];
  private PlayerState[] localPlayerStateSnapshots = new PlayerState[1024];

  // Monitoring.
  private int receivedStates;
  private int replayedStates;

  protected override void Awake() {
    base.Awake();

    // Setup network event handling.
    netChannel.Subscribe<NetCommand.JoinAccepted>(HandleJoinAccepted);
    netChannel.Subscribe<NetCommand.PlayerJoined>(HandleOtherPlayerJoined);
    netChannel.Subscribe<NetCommand.PlayerLeft>(HandleOtherPlayerLeft);
    netChannel.SubscribeQueue(worldStateQueue);
  }

  protected override void Update() {
    base.Update();

    if (localPlayerInput == null) {
      // If the input component isn't created yet, take no action.
      return;
    }

    // Fixed timestep loop.
    accumulator += Time.deltaTime;
    while (accumulator >= Time.fixedDeltaTime) {
      accumulator -= Time.fixedDeltaTime;
      var inputs = localPlayerInput.SampleInputs();

      // Update our snapshot buffers.
      // TODO: The snapshot might only need pos/rot.
      uint bufidx = worldTick % 1024;
      localPlayerInputsSnapshots[bufidx] = inputs;
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
      netChannel.SendCommand(serverPeer, command);

      // Monitoring.
      if (worldTick % 100 == 0) {
        //Debug.Log($"Beginning of tick {command.WorldTick} = {localPlayer.GameObject.transform.position}");
      }

      // Prediction - Apply inputs to the associated player controller and simulate the world.
      localPlayer.Controller.SetPlayerInputs(inputs);
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
      receivedStates++;
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
          replayedStates++;
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
    DebugUI.ShowValue("recv states", receivedStates);
    DebugUI.ShowValue("repl states", replayedStates);
  }

  public void TryJoinServer(string host, int port, PlayerSetupData playerSetupData) {
    Debug.Log($"Connecting to host {host}:{port}...");
    this.playerSetupData = playerSetupData;
    netChannel.ConnectToServer(host, port);
    LoadGameScene();
  }

  private Player AddPlayerFromInitialServerState(InitialPlayerState initialState) {
    var playerObject = networkObjectManager.CreatePlayerGameObject(
        initialState.NetworkObjectState.NetworkId,
        initialState.PlayerState.Position).gameObject;
    var player = playerManager.AddPlayer(
        initialState.PlayerId, initialState.Metadata, playerObject);

    // Update kinematic caches.
    activeKinematicMotors.Add(player.Motor);

    return player;
  }

  private void InitializeLocalPlayer() {
    // Create input component.
    localPlayerInput = localPlayer.GameObject.AddComponent<ClientPlayerInput>();

    // Setup camera.
    Camera.main.gameObject.AddComponent<CPMCameraController>();
  }

  /** Network command handling */

  private void HandleJoinAccepted(NetCommand.JoinAccepted cmd) {
    Debug.Log("Server join successful!");

    // Create our player object and attach client-specific components.
    localPlayer = AddPlayerFromInitialServerState(cmd.YourPlayerState);
    InitializeLocalPlayer();

    // Create player objects for existing clients.
    foreach (var state in cmd.ExistingPlayerStates) {
      AddPlayerFromInitialServerState(state);
    }
  }

  private void HandleOtherPlayerJoined(NetCommand.PlayerJoined cmd) {
    Debug.Log($"{cmd.PlayerState.Metadata.Name} joined the server.");
    AddPlayerFromInitialServerState(cmd.PlayerState);
  }

  private void HandleOtherPlayerLeft(NetCommand.PlayerLeft cmd) {
    var player = playerManager.GetPlayer(cmd.PlayerId);
    Debug.Log($"{player.Metadata.Name} left the server.");
    networkObjectManager.DestroyNetworkObject(player.NetworkObject);
    playerManager.RemovePlayer(player.PlayerId);

    // Update kinematic caches.
    activeKinematicMotors.Remove(player.Motor);
  }

  protected override void OnPeerConnected(NetPeer peer) {
    Debug.Log("Connected to host: " + peer.EndPoint);
    serverPeer = peer;

    // Send a join request.
    netChannel.SendCommand(serverPeer, new NetCommand.JoinRequest {
      PlayerSetupData = playerSetupData
    });
  }

  protected override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
    if (serverPeer != null && peer != serverPeer) {
      Debug.LogError("Unexpected mismatch between disconnect peer and server peer!");
    }
    Debug.Log("Disconnected from host: " + disconnectInfo.Reason);
    serverPeer = null;

    // Return to the lobby.
    LoadLobbyScene();
  }
}
