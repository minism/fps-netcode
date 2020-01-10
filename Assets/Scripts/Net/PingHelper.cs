using System;
using System.Net;
using System.Collections.Generic;
using UnityEngine;

public class PingHelper {
  private struct Listener {
    public Action<int> callback;
    public DateTime sendTime;
  }

  private Dictionary<IPEndPoint, Listener> listeners =
      new Dictionary<IPEndPoint, Listener>();

  public void AddListener(IPEndPoint endpoint, Action<int> callback) {
    if (listeners.ContainsKey(endpoint)) {
      Debug.LogWarning("Ignore second in-flight ping to the same endpoint (client should de-dupe)");
      return;
    }
    listeners.Add(endpoint, new Listener {
      callback = callback,
      sendTime = DateTime.Now,
    });
  }

  public void ReceivePong(IPEndPoint endpoint) {
    if (listeners.ContainsKey(endpoint)) {
      var listener = listeners[endpoint];
      var latency = DateTime.Now - listener.sendTime;
      listener.callback(latency.Milliseconds);
      listeners.Remove(endpoint);
    }
  }
}
