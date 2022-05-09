using LiteNetLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Helper class to manage active players.
public class PlayerManager : IPlayerLookup {
  private Dictionary<byte, Player> players;

  // Fast-cache of player IDs.
  private byte[] playerIds = new byte[0];

  public PlayerManager() {
    players = new Dictionary<byte, Player>();
  }

  public bool TryGetPlayer(byte id, out Player player) {
    return players.TryGetValue(id, out player);
  }

  public Player GetPlayer(byte id) {
    return players[id];
  }

  public List<Player> GetPlayers() {
    return new List<Player>(players.Values);
  }

  public byte[] GetPlayerIds() {
    return playerIds;
  }

  public Player GetPlayerForPeer(NetPeer peer) {
    return players[(byte)peer.Id];
  }

  public bool TryGetPlayerForPeer(NetPeer peer, out Player player) {
    return TryGetPlayer((byte)peer.Id, out player);
  }

  public Player AddPlayer(byte playerId, PlayerMetadata metadata, GameObject playerGameObject) {
    var player = new Player {
      Id = playerId,
      Metadata = metadata,
      GameObject = playerGameObject,
      NetworkObject = playerGameObject.GetComponent<NetworkObject>(),
      Controller = playerGameObject.GetComponent<IPlayerController>(),
    };
    players.Add(playerId, player);
    CachePlayerIds();
    return player;
  }

  public void RemovePlayer(byte playerId) {
    players.Remove(playerId);
    CachePlayerIds();
  }

  public void Clear() {
    players.Clear();
    CachePlayerIds();
  }

  private void CachePlayerIds() {
    playerIds = GetPlayers().Select(p => p.Id).ToArray();
  }
}

public interface IPlayerLookup {
  Player GetPlayer(byte id);
}
