using UnityEngine;
using System.Collections;

[System.Serializable]
public struct DebugNetworkSettings {
  public bool SimulatePacketLoss;
  public bool SimulateLatency;
  public int PacketLossChance;
  public int MinLatency;
  public int MaxLatency;
}
