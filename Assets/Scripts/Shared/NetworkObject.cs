using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Indicates a synchronized presence over the network for this object.
/// NetworkID is common to all clients and server.
/// Similar concept to NetworkIdentity in Unity's deprecated implementation.
[RequireComponent(typeof(Rigidbody))]
public class NetworkObject : MonoBehaviour {
  private ushort _networkId;

  private new Rigidbody rigidbody;

  private void Start() {
    // For now assume these exist, but eventually we will need to support
    // network objects that don't depend on rigidbodies.
    rigidbody = GetComponent<Rigidbody>();
  }

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

  public NetworkObjectState ToNetworkState() {
    return new NetworkObjectState {
      NetworkId = NetworkId,
      Position = transform.position,
      Rotation = transform.rotation.eulerAngles,
      Velocity = rigidbody.velocity,
      AngularVelocity = rigidbody.angularVelocity,
    };
  }

  public void ApplyNetworkState(NetworkObjectState state) {
    transform.position = state.Position;
    transform.rotation = Quaternion.Euler(state.Rotation);
    rigidbody.velocity = state.Velocity;
    rigidbody.angularVelocity = state.AngularVelocity;
  }

  private void OnGUI() {
    if (GamePrefs.DebugMode) {
      var p = Camera.main.WorldToScreenPoint(transform.position);
      var screenRect = new Rect(p.x, Screen.height - p.y, 100, 20);
      GUI.Label(screenRect, $"id: {NetworkId}");
    }
  }
}
