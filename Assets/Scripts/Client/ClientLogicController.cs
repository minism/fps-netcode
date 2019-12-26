using UnityEngine;
using LiteNetLib;
using KinematicCharacterController;
using NetCommand;

/// Primary logic controller for managing client game state.
public class ClientLogicController : BaseLogicController, ClientSimulation.Handler {
  private NetPeer serverPeer;
  private ClientPlayerInput localPlayerInput;
  private Player localPlayer = new Player();
  private PlayerSetupData playerSetupData;

  // Delegate that manages the world simulation, prediction, reconciliation.
  private ClientSimulation simulation;

  // Monitoring.
  private int receivedStates;
  private int replayedStates;

  protected override void Awake() {
    base.Awake();

    // Setup network event handling.
    netChannel.Subscribe<NetCommand.JoinAccepted>(HandleJoinAccepted);
    netChannel.Subscribe<NetCommand.PlayerJoined>(HandleOtherPlayerJoined);
    netChannel.Subscribe<NetCommand.PlayerLeft>(HandleOtherPlayerLeft);
    netChannel.Subscribe<NetCommand.WorldState>(HandleWorldState);
  }

  protected override void Update() {
    base.Update();

    // Simulation is only running when we're connected to a server.
    if (simulation != null) {
      simulation.Update(Time.deltaTime);
    }
  }

  public void StartClient(string host, int port, PlayerSetupData playerSetupData) {
    Debug.Log($"Connecting to host {host}:{port}...");
    this.playerSetupData = playerSetupData;
    netChannel.ConnectToServer(host, port);
    LoadGameScene();

    // Initialize simulation.
    simulation = new ClientSimulation(
        localPlayer, playerManager, this);
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

  /** ClientSimulation.Handler interface */
  public PlayerInputs? SampleInputs() {
    if (localPlayerInput == null) {
      return null;
    }
    return localPlayerInput.SampleInputs();
  }

  public void SendInputs(PlayerInput command) {
    netChannel.SendCommand(serverPeer, command);
  }

  /** Network command handling */
  private void HandleWorldState(NetCommand.WorldState cmd) {
    if (simulation != null) {
      simulation.EnqueueWorldState(cmd);
    }
  }

  private void HandleJoinAccepted(NetCommand.JoinAccepted cmd) {
    Debug.Log("Server join successful!");

    // Create our player object and attach client-specific components.
    localPlayer.CopyFrom(AddPlayerFromInitialServerState(cmd.YourPlayerState));
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
