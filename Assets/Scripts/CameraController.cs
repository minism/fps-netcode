using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour {
  [Header("Target")]
  public Transform followTarget;
  public float targetEyeHeight;
  public float targetEyeDepth;
  public float followSharpness = 10000f;

  [Header("Rotation params")]
  public float minTilt = -90f;
  public float maxTilt = 90f;
  public float rotationSpeed = 1f;
  public float rotationSharpness = 10000f;

  private new Camera camera;
  private Vector3 targetYawDirection = Vector3.forward;
  private float targetTilt = 0;
  private Vector3 targetFollowPosition;

  private void Start() {
    camera = GetComponent<Camera>();
  }

  private void Update() {
    if (Input.GetMouseButtonDown(0)) {
      Cursor.lockState = CursorLockMode.Locked;
    }

    // Dont move the camera unless we've locked the cursor.
    if (Cursor.lockState != CursorLockMode.Locked) {
      return;
    }

    // TODO: This should be moved out to a player input component.
    float axisRight = Input.GetAxisRaw("Mouse X");
    float axisUp = Input.GetAxisRaw("Mouse Y");
    var lookInputVector = new Vector3(axisRight, axisUp, 0f);
    ApplyLookInput(lookInputVector, Time.deltaTime);
  }

  public void ApplyLookInput(Vector3 lookInputVector, float dt) {
    // Process yaw.
    Quaternion rotFromInput = Quaternion.Euler(Vector3.up * (lookInputVector.x * rotationSpeed));
    targetYawDirection = rotFromInput * targetYawDirection;

    // Process tilt.
    targetTilt -= lookInputVector.y * rotationSpeed;
    targetTilt = Mathf.Clamp(targetTilt, minTilt, maxTilt);

    // Lerp the followed position.
    targetFollowPosition = Vector3.Lerp(
        targetFollowPosition, followTarget.position, 1f - Mathf.Exp(-followSharpness * dt));

    // Lerp the rotation.
    Quaternion yawRot = Quaternion.LookRotation(targetYawDirection, followTarget.up);
    Quaternion tiltRot = Quaternion.Euler(targetTilt, 0, 0);
    // TODO: Apply this optimization here (two quats at once) to StarArk code.
    Quaternion targetRot = Quaternion.Slerp(
        transform.rotation, yawRot * tiltRot, 1f - Mathf.Exp(-rotationSharpness * dt));

    // Apply framing.
    var finalPosition = targetFollowPosition;
    finalPosition += Vector3.up * targetEyeHeight;
    finalPosition += transform.forward * targetEyeDepth;

    // Apply the final position & rotation.
    transform.rotation = targetRot;
    transform.position = finalPosition;
  }
}