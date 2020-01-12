using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Client world simulation including prediction and state rewind.
// Inputs are state frames from the server.
// Outputs are player command frames to the server.
public class ServerSimulation : BaseSimulation {
  // Debugging.
  public float debugPhysicsErrorChance;

  // Delegate for processing input packets.
  private PlayerInputProcessor playerInputProcessor;

  // Reusable hash set for players whose input we've checked each frame.
  private HashSet<byte> unprocessedPlayerIds = new HashSet<byte>();

  // Synchronization states for players.
  private Dictionary<byte, bool> playerSyncState = new Dictionary<byte, bool>();

  // Timestamps for adjustment commands.
  private Dictionary<byte, DateTime> playerLastAdjustmentTimes =
      new Dictionary<byte, DateTime>();

  // I/O interface for world states.
  public interface Handler {
    void BroadcastWorldState(NetCommand.WorldState state);
    void AdjustPlayerSimulation(Player player, int actualTickLead, int tickOffset);
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

  public void ClearPlayerState(Player player) {
    playerSyncState[player.PlayerId] = false;
  }

  public void EnqueuePlayerInput(WithPeer<NetCommand.PlayerInput> input) {
    Player player;
    try {
      player = playerManager.GetPlayerForPeer(input.Peer);
    } catch (KeyNotFoundException) {
      return;  // The player already disconnected, so just ignore this packet.
    }
    playerInputProcessor.EnqueueInput(input.Value, player, WorldTick);
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
      unprocessedPlayerIds.Remove(player.PlayerId);

      // Mark the player as synchronized.
      playerSyncState[player.PlayerId] = true;
    }

    // Any remaining players without inputs have their latest input command repeated,
    // but we notify them that they need to fast-forward their simulation to improve buffering.
    foreach (var playerId in unprocessedPlayerIds) {
      // If the player is not yet synchronized, this isn't an error.
      if (!playerSyncState.ContainsKey(playerId) || !playerSyncState[playerId]) {
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

      // Tell the client it needs to increase its tick lead.
      // TODO: Come up with a smarter mechanism for determining this value.
      if (!playerLastAdjustmentTimes.ContainsKey(playerId) ||
          now - playerLastAdjustmentTimes[playerId] > Settings.MinClientAdjustmentInterval) {
        playerLastAdjustmentTimes[playerId] = now;
        handler.AdjustPlayerSimulation(player, 0, 5);
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
    DebugUI.ShowValue("sv tick", WorldTick);
    var players = playerManager.GetPlayers();
    if (players.Count > 0) {
      playerInputProcessor.LogQueueStatsForPlayer(players[0], WorldTick);
    }
  }

  private void BroadcastWorldState(float dt) {
    var worldStateCmd = new NetCommand.WorldState {
      WorldTick = WorldTick,
      PlayerStates = playerManager.GetPlayers().Select(
          p => p.Controller.ToNetworkState()).ToArray(),
    };
    handler.BroadcastWorldState(worldStateCmd);
  }
}
