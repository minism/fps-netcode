using Ice;
using System.Collections;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(Lifetime))]
public class HitscanAttack : MonoBehaviour {
  public AnimationCurve fadeCurve;
  public float force = 10f;

  private LineRenderer lr;
  private Lifetime lt;
  private Color color;

  private void Start() {
    lr = GetComponent<LineRenderer>();
    lt = GetComponent<Lifetime>();
    color = lr.material.color;
  }

  private void Update() {
    var theta = lt.TimeLeft / lt.lifetime;
    var alpha = fadeCurve.Evaluate(theta);
    color.a = alpha;
    lr.material.color = color;
  }

  public GameObject CheckHit(bool excludeLocalPlayer = false) {
    int mask = LayerMask.GetMask("Player");
    var hits = Physics.RaycastAll(transform.position, transform.forward, float.MaxValue, mask);
    if (!excludeLocalPlayer) {
      return hits.Length > 0 ? hits[0].collider.gameObject : null;
    }

    // Exclude the local player.
    // TODO: Better solution for this.
    var first = hits.FirstOrDefault(hit => hit.collider.GetComponent<CPMPlayerController>() == null);
    if (first.Equals(default(RaycastHit))) {
      return null;
    }
    return first.collider.gameObject;
  }

  public void AddForceToPlayer(CPMPlayerController player) {
    player.AddKnockbackForce(transform.forward * force);
  }
}
