using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Various per-player-connection data the server simulation needs to track.
public class PlayerConnectionInfo {
  // Whether the player is synchronized yet.
  public bool synchronized;

  // The latest (highest) input tick from the player.
  public uint latestInputTick;
}

// Client world simulation including prediction and state rewind.
// Inputs are state frames from the server.
// Outputs are player command frames to the server.
public class ServerSimulation : BaseSimulation {
  // Debugging.
  public float debugPhysicsErrorChance;

  // Delegates with some encapsulated simulation logic for simplicity.
  private PlayerInputProcessor playerInputProcessor;

  // Reusable hash set for players whose input we've checked each frame.
  private HashSet<byte> unprocessedPlayerIds = new HashSet<byte>();

  // Snapshot buffers for player state history, used for attack rollbacks.
  private Dictionary<byte, PlayerState[]> playerStateSnapshots = new Dictionary<byte, PlayerState[]>();

  // Simulation info for each player, indexed by player ID (peer ID).
  private Dictionary<byte, PlayerConnectionInfo> playerConnectionInfo
      = new Dictionary<byte, PlayerConnectionInfo>();

  // Current input struct for each player.
  // This is only needed because the ProcessAttack delegate flow is a bit too complicated.
  // TODO: Simplify this.
  private Dictionary<byte, TickInput> currentPlayerInput = new Dictionary<byte, TickInput>();

  // I/O interface for world states.
  public interface Handler {
    void SendWorldState(Player player, NetCommand.WorldState state);
  }
  private Handler handler;

  // World state broadcasts can happen at an independent rate.
  private FixedTimer worldStateBroadcastTimer;

  // Monitoring.
  private int missedInputs;

  public ServerSimulation(
      float debugPhysicsErrorChance,
      PlayerManager playerManager,
      NetworkObjectManager networkObjectManager,
      Handler handler) : base(playerManager, networkObjectManager) {
    this.debugPhysicsErrorChance = debugPhysicsErrorChance;
    this.handler = handler;
    playerInputProcessor = new PlayerInputProcessor();

    // Initialize timers.
    worldStateBroadcastTimer = new FixedTimer(Settings.ServerSendRate, BroadcastWorldState);
    worldStateBroadcastTimer.Start();
  }

  public void InitializePlayerState(Player player) {
    playerConnectionInfo[player.Id] = new PlayerConnectionInfo();
    playerStateSnapshots[player.Id] = new PlayerState[1024];
  }

  public void ClearPlayerState(Player player) {
    playerConnectionInfo.Remove(player.Id);
    playerStateSnapshots.Remove(player.Id);
  }

  public void EnqueuePlayerInput(WithPeer<NetCommand.PlayerInputCommand> input) {
    Player player;
    try {
      player = playerManager.GetPlayerForPeer(input.Peer);
    } catch (KeyNotFoundException) {
      return;  // The player already disconnected, so just ignore this packet.
    }
    playerInputProcessor.EnqueueInput(input.Value, player, WorldTick);

    // Update connection info for the player.
    playerConnectionInfo[player.Id].latestInputTick =
        input.Value.StartWorldTick + (uint)input.Value.Inputs.Length - 1;
  }

  public bool ProcessPlayerAttack(Player player, HitscanAttack attack) {
    // First, rollback the state of all attackable entities (for now just players).
    // The world is not rolled back to the tick the players input was for, since
    // players are running ahead of the simulation (tick lead). Rather, we
    // rollback to the the tick of the server world state the player was seeing
    // at the time of attack on their client.
    // This is typically known as "Lag Compensation": 
    // @see https://developer.valvesoftware.com/wiki/Latency_Compensating_Methods_in_Client/Server_In-game_Protocol_Design_and_Optimization#:~:text=Lag%20compensation%20is%20a%20method,the%20user%20performed%20some%20action.

    // TODO: Clean up the whole player delegate path, it sucks.
    var remoteViewTick = currentPlayerInput[player.Id].RemoteViewTick;

    // If client interp is enabled, we estimate by subtracting another tick, but I'm not sure
    // if this is correct or not, needs more work.
    if (Settings.UseClientInterp) {
      remoteViewTick--;
    }

    uint bufidx = remoteViewTick % 1024;
    var head = new Dictionary<byte, PlayerState>();
    foreach (var entry in playerStateSnapshots) {
      var otherPlayer = playerManager.GetPlayer(entry.Key);
      head[otherPlayer.Id] = otherPlayer.Controller.ToNetworkState();
      var historicalState = entry.Value[bufidx];
      otherPlayer.Controller.ApplyNetworkState(historicalState);
    }

    // Now check for collisions.
    var playerObjectHit = attack.CheckHit();

    // Debugging.
    foreach (var entry in playerStateSnapshots) {
      var otherPlayer = playerManager.GetPlayer(entry.Key);
      if (otherPlayer.Id != player.Id) {
        this.Log($"Other player at ${otherPlayer.GameObject.transform.position} for remote view tick ${remoteViewTick}");
      }
    }

    // Finally, revert all the players to their head state.
    foreach (var entry in playerStateSnapshots) {
      var otherPlayer = playerManager.GetPlayer(entry.Key);
      otherPlayer.Controller.ApplyNetworkState(head[entry.Key]);
    }

    // Apply the result of the this.
    if (playerObjectHit != null) {
      this.Log("Registering authoritative player hit");
      attack.AddForceToPlayer(playerObjectHit.GetComponent<CPMPlayerController>());
      return true;
    }
    return false;
  }

  // Process a single world tick update.
  protected override void Tick(float dt) {
    var now = DateTime.Now;

    // Apply inputs to each player.
    unprocessedPlayerIds.Clear();
    unprocessedPlayerIds.UnionWith(playerManager.GetPlayerIds());
    var tickInputs = playerInputProcessor.DequeueInputsForTick(WorldTick);
    foreach (var tickInput in tickInputs) {
      var player = tickInput.Player;
      player.Controller.SetPlayerInputs(tickInput.Inputs);
      currentPlayerInput[player.Id] = tickInput;
      unprocessedPlayerIds.Remove(player.Id);

      // Mark the player as synchronized.
      if (playerConnectionInfo.ContainsKey(player.Id)) {
        playerConnectionInfo[player.Id].synchronized = true;
      }
    }

    // Any remaining players without inputs have their latest input command repeated,
    // but we notify them that they need to fast-forward their simulation to improve buffering.
    foreach (var playerId in unprocessedPlayerIds) {
      // If the player is not yet synchronized, this isn't an error.
      if (!playerConnectionInfo.ContainsKey(playerId) ||
          !playerConnectionInfo[playerId].synchronized) {
        continue;
      }

      var player = playerManager.GetPlayer(playerId);
      DebugUI.ShowValue("sv missed inputs", ++missedInputs);
      TickInput latestInput;
      if (playerInputProcessor.TryGetLatestInput(playerId, out latestInput)) {
        player.Controller.SetPlayerInputs(latestInput.Inputs);
      } else {
        this.LogWarning($"No inputs for player #{playerId} and no history to replay.");
      }
    }

    // Advance the world simulation.
    SimulateWorld(dt);
    if (UnityEngine.Random.value < debugPhysicsErrorChance) {
      this.Log("Injecting random physics error.");
      playerManager.GetPlayers().ForEach(
          p => p.GameObject.transform.Translate(new Vector3(1, 0, 0)));
    }
    ++WorldTick;

    // Snapshot everything.
    var bufidx = WorldTick % 1024;
    playerManager.GetPlayers().ForEach(p => {
      playerStateSnapshots[p.Id][bufidx] = p.Controller.ToNetworkState();
    });

    // Update post-tick timers.
    worldStateBroadcastTimer.Update(dt);
  }

  protected override void PostUpdate() {
    // Monitoring.
    var players = playerManager.GetPlayers();
    if (players.Count > 0) {
      playerInputProcessor.LogQueueStatsForPlayer(players[0], WorldTick);
    }
  }

  private void BroadcastWorldState(float dt) {
    var players = playerManager.GetPlayers();
    var playerStates = players.Select(p => p.Controller.ToNetworkState()).ToArray();
    foreach (var player in players) {
      var cmd = new NetCommand.WorldState {
        WorldTick = WorldTick,
        YourLatestInputTick = playerConnectionInfo[player.Id].latestInputTick,
        PlayerStates = playerStates,
      };
      handler.SendWorldState(player, cmd);
    }
  }
}
