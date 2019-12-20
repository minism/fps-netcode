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

  /// Metadata about each command.
  public static class Metadata {

    /// Mapping of the command type to its default delivery method, for convenience.
    public static Dictionary<Type, DeliveryMethod> DeliveryType = new Dictionary<Type, DeliveryMethod>() {
      { typeof(JoinRequest), DeliveryMethod.ReliableOrdered },
      { typeof(JoinAccepted), DeliveryMethod.ReliableOrdered },
      { typeof(PlayerJoined), DeliveryMethod.ReliableOrdered },
      { typeof(PlayerLeft), DeliveryMethod.ReliableOrdered },
    };
  }
}
