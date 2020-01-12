using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPlayerController {
  Transform GetPlayerHeadTransform();
  PlayerState ToNetworkState();
  void ApplyNetworkState(PlayerState state);
  void SetPlayerInputs(PlayerInputs inputs);
  void Simulate(float dt);
}
