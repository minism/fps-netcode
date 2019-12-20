using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Component that first receives kb/gamepad input for the player.
public class ClientPlayerInput : MonoBehaviour {
  public CameraController cameraController;

  public interface Handler {
    void HandleClientPlayerInput(in PlayerInputs inputs);
  }

  public Handler InputHandler { get; set; }

  public PlayerInputs SampleInputs() {
    return new PlayerInputs {
      ForwardAxis = Input.GetAxisRaw("Vertical"),
      RightAxis = Input.GetAxisRaw("Horizontal"),
      CameraOrientation = cameraController.transform.rotation,
      Jump = Input.GetKeyDown(KeyCode.Space),
    };
  }

  private void Update() {
    if (InputHandler != null) {
      var inputs = SampleInputs();
      InputHandler.HandleClientPlayerInput(in inputs);
    }
  }
}
