using UnityEngine;

public class FakePlayer : MonoBehaviour, IPlayerController {
  public float maxVelocity = 30;
  public float accel = 50;

  private Vector3 velocity = Vector3.zero;
  private Vector2 input = Vector2.zero;

  public void ApplyNetworkState(PlayerState state) {
    transform.position = state.Position;
    //transform.rotation = state.Rotation;
    velocity = state.Velocity;
  }

  public void SetPlayerInputs(PlayerInputs inputs) {
    input = new Vector2(inputs.ForwardAxis, inputs.RightAxis);
  }

  public void Simulate(float dt) {
    if (input.sqrMagnitude > 0) {
      velocity += new Vector3(input.y, 0, input.x) * accel * dt;
    } else {
      velocity = Vector3.zero;
    }
    velocity = Vector3.ClampMagnitude(velocity, maxVelocity);
    transform.position += velocity * dt;
  }

  public PlayerState ToNetworkState() {
    return new PlayerState {
      Position = transform.position,
      //Rotation = transform.rotation,
      Velocity = velocity,
    };
  }

  public void SetPlayerAttackDelegate(PlayerAttackDelegate d) { }
}
