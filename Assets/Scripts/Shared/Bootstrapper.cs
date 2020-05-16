using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Responsible for initializing the client or server state of the game
/// in a certain configuration. Sets up the state of any logic controllers
/// needed and then hands off control to them.
public class Bootstrapper : MonoBehaviour {
  public string initialScene = "Lobby";
  public ClientLogicController clientLogicController;
  public ServerLogicController serverLogicController;

  private static string DEFAULT_HOST = "localhost";
  private static int DEFAULT_PORT = 10770;

  private void Start() {
    // Parse command line arguments.
    var host = Hotel.Util.GetFlagValue("--host");
    var port = Hotel.Util.GetFlagValue("--port");

    // If host and port was specified via command line, start a server immediately.
    if (!string.IsNullOrEmpty(port) && !string.IsNullOrEmpty(host)) {
      // Also override the target framerate to be reasonable so we dont just burn CPU on a server.
      Application.targetFrameRate = 60;
      StartGameAsServer(host, int.Parse(port));
    } else {
      SceneManager.LoadScene(initialScene);
    }
  }

  public async Task StartGameAsServer() {
    StartGameAsServer(DEFAULT_HOST, DEFAULT_PORT);
  }

  public async Task StartGameAsServer(string host, int port) {
    Debug.Log($"Starting game as dedicated server on port {port}.");
    clientLogicController.gameObject.SetActive(false);
    await serverLogicController.StartServer(host, port);
  }

  public async Task StartGameAsListenServer() {
    Debug.Log("Starting game as a listen server.");
    await serverLogicController.StartServer(DEFAULT_HOST, DEFAULT_PORT, false);
    // Fake player data for now.
    var playerSetupData = new PlayerSetupData {
      Name = "Player",
    };
    clientLogicController.StartClient("localhost", DEFAULT_PORT, 0, playerSetupData);
  }

  public void StartGameAsClient(Hotel.GameServer serverToJoin, int initialLatency) {
    Debug.Log($"Joining server {serverToJoin.host}:{serverToJoin.port}...");
    serverLogicController.gameObject.SetActive(false);

    // Fake player data for now.
    var playerSetupData = new PlayerSetupData {
      Name = "Player",
    };
    clientLogicController.StartClient(
        serverToJoin.host, serverToJoin.port, initialLatency, playerSetupData);
  }
}
