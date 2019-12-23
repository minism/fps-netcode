using UnityEngine;
using LiteNetLib;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
using KinematicCharacterController;

/// Base component for primary logic and dependencies needed by both client and server.
[RequireComponent(typeof(NetworkObjectManager))]
public abstract class BaseLogicController : MonoBehaviour {
  public NetMonitor netMonitor;
  public DebugNetworkSettings debugNetworkSettings;

  // Delegates.
  protected NetworkObjectManager networkObjectManager;
  protected NetChannel netChannel;
  protected PlayerManager playerManager;

  // Cached lists for keeping track of kinematic objects in the scene.
  protected List<KinematicCharacterMotor> activeKinematicMotors = new List<KinematicCharacterMotor>();
  protected List<PhysicsMover> activePhysicsMovers = new List<PhysicsMover>();

  protected virtual void Awake() {
    networkObjectManager = GetComponent<NetworkObjectManager>();
    playerManager = new PlayerManager();
    netChannel = new NetChannel(debugNetworkSettings);
    if (netMonitor != null) {
      netChannel.SetNetMonitor(netMonitor);
    }

    netChannel.PeerConnectedHandler += OnPeerConnected;
    netChannel.PeerDisconnectedHandler += OnPeerDisconnected;
  }

  protected virtual void Start() { }

  protected virtual void Update() {
    netChannel.Update();

    if (Input.GetKeyDown(KeyCode.F1)) {
      TearDownGameScene();
      LoadLobbyScene();
    }
  }

  protected void SimulateWorld(float dt) {
    //KinematicCharacterSystem.Simulate(dt, activeKinematicMotors, activePhysicsMovers);
    playerManager.GetPlayers().ForEach(p => p.Controller.Simulate(dt));
  }

  private void OnApplicationQuit() {
    TearDownGameScene();
  }

  protected virtual void TearDownGameScene() {
    Debug.Log("Stopping network stack.");
    Cursor.lockState = CursorLockMode.None;
    netChannel.Stop();
  }

  protected void LoadGameScene() {
    SceneManager.LoadScene("Game");
  }

  protected AsyncOperation LoadGameSceneAsync() {
    return SceneManager.LoadSceneAsync("Game");
  }

  protected void LoadLobbyScene() {
    SceneManager.LoadScene("Lobby");
  }

  protected abstract void OnPeerConnected(NetPeer peer);

  protected abstract void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo);
}
