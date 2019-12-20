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

  // The current world tick.
  private uint worldTick = 0;

  // Queue for incoming world states from the server.
  private Queue<NetCommand.WorldState> worldStateQueue = new Queue<NetCommand.WorldState>();

  // Snapshot buffers for input and state used for prediction & replay.
  private PlayerInputs[] localPlayerInputsSnapshots = new PlayerInputs[1024];
  private PlayerState[] localPlayerStateSnapshots = new PlayerState[1024];

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

      // Send an input packet immediately.
      var command = new NetCommand.PlayerInput {
        WorldTick = worldTick,
        Inputs = inputs,
      };
      netChannel.SendCommand(serverPeer, command);

      // Update our snapshot buffers.
      // TODO: We may not need to actually store velocity here.
      uint bufidx = worldTick % 1024;
      localPlayerInputsSnapshots[bufidx] = inputs;
      localPlayerStateSnapshots[bufidx] = localPlayer.Controller.ToNetworkState();

      // Apply inputs to the associated player controller and simulate the world.
      localPlayer.Controller.SetPlayerInputs(command.Inputs);
      SimulateKinematicSystem(Time.fixedDeltaTime);

      ++worldTick;
    }

    // Step through the incoming world state queue.
    // TODO: This is going to need to be structured pretty differently with other players.
    while (worldStateQueue.Count > 0) {
      // Lookup the historical state for the world tick we got.
      var incomingState = worldStateQueue.Dequeue();
      // TODO: Fix this assumption.
      var incomingPlayerState = incomingState.PlayerStates[0];
      uint bufidx = incomingState.WorldTick % 1024;
      var stateSnapshot = localPlayerStateSnapshots[bufidx];

      // Compare the historical state to see how off it was.
      var error = incomingPlayerState.MotorState.Position - stateSnapshot.MotorState.Position;
      if (error.sqrMagnitude > 0.0000001f) {
        Debug.Log("Divergence, rewinding.");

        // Rewind the player state to the historical snapshot
        localPlayer.Controller.ApplyNetworkState(stateSnapshot);

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
          SimulateKinematicSystem(Time.fixedDeltaTime);

          ++replayTick;
        }
      }
    }
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
        initialState.PlayerState.MotorState.Position).gameObject;
    var player = playerManager.AddPlayer(
        initialState.PlayerId, initialState.Metadata, playerObject);

    // Update kinematic caches.
    activeKinematicMotors.Add(player.Motor);

    return player;
  }

  private void InitializeLocalPlayer() {
    // Create input component.
    localPlayerInput = gameObject.AddComponent<ClientPlayerInput>();
    localPlayerInput.cameraController = FindObjectOfType<CameraController>();
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
