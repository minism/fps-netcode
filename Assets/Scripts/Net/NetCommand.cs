using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

/// Network top-level command data structures.
namespace NetCommand {
  /** Client -> Server commands. */

  public class JoinRequest {
    public PlayerSetupData PlayerSetupData { get; set; }
  }

  /**
   * Custom serializable structure for reduntant player inputs.
   * 
   * Uses two strategies for compression:
   *   - Player keys are compressed using a bitfield.
   *   - We only store ticks where keys actually changed.
   */
  public struct PlayerInputCommand : INetSerializable {
    // The world tick for the first input in the array.
    public int StartWorldTick;

    // An array of inputs, one entry for tick.  Ticks are guaranteed to be contiguous.
    public PlayerInputs[] Inputs;

    // For each input:
    // Delta between the input world tick and the tick the server was at for that input.
    // TODO: This may be overkill, determining an average is probably better, but for now
    // this will give us 100% accuracy over lag compensation.
    public short[] ClientWorldTickDeltas;

    public void Serialize(NetDataWriter writer) {
      writer.Put(StartWorldTick);
      writer.Put(Inputs.Length);
      for (int i = 0; i < Inputs.Length; i++) {
        // Input.
        writer.Put(Inputs[i].GetKeyBitfield());
        writer.Put(Inputs[i].ViewDirection);

        // Tick delta.
        writer.Put(ClientWorldTickDeltas[i]);
      }
    }

    public void Deserialize(NetDataReader reader) {
      StartWorldTick = reader.GetInt();
      var length = reader.GetInt();
      Inputs = new PlayerInputs[length];
      ClientWorldTickDeltas = new short[length];
      for (int i = 0; i < length; i++) {
        Inputs[i].ApplyKeyBitfield(reader.GetByte());
        Inputs[i].ViewDirection = reader.GetQuaternion();
        ClientWorldTickDeltas[i] = reader.GetShort();
      }
    }
  }

  /** Server -> Client commands. */

  public class JoinAccepted {
    public InitialPlayerState YourPlayerState { get; set; }
    public InitialPlayerState[] ExistingPlayerStates { get; set; }

    // The current world tick on the server.
    // The client will initially set theirs to this plus a latency estimate.
    public int WorldTick { get; set; }
  }

  public class PlayerJoined {
    public InitialPlayerState PlayerState { get; set; }
  }

  public class PlayerLeft {
    public byte PlayerId { get; set; }
  }

  public class SpawnObject {
    // TODO: Should world tick go in here?
    // TODO: Utilize network object state better.
    public NetworkObjectState NetworkObjectState { get; set; }

    // The type of object being created.
    public NetworkObjectType Type { get; set; }

    // The player that created the object.
    public byte CreatorPlayerId { get; set; }

    // If this was an attack, indicates that it connected with a player
    // TODO: This is obviously not the right location for this.
    public bool WasAttackHit { get; set; }

    // Initial state for the object.
    public Vector3 Position { get; set; }
    public Quaternion Orientation { get; set; }
  }

  public class WorldState {
    // The world tick this data represents.
    public int WorldTick { get; set; }

    // The last world tick the server acknowledged for you.
    // The client should use this to determine the last acked input, as well as to compute
    // its relative simulation offset.
    public int YourLatestInputTick { get; set; }

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
      { typeof(SpawnObject), DeliveryMethod.ReliableOrdered },

      // Input and world state can be unreliable since it is sent every frame, but we use
      // sequenced so that older packets are simply dropped since we don't care about them anymore.
      { typeof(PlayerInputCommand), DeliveryMethod.Sequenced },
      { typeof(WorldState), DeliveryMethod.Sequenced },
    };
  }
}
