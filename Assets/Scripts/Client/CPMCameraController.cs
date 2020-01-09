using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CPMCameraController : MonoBehaviour {
  public Transform PlayerView { get; set; }

  private void Update() {
    if (PlayerView == null) {
      return;
    }
    if (Cursor.lockState != CursorLockMode.Locked) {
      if (Input.GetButtonDown("Fire1"))
        Cursor.lockState = CursorLockMode.Locked;
    }
    var t = PlayerView.transform;
    transform.SetPositionAndRotation(t.position, t.rotation);
  }
}
