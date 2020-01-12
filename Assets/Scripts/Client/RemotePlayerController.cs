using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A controller for interpolated remote players.
public class RemotePlayerController : MonoBehaviour, IPlayerController {
  private Vector3 velocity;
  private Vector3 targetPosition;
  private Quaternion targetRotation;

  public void Update() {
    Debug.Log($"remote player vel {velocity}");
    // TODO: Real interp should go here.
    transform.position = targetPosition;
    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 0.1f);
  }

  public Transform GetPlayerHeadTransform() {
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
    targetRotation = Quaternion.Euler(0, state.Rotation.y, 0);
    velocity = state.Velocity;
    if (state.Grounded) {
      velocity.y = 0;
    }
  }
}

