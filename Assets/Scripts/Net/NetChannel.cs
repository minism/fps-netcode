using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

/// Central controller for sending network commands and receiving network events.
/// Used by both client and server, so logic should be relatively generic.
public class NetChannel : INetEventListener, INetChannel {
  // TODO: This key should be obfuscated in the build somehow?
  private static string CONNECTION_KEY = "498j98dfa9sd8fh";

  // Listenable events.
  public Action<NetPeer> PeerConnectedHandler { get; set; }
  public Action<NetPeer, DisconnectInfo> PeerDisconnectedHandler { get; set; }

  private bool acceptConnections;
  private NetManager netManager;
  private NetPacketProcessor netPacketProcessor;
  private NetDataWriter netDataWriter;
  private NetMonitor netMonitor;

  public NetChannel() {
    netManager = new NetManager(this) {
      AutoRecycle = true
    };
    netPacketProcessor = new NetPacketProcessor();
    netDataWriter = new NetDataWriter();

    // Register nested types used in net commands.
    netPacketProcessor.RegisterNestedType(
        NetExtensions.SerializeVector3, NetExtensions.DeserializeVector3);
    netPacketProcessor.RegisterNestedType<PlayerSetupData>();
    netPacketProcessor.RegisterNestedType<PlayerMetadata>();
    netPacketProcessor.RegisterNestedType<InitialPlayerState>();
    netPacketProcessor.RegisterNestedType<NetworkObjectState>();
  }

  /// Update should be called every frame.
  public void Update() {
    netManager.PollEvents();
  }

	/// Destroy should be called if the channel will never be used again.
	public void Destroy() {
    netManager.Stop();
  }

  /// Attempt to connect to an endpoint, the channel will act as a client.
  public void ConnectToServer(string host, int port) {
    if (netManager.IsRunning) {
      Debug.LogWarning("Network manager already running, doing nothing.");
      return;
    }
    netManager.Start();
    netManager.Connect(host, port, CONNECTION_KEY);
  }

	/// Starts listening for connections, the channel will act as as server.
	public void StartServer(int port) {
    if (netManager.IsRunning) {
      Debug.LogWarning("Network manager already running, doing nothing.");
      return;
    }
    acceptConnections = true;
    netManager.Start(port);
  }

  public void SetNetMonitor(NetMonitor netMonitor) {
    this.netMonitor = netMonitor;
  }

  /** INetChannel methods */

  public void Subscribe<T>(Action<T> onReceiveHandler) where T : class, new() {
    netPacketProcessor.SubscribeReusable(onReceiveHandler);
  }

  public void Subscribe<T>(Action<T, NetPeer> onReceiveHandler) where T : class, new() {
    netPacketProcessor.SubscribeReusable(onReceiveHandler);
  }

  public void SendCommand<T>(NetPeer peer, T command) where T : class, new() {
    var deliveryMethod = NetCommand.Metadata.DeliveryType[typeof(T)];
    netPacketProcessor.Send(peer, command, deliveryMethod);
  }

  public void BroadcastCommand<T>(T command) where T : class, new() {
    var deliveryMethod = NetCommand.Metadata.DeliveryType[typeof(T)];
    netDataWriter.Reset();
    netPacketProcessor.Write(netDataWriter, command);
    netManager.SendToAll(netDataWriter, deliveryMethod);
  }

  public void BroadcastCommand<T>(T command, NetPeer excludedPeer) where T : class, new() {
    var deliveryMethod = NetCommand.Metadata.DeliveryType[typeof(T)];
    netDataWriter.Reset();
    netPacketProcessor.Write(netDataWriter, command);
    netManager.SendToAll(netDataWriter, deliveryMethod, excludedPeer);
  }

  /**
   * Litenet Network events.
   * 
   * These are lower level and handled directly by the channel, exposed as higher level APIs.
   */

  public void OnPeerConnected(NetPeer peer) {
    PeerConnectedHandler?.Invoke(peer);
  }

  public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
    PeerDisconnectedHandler?.Invoke(peer, disconnectInfo);
  }

  public void OnConnectionRequest(ConnectionRequest request) {
    if (!acceptConnections) {
      request.Reject();
    }
    request.AcceptIfKey(CONNECTION_KEY);
  }

  public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod) {
    netPacketProcessor.ReadAllPackets(reader, peer);
  }

  public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
    // NOP
  }

  public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
    Debug.LogWarning("Network error - " + socketError);
  }

  public void OnNetworkLatencyUpdate(NetPeer peer, int latency) {
    if (netMonitor != null) {
      netMonitor.SetLatency(latency);
    }
  }
}
