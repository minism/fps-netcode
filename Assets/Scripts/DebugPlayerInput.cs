using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ClientPlayerInput))]
public class DebugPlayerInput : MonoBehaviour, ClientPlayerInput.Handler {
  public PlayerController playerController;

  private ClientPlayerInput clientPlayerInput;

  private void Start() {
    clientPlayerInput = GetComponent<ClientPlayerInput>();
    clientPlayerInput.InputHandler = this;
  }

  public void HandleClientPlayerInput(in ClientPlayerInput.Inputs inputs) {
    playerController.SetPlayerInput(in inputs);
  }
}
