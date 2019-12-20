using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Indicates a synchronized presence over the network for this object.
/// NetworkID is common to all clients and server.
/// Similar concept to NetworkIdentity in Unity's deprecated implementation.
public class NetworkObject : MonoBehaviour {
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

  public NetworkObjectState ToNetworkState() {
    return new NetworkObjectState {
      NetworkId = NetworkId,
      //Position = transform.position,
      //Rotation = transform.rotation.eulerAngles,
      //Velocity = GetVelocity(),
      //AngularVelocity = GetAngularVelocity(),
    };
  }

  public void ApplyNetworkState(NetworkObjectState state) {
    //transform.position = state.Position;
    //transform.rotation = Quaternion.Euler(state.Rotation);
    //SetVelocity(state.Velocity);
    //SetAngularVelocity(state.AngularVelocity);
  }

  //protected abstract Vector3 GetVelocity();
  //protected abstract Vector3 GetAngularVelocity();
  //protected abstract void SetVelocity(Vector3 velocity);
  //protected abstract void SetAngularVelocity(Vector3 velocity);

  private void OnGUI() {
    if (GamePrefs.DebugMode) {
      var p = Camera.main.WorldToScreenPoint(transform.position);
      var screenRect = new Rect(p.x, Screen.height - p.y, 100, 20);
      GUI.Label(screenRect, $"id: {NetworkId}");
    }
  }
}
