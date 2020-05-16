using LiteNetLib.Utils;
using UnityEngine;

/// Extensions to the LiteNetLib NetDataReader/NetDataWriter interface for other types.
public static class NetExtensions {
  private const float QUAT_FLOAT_PRECISION_MULT = 10000f;

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

  public static void SerializeQuaternion(NetDataWriter writer, Quaternion quaternion) {
    // Utilize "Smallest three" strategy.
    // Reference: https://gafferongames.com/post/snapshot_compression/
    byte maxIndex = 0;
    float maxValue = float.MinValue;
    float maxValueSign = 1;

    // Find the largest value in the quaternion and save its index.
    for (byte i = 0; i < 4; i++) {
      var value = quaternion[i];
      var absValue = Mathf.Abs(value);
      if (absValue > maxValue) {
        maxIndex = i;
        maxValue = absValue;

        // Note the sign of the maxValue for later.
        maxValueSign = Mathf.Sign(value);
      }
    }

    // Encode the smallest three components.
    short a, b, c;
    switch (maxIndex) {
      case 0:
        a = (short)(quaternion.y * maxValueSign * QUAT_FLOAT_PRECISION_MULT);
        b = (short)(quaternion.z * maxValueSign * QUAT_FLOAT_PRECISION_MULT);
        c = (short)(quaternion.w * maxValueSign * QUAT_FLOAT_PRECISION_MULT);
        break;
      case 1:
        a = (short)(quaternion.x * maxValueSign * QUAT_FLOAT_PRECISION_MULT);
        b = (short)(quaternion.z * maxValueSign * QUAT_FLOAT_PRECISION_MULT);
        c = (short)(quaternion.w * maxValueSign * QUAT_FLOAT_PRECISION_MULT);
        break;
      case 2:
        a = (short)(quaternion.x * maxValueSign * QUAT_FLOAT_PRECISION_MULT);
        b = (short)(quaternion.y * maxValueSign * QUAT_FLOAT_PRECISION_MULT);
        c = (short)(quaternion.w * maxValueSign * QUAT_FLOAT_PRECISION_MULT);
        break;
      case 3:
        a = (short)(quaternion.x * maxValueSign * QUAT_FLOAT_PRECISION_MULT);
        b = (short)(quaternion.y * maxValueSign * QUAT_FLOAT_PRECISION_MULT);
        c = (short)(quaternion.z * maxValueSign * QUAT_FLOAT_PRECISION_MULT);
        break;
      default:
        throw new System.InvalidProgramException("Unexpected quaternion index.");
    }

    writer.Put(maxIndex);
    writer.Put(a);
    writer.Put(b);
    writer.Put(c);
  }

  public static Quaternion DeserializeQuaternion(NetDataReader reader) {
    // Read values from the wire and map back to normal float precision.
    byte maxIndex = reader.GetByte();
    float a = reader.GetShort() / QUAT_FLOAT_PRECISION_MULT;
    float b = reader.GetShort() / QUAT_FLOAT_PRECISION_MULT;
    float c = reader.GetShort() / QUAT_FLOAT_PRECISION_MULT;

    // Reconstruct the fourth value.
    float d = Mathf.Sqrt(1f - (a * a + b * b + c * c));
    switch (maxIndex) {
      case 0:
        return new Quaternion(d, a, b, c);
      case 1:
        return new Quaternion(a, d, b, c);
      case 2:
        return new Quaternion(a, b, d, c);
      case 3:
        return new Quaternion(a, b, c, d);
      default:
        throw new System.InvalidProgramException("Unexpected quaternion index.");
    }
  }

  public static void Put(this NetDataWriter writer, Quaternion quaternion) {
    SerializeQuaternion(writer, quaternion);
  }

  public static Quaternion GetQuaternion(this NetDataReader reader) {
    return DeserializeQuaternion(reader);
  }

  public static void Put(this NetDataWriter writer, Vector3 vector) {
    SerializeVector3(writer, vector);
  }

  public static Vector3 GetVector3(this NetDataReader reader) {
    return DeserializeVector3(reader);
  }

  public static void PutArray<T>(this NetDataWriter writer, T[] array) where T : INetSerializable {
    writer.Put((ushort)array.Length);
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