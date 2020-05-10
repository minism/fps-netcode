using LiteNetLib;
using System;
using System.Collections.Generic;

/// Network top-level command data structures.
namespace NetCommand {
  /** Client -> Server commands. */

  public class JoinRequest {
    public PlayerSetupData PlayerSetupData { get; set; }
  }

  public class PlayerInputCommand {
    // The world tick for the first input in the array.
    public uint StartWorldTick { get; set; }

    // An array of inputs, one entry for tick.  Ticks are guaranteed to be contiguous.
    public PlayerInputs[] Inputs { get; set; }
  }

  /** Server -> Client commands. */

  public class JoinAccepted {
    public InitialPlayerState YourPlayerState { get; set; }
    public InitialPlayerState[] ExistingPlayerStates { get; set; }

    // The current world tick on the server.
    // The client will initially set theirs to this plus a latency estimate.
    public uint WorldTick { get; set; }
  }

  public class PlayerJoined {
    public InitialPlayerState PlayerState { get; set; }
  }

  public class PlayerLeft {
    public byte PlayerId { get; set; }
  }

  public class WorldState {
    // The world tick this data represents.
    public uint WorldTick { get; set; }

    // The last world tick the server acknowledged for you.
    // The client should use this to determine the last acked input, as well as to compute
    // its relative simulation offset.
    public uint YourLatestInputTick { get; set; }

    // States for all active players.
    public PlayerState[] PlayerStates { get; set; }
  }

  // Inform the client that they are running too far ahead or behind, and to adjust.
  public class AdjustSimulation {
    // How far the client is leading the server.
    // A value of zero indicates means we're actually dropping (and replaying) input.
    public int ActualTickLead { get; set; }

    // The amount of ticks the client should offset their simulation by.
    public int TickOffset { get; set; }
  }

  /// Metadata about each command.
  public static class Metadata {

    /// Mapping of the command type to its default delivery method, for convenience.
    public static Dictionary<Type, DeliveryMethod> DeliveryType = new Dictionary<Type, DeliveryMethod>() {
      // Major state changes must be reliable ordered.
      { typeof(JoinRequest), DeliveryMethod.ReliableOrdered },
      { typeof(JoinAccepted), DeliveryMethod.ReliableOrdered },
      { typeof(PlayerJoined), DeliveryMethod.ReliableOrdered },
      { typeof(PlayerLeft), DeliveryMethod.ReliableOrdered },
      { typeof(AdjustSimulation), DeliveryMethod.ReliableOrdered },

      // Input and world state can be unreliable since it is sent every frame, but we use
      // sequenced so that older packets are simply dropped since we don't care about them anymore.
      { typeof(PlayerInputCommand), DeliveryMethod.Sequenced },
      { typeof(WorldState), DeliveryMethod.Sequenced },
    };
  }
}
