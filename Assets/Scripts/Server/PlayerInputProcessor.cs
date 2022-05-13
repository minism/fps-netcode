using Priority_Queue;
using System;
using System.Collections.Generic;

// Simple structure representing a particular players inputs at a world tick.
public struct TickInput {
  public int WorldTick;

  // The remote world tick the player saw other entities at for this input.
  // Needed for server-side lag compensation.
  // (This is equivalent to lastServerWorldTick on the client).
  public int RemoteViewTick;

  public Player Player;
  public PlayerInputs Inputs;
}

// Processes input network commands from a set of players and presents them in
// a way to the simulation which is easier to interact with.
public class PlayerInputProcessor {
  private SimplePriorityQueue<TickInput> queue = new SimplePriorityQueue<TickInput>();
  private Dictionary<byte, TickInput> latestPlayerInput = new Dictionary<byte, TickInput>();

  // Monitoring.
  private Ice.MovingAverage averageInputQueueSize = new Ice.MovingAverage(10);

  public void LogQueueStatsForPlayer(Player player, int worldTick) {
    int count = 0;
    foreach (var entry in queue) {
      if (entry.Player.Id == player.Id && entry.WorldTick >= worldTick) {
        count++;
        worldTick++;
      }
    }
    averageInputQueueSize.Push(count);
    this.LogValue("sv avg input queue", averageInputQueueSize.Average());
  }

  public int GetLatestPlayerInputTick(byte playerId) {
    TickInput input;
    if (!TryGetLatestInput(playerId, out input)) {
      return 0;
    }
    return input.WorldTick;
  }

  public bool TryGetLatestInput(byte playerId, out TickInput ret) {
    return latestPlayerInput.TryGetValue(playerId, out ret);
  }

  public List<TickInput> DequeueInputsForTick(int worldTick) {
    var ret = new List<TickInput>();
    TickInput entry;
    while (queue.TryDequeue(out entry)) {
      if (entry.WorldTick < worldTick) {
      } else if (entry.WorldTick == worldTick) {
        ret.Add(entry);
      } else {
        // We dequeued a future input, put it back in.
        queue.Enqueue(entry, entry.WorldTick);
        break;
      }
    }
    return ret;
  }

  public void EnqueueInput(NetCommand.PlayerInputCommand command, Player player, int lastAckedInputTick) {
    // Calculate the last tick in the incoming command.
    int maxTick = command.StartWorldTick + command.Inputs.Length - 1;

    // Queue any inputs we haven't yet acked.
    int startIndex = lastAckedInputTick >= command.StartWorldTick
      ? lastAckedInputTick - command.StartWorldTick + 1
      : 0;

    // Note that even if the client is behind, we still want to queue historical
    // inputs so we can use the "best" (most recent) input when filling gaps.

    // Scan for inputs which haven't been handled yet.
    for (int i = startIndex; i < command.Inputs.Length; ++i) {
      // Apply inputs to the associated player controller and simulate the world.
      var worldTick = command.StartWorldTick + i;
      var tickInput = new TickInput {
        WorldTick = worldTick,
        RemoteViewTick = worldTick - command.ClientWorldTickDeltas[i],
        Player = player,
        Inputs = command.Inputs[i],
      };
      queue.Enqueue(tickInput, worldTick);

      // Store the latest input in case the simulation needs to repeat missed frames.
      latestPlayerInput[player.Id] = tickInput;
    }
  }
}
