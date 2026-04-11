using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AegisPillarDamageable : MonoBehaviour
{
    [Header("Hits")]
    [SerializeField, Min(1)] private int hitsToDisable = 6;

    [Header("Runes")]
    [SerializeField] private bool autoCollectRunesFromChildren = true;
    [SerializeField] private List<GameObject> runes = new List<GameObject>();

    public event Action<AegisPillarDamageable> OnPillarDisabled;

    public bool IsDisabled => _isDisabled;
    public int HitsRemaining => Mathf.Max(0, hitsToDisable - _hitsTaken);

    private readonly List<GameObject> _sortedRunesDescending = new List<GameObject>();
    private Collider2D[] _allColliders;
    private int _hitsTaken;
    private bool _isDisabled;

    private void Awake()
    {
        InitializeRuntimeState();
    }

    private void OnEnable()
    {
        if (_allColliders == null || _allColliders.Length == 0)
            _allColliders = GetComponentsInChildren<Collider2D>(true);

        SyncRuneVisuals();
    }

    private void OnValidate()
    {
        if (hitsToDisable < 1)
            hitsToDisable = 1;
    }

    public bool TryRegisterHit(GameObject attacker)
    {
        if (_isDisabled)
            return false;

        _hitsTaken = Mathf.Min(_hitsTaken + 1, hitsToDisable);
        SyncRuneVisuals();

        if (_hitsTaken >= hitsToDisable)
            DisablePillar();

        return true;
    }

    /// <summary>
    /// Disabilita immediatamente il pillar per test/debug senza richiedere colpi.
    /// </summary>
    public bool ForceDisableForDebug()
    {
        if (_isDisabled)
            return false;

        _hitsTaken = hitsToDisable;
        SyncRuneVisuals();
        DisablePillar();
        return true;
    }

    private void InitializeRuntimeState()
    {
        _allColliders = GetComponentsInChildren<Collider2D>(true);
        CollectRunes();

        _hitsTaken = Mathf.Clamp(_hitsTaken, 0, hitsToDisable);
        _isDisabled = _hitsTaken >= hitsToDisable;

        SyncRuneVisuals();

        if (_isDisabled)
            SetCollidersEnabled(false);
    }

    private void CollectRunes()
    {
        _sortedRunesDescending.Clear();

        if (autoCollectRunesFromChildren)
        {
            Transform[] descendants = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform child = descendants[i];
                if (child == null || child == transform)
                    continue;

                GameObject childObject = child.gameObject;
                if (childObject == null)
                    continue;

                if (childObject.GetComponent<SpriteRenderer>() == null)
                    continue;

                if (childObject.name.IndexOf("rune", StringComparison.OrdinalIgnoreCase) >= 0)
                    _sortedRunesDescending.Add(childObject);
            }
        }
        else
        {
            for (int i = 0; i < runes.Count; i++)
            {
                if (runes[i] != null)
                    _sortedRunesDescending.Add(runes[i]);
            }
        }

        _sortedRunesDescending.Sort(CompareRuneObjectsDescending);
    }

    private void SyncRuneVisuals()
    {
        if (_sortedRunesDescending.Count == 0)
            CollectRunes();

        for (int i = 0; i < _sortedRunesDescending.Count; i++)
        {
            GameObject rune = _sortedRunesDescending[i];
            if (rune == null)
                continue;

            bool shouldBeActive = i >= _hitsTaken;
            if (rune.activeSelf != shouldBeActive)
                rune.SetActive(shouldBeActive);
        }
    }

    private void DisablePillar()
    {
        if (_isDisabled)
            return;

        _isDisabled = true;
        SetCollidersEnabled(false);
        OnPillarDisabled?.Invoke(this);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_allColliders == null || _allColliders.Length == 0)
            _allColliders = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < _allColliders.Length; i++)
        {
            if (_allColliders[i] != null)
                _allColliders[i].enabled = enabled;
        }
    }

    private static int CompareRuneObjectsDescending(GameObject left, GameObject right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return 1;
        if (right == null) return -1;

        int leftNum = ExtractTrailingNumber(left.name);
        int rightNum = ExtractTrailingNumber(right.name);

        int numberCompare = rightNum.CompareTo(leftNum);
        if (numberCompare != 0)
            return numberCompare;

        return string.Compare(right.name, left.name, StringComparison.OrdinalIgnoreCase);
    }

    private static int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
            return int.MinValue;

        int end = value.Length - 1;
        while (end >= 0 && char.IsDigit(value[end]))
            end--;

        int start = end + 1;
        if (start >= value.Length)
            return int.MinValue;

        string numberPart = value.Substring(start);
        return int.TryParse(numberPart, out int parsed) ? parsed : int.MinValue;
    }
}
