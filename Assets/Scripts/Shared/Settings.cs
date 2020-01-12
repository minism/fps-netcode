using UnityEngine;

public static class Settings {
	public static float SimulationTickRate = 1 / Time.fixedDeltaTime;
	public static float SimulationTickInterval = Time.fixedDeltaTime;
  public static float ServerSendRate = SimulationTickRate / 2;
	public static float ServerSendInterval = 1 / ServerSendRate;
}
