using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class DebugUI : MonoBehaviour {
  public Text debugText;

  private Dictionary<string, Dictionary<string, object>> debugValues = new Dictionary<string, Dictionary<string, object>>();

  private void Awake() {
    DontDestroyOnLoad(gameObject);

    Refresh();
  }

  public void ShowValueInternal(string category, string key, object val) {
    if (!debugValues.ContainsKey(category)) {
      debugValues[category] = new Dictionary<string, object>();
    }
    debugValues[category][key] = val;
    Refresh();
  }

  public void HideValueInternal(string category, string key) {
    if (!debugValues.ContainsKey(category)) {
      return;
    }
    debugValues[category].Remove(key);
    if (debugValues[category].Count < 1) {
      debugValues.Remove(category);
    }
    Refresh();
  }

  public static void ShowValue(string category, string key, object val) {
    Instance.ShowValueInternal(category, key, val);
  }

  public static void HideValue(string category, string key) {
    Instance.HideValueInternal(category, key);
  }

  private void Refresh() {
    StringBuilder builder = new StringBuilder();
    var categories = debugValues.Keys.OrderBy(k => k);
    foreach (var category in categories) {
      builder.AppendLine($"[{category}]");
      var keys = debugValues[category].Keys.OrderBy(k => k);
      foreach (var key in keys) {
        builder.AppendLine($"  {key}: {debugValues[category][key]}");
      }
    }
    debugText.text = builder.ToString();
  }

  // Singleton access.
  private static DebugUI _instance;
  private static DebugUI Instance {
    get {
      if (_instance == null) {
        _instance = GameObject.FindObjectOfType<DebugUI>();
      }
      return _instance;
    }
  }
}
