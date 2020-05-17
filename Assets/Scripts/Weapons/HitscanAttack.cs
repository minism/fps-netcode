using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class HitscanAttack : MonoBehaviour {
  private LineRenderer lr;

  private void Start() {
    lr = GetComponent<LineRenderer>();
  }
}