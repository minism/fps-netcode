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
  public NetworkObjectState NetworkObjectState;

  public void Serialize(NetDataWriter writer) {
    writer.Put(PlayerId);
    writer.Put(Metadata);
    writer.Put(NetworkObjectState);
  }

  public void Deserialize(NetDataReader reader) {
    PlayerId = reader.GetByte();
    Metadata = reader.Get<PlayerMetadata>();
    NetworkObjectState = reader.Get<NetworkObjectState>();
  }
}

// 50 bytes
public struct NetworkObjectState : INetSerializable {
  public ushort NetworkId;
  public Vector3 Position;
  public Vector3 Rotation;
  public Vector3 Velocity;
  public Vector3 AngularVelocity;

  public void Serialize(NetDataWriter writer) {
    writer.Put(NetworkId);
    writer.Put(Position);
		writer.Put(Rotation);
		writer.Put(Velocity);
		writer.Put(AngularVelocity);
  }

  public void Deserialize(NetDataReader reader) {
    NetworkId = reader.GetUShort(); 
    Position = reader.GetVector3();
    Rotation = reader.GetVector3();
    Velocity = reader.GetVector3();
    AngularVelocity = reader.GetVector3();
  }
}

