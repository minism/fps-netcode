using UnityEngine;
using LiteNetLib;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

/// Primary logic controller for managing server game state.
public class ServerLogicController : BaseLogicController {
  // Debugging.
  public float debugPhysicsErrorChance;

  // Currently connected peers indexed by their peer ID.
  private HashSet<NetPeer> connectedPeers = new HashSet<NetPeer>();

  // A handle to the game server registered with the hotel master server.
  private Hotel.RegisteredGameServer hotelGameServer;

  // Queue for incoming player input commands.
  // These are processed explicitly in a fixed update loop.
  private Queue<WithPeer<NetCommand.PlayerInput>> playerInputQueue =
      new Queue<WithPeer<NetCommand.PlayerInput>>();

  // Monitoring.
  private int maxInputQueueSize;
  private int maxInputArraySize;

  // The latest world tick that has been simulated.
  private uint lastSimulatedWorldTick = 0;

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

    // Monitoring.
    DebugUI.ShowValue("max input packet queue", maxInputQueueSize);
    DebugUI.ShowValue("max input array", maxInputArraySize);

    // Process the player input queue.
    // TODO: An optimization would be to make this a priority queue, and process
    // matching world ticks in lock-step.  I don't actually know how often that
    // would happen though.
    while (playerInputQueue.Count > 0) {
      // Monitoring.
      if (playerInputQueue.Count > maxInputQueueSize) {
        maxInputQueueSize = playerInputQueue.Count;
      }

      var entry = (WithPeer<NetCommand.PlayerInput>) playerInputQueue.Dequeue();
      var player = playerManager.GetPlayerForPeer(entry.Peer);
      var command = entry.Value;

      // Monitoring.
      if (command.Inputs.Length > maxInputArraySize) {
        maxInputArraySize = command.Inputs.Length;
      }
      if (command.StartWorldTick % 100 == 0) {
        //Debug.Log($"Beginning of tick {command.WorldTick} = {player.GameObject.transform.position}");
      }

      // Calculate the last tick in the incoming command.
      uint maxTick = command.StartWorldTick + (uint)command.Inputs.Length - 1;

      // Check if there are new inputs to simulate.
      if (maxTick >= lastSimulatedWorldTick) {
        uint start = lastSimulatedWorldTick > command.StartWorldTick
            ? lastSimulatedWorldTick - command.StartWorldTick : 0;
        for (int i = (int)start; i < command.Inputs.Length; ++i) {
          // Apply inputs to the associated player controller and simulate the world.
          player.Controller.SetPlayerInputs(command.Inputs[i]);
          SimulateWorld(Time.fixedDeltaTime);
          if (Random.value < debugPhysicsErrorChance) {
            Debug.Log("Injecting random physics error.");
            player.GameObject.transform.Translate(new Vector3(1, 0, 0));
          }
        }
      }

      // Update our latest simulated tick record.
      lastSimulatedWorldTick = maxTick + 1;

      // Broadcast the world state.
      // The new world state tick is N+1, given input world tick N.
      var worldStateCmd = new NetCommand.WorldState {
        WorldTick = lastSimulatedWorldTick,
        PlayerStates = playerManager.GetPlayers().Select(
            p => p.Controller.ToNetworkState()).ToArray(),
      };
      netChannel.BroadcastCommand(worldStateCmd);

      // Monitoring.
      if (command.StartWorldTick % 100 == 0) {
        //Debug.Log($"Sending tick {worldStateCmd.WorldTick} = {worldStateCmd.PlayerStates[0].Position}");
      }
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

  private void SimulateWorld(float dt) {
    //KinematicCharacterSystem.Simulate(dt, activeKinematicMotors, activePhysicsMovers);
    playerManager.GetPlayers().ForEach(p => p.Controller.Simulate(dt));
  }
}
