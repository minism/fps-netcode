using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;
using KinematicCharacterController;

/// Central controller for sending network commands and receiving network events.
/// Used by both client and server, so logic should be relatively generic.
public class NetChannel : INetEventListener, INetChannel {
  // TODO: This key should be obfuscated in the build somehow?
  private static string CONNECTION_KEY = "498j98dfa9sd8fh";

  // Listenable events.
  public Action<NetPeer> PeerConnectedHandler { get; set; }
  public Action<NetPeer, DisconnectInfo> PeerDisconnectedHandler { get; set; }

  // Lookup for latency by peer.
  public Dictionary<NetPeer, int> PeerLatency { get; private set; }
      = new Dictionary<NetPeer, int>();

  private bool acceptConnections;
  private NetManager netManager;
  private NetPacketProcessor netPacketProcessor;
  private NetDataWriter netDataWriter;
  private NetMonitor netMonitor;

  public NetChannel(DebugNetworkSettings debugNetworkSettings) {
    netManager = new NetManager(this) {
      AutoRecycle = true,
      SimulatePacketLoss = debugNetworkSettings.SimulatePacketLoss,
      SimulateLatency = debugNetworkSettings.SimulateLatency,
      SimulationPacketLossChance = debugNetworkSettings.PacketLossChance,
      SimulationMinLatency = debugNetworkSettings.MinLatency,
      SimulationMaxLatency = debugNetworkSettings.MaxLatency,
    };
    netPacketProcessor = new NetPacketProcessor();
    netDataWriter = new NetDataWriter();

    // Register nested types used in net commands.
    netPacketProcessor.RegisterNestedType(
        NetExtensions.SerializeVector3, NetExtensions.DeserializeVector3);
    netPacketProcessor.RegisterNestedType(
        NetExtensions.SerializeQuaternion, NetExtensions.DeserializeQuaternion);
    netPacketProcessor.RegisterNestedType(
        NetExtensions.SerializeKinematicMotorState,
        NetExtensions.DeserializeKinematicMotorState);
    netPacketProcessor.RegisterNestedType<PlayerSetupData>();
    netPacketProcessor.RegisterNestedType<PlayerMetadata>();
    netPacketProcessor.RegisterNestedType<InitialPlayerState>();
    netPacketProcessor.RegisterNestedType<PlayerState>();
    netPacketProcessor.RegisterNestedType<NetworkObjectState>();
    netPacketProcessor.RegisterNestedType<PlayerInputs>();
  }

  /// Update should be called every frame.
  public void Update() {
    netManager.PollEvents();
  }

	/// Stop all networking activity (shuts down the client or server).
	public void Stop() {
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

  public void SubscribeQueue<T>(Queue<T> queue) where T : class, new() {
    // TODO: Optimize with a packet pool using queue max size.
    netPacketProcessor.Subscribe((T data) => {
      queue.Enqueue(data);
    }, () => { return new T(); });
  }

  public void SubscribeQueue<T>(Queue<WithPeer<T>> queue) where T : class, new() {
    // TODO: Optimize with a packet pool using queue max size.
    netPacketProcessor.Subscribe((T data, NetPeer peer) => {
      queue.Enqueue(new WithPeer<T> { Peer = peer, Value = data });
    }, () => { return new T(); });
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
    PeerLatency[peer] = latency;
    DebugUI.ShowValue("ping", latency);
  }
}
