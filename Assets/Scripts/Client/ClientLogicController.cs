using LiteNetLib;
using UnityEngine;
using UnityEngine.SocialPlatforms;

/// Primary logic controller for managing client game state.
public class ClientLogicController : BaseLogicController, ClientSimulation.Handler {
  [Header("Client debug settings")]
  public bool debugAutoMovement;

  private NetPeer serverPeer;
  private ClientPlayerInput localPlayerInput;
  private Player localPlayer;
  private PlayerSetupData playerSetupData;

  // Audio stuff.
  public AudioSource clientHitSound;
  public AudioSource hitConfirmSound;

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
    netChannel.Subscribe<NetCommand.SpawnObject>(HandleSpawnObject);
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
    if (debugAutoMovement) {
      localPlayerInput.DebugAutoMovement = true;
    }

    // Setup camera and attach to the local player camera anchor.
    var cpmCamera = Camera.main.gameObject.AddComponent<CPMCameraController>();
    cpmCamera.player = localPlayer.Controller as CPMPlayerController;

    // Inject the attack handler.
    localPlayer.Controller.SetPlayerAttackDelegate(HandleLocalPlayerAttack);

    // Disable the Player View for the local player so we don't see ourselves.
    var localView = Ice.ObjectUtil.FindChildWithTag(localPlayer.GameObject, "PlayerView");
    if (localView != null) {
      localView.SetActive(false);
    }
  }

  /** ClientSimulation.Handler interface */
  public PlayerInputs? SampleInputs() {
    if (localPlayerInput == null) {
      return null;
    }
    return localPlayerInput.SampleInputs();
  }

  public void SendInputs(NetCommand.PlayerInputCommand command) {
    netChannel.SendNSCommand(serverPeer, command);
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

  private void HandleSpawnObject(NetCommand.SpawnObject cmd) {
    // Only spawn the object if it wasn't created by us.
    // TODO: Instead we should attach to the live object.
    if (cmd.CreatorPlayerId != localPlayer.Id) {
      networkObjectManager.SpawnPlayerObject(
          cmd.NetworkObjectState.NetworkId,
          cmd.Type,
          cmd.Position,
          cmd.Orientation);
    } else if (cmd.WasAttackHit) {
      // Play a hit confirm sound for debugging purposes.
      hitConfirmSound.Play();
    }
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

  /**
   * IPlayerActionHandler interface.
   * 
   * TODO - Consider breaking this into a delegate.
   */
  public void HandleLocalPlayerAttack(
      NetworkObjectType type, Vector3 position, Quaternion orientation) {
    var obj = networkObjectManager.SpawnPlayerObject(0, type, position, orientation, true);

    // Check hit for logging purposes but dont do anything with this yet.
    var playerHit = obj.GetComponent<HitscanAttack>().CheckHit();
    if (playerHit != null) {
      clientHitSound.Play();
      Debug.Log($"On our end, we hit {playerHit.name}");
    }
  }
}
