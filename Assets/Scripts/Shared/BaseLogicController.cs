using UnityEngine;
using LiteNetLib;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;

/// Base component for primary logic and dependencies needed by both client and server.
[RequireComponent(typeof(NetworkObjectManager))]
public abstract class BaseLogicController : MonoBehaviour {
  public NetMonitor netMonitor;

  protected NetworkObjectManager networkObjectManager;
  protected NetChannel netChannel;
  protected PlayerManager playerManager;

  protected virtual void Awake() {
    networkObjectManager = GetComponent<NetworkObjectManager>();
    playerManager = new PlayerManager();
    netChannel = new NetChannel();
    if (netMonitor != null) {
      netChannel.SetNetMonitor(netMonitor);
    }

    netChannel.PeerConnectedHandler += OnPeerConnected;
    netChannel.PeerDisconnectedHandler += OnPeerDisconnected;
  }

  protected virtual void Start() { }

  protected virtual void Update() {
    netChannel.Update();
  }

  protected virtual void OnApplicationQuit() {
    netChannel.Destroy();
  }

  protected void LoadGameScene() {
    SceneManager.LoadScene("Game");
  }

  protected AsyncOperation InitializeGameSceneAsync() {
    return SceneManager.LoadSceneAsync("Game");
  }

  protected void LoadLobbyScene() {
    SceneManager.LoadScene("Lobby");
  }

  protected abstract void OnPeerConnected(NetPeer peer);

  protected abstract void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo);
}
