using System;
using UnityEngine;
using LiteNetLib;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// Primary logic controller for managing client game state.
public class ClientLogicController : BaseLogicController, ClientPlayerInput.Handler {
  private NetPeer serverPeer;
  private PlayerSetupData playerSetupData;
  private ClientPlayerInput localPlayerInput;
  private Player localPlayer;

  protected override void Awake() {
    base.Awake();

    // Setup network event handling.
    netChannel.Subscribe<NetCommand.JoinAccepted>(HandleJoinAccepted);
    netChannel.Subscribe<NetCommand.PlayerJoined>(HandleOtherPlayerJoined);
    netChannel.Subscribe<NetCommand.PlayerLeft>(HandleOtherPlayerLeft);
  }

  protected override void Update() {
    base.Update();
  }

  public void TryJoinServer(string host, int port, PlayerSetupData playerSetupData) {
    Debug.Log($"Connecting to host {host}:{port}...");
    this.playerSetupData = playerSetupData;
		netChannel.ConnectToServer(host, port);
    LoadGameScene();
  }

  public void HandleClientPlayerInput(in ClientPlayerInput.Inputs inputs) {
  }

  private Player AddPlayerFromInitialServerState(InitialPlayerState playerData) {
    var playerObject = networkObjectManager.CreatePlayerGameObject(
        playerData.NetworkObjectState.NetworkId,
        playerData.NetworkObjectState.Position).gameObject;
    var player = playerManager.AddPlayer(
        playerData.PlayerId, playerData.Metadata, playerObject);
    return player;
  }

  /** Network command handling */

  private void HandleJoinAccepted(NetCommand.JoinAccepted cmd) {
		Debug.Log("Server join successful!");

    // Create our player object and attach client-specific components.
    localPlayer = AddPlayerFromInitialServerState(cmd.YourPlayerState);

    // Create player objects for existing clients.
    foreach (var state in cmd.ExistingPlayerStates) {
      AddPlayerFromInitialServerState(state);
    }
  }

  private void HandleOtherPlayerJoined(NetCommand.PlayerJoined cmd) {
    Debug.Log($"{cmd.PlayerState.Metadata.Name} joined the server.");
    AddPlayerFromInitialServerState(cmd.PlayerState);
  }

  private void HandleOtherPlayerLeft(NetCommand.PlayerLeft cmd) {
    var player = playerManager.GetPlayer(cmd.PlayerId);
    Debug.Log($"{player.Metadata.Name} left the server.");
    networkObjectManager.DestroyNetworkObject(player.NetworkObject);
    playerManager.RemovePlayer(player.PlayerId);
  }

	protected override void OnPeerConnected(NetPeer peer) {
    Debug.Log("Connected to host: " + peer.EndPoint);
    serverPeer = peer;

    // Send a join request.
    netChannel.SendCommand(serverPeer, new NetCommand.JoinRequest {
      PlayerSetupData = playerSetupData
    });
  }

  protected override void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
    if (serverPeer != null && peer != serverPeer) {
      Debug.LogError("Unexpected mismatch between disconnect peer and server peer!");
    }
    Debug.Log("Disconnected from host: " + disconnectInfo.Reason);
    serverPeer = null;

    // Return to the lobby.
    LoadLobbyScene();
  }
}
