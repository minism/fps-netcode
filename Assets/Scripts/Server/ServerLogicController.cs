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
  private HashSet<NetPeer> connectedPeers = new HashSet<NetPeer>();

  // A handle to the game server registered with the hotel master server.
  private Hotel.RegisteredGameServer hotelGameServer;

  // Queue for incoming player input commands.
  // These are processed explicitly in a fixed update loop.
  private Queue<WithPeer<NetCommand.PlayerInput>> playerInputQueue
      = new Queue<WithPeer<NetCommand.PlayerInput>>();

  // Monitoring.
  private int sentPlayerNearOriginStates;

  protected override void Awake() {
    base.Awake();

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
    // TODO: An optimization would be to make this a priority queue, and process
    // matching world ticks in lock-step.  I don't actually know how often that
    // would happen though.
    while (playerInputQueue.Count > 0) {
      var entry = playerInputQueue.Dequeue();
      var peer = entry.Peer;
      var command = entry.Value;

      // Apply inputs to the associated player controller and simulate the world.
      var player = playerManager.GetPlayerForPeer(peer);
      player.Controller.SetPlayerInputs(command.Inputs);
      SimulateKinematicSystem(Time.fixedDeltaTime);

      // Broadcast the world state.
      // The new world state tick is N+1, given input world tick N.
      var worldStateCmd = new NetCommand.WorldState {
        WorldTick = command.WorldTick + 1,
        PlayerStates = playerManager.GetPlayers().Select(
            p => p.Controller.ToNetworkState()).ToArray(),
      };
      if (worldStateCmd.PlayerStates[0].MotorState.Position.magnitude < 5) {
        sentPlayerNearOriginStates++;
      }
      netChannel.BroadcastCommand(worldStateCmd);
    }

    DebugUI.ShowValue("SentNearOrigin", sentPlayerNearOriginStates);
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
  private Player CreateServerPlayer(byte playerId, PlayerMetadata metadata) {
    // Setup the serverside object for the player.
    var position = Vector3.zero;
    var playerNetworkObject = networkObjectManager.CreatePlayerGameObject(0, position);
    var player = playerManager.AddPlayer(playerId, metadata, playerNetworkObject.gameObject);

    // Update kinematic caches.
    activeKinematicMotors.Add(player.Motor);

    return player;
  }

  /// Tear down all server authoritative state for a player.
  private void DestroyServerPlayer(Player player) {
    Debug.Log($"{player.Metadata.Name} left the server.");

    // Update managers.
    networkObjectManager.DestroyNetworkObject(player.NetworkObject);
    playerManager.RemovePlayer(player.PlayerId);

    // Update kinematic caches.
    activeKinematicMotors.Remove(player.Motor);

    // Notify peers.
    netChannel.BroadcastCommand(new NetCommand.PlayerLeft {
      PlayerId = player.PlayerId,
    }, player.peer);
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
    var player = CreateServerPlayer(playerId, metadata);

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
    var player = playerManager.GetPlayerForPeer(peer);
    if (player != null) {
      DestroyServerPlayer(player);
    }
  }
}
