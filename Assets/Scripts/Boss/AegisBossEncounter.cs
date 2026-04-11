using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AegisBossEncounter : MonoBehaviour
{
    [SerializeField] private Health aegisHealth;
    [SerializeField] private List<Transform> pillarRoots = new List<Transform>();
    [SerializeField, Min(1)] private int damagePerDisabledPillar = 1;

    private readonly List<AegisPillarDamageable> _pillars = new List<AegisPillarDamageable>();
    private readonly HashSet<AegisPillarDamageable> _processedPillars = new HashSet<AegisPillarDamageable>();
    private bool _eventsBound;
    private int _lastDisabledPillarCount = -1;

    private void Awake()
    {
        if (aegisHealth == null)
            aegisHealth = GetComponent<Health>();
    }

    private void OnEnable()
    {
        RebuildPillarsFromSceneReferences();
        BindEvents();
        SyncBossHealthFromPillarState();
    }

    private void OnDisable()
    {
        UnbindEvents();
        _lastDisabledPillarCount = -1;
    }

    private void Update()
    {
        SyncBossHealthFromPillarState();
    }

    private void OnValidate()
    {
        if (damagePerDisabledPillar < 1)
            damagePerDisabledPillar = 1;
    }

    private void RebuildPillarsFromSceneReferences()
    {
        _pillars.Clear();
        _processedPillars.Clear();

        for (int i = 0; i < pillarRoots.Count; i++)
        {
            Transform root = pillarRoots[i];
            if (root == null)
                continue;

            AegisPillarDamageable pillar = root.GetComponent<AegisPillarDamageable>();
            if (pillar == null)
                continue;

            if (_pillars.Contains(pillar))
                continue;

            _pillars.Add(pillar);
        }

        if (_pillars.Count == 0)
        {
            AegisPillarDamageable[] discovered = FindObjectsByType<AegisPillarDamageable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < discovered.Length; i++)
            {
                AegisPillarDamageable pillar = discovered[i];
                if (pillar == null || _pillars.Contains(pillar))
                    continue;

                _pillars.Add(pillar);
            }
        }
    }

    private void BindEvents()
    {
        if (_eventsBound)
            return;

        for (int i = 0; i < _pillars.Count; i++)
        {
            AegisPillarDamageable pillar = _pillars[i];
            if (pillar == null)
                continue;

            pillar.OnPillarDisabled -= HandlePillarDisabled;
            pillar.OnPillarDisabled += HandlePillarDisabled;

            if (pillar.IsDisabled)
                _processedPillars.Add(pillar);
        }

        _eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!_eventsBound)
            return;

        for (int i = 0; i < _pillars.Count; i++)
        {
            AegisPillarDamageable pillar = _pillars[i];
            if (pillar != null)
                pillar.OnPillarDisabled -= HandlePillarDisabled;
        }

        _eventsBound = false;
    }

    private void HandlePillarDisabled(AegisPillarDamageable pillar)
    {
        if (pillar == null)
            return;

        if (!_processedPillars.Add(pillar))
            return;

        SyncBossHealthFromPillarState();
    }

    private void SyncBossHealthFromPillarState()
    {
        if (aegisHealth == null)
            return;

        int disabledCount = 0;
        for (int i = 0; i < _pillars.Count; i++)
        {
            AegisPillarDamageable pillar = _pillars[i];
            if (pillar != null && pillar.IsDisabled)
                disabledCount++;
        }

        if (disabledCount == _lastDisabledPillarCount)
            return;

        _lastDisabledPillarCount = disabledCount;

        int expectedHp = Mathf.Clamp(
            aegisHealth.MaxHp - (disabledCount * damagePerDisabledPillar),
            0,
            aegisHealth.MaxHp);

        if (aegisHealth.CurrentHp > expectedHp)
            aegisHealth.SetHp(expectedHp);

    }
}
