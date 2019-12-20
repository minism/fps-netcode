using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour {
  public Transform joinButtonContainer;

  private GameObject joinButtonPrototype;

  private async void Start() {
    // Setup references.
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
    FindObjectOfType<Bootstrapper>().StartGameAsServer();
  }

  public void Listen() {
    FindObjectOfType<Bootstrapper>().StartGameAsListenServer();
  }

  public void Quit() {
    Application.Quit();
  }

  private async Task RefreshServerList() {
    CleanupJoinButtons();

    var servers = await Hotel.HotelClient.Instance.ListGameServers();
    foreach (var server in servers) {
      var obj = Instantiate(joinButtonPrototype, joinButtonContainer);
      obj.SetActive(true);
      obj.name = "Join Button";
      var text = obj.GetComponentInChildren<Text>();
      text.text = $"Join '{server.name}'";

      // Add a click listener.
      obj.GetComponent<Button>().onClick.AddListener(() => {
        FindObjectOfType<Bootstrapper>().StartGameAsClient(server);
      });
    }
  }
  
  private void CleanupJoinButtons() {
    Ice.ObjectUtil.DestroyChildren(joinButtonContainer);
  }
}
