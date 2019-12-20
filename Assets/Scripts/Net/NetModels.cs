using KinematicCharacterController;
using LiteNetLib.Utils;
using UnityEngine;


/// Shared network data structures.

/// Data entered by a player when joining a game.
public struct PlayerSetupData : INetSerializable {
  public string Name;

  public void Serialize(NetDataWriter writer) {
    writer.Put(Name);
  }

  public void Deserialize(NetDataReader reader) {
    Name = reader.GetString();
  }
}

/// Metadata for a player.
public struct PlayerMetadata : INetSerializable {
  public string Name;

  public void Serialize(NetDataWriter writer) {
    writer.Put(Name);
  }

  public void Deserialize(NetDataReader reader) {
    Name = reader.GetString();
  }
}

/// Initial player state sent once to each client.
public struct InitialPlayerState : INetSerializable {
  public byte PlayerId;
  public PlayerMetadata Metadata;
  public PlayerState PlayerState;
  public NetworkObjectState NetworkObjectState;

  public void Serialize(NetDataWriter writer) {
    writer.Put(PlayerId);
    writer.Put(Metadata);
    writer.Put(PlayerState);
    writer.Put(NetworkObjectState);
  }

  public void Deserialize(NetDataReader reader) {
    PlayerId = reader.GetByte();
    Metadata = reader.Get<PlayerMetadata>();
    PlayerState = reader.Get<PlayerState>();
    NetworkObjectState = reader.Get<NetworkObjectState>();
  }
}

/// Per-frame player state.
/// TODO: See later if the physics state can be merged with network state,
/// once we have non-player networked objects.
public struct PlayerState : INetSerializable {
  public Vector3 SimplePosition;
  public KinematicCharacterMotorState MotorState;

  public void Serialize(NetDataWriter writer) {
    writer.Put(SimplePosition);
    NetExtensions.SerializeKinematicMotorState(writer, MotorState);
  }

  public void Deserialize(NetDataReader reader) {
    SimplePosition = reader.GetVector3();
    MotorState = NetExtensions.DeserializeKinematicMotorState(reader);
  }
}

/// Player input data.
public struct PlayerInputs : INetSerializable {
  public float ForwardAxis;
  public float RightAxis;
  public Quaternion CameraOrientation;
  public bool Jump;

  public void Serialize(NetDataWriter writer) {
    writer.Put(ForwardAxis);
    writer.Put(RightAxis);
    writer.Put(CameraOrientation);
    writer.Put(Jump);
  }

  public void Deserialize(NetDataReader reader) {
    ForwardAxis = reader.GetFloat();
    RightAxis = reader.GetFloat();
    CameraOrientation = reader.GetQuaternion();
    Jump = reader.GetBool();
  }
}

// 50 bytes
public struct NetworkObjectState : INetSerializable {
  public ushort NetworkId;
  //public Vector3 Position;
  //public Vector3 Rotation;
  //public Vector3 Velocity;
  //public Vector3 AngularVelocity;

  public void Serialize(NetDataWriter writer) {
    writer.Put(NetworkId);
    //writer.Put(Position);
    //writer.Put(Rotation);
    //writer.Put(Velocity);
    //writer.Put(AngularVelocity);
  }

  public void Deserialize(NetDataReader reader) {
    NetworkId = reader.GetUShort();
    //Position = reader.GetVector3();
    //Rotation = reader.GetVector3();
    //Velocity = reader.GetVector3();
    //AngularVelocity = reader.GetVector3();
  }
}

