using System;
using System.Collections.Generic;
using LiteNetLib;
using UnityEngine;

/// Network top-level command data structures.
namespace NetCommand {
  /** Client -> Server commands. */

  public class JoinRequest {
    public PlayerSetupData PlayerSetupData { get; set; }
  }

  public class PlayerInput {
    public uint WorldTick { get; set; }
    public PlayerInputs Inputs { get; set; }
  }

  /** Server -> Client commands. */

  public class JoinAccepted {
    public InitialPlayerState YourPlayerState { get; set; }
    public InitialPlayerState[] ExistingPlayerStates { get; set; }
  }

  public class PlayerJoined {
    public InitialPlayerState PlayerState { get; set; }
  }

  public class PlayerLeft {
    public byte PlayerId { get; set; }
  }

  public class WorldState {
    public uint WorldTick { get; set; }
    public PlayerState[] PlayerStates { get; set; }
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

      // Input and world state can be unreliable since it is sent every frame, but we use
      // sequenced so that older packets are simply dropped since we don't care about them anymore.
      { typeof(PlayerInput), DeliveryMethod.ReliableOrdered },
      { typeof(WorldState), DeliveryMethod.ReliableOrdered },
    };
  }
}
