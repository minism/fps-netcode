using System;
using System.Collections.Generic;
using System.Net;

public class PingHelper {
  private struct Listener {
    public Action<int> callback;
    public DateTime sendTime;
  }

  private Dictionary<IPEndPoint, Listener> listeners =
      new Dictionary<IPEndPoint, Listener>();

  public void AddListener(IPEndPoint endpoint, Action<int> callback) {
    var listener = new Listener {
      callback = callback,
      sendTime = DateTime.Now,
    };
    listeners[endpoint] = listener;
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
