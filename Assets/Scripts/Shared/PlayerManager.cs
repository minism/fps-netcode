using System.Collections.Generic;
using LiteNetLib;
using UnityEngine;

/// Helper class to manage active players.
public class PlayerManager : IPlayerLookup {
  private Dictionary<byte, Player> players;

  public PlayerManager() {
    players = new Dictionary<byte, Player>();
  }

  public Player GetPlayer(byte id) {
    return players[id];
  }

  public List<Player> GetPlayers() {
    return new List<Player>(players.Values);
  }

  public Player GetPlayerForPeer(NetPeer peer) {
    return players[(byte)peer.Id];
  }

  public Player AddPlayer(byte playerId, PlayerMetadata metadata, GameObject playerGameObject) {
    var player = new Player {
      PlayerId = playerId,
      Metadata = metadata,
      NetworkObject = playerGameObject.GetComponent<NetworkObject>(),
      Controller = playerGameObject.GetComponent<PlayerController>(),
      Motor = playerGameObject.GetComponent<KinematicCharacterController.KinematicCharacterMotor>(),
      //Rigidbody = playerGameObject.GetComponent<Rigidbody>(),
    };
    players.Add(playerId, player);
    return player;
  }

  public void RemovePlayer(byte playerId) {
    players.Remove(playerId);
  }
}

public interface IPlayerLookup {
  Player GetPlayer(byte id);
}
