using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ClientPlayerInput))]
public class DebugPlayerInput : MonoBehaviour {
  public GameObject player;
  private IPlayerController playerController;
  private ClientPlayerInput clientPlayerInput;

  private void Start() {
    playerController = player.GetComponent<IPlayerController>();
    clientPlayerInput = GetComponent<ClientPlayerInput>();
  }

  public void Update() {
    playerController.SetPlayerInputs(clientPlayerInput.SampleInputs());
    playerController.Simulate(Time.deltaTime);
  }

}
