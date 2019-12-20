using UnityEngine;
using LiteNetLib;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using System.Collections;
using KinematicCharacterController;

/// Primary logic controller for managing server game state.
public class ServerLogicController : BaseLogicController {
  // Currently connected peers indexed by their peer ID.
  private HashSet<NetPeer> connectedPeers;

  // A handle to the game server registered with the hotel master server.
  private Hotel.RegisteredGameServer hotelGameServer;

  // Queue for incoming player input commands.
  // These are processed explicitly in a fixed update loop.
  private Queue<WithPeer<NetCommand.PlayerInput>> playerInputQueue;

  // A re-usable list for keeping track of physics movers.  Not used yet.
  private List<PhysicsMover> movingPhysicsMovers;

  protected override void Awake() {
    base.Awake();

    connectedPeers = new HashSet<NetPeer>();
    playerInputQueue = new Queue<WithPeer<NetCommand.PlayerInput>>();
    movingPhysicsMovers = new List<PhysicsMover>();

    // Setup network event handling.
    netChannel.Subscribe<NetCommand.JoinRequest>(HandleJoinRequest);
    netChannel.SubscribeQueue<NetCommand.PlayerInput>(playerInputQueue);
  }

  protected override void Start() {
    base.Start();
    networkObjectManager.SetAuthoritative(true);
  }

  protected override void Update() {
    base.Update();

    // Process the player input queue.
    while (playerInputQueue.Count > 0) {
      var entry = playerInputQueue.Dequeue();
      var peer = entry.Peer;
      var command = entry.Value;

      // Apply inputs to the associated player controller.
      var player = playerManager.GetPlayerForPeer(peer);
      player.Controller.SetPlayerInputs(command.Inputs);
      var motors = new List<KinematicCharacterMotor> { player.Motor };

      // TODO: An optimization here would be to collect all inputs for the same world tick?
      // Not sure exactly yet.
      KinematicCharacterSystem.Simulate(Time.fixedDeltaTime, motors, movingPhysicsMovers);

      ++worldTick;
    }
  }

  protected override void TearDownGameScene() {
    base.TearDownGameScene();
    if (hotelGameServer != null) {
      hotelGameServer.Destroy();
      hotelGameServer = null;
    }
  }

  public async Task StartServer(int port) {
    await StartServer(port, true);
  }

  public async Task StartServer(int port, bool loadScene) {
    netChannel.StartServer(port);
    hotelGameServer = await Hotel.HotelClient.Instance.StartHostingServer(
        "localhost", port, 8, "Test");
    if (loadScene) {
      LoadGameScene();
    }
  }

  /// Setup all server authoritative state for a new player.
  private Player InitializeServerPlayer(byte playerId, PlayerMetadata metadata) {
    // Setup the serverside object for the player.
    var position = Vector3.zero;
    var playerNetworkObject = networkObjectManager.CreatePlayerGameObject(0, position);
    var player = playerManager.AddPlayer(playerId, metadata, playerNetworkObject.gameObject);
    return player;
  }

  /** Network command handling */

  private void HandleJoinRequest(NetCommand.JoinRequest cmd, NetPeer peer) {
    // TODO: Validation should occur here, if any.
    var playerName = cmd.PlayerSetupData.Name;
    Debug.Log($"{playerName} connected to the server.");
    var metadata = new PlayerMetadata {
      Name = playerName,
    };

    // Initialize the server player model - Peer ID is used as player ID always.
    var existingPlayers = playerManager.GetPlayers();
    var playerId = (byte)peer.Id;
    var player = InitializeServerPlayer(playerId, metadata);

    // Transmit existing player state to new player and new player state to
    // existing clients. Separate RPCs with the same payload are used so that
    // the joining player can distinguish their own player ID.
    var joinAcceptedCmd = CommandBuilder.BuildJoinAcceptedCmd(player, existingPlayers);
    var playerJoinedCmd = CommandBuilder.BuildPlayerJoinedCmd(player);
    netChannel.SendCommand(peer, joinAcceptedCmd);
    netChannel.BroadcastCommand(playerJoinedCmd, peer);
  }

  private void HandlePlayerInput(NetCommand.PlayerInput cmd, NetPeer peer) {
    var player = playerManager.GetPlayerForPeer(peer);
  }

  protected override void OnPeerConnected(NetPeer peer) {
    connectedPeers.Add(peer);
    hotelGameServer.UpdateNumPlayers(connectedPeers.Count);
  }

  protected override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
    connectedPeers.Remove(peer);
    hotelGameServer.UpdateNumPlayers(connectedPeers.Count);

    // Tear down the associated player if it exists.
    var player = playerManager.GetPlayerForPeer(peer);
    if (player != null) {
      Debug.Log($"{player.Metadata.Name} left the server.");
      networkObjectManager.DestroyNetworkObject(player.NetworkObject);
      playerManager.RemovePlayer(player.PlayerId);
    }
    netChannel.BroadcastCommand(new NetCommand.PlayerLeft {
      PlayerId = player.PlayerId,
    }, peer);
  }
}
