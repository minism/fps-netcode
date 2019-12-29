using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CPMCameraController : MonoBehaviour {
  public Transform playerView;

  private void Start() {
    // Hide the cursor
    //Cursor.visible = false;
    if (playerView == null) {
      playerView = FindObjectOfType<CPMPlayerController>().playerView;
    }
  }

    private void Update() {
    if (Cursor.lockState != CursorLockMode.Locked) {
      if (Input.GetButtonDown("Fire1"))
        Cursor.lockState = CursorLockMode.Locked;
    }
    var t = playerView.transform;
    transform.SetPositionAndRotation(t.position, t.rotation);
  }
}
