using System.Collections.Generic;
using System.Linq;

/// Helper method for building network commands.
public static class CommandBuilder {
  public static NetCommand.PlayerJoined BuildPlayerJoinedCmd(Player newPlayer) {
    return new NetCommand.PlayerJoined {
      PlayerState = newPlayer.ToInitialPlayerState(),
    };
  }

  public static NetCommand.JoinAccepted BuildJoinAcceptedCmd(
      Player newPlayer, List<Player> existingPlayers) {
    return new NetCommand.JoinAccepted {
      YourPlayerState = newPlayer.ToInitialPlayerState(),
      ExistingPlayerStates = existingPlayers.Select(p => p.ToInitialPlayerState()).ToArray(),
    };
  }
}
