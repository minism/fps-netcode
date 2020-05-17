using UnityEngine;

public interface IPlayerActionHandler {
  void CreatePlayerAttack(NetworkObjectType type, Vector3 position, Quaternion orientation);
}

public interface IPlayerController {
  PlayerState ToNetworkState();
  void ApplyNetworkState(PlayerState state);
  void SetPlayerInputs(PlayerInputs inputs);
  void Simulate(float dt);
  void SetPlayerActionHandler(IPlayerActionHandler handler);
}
