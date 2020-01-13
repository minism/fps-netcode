using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Component that first receives kb/gamepad input for the player.
public class ClientPlayerInput : MonoBehaviour {
  public PlayerInputs SampleInputs() {
    return new PlayerInputs {
      ForwardAxis = Input.GetAxisRaw("Vertical"),
      RightAxis = Input.GetAxisRaw("Horizontal"),
      ViewDirection = Camera.main.transform.rotation,
      Jump = Input.GetKey(KeyCode.Space),
    };
  }
}
