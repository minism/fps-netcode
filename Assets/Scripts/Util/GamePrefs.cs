using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GamePrefs {
  private const string NAME_KEY = "name";

  public static bool DebugMode { get; set; } = true;

  public static string GetPlayerName() {
    if (!PlayerPrefs.HasKey(NAME_KEY)) {
      return "Player";
    }
    return PlayerPrefs.GetString(NAME_KEY);
  }

  public static void SetPlayerName(string name) {
    PlayerPrefs.SetString(NAME_KEY, name);
  }
}
