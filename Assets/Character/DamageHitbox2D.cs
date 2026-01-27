using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DamageHitbox2D : MonoBehaviour
{
    [Header("Filters")]
    public LayerMask targetLayers;
    public bool ignoreTriggers = true;

    private Collider2D _col;
    private Transform _ownerRoot;
    private int _damage;

    // Per colpire una sola volta per entità (anche se ha più collider)
    private readonly HashSet<Health> _alreadyHit = new();

    private void Awake()
    {
        _col = GetComponent<Collider2D>();
        _col.isTrigger = true;
        _col.enabled = false;
    }

    public void Begin(Transform owner, int damage)
    {
        _ownerRoot = owner != null ? owner.root : null;
        _damage = damage;

        _alreadyHit.Clear();
        _col.enabled = true;
    }

    public void End()
    {
        _col.enabled = false;
        _alreadyHit.Clear();
        _ownerRoot = null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (ignoreTriggers && other.isTrigger) return;

        if (((1 << other.gameObject.layer) & targetLayers) == 0) return;

        // evita autocolpi
        if (_ownerRoot != null && other.transform.root == _ownerRoot) return;

        var health = other.GetComponentInParent<Health>();
        if (health == null || health.IsDead) return;

        if (_alreadyHit.Contains(health)) return;
        _alreadyHit.Add(health);

        health.TakeDamage(_damage);
    }
}