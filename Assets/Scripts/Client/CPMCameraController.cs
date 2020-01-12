using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CPMCameraController : MonoBehaviour {
  public Transform PlayerHead;

  private void Update() {
    if (PlayerHead == null) {
      return;
    }
    if (Cursor.lockState != CursorLockMode.Locked) {
      if (Input.GetButtonDown("Fire1"))
        Cursor.lockState = CursorLockMode.Locked;
    }
    var t = PlayerHead.transform;
    transform.SetPositionAndRotation(t.position, t.rotation);
  }
}
