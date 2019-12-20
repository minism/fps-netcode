using LiteNetLib;

public struct WithPeer<T> {
  public NetPeer Peer;
  public T Value;
}
