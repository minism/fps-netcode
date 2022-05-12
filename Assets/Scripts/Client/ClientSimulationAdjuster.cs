using System.Diagnostics;
using UnityEngine;

public class ClientSimulationAdjuster : ISimulationAdjuster {
  public float AdjustedInterval { get; private set; } = 1.0f;

  // The actual number of ticks our inputs are arriving ahead of the server simulation.
  // The goal of the adjuster is to get this value as close to 1 as possible without going under.
  private Ice.MovingAverage actualTickLeadAvg = new Ice.MovingAverage((int)Settings.ServerSendRate * 2);

  private int estimatedMissedInputs;

  // Extrapolate based on latency what our client tick should be.
  public uint GuessClientTick(uint receivedServerTick, int serverLatencyMs) {
    float serverLatencySeconds = serverLatencyMs / 1000f;
    uint estimatedTickLead = (uint)(serverLatencySeconds * 1.5 / Time.fixedDeltaTime) + 4;
    this.Log($"Initializing client with estimated tick lead of {estimatedTickLead}, ping: {serverLatencyMs}");
    return receivedServerTick + estimatedTickLead;
  }

  public void NotifyActualTickLead(int actualTickLead) {
    actualTickLeadAvg.Push(actualTickLead);

    // TODO: This logic needs significant tuning.

    // Negative lead means dropped inputs which is worse than buffering, so immediately move the
    // simulation forward rather than waiting for the average to catch up.
    if (actualTickLead < 0) {
      this.Log("Dropped an input, got an actual tick lead of " + actualTickLead);
      actualTickLeadAvg.ForceSet(actualTickLead);
      estimatedMissedInputs++;
    }

    // Check for a steady average of a healthy connection before backing off the simulation.
    var avg = actualTickLeadAvg.Average();
    if (avg <= -16) {
      AdjustedInterval = 0.875f;
    } else if (avg <= -8) {
      AdjustedInterval = 0.9375f;
    } else if (avg < 0) {
      AdjustedInterval = 0.75f;
    } else if (avg < 0) {
      AdjustedInterval = 0.96875f;
    } else if (avg >= 16) {
      AdjustedInterval = 1.125f;
    } else if (avg >= 8) {
      AdjustedInterval = 1.0625f;
    } else if (avg >= 4) {
      AdjustedInterval = 1.03125f;
    } else if (avg >= 2 && Settings.UseAggressiveLagReduction) {
      AdjustedInterval = 1.015625f;
    } else {
      AdjustedInterval = 1f;
    }
  }

  public void Monitoring() {
    this.LogValue("cl tick lead avg", actualTickLeadAvg.Average());
    this.LogValue("cl sim factor", AdjustedInterval);
    this.LogValue("cl est. missed inputs", estimatedMissedInputs);
  }
}
