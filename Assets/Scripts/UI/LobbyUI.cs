using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour {
  public Transform joinButtonContainer;

  private Bootstrapper bootstrapper;
  private ClientLogicController clientController;

  private GameObject joinButtonPrototype;
  private Hotel.GameServer[] gameServers;
  private Dictionary<int, int> serverLatencies = new Dictionary<int, int>();

  private async void Start() {
    // Setup references.
    bootstrapper = FindObjectOfType<Bootstrapper>();
    clientController = FindObjectOfType<ClientLogicController>();
    var button = joinButtonContainer.GetComponentInChildren<Button>();
    if (button == null) {
      Debug.LogError("Expected a prototype join button in the container.");
    } else {
      joinButtonPrototype = button.gameObject;
      joinButtonPrototype.SetActive(false);
      joinButtonPrototype.transform.parent = null;
    }

    Debug.Log("Waiting for hotel client to start...");
    await Hotel.HotelClient.Instance.WaitUntilInitialized();
    Debug.Log("Loading initial server list...");
    await RefreshServerList();
    Debug.Log("Servers loaded.");
  }

  public void Refresh() {
    RefreshServerList();
  }

  public void Host() {
    bootstrapper.StartGameAsServer();
  }

  public void Listen() {
    bootstrapper.StartGameAsListenServer();
  }

  public void Quit() {
    Application.Quit();
  }

  private async Task RefreshServerList() {
    gameServers = await Hotel.HotelClient.Instance.ListGameServers();
    foreach (var server in gameServers) {
      // Fetch ping info if we don't yet have it.
      if (!serverLatencies.ContainsKey(server.id)) {
        clientController.PingServer(server.ResolveIPEndPoint(), (latency) => {
          serverLatencies[server.id] = latency;
          UpdateDisplay();
        });
      }
    }
    UpdateDisplay();
  }
  
  private void CleanupJoinButtons() {
    Ice.ObjectUtil.DestroyChildren(joinButtonContainer);
  }

  private void UpdateDisplay() {
    CleanupJoinButtons();
    foreach (var server in gameServers) {
      var obj = Instantiate(joinButtonPrototype, joinButtonContainer);
      obj.SetActive(true);
      obj.name = "Join Button";
      var text = obj.GetComponentInChildren<Text>();

      var hasPing = serverLatencies.ContainsKey(server.id);
      var ping = hasPing ? serverLatencies[server.id].ToString() : "waiting for ping...";
      text.text = $"Join '{server.name}' (Players: {server.numPlayers}/{server.maxPlayers}) (Ping: {ping})";

      // Add a click listener.
      if (hasPing) {
        obj.GetComponent<Button>().onClick.RemoveAllListeners();
        obj.GetComponent<Button>().onClick.AddListener(() => {
          FindObjectOfType<Bootstrapper>().StartGameAsClient(server, serverLatencies[server.id]);
        });
      }
    }
  }
}
