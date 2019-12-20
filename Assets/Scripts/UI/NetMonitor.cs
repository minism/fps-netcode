using System.Collections;
using System.Collections.Generic;
using LiteNetLib;
using UnityEngine;

public class NetMonitor : MonoBehaviour, LiteNetLib.INetLogger
{
  public bool enableLiteNetLogging = true;
  public UnityEngine.UI.Text pingText;

  public void Start() {
    if (enableLiteNetLogging) {
      LiteNetLib.NetDebug.Logger = this;
    }
  }

  public void SetLatency(int latency) {
    if (pingText == null) {
      return;
    }
    pingText.text = "Ping: " + latency;
  }

  public void WriteNet(NetLogLevel level, string str, params object[] args) {
    Debug.Log(string.Format("[lite-net {0}] {1}", level, str));
  }
}
