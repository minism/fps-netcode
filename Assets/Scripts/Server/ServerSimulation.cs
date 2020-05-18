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

  // Simulation info for each player, indexed by player ID (peer ID).
  private Dictionary<byte, PlayerConnectionInfo> playerConnectionInfo
      = new Dictionary<byte, PlayerConnectionInfo>();

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
  }

  public void ClearPlayerState(Player player) {
    InitializePlayerState(player);
  }

  public void EnqueuePlayerInput(WithPeer<NetCommand.PlayerInputCommand> input) {
    Player player;
    try {
      player = playerManager.GetPlayerForPeer(input.Peer);
    } catch (KeyNotFoundException) {
      return;  // The player already disconnected, so just ignore this packet.
    }
    playerInputProcessor.EnqueueInput(input.Value, player, WorldTick);

    // Mark the latest tick for the player.
    playerConnectionInfo[player.Id].latestInputTick =
        input.Value.StartWorldTick + (uint)input.Value.Inputs.Length - 1;
  }

  public bool ProcessAttack(HitscanAttack attack) {
    // TODO: Lag compensation should go here, we should look for historical player positions.
    var playerObjectHit = attack.CheckHit();
    if (playerObjectHit != null) {
      Debug.Log("Registering authoritative player hit");
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
      unprocessedPlayerIds.Remove(player.Id);

      // Mark the player as synchronized.
      playerConnectionInfo[player.Id].synchronized = true;
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
        Debug.LogWarning($"No inputs for player #{playerId} and no history to replay.");
      }
    }

    // Advance the world simulation.
    SimulateWorld(dt);
    if (UnityEngine.Random.value < debugPhysicsErrorChance) {
      Debug.Log("Injecting random physics error.");
      playerManager.GetPlayers().ForEach(
          p => p.GameObject.transform.Translate(new Vector3(1, 0, 0)));
    }
    ++WorldTick;

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
