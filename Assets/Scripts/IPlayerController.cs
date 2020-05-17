using UnityEngine;

public delegate void PlayerAttackDelegate(
    NetworkObjectType type, Vector3 position, Quaternion orientation);

public interface IPlayerController {
  PlayerState ToNetworkState();
  void ApplyNetworkState(PlayerState state);
  void SetPlayerInputs(PlayerInputs inputs);
  void Simulate(float dt);
  void SetPlayerAttackDelegate(PlayerAttackDelegate d);
}
