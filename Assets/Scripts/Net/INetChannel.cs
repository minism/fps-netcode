using LiteNetLib;
using System;
using System.Collections.Generic;

/// An interface for sending/receiving network commands.
public interface INetChannel {
  void Subscribe<T>(Action<T> onReceiveHandler) where T : class, new();
  void Subscribe<T>(Action<T, NetPeer> onReceiveHandler) where T : class, new();
  void SubscribeQueue<T>(Queue<T> queue) where T : class, new();
  void SubscribeQueue<T>(Queue<WithPeer<T>> queue) where T : class, new();
  void SendCommand<T>(NetPeer peer, T command) where T : class, new();
  void BroadcastCommand<T>(T command) where T : class, new();
  void BroadcastCommand<T>(T command, NetPeer excludedPeer) where T : class, new();
}
