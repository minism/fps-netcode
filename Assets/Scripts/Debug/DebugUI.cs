using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class DebugUI : MonoBehaviour {
  public Text debugText;

  private Dictionary<string, object> debugValues;

  private void Awake() {
    DontDestroyOnLoad(gameObject);

    debugValues = new Dictionary<string, object>();
    Refresh();
  }

  public void ShowValueInternal(string key, object val) {
    debugValues[key] = val;
    Refresh();
  }

  public void HideValueInternal(string key) {
    if (debugValues.Remove(key)) {
      Refresh();
    }
  }

  public static void ShowValue(string key, object val) {
    Instance.ShowValueInternal(key, val);
  }

  public static void HideValue(string key) {
    Instance.HideValueInternal(key);
  }

  private void Refresh() {
    StringBuilder builder = new StringBuilder();
    foreach (var item in debugValues) {
      builder.AppendLine($"[{item.Key}] {item.Value}");
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
