using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CPMCameraController : MonoBehaviour {
  public Transform followTarget;
  public float playerHeadHeight = 1.5f;

  public float xMouseSensitivity = 2f;
  public float yMouseSensitivity = 2f;

  private float rotX = 0.0f;
  private float rotY = 0.0f;

  private void Update() {
    if (followTarget == null) {
      return;
    }
    if (Cursor.lockState != CursorLockMode.Locked) {
      if (Input.GetButtonDown("Fire1"))
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Snap to the player head position.
    transform.position = followTarget.position + Vector3.up * playerHeadHeight;

    // Process rotation input.
    rotX -= Input.GetAxisRaw("Mouse Y") * xMouseSensitivity;
    rotY += Input.GetAxisRaw("Mouse X") * yMouseSensitivity;
    rotX = Mathf.Clamp(rotX, -90, 90);

    // Set orientation.
    // The camera is allowed to move freely not dependent on any tick rate or anything.
    // The network layer will snapshot the camera direction along with player inputs.
    // But this means the player can freely look around at say, 144hz, even if we're running
    // a much lower physics or network tick rate.
    transform.rotation = Quaternion.Euler(rotX, rotY, 0);
  }
}
