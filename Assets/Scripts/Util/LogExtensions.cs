using UnityEngine;

/// Better debug logger which prepends class name.
public static class LogExtensions {

  public static void Log(this object obj, object message) {
    Debug.Log(FormatMessage(obj, message));
  }

  public static void LogAssertion(this object obj, object message) {
    Debug.LogAssertion(FormatMessage(obj, message));
  }

  public static void LogError(this object obj, object message) {
    Debug.LogError(FormatMessage(obj, message));
  }

  public static void LogWarning(this object obj, object message) {
    Debug.LogWarning(FormatMessage(obj, message));
  }

  private static string FormatMessage(this object obj, object message) {
    var className = obj.GetType().Name;
    return $"({className}) {message}";
  }

}
