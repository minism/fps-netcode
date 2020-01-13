using UnityEngine;
using System.Collections;

[System.Serializable]
public struct DebugNetworkSettings {
  // Sent directly to LiteNetLib.
  public bool SimulatePacketLoss;
  public bool SimulateLatency;
  public int PacketLossChance;
  public int MinLatency;
  public int MaxLatency;

  // Other stuff we handle directly.
  public bool SimulateLargeStalls;
  public float LargeStallsInterval;
  public float LargeStallsDuration;
  public int LargeStallsLatency;
  public int LargeStallsPacketLoss;
}
