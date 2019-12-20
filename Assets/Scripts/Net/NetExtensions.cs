using LiteNetLib.Utils;
using UnityEngine;

/// Extensions to the LiteNetLib NetDataReader/NetDataWriter interface for other types.
public static class NetExtensions {
  public static void SerializeVector3(NetDataWriter writer, Vector3 vector) {
    writer.Put(vector.x);
    writer.Put(vector.y);
    writer.Put(vector.z);
  }

  public static Vector3 DeserializeVector3(NetDataReader reader) {
    Vector3 v;
    v.x = reader.GetFloat();
    v.y = reader.GetFloat();
    v.z = reader.GetFloat();
    return v;
  }

  public static void Put(this NetDataWriter writer, Vector3 vector) {
    SerializeVector3(writer, vector);
  }

  public static Vector3 GetVector3(this NetDataReader reader) {
    return DeserializeVector3(reader);
  }

  public static void PutArray<T>(this NetDataWriter writer, T[] array) where T : INetSerializable {
    writer.Put((ushort) array.Length);
    foreach (var obj in array) {
      writer.Put<T>(obj);
    }
  }

  public static T[] GetArray<T>(this NetDataReader reader) where T : INetSerializable, new() {
    var len = reader.GetUShort();
    var array = new T[len];
    for (int i = 0; i < len; i++) {
      array[i] = reader.Get<T>();
    }
    return array;
  }
}