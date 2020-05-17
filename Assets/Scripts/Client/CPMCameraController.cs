using UnityEngine;
using Ice;

public class CPMCameraController : MonoBehaviour {
  public CPMPlayerController player;

  public float xMouseSensitivity = 2f;
  public float yMouseSensitivity = 2f;

  private float rotX = 0.0f;
  private float rotY = 0.0f;

  private DoubleBuffer<Vector3> positionBuffer = new DoubleBuffer<Vector3>();

  public void PlayerPositionUpdated() {
    var targetPos = player.transform.position + Vector3.up * player.playerHeadHeight;
    positionBuffer.Push(targetPos);
  }

  private void Update() {
    // Handle cursor lock state
    if (Cursor.lockState != CursorLockMode.Locked && Input.GetButtonDown("Fire1")) {
      Cursor.visible = false;
      Cursor.lockState = CursorLockMode.Locked;
    } else if (Input.GetKeyDown(KeyCode.Escape)) {
      Cursor.visible = true;
      Cursor.lockState = CursorLockMode.None;
    }

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

    // Interpolate position.
    transform.position = Vector3.Lerp(
                                positionBuffer.Old(),
                                positionBuffer.New(),
                                InterpolationController.InterpolationFactor);
  }
}
