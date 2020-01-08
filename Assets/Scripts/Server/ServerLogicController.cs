using UnityEngine;
using LiteNetLib;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

/// Primary logic controller for managing server game state.
public class ServerLogicController : BaseLogicController, ServerSimulation.Handler {
  // Debugging.
  public float debugPhysicsErrorChance;

  // Delegate that manages the world simulation.
  private ServerSimulation simulation;

  // Currently connected peers indexed by their peer ID.
  private HashSet<NetPeer> connectedPeers = new HashSet<NetPeer>();

  // A handle to the game server registered with the hotel master server.
  private Hotel.RegisteredGameServer hotelGameServer;

  protected override void Awake() {
    base.Awake();

    // Setup network event handling.
    netChannel.Subscribe<NetCommand.JoinRequest>(HandleJoinRequest);
    netChannel.Subscribe<NetCommand.PlayerInput>(HandlePlayerInput);
  }

  protected override void Start() {
    base.Start();
    networkObjectManager.SetAuthoritative(true);
  }

  protected override void Update() {
    base.Update();
    
    if (simulation != null) {
      simulation.Update(Time.deltaTime);
    }
  }

  protected override void TearDownGameScene() {
    base.TearDownGameScene();
    if (simulation != null) {
      simulation = null;
    }
    if (hotelGameServer != null) {
      hotelGameServer.Destroy();
      hotelGameServer = null;
    }
  }

  public async Task StartServer(string host, int port) {
    await StartServer(host, port, true);
  }

  public async Task StartServer(string host, int port, bool loadScene) {
    netChannel.StartServer(port);
    hotelGameServer = await Hotel.HotelClient.Instance.StartHostingServer(
        host, port, 8, "Test");
    if (loadScene) {
      LoadGameScene();
    }

    // Initialize simulation.
    simulation = new ServerSimulation(debugPhysicsErrorChance, playerManager, this);
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
    }, player.Peer);
  }

  /** Simulation.Handler interface */
  public void BroadcastWorldState(NetCommand.WorldState state) {
    netChannel.BroadcastCommand(state);
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
    var joinAcceptedCmd = CommandBuilder.BuildJoinAcceptedCmd(
        player, existingPlayers, simulation.WorldTick);
    var playerJoinedCmd = CommandBuilder.BuildPlayerJoinedCmd(player);
    netChannel.SendCommand(peer, joinAcceptedCmd);
    netChannel.BroadcastCommand(playerJoinedCmd, peer);
  }

  private void HandlePlayerInput(NetCommand.PlayerInput cmd, NetPeer peer) {
    simulation.EnqueuePlayerInput(
        new WithPeer<NetCommand.PlayerInput> { Peer = peer, Value = cmd });
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
