using System.Diagnostics;
using UnityEngine;

public class ClientSimulationAdjuster : ISimulationAdjuster {
  public float AdjustedInterval { get; private set; } = 1.0f;

  // The actual number of ticks our inputs are arriving ahead of the server simulation.
  // The goal of the adjuster is to get this value as close to 1 as possible without going under.
  private Ice.MovingAverage actualTickLeadAvg = new Ice.MovingAverage((int)Settings.ServerSendRate * 2);

  private int estimatedMissedInputs;

  private Stopwatch droppedInputTimer = new Stopwatch();

  // Extrapolate based on latency what our client tick should be.
  public uint GuessClientTick(uint receivedServerTick, int serverLatencyMs) {
    float serverLatencySeconds = serverLatencyMs / 1000f;
    uint estimatedTickLead = (uint)(serverLatencySeconds * 1.5 / Time.fixedDeltaTime) + 4;
    UnityEngine.Debug.Log($"Initializing client with estimated tick lead of {estimatedTickLead}, ping: {serverLatencyMs}");
    return receivedServerTick + estimatedTickLead;
  }

  public void NotifyActualTickLead(int actualTickLead) {
    actualTickLeadAvg.Push(actualTickLead);

    // TODO: This logic needs significant tuning.

    // Negative lead means dropped inputs which is worse than buffering, so immediately move the
    // simulation forward.
    if (actualTickLead < 0) {
      UnityEngine.Debug.Log("Dropped an input, got an actual tick lead of " + actualTickLead);
      droppedInputTimer.Restart();
      estimatedMissedInputs++;
    }
    if (droppedInputTimer.IsRunning && droppedInputTimer.ElapsedMilliseconds < 1000) {
      AdjustedInterval = 0.9375f;
      return;
    }

    // Check for a steady average of a healthy connection before backing off the simulation.
    var avg = actualTickLeadAvg.Average();
    if (avg >= 16) {
      AdjustedInterval = 1.125f;
    } else if (avg >= 8) {
      AdjustedInterval = 1.0625f;
    } else if (avg >= 4) {
      AdjustedInterval = 1.03125f;
    } else {
      AdjustedInterval = 1f;
    }
  }

  public void Monitoring() {
    DebugUI.ShowValue("cl tick lead avg", actualTickLeadAvg.Average());
    DebugUI.ShowValue("cl sim factor", AdjustedInterval);
    DebugUI.ShowValue("cl est. missed inputs", estimatedMissedInputs);
  }
}
