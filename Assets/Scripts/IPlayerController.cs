public interface IPlayerController {
  PlayerState ToNetworkState();
  void ApplyNetworkState(PlayerState state);
  void SetPlayerInputs(PlayerInputs inputs);
  void Simulate(float dt);
}
