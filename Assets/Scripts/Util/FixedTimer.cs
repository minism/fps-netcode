using System;

/// Simple timer which executes a callback at an exactly even rate. For accurate
/// physics simulations.
/// i.e. a "Fixed time step" implementation.
public class FixedTimer {
  private float accumulator;
  private readonly Action<float> callback;
  private readonly float tickRate;
  private readonly float fixedDelta;
  private bool running;

  public float LerpAlpha => (float)accumulator / fixedDelta;

  public FixedTimer(float tickRate, Action<float> callback) {
    this.tickRate = tickRate;
    this.callback = callback;
    fixedDelta = 1.0f / this.tickRate;
  }

  public void Start() {
    accumulator = 0.0f;
    running = true;
  }

  public void Stop() {
    running = false;
  }

  public void Update(float dt) {
    accumulator += dt;
    while (accumulator >= fixedDelta) {
      if (!running) {
        return;
      }
      callback(fixedDelta);
      accumulator -= fixedDelta;
    }
  }
}