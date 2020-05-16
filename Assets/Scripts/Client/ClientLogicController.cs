using LiteNetLib;
using UnityEngine;

/// Primary logic controller for managing client game state.
public class ClientLogicController : BaseLogicController, ClientSimulation.Handler {
  private NetPeer serverPeer;
  private ClientPlayerInput localPlayerInput;
  private Player localPlayer;
  private PlayerSetupData playerSetupData;

  // Delegate that manages the world simulation, prediction, reconciliation.
  private ClientSimulation simulation;

  // The most accurate server latency estimate to use for initialization.
  private int initialServerLatency;
  private int bestServerLatency {
    get {
      if (netChannel.PeerLatency.ContainsKey(serverPeer)) {
        return netChannel.PeerLatency[serverPeer];
      }
      return initialServerLatency;
    }
  }

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

  public void StartClient(
    string host, int port, int initialServerLatency, PlayerSetupData playerSetupData) {
    Debug.Log($"Connecting to host {host}:{port}...");
    this.playerSetupData = playerSetupData;
    this.initialServerLatency = initialServerLatency;
    netChannel.ConnectToServer(host, port);
    LoadGameScene();
  }

  protected override void TearDownGameScene() {
    base.TearDownGameScene();
    if (simulation != null) {
      simulation = null;
    }
  }

  private Player AddPlayerFromInitialServerState(InitialPlayerState initialState, bool isRemote) {
    var playerObject = networkObjectManager.CreatePlayerGameObject(
        initialState.NetworkObjectState.NetworkId,
        initialState.PlayerState.Position, isRemote).gameObject;
    var player = playerManager.AddPlayer(
        initialState.PlayerId, initialState.Metadata, playerObject);

    return player;
  }

  private void InitializeLocalPlayer() {
    // Create input component.
    localPlayerInput = localPlayer.GameObject.AddComponent<ClientPlayerInput>();

    // Setup camera and attach to the local player camera anchor.
    var cpmCamera = Camera.main.gameObject.AddComponent<CPMCameraController>();
    cpmCamera.followTarget = localPlayer.GameObject.transform;
  }

  /** ClientSimulation.Handler interface */
  public PlayerInputs? SampleInputs() {
    if (localPlayerInput == null) {
      return null;
    }
    return localPlayerInput.SampleInputs();
  }

  public void SendInputs(NetCommand.PlayerInputCommand command) {
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
    Debug.Log("Local player network ID is " + cmd.YourPlayerState.NetworkObjectState.NetworkId);
    localPlayer = AddPlayerFromInitialServerState(cmd.YourPlayerState, false);
    InitializeLocalPlayer();

    // Initialize simulation.
    simulation = new ClientSimulation(
        localPlayer, playerManager, networkObjectManager, this, bestServerLatency, cmd.WorldTick);

    // Create player objects for existing clients.
    foreach (var state in cmd.ExistingPlayerStates) {
      AddPlayerFromInitialServerState(state, true);
    }
  }

  private void HandleOtherPlayerJoined(NetCommand.PlayerJoined cmd) {
    Debug.Log($"{cmd.PlayerState.Metadata.Name} joined the server.");
    AddPlayerFromInitialServerState(cmd.PlayerState, true);
  }

  private void HandleOtherPlayerLeft(NetCommand.PlayerLeft cmd) {
    var player = playerManager.GetPlayer(cmd.PlayerId);
    Debug.Log($"{player.Metadata.Name} left the server.");
    networkObjectManager.DestroyNetworkObject(player.NetworkObject);
    playerManager.RemovePlayer(player.Id);
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
    TearDownGameScene();
    LoadLobbyScene();
  }
}
