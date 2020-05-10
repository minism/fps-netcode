using System.Collections.Generic;
using UnityEngine;

// A controller for interpolated remote players.
public class RemotePlayerController : MonoBehaviour, IPlayerController {
  private Vector3 velocity;
  private Vector3 targetPosition;
  private Quaternion targetRotation;

  // Interpolate between incoming server states.
  private Queue<PlayerState> stateQueue = new Queue<PlayerState>();
  private PlayerState? lastState = null;
  private float stateTimer = 0;

  public void SetPlayerInputs(PlayerInputs inputs) { }

  public void Simulate(float dt) {
    // TODO: Do this in update for smoothness.
    stateTimer += dt;
    if (stateTimer > Settings.ServerSendInterval) {
      stateTimer -= Settings.ServerSendInterval;
      if (stateQueue.Count > 1) {
        lastState = stateQueue.Dequeue();
      }
    }

    // We can only interpolate if we have a previous and next world state.
    if (!lastState.HasValue || stateQueue.Count < 1) {
      Debug.LogWarning("RemotePlayer: not enough states to interp");
      return;
    }

    // TODO: Probably need to handle a queue size of 3 if we've fallen behind, snap?
    DebugUI.ShowValue("RemotePlayer q size", stateQueue.Count);
    var nextState = stateQueue.Peek();
    float theta = stateTimer / Settings.ServerSendInterval;
    transform.position = Vector3.Lerp(
        lastState.Value.Position, nextState.Position, theta);
    var a = Quaternion.Euler(0, lastState.Value.Rotation.y, 0);
    var b = Quaternion.Euler(0, nextState.Rotation.y, 0);
    transform.rotation = Quaternion.Slerp(a, b, theta);
  }

  public PlayerState ToNetworkState() {
    // We dont expect this to ever be called from the client.
    throw new System.NotImplementedException();
  }

  public void ApplyNetworkState(PlayerState state) {
    stateQueue.Enqueue(state);
  }
}

