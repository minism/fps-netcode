using UnityEngine;

public class DoubleBuffer<T> {
  private T[] values = new T[2];
  private int swapIndex = 0;

  public void Push(T value) {
    swapIndex = NextSwapIndex();
    values[swapIndex] = value;
  }

  public T New() {
    return values[swapIndex];
  }

  public T Old() {
    return values[NextSwapIndex()];
  }

  private int NextSwapIndex() {
    return (swapIndex == 0 ? 1 : 0);
  }
}
