using UnityEngine;
using System;
using System.Collections.Generic;

// Keeps track of how far ahead / behind clients are and notifies them
// when they need to adjust.
public class PlayerSimulationAdjuster {
  private ServerSimulation.Handler handler;

  // The last time we sent an RPC to the player to adjust their window.  We don't
  // want to do this too often because of network delay which could exacerbate the
  // situation.
  // TODO: Need to do more research, but its possible this should be based on the 
  // known ping to the client?
  private Dictionary<byte, DateTime> lastAdjustmentTimes =
      new Dictionary<byte, DateTime>();

  // The last time we got an input from a player which was below the ideal tick lead threshold. 
  private Dictionary<byte, DateTime> lastIdealInputTimes =
      new Dictionary<byte, DateTime>();

  public PlayerSimulationAdjuster(ServerSimulation.Handler handler) {
    this.handler = handler;
  }

  // Notifies the adjuster that an input was dropped.
  public void NotifyDroppedInput(Player player) {
    // Tell the client it needs to increase its tick lead.
    // TODO: Come up with a smarter mechanism for determining this value.
    MaybeAdjust(player, 0, 5);
  }

  public void NotifyReceivedInput(
      NetCommand.PlayerInput command, Player player, uint serverWorldTick) {
    // Initialize any assumptions.
    var now = DateTime.Now;
    if (!lastIdealInputTimes.ContainsKey(player.PlayerId)) {
      lastIdealInputTimes[player.PlayerId] = now;
    }

    uint maxTick = command.StartWorldTick + (uint)command.Inputs.Length - 1;
    if (maxTick >= serverWorldTick) {
      uint lead = maxTick - serverWorldTick;
      DebugUI.ShowValue("deleteme", lead);
      if (lead < Settings.ClientIdealBufferedInputLimit) {
        lastIdealInputTimes[player.PlayerId] = now;
      } else {
        // If the client has sustained a lead which is too far, it needs to decrease its lead.
        if (now - lastIdealInputTimes[player.PlayerId] > Settings.ClientBufferTooHighInterval) {
          MaybeAdjust(player, (int)lead, -(int)lead/3);
        }
      }
    } else {
      // TODO: Do we need to handle this case? Its equivalent to dropped input.
      // Actually if so, we could probably just deal with it here.
    }
  }

  private void MaybeAdjust(Player player, int actualTickLead, int tickOffset) {
    var now = DateTime.Now;
    if (!lastAdjustmentTimes.ContainsKey(player.PlayerId) ||
        now - lastAdjustmentTimes[player.PlayerId] > Settings.MinClientAdjustmentInterval) {
      lastAdjustmentTimes[player.PlayerId] = now;
      handler.AdjustPlayerSimulation(player, actualTickLead, tickOffset);
    }
  }
}
