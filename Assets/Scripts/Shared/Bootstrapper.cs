using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// Responsible for initializing the client or server state of the game
/// in a certain configuration. Sets up the state of any logic controllers
/// needed and then hands off control to them.
public class Bootstrapper : MonoBehaviour {
  public ClientLogicController clientLogicController;
  public ServerLogicController serverLogicController;

  private static int PORT = 10770;

  public async Task StartGameAsServer() {
    Debug.Log("Starting game as dedicated server.");
    await serverLogicController.StartServer(PORT);
  }

  public async Task StartGameAsListenServer() {
    Debug.Log("Starting game as a listen server.");
    await serverLogicController.StartServer(PORT, false);
    // Fake player data for now.
    var playerSetupData = new PlayerSetupData {
      Name = "Player",
    };
    clientLogicController.TryJoinServer("localhost", PORT, playerSetupData);
  }

  public void StartGameAsClient(Hotel.GameServer serverToJoin) {
    Debug.Log($"Joining server {serverToJoin.host}:{serverToJoin.port}...");
    // Fake player data for now.
    var playerSetupData = new PlayerSetupData {
      Name = "Player",
    };
    clientLogicController.TryJoinServer(serverToJoin.host, serverToJoin.port, playerSetupData);
  }
}
