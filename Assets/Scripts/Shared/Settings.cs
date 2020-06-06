using System;
using UnityEngine;

public static class Settings {
  public static float SimulationTickRate = 1 / Time.fixedDeltaTime;
  public static float SimulationTickInterval = Time.fixedDeltaTime;
  public static float ServerSendRate = SimulationTickRate / 2;
  public static float ServerSendInterval = 1 / ServerSendRate;

  // Whether to interpolate remote entities on the client for smoothness.
  // Similar to source cl_interp=1.
  public static bool UseClientInterp = true;

  public static bool UseAggressiveLagReduction = true;

  // The maximum age of the last server state in milliseconds the client will continue simulating.
  public static float MaxStaleServerStateAgeMs = 500;
  public static int MaxStaleServerStateTicks = Mathf.CeilToInt(
      MaxStaleServerStateAgeMs / SimulationTickRate);
  public static bool FreezeClientOnStaleServer = false;
}
