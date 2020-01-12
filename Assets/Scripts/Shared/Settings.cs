using UnityEngine;
using System;

public static class Settings {
	public static float SimulationTickRate = 1 / Time.fixedDeltaTime;
	public static float SimulationTickInterval = Time.fixedDeltaTime;
  public static float ServerSendRate = SimulationTickRate / 2;
	public static float ServerSendInterval = 1 / ServerSendRate;

	// Realtime adjustment settings.
	public static TimeSpan MinClientAdjustmentInterval = TimeSpan.FromSeconds(1);
	public static TimeSpan ClientBufferTooHighInterval = TimeSpan.FromSeconds(5);
	public static uint ClientIdealBufferedInputLimit = 3;
}
