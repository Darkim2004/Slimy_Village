using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AegisBossEncounter : MonoBehaviour
{
    [SerializeField] private Health aegisHealth;
    [SerializeField] private List<Transform> pillarRoots = new List<Transform>();
    [SerializeField, Min(1)] private int damagePerDisabledPillar = 1;

    [Header("Debug Testing")]
    [SerializeField] private bool enableDebugBreakHotkey;
    [SerializeField] private KeyCode debugBreakHotkey = KeyCode.F8;
    [SerializeField, Min(1)] private int debugBreakCount = 1;

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
        ApplyPersistedPillarState();
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

        if (enableDebugBreakHotkey && Input.GetKeyDown(debugBreakHotkey))
            DebugBreakPillars(debugBreakCount);
    }

    private void OnValidate()
    {
        if (damagePerDisabledPillar < 1)
            damagePerDisabledPillar = 1;

        if (debugBreakCount < 1)
            debugBreakCount = 1;
    }

    [ContextMenu("DEBUG/Break 1 Pillar")]
    private void DebugBreakOnePillarContextMenu()
    {
        DebugBreakPillars(1);
    }

    [ContextMenu("DEBUG/Break N Pillars (debugBreakCount)")]
    private void DebugBreakConfiguredPillarsContextMenu()
    {
        DebugBreakPillars(debugBreakCount);
    }

    /// <summary>
    /// Comando di test: rompe istantaneamente un numero specifico di pillar ancora attivi.
    /// </summary>
    public int DebugBreakPillars(int count)
    {
        if (count <= 0)
            return 0;

        RebuildPillarsFromSceneReferences();

        int broken = 0;
        for (int i = 0; i < _pillars.Count && broken < count; i++)
        {
            AegisPillarDamageable pillar = _pillars[i];
            if (pillar == null || pillar.IsDisabled)
                continue;

            if (pillar.ForceDisableForDebug())
                broken++;
        }

        SyncBossHealthFromPillarState();
        return broken;
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

    private void ApplyPersistedPillarState()
    {
        WorldSaveSystem saveSystem = WorldSaveSystem.Instance;
        if (saveSystem == null)
            return;

        if (!saveSystem.TryGetAegisState(out AegisStateData state) || state == null || state.pillars == null || state.pillars.Count == 0)
            return;

        for (int i = 0; i < _pillars.Count; i++)
        {
            AegisPillarDamageable pillar = _pillars[i];
            if (pillar == null)
                continue;

            AegisPillarStateData matching = null;
            for (int j = 0; j < state.pillars.Count; j++)
            {
                AegisPillarStateData candidate = state.pillars[j];
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.pillarName, pillar.name, StringComparison.OrdinalIgnoreCase))
                {
                    matching = candidate;
                    break;
                }
            }

            if (matching == null && i < state.pillars.Count)
                matching = state.pillars[i];

            if (matching == null)
                continue;

            pillar.RestoreState(matching.hitsTaken, matching.disabled);
            if (pillar.IsDisabled)
                _processedPillars.Add(pillar);
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
