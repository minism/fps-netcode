using UnityEngine;

/// Component that first receives kb/gamepad input for the player.
public class ClientPlayerInput : MonoBehaviour {
  public PlayerInputs SampleInputs() {
    var vert = Input.GetAxisRaw("Vertical");
    var horiz = Input.GetAxisRaw("Horizontal");
    return new PlayerInputs {
      Forward = vert > 0,
      Back = vert < 0,
      Right = horiz > 0,
      Left = horiz < 0,
      ViewDirection = Camera.main.transform.rotation,
      Jump = Input.GetKey(KeyCode.Space),
    };
  }
}
