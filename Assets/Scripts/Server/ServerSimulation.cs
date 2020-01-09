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

  // I/O interface for world states.
  public interface Handler {
    void BroadcastWorldState(NetCommand.WorldState state);
  }
  private Handler handler;

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
  }

  public void EnqueuePlayerInput(WithPeer<NetCommand.PlayerInput> input) {
    var player = playerManager.GetPlayerForPeer(input.Peer);
    playerInputProcessor.EnqueueInput(input.Value, player, WorldTick);
  }

  // Process a single world tick update.
  protected override void Tick() {
    // Apply inputs to each player.
    unprocessedPlayerIds.UnionWith(playerManager.GetPlayerIds());
    var tickInputs = playerInputProcessor.DequeueInputsForTick(WorldTick);
    foreach (var tickInput in tickInputs) {
      var player = tickInput.Player;
      player.Controller.SetPlayerInputs(tickInput.Inputs);
      unprocessedPlayerIds.Remove(player.PlayerId);
    }

    // Any remaining players without inputs have their latest input command repeated,
    // but we notify them that they need to fast-forward their simulation to improve buffering.
    foreach (var playerId in unprocessedPlayerIds) {
      DebugUI.ShowValue("missed inputs", ++missedInputs);
      TickInput latestInput;
      if (playerInputProcessor.TryGetLatestInput(playerId, out latestInput)) {
        playerManager.GetPlayer(playerId).Controller.SetPlayerInputs(latestInput.Inputs);
      } else {
        Debug.LogWarning($"No inputs for player #{playerId} and no history to replay.");
      }
    }

    // TODO: Check for missing inputs and notify player here.

    // Advance the world simulation.
    SimulateWorld(Time.fixedDeltaTime);
    if (Random.value < debugPhysicsErrorChance) {
      Debug.Log("Injecting random physics error.");
      playerManager.GetPlayers().ForEach(
          p => p.GameObject.transform.Translate(new Vector3(1, 0, 0)));
    }
    ++WorldTick;

    // Broadcast the world state.
    // The new world state tick is N+1, given input world tick N.
    var worldStateCmd = new NetCommand.WorldState {
      WorldTick = WorldTick,
      PlayerStates = playerManager.GetPlayers().Select(
          p => p.Controller.ToNetworkState()).ToArray(),
    };
    handler.BroadcastWorldState(worldStateCmd);
  }

  protected override void PostUpdate() {
    // Monitoring.
    DebugUI.ShowValue("tick", WorldTick);
    var players = playerManager.GetPlayers();
    if (players.Count > 0) {
      playerInputProcessor.LogQueueStatsForPlayer(players[0], WorldTick);
    }
  }
}
