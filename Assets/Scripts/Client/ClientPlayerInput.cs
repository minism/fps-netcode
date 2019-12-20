using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Component that first receives kb/gamepad input for the player.
public class ClientPlayerInput : MonoBehaviour {
  public CameraController cameraController;

  public interface Handler {
    void HandleClientPlayerInput(in Inputs inputs);
  }

  // Movement input for a single frame.
  public struct Inputs {
    public float ForwardAxis;
    public float RightAxis;
    public Vector3 ViewDirection;
    public bool Jump;
  }

  public Handler InputHandler { get; set; }

  private void Update() {
    var inputs = new Inputs {
      ForwardAxis = Input.GetAxisRaw("Vertical"),
      RightAxis = Input.GetAxisRaw("Horizontal"),
      ViewDirection = cameraController.transform.forward,
      Jump = Input.GetKeyDown(KeyCode.Space),
    };
    if (InputHandler != null) {
      InputHandler.HandleClientPlayerInput(in inputs);
    }
  }
}
