using System;
using UnityEngine;

public static class Settings {
  // The rate of the world simulation. This is equivalent on client and server.
  public static float SimulationTickRate = 30;
  public static float SimulationTickInterval = 1 / SimulationTickRate;

  // The rate that the server sends world snapshots to the client.
  public static float ServerSendRate = 30;
  public static float ServerSendInterval = 1 / ServerSendRate;

  // The maximum number of historical inputs the client sends to the server.
  public static int ClientMaxHistoricalInputs = 5;

  // Whether to enable time dialation on the client to improve input buffer
  // (Overwatch's netcode method).
  public static bool ClientEnableTimeDialation = false;

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
