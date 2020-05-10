using UnityEngine;

/// Indicates a synchronized presence over the network for this object.
/// NetworkID is common to all clients and server.
/// Similar concept to NetworkIdentity in Unity's deprecated implementation.
public class NetworkObject : MonoBehaviour {
  private INetworkComponent[] networkComponents;

  private ushort _networkId;

  public ushort NetworkId {
    get {
      return _networkId;
    }
    set {
      if (_networkId > 0) {
        throw new System.InvalidOperationException("Cannot set networkID more than once.");
      }
      _networkId = value;
    }
  }

  private void Awake() {
    networkComponents = GetComponents<INetworkComponent>();
  }

  public NetworkObjectState ToNetworkState() {
    return new NetworkObjectState {
      NetworkId = NetworkId,
    };
  }

  public void ApplyNetworkState(NetworkObjectState state) {
  }

  private void OnGUI() {
    if (GamePrefs.DebugMode) {
      var p = Camera.main.WorldToScreenPoint(transform.position);
      var screenRect = new Rect(p.x, Screen.height - p.y, 100, 20);
      GUI.Label(screenRect, $"id: {NetworkId}");
    }
  }
}
