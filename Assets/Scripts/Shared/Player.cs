using LiteNetLib;
using UnityEngine;

/// Top-level handle for an active player in the game.
public class Player : IReadonlyPlayer {
  // The ID for the player which is unique across all connected clients.
  public byte PlayerId { get; set; }

  // Set-once metadata for the player, not transmitted on all packets.
  public PlayerMetadata Metadata { get; set; }

  // The associated in-scene game object.
  public GameObject GameObject { get; set; }

  // The associated in-scene network component.
  public NetworkObject NetworkObject { get; set; }

  // The associated in-scene controller component.
  public IPlayerController Controller { get; set; }

  // The associated in-scene kinematic motor component.
  public KinematicCharacterController.KinematicCharacterMotor Motor { get; set; }

  // The associated network peer for the player (only set on server).
  public NetPeer Peer { get; set; }

  public InitialPlayerState ToInitialPlayerState() {
    return new InitialPlayerState {
      PlayerId = PlayerId,
      Metadata = Metadata,
      NetworkObjectState = new NetworkObjectState {
        NetworkId = NetworkObject.NetworkId,
      },
      PlayerState = Controller.ToNetworkState(),
    };
  }
}

public interface IReadonlyPlayer {
  byte PlayerId { get; }
  PlayerMetadata Metadata { get; }
}
