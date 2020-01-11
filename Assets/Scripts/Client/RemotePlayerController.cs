using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A controller for interpolated remote players.
public class RemotePlayerController : MonoBehaviour, IPlayerController {
  private Vector3 velocity;
  private Vector3 targetPosition;
  private Quaternion targetRotation;

  public void Update() {
    transform.position = Vector3.Lerp(transform.position, targetPosition, 0.1f);
    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 0.1f);
  }

  public Transform GetPlayerViewTransform() {
    return transform;
  }

  public void SetPlayerInputs(PlayerInputs inputs) { }

  public void Simulate(float dt) {
    targetPosition += velocity * dt;
  }

  public PlayerState ToNetworkState() {
    // We dont expect this to ever be called from the client.
    throw new System.NotImplementedException();
  }

  public void ApplyNetworkState(PlayerState state) {
    targetPosition = state.Position;
    targetRotation = Quaternion.LookRotation(state.Rotation, Vector3.up);
    velocity = state.Velocity;
  }
}

