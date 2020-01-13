using UnityEngine;

/**
 * Based on https://www.kinematicsoup.com/news/2016/8/9/rrypp5tkubynjwxhxjzd42s3o034o8
 * 
 * Tracks the delta between monitor refresh and simulation tick rate to provide
 * an interpolation factor that view code can use.
 */
public class InterpolationController : MonoBehaviour {
  public static float InterpolationFactor { get; private set; } = 1f;

  private DoubleBuffer<float> timestampBuffer = new DoubleBuffer<float>();

  // TODO: Should use the simulate(dt) API instead.
  private void FixedUpdate() {
    timestampBuffer.Push(Time.fixedTime);
  }

  private void Update() {
    float newTime = timestampBuffer.New();
    float oldTime = timestampBuffer.Old();

    if (newTime != oldTime) {
      InterpolationFactor = (Time.time - newTime) / (newTime - oldTime);
    } else {
      InterpolationFactor = 1;
    }
  }
}
