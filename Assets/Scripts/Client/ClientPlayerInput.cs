using UnityEngine;

/// Component that first receives kb/gamepad input for the player.
public class ClientPlayerInput : MonoBehaviour {
  // Debug stuff.
  public bool DebugAutoMovement { get; set; }
  private float debugAutoMoveTime = 0;
  private float debugAutoMoveTimer = 1;
  private PlayerInputs debugAutoMoveInputs;

  public PlayerInputs SampleInputs() {
    if (DebugAutoMovement) {
      return GetDebugAutoInputs();
    }

    var vert = Input.GetAxisRaw("Vertical");
    var horiz = Input.GetAxisRaw("Horizontal");
    return new PlayerInputs {
      Forward = vert > 0,
      Back = vert < 0,
      Right = horiz > 0,
      Left = horiz < 0,
      ViewDirection = Camera.main.transform.rotation,
      Jump = Input.GetKey(KeyCode.Space),
      Fire = Input.GetMouseButton(0),
    };
  }

  private PlayerInputs GetDebugAutoInputs() {
    if (Time.time - debugAutoMoveTime > debugAutoMoveTimer) {
      debugAutoMoveTime = Time.time;
      debugAutoMoveTimer = Random.Range(0.5f, 1);

      var vert = Random.value;
      var horiz = Random.value;
      var jump = Random.value;
      debugAutoMoveInputs = new PlayerInputs {
        Forward = vert < 0.4,
        Back = 0.4 < vert && vert < 0.8,
        Right = horiz < 0.4,
        Left = 0.4 < horiz && horiz < 0.8,
        Jump = jump < 0.2,
        ViewDirection = Camera.main.transform.rotation,
        Fire = false,
      };
    }

    return debugAutoMoveInputs;
  }
}
