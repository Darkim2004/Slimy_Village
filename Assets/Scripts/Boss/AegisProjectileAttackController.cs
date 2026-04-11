using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class AegisProjectileAttackController : MonoBehaviour
{
    private enum AttackType
    {
        None,
        Projectile,
        Summon
    }

    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap collisionTilemap;
    [SerializeField] private Animator aegisAnimator;
    [SerializeField] private GameObject ringPrefab;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private List<GameObject> summonPrefabPool = new List<GameObject>();

    [Header("Attack Cycle")]
    [SerializeField] private bool enableProjectileAttack = true;
    [SerializeField] private bool enableSummonAttack = true;
    [SerializeField, Min(0f)] private float attackCooldownSeconds = 2f;
    [SerializeField, Min(0f)] private float startupDelaySeconds = 0.5f;

    [Header("Projectile Attack")]
    [SerializeField, Min(1)] private int minProjectiles = 2;
    [SerializeField, Min(1)] private int maxProjectiles = 4;
    [SerializeField, Min(0)] private int cellRadius = 3;
    [SerializeField, Min(0f)] private float ringTelegraphSeconds = 1f;
    [SerializeField, Min(0f)] private float preSequenceDelaySeconds = 4.5f;
    [SerializeField, Min(0.1f)] private float attackTotalDurationSeconds = 6f;
    [SerializeField, Min(1)] private int projectileDamage = 1;
    [SerializeField, Min(0.05f)] private float projectileLifetimeFallback = 0.7f;

    [Header("Summon Attack")]
    [SerializeField, Min(1)] private int minSummons = 2;
    [SerializeField, Min(1)] private int maxSummons = 4;
    [SerializeField, Min(0)] private int summonCellRadius = 3;
    [SerializeField, Min(0f)] private float summonRingDelaySeconds = 2f;
    [SerializeField, Min(0f)] private float summonCountdownSeconds = 0.5f;
    [SerializeField, Min(0.1f)] private float summonTotalDurationSeconds = 3.5f;
    [SerializeField, Min(0)] private int maxActiveSummons = 8;

    [Header("Animation")]
    [SerializeField] private string attackAnimationState = "attack3";
    [SerializeField] private string summonAttackAnimationState = "attack4";
    [SerializeField] private string deathAnimationState = "death";
    [SerializeField] private bool freezeDeathOnLastFrame = true;
    [SerializeField, Min(0f)] private float deathFreezeDelayFallback = 1f;
    [SerializeField] private string idleAnimationState = "idle";

    private readonly List<Vector3Int> _candidateCells = new List<Vector3Int>();
    private readonly List<Vector3Int> _selectedProjectileCells = new List<Vector3Int>();
    private readonly List<Vector3Int> _selectedSummonCells = new List<Vector3Int>();
    private readonly List<GameObject> _activeRings = new List<GameObject>();
    private readonly List<GameObject> _activeSummons = new List<GameObject>();

    private Coroutine _attackLoopRoutine;
    private Coroutine _deathFreezeRoutine;
    private Health _aegisHealth;
    private bool _deathHandled;

    private void Awake()
    {
        _aegisHealth = GetComponent<Health>();

        if (aegisAnimator == null)
            aegisAnimator = GetComponent<Animator>();

        ResolveRuntimeReferences();
    }

    private void OnEnable()
    {
        if (_deathFreezeRoutine != null)
        {
            StopCoroutine(_deathFreezeRoutine);
            _deathFreezeRoutine = null;
        }

        if (aegisAnimator != null)
            aegisAnimator.speed = 1f;

        if (_aegisHealth != null)
        {
            _aegisHealth.onDeath -= HandleAegisDeath;
            _aegisHealth.onDeath += HandleAegisDeath;

            if (_aegisHealth.IsDead)
            {
                HandleAegisDeath();
                return;
            }

            _deathHandled = false;
        }

        if (_attackLoopRoutine == null)
            _attackLoopRoutine = StartCoroutine(AttackLoopRoutine());
    }

    private void OnDisable()
    {
        if (_attackLoopRoutine != null)
        {
            StopCoroutine(_attackLoopRoutine);
            _attackLoopRoutine = null;
        }

        if (_deathFreezeRoutine != null)
        {
            StopCoroutine(_deathFreezeRoutine);
            _deathFreezeRoutine = null;
        }

        if (_aegisHealth != null)
            _aegisHealth.onDeath -= HandleAegisDeath;

        CleanupActiveRings();
    }

    private void OnValidate()
    {
        if (minProjectiles < 1)
            minProjectiles = 1;

        if (maxProjectiles < 1)
            maxProjectiles = 1;

        if (maxProjectiles < minProjectiles)
            maxProjectiles = minProjectiles;

        if (cellRadius < 0)
            cellRadius = 0;

        if (projectileDamage < 1)
            projectileDamage = 1;

        if (projectileLifetimeFallback < 0.05f)
            projectileLifetimeFallback = 0.05f;

        if (attackTotalDurationSeconds < 0.1f)
            attackTotalDurationSeconds = 0.1f;

        if (preSequenceDelaySeconds < 0f)
            preSequenceDelaySeconds = 0f;

        if (preSequenceDelaySeconds > attackTotalDurationSeconds)
            preSequenceDelaySeconds = attackTotalDurationSeconds;

        if (minSummons < 1)
            minSummons = 1;

        if (maxSummons < 1)
            maxSummons = 1;

        if (maxSummons < minSummons)
            maxSummons = minSummons;

        if (summonCellRadius < 0)
            summonCellRadius = 0;

        if (summonTotalDurationSeconds < 0.1f)
            summonTotalDurationSeconds = 0.1f;

        if (summonRingDelaySeconds < 0f)
            summonRingDelaySeconds = 0f;

        if (summonRingDelaySeconds > summonTotalDurationSeconds)
            summonRingDelaySeconds = summonTotalDurationSeconds;

        if (summonCountdownSeconds < 0f)
            summonCountdownSeconds = 0f;

        if (maxActiveSummons < 0)
            maxActiveSummons = 0;

        if (deathFreezeDelayFallback < 0f)
            deathFreezeDelayFallback = 0f;
    }

    private void HandleAegisDeath()
    {
        if (_deathHandled)
            return;

        _deathHandled = true;

        if (_attackLoopRoutine != null)
        {
            StopCoroutine(_attackLoopRoutine);
            _attackLoopRoutine = null;
        }

        if (_deathFreezeRoutine != null)
        {
            StopCoroutine(_deathFreezeRoutine);
            _deathFreezeRoutine = null;
        }

        CleanupActiveRings();

        if (aegisAnimator != null)
            aegisAnimator.speed = 1f;

        PlayAnimation(deathAnimationState);

        if (freezeDeathOnLastFrame)
            _deathFreezeRoutine = StartCoroutine(FreezeDeathOnLastFrameRoutine());
    }

    private IEnumerator FreezeDeathOnLastFrameRoutine()
    {
        float waitSeconds = GetDeathAnimationLengthOrFallback();
        if (waitSeconds > 0f)
            yield return new WaitForSeconds(waitSeconds);

        if (aegisAnimator == null)
        {
            _deathFreezeRoutine = null;
            yield break;
        }

        int deathStateHash = Animator.StringToHash(deathAnimationState);
        if (aegisAnimator.HasState(0, deathStateHash))
        {
            aegisAnimator.Play(deathStateHash, 0, 0.999f);
            aegisAnimator.Update(0f);
        }

        aegisAnimator.speed = 0f;
        _deathFreezeRoutine = null;
    }

    private float GetDeathAnimationLengthOrFallback()
    {
        if (aegisAnimator == null || aegisAnimator.runtimeAnimatorController == null)
            return deathFreezeDelayFallback;

        AnimationClip[] clips = aegisAnimator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
                continue;

            if (string.Equals(clip.name, deathAnimationState, System.StringComparison.Ordinal))
                return clip.length;
        }

        return deathFreezeDelayFallback;
    }

    private IEnumerator AttackLoopRoutine()
    {
        if (startupDelaySeconds > 0f)
            yield return new WaitForSeconds(startupDelaySeconds);

        while (true)
        {
            ResolveRuntimeReferences();

            AttackType attackType = ChooseNextAttackType();
            if (attackType == AttackType.Projectile)
                yield return ExecuteProjectileAttackRoutine();
            else if (attackType == AttackType.Summon)
                yield return ExecuteSummonAttackRoutine();

            float cooldown = Mathf.Max(0f, attackCooldownSeconds);
            if (cooldown > 0f)
                yield return new WaitForSeconds(cooldown);
            else
                yield return null;
        }
    }

    private AttackType ChooseNextAttackType()
    {
        if (_aegisHealth != null && _aegisHealth.IsDead)
            return AttackType.None;

        bool projectileAvailable = enableProjectileAttack && CanExecuteProjectileAttack();
        bool summonAvailable = enableSummonAttack && CanExecuteSummonAttack();

        if (!projectileAvailable && !summonAvailable)
            return AttackType.None;

        if (projectileAvailable && summonAvailable)
            return Random.value < 0.5f ? AttackType.Projectile : AttackType.Summon;

        return projectileAvailable ? AttackType.Projectile : AttackType.Summon;
    }

    private bool CanExecuteProjectileAttack()
    {
        return playerTransform != null
            && groundTilemap != null
            && ringPrefab != null
            && projectilePrefab != null;
    }

    private bool CanExecuteSummonAttack()
    {
        return playerTransform != null
            && groundTilemap != null
            && ringPrefab != null
            && HasAnySummonPrefab()
            && GetRemainingSummonSlots() > 0;
    }

    private bool HasAnySummonPrefab()
    {
        if (summonPrefabPool == null || summonPrefabPool.Count == 0)
            return false;

        for (int i = 0; i < summonPrefabPool.Count; i++)
        {
            if (summonPrefabPool[i] != null)
                return true;
        }

        return false;
    }

    private IEnumerator ExecuteProjectileAttackRoutine()
    {
        if (!CanExecuteProjectileAttack())
            yield break;

        PlayAnimation(attackAnimationState);

        float attackStartTime = Time.time;

        float preDelay = Mathf.Max(0f, preSequenceDelaySeconds);
        if (preDelay > 0f)
            yield return new WaitForSeconds(preDelay);

        // Campiona la posizione player al termine del pre-delay (4.5s di default).
        SelectProjectileTargetCells();

        if (_selectedProjectileCells.Count > 0)
        {
            SpawnTelegraphRings(_selectedProjectileCells);

            float telegraph = Mathf.Max(0f, ringTelegraphSeconds);
            if (telegraph > 0f)
                yield return new WaitForSeconds(telegraph);

            SpawnProjectilesAtCells(_selectedProjectileCells);
            CleanupActiveRings();
        }

        float elapsed = Time.time - attackStartTime;
        float remaining = Mathf.Max(0f, attackTotalDurationSeconds - elapsed);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        PlayAnimation(idleAnimationState);
    }

    private IEnumerator ExecuteSummonAttackRoutine()
    {
        if (!CanExecuteSummonAttack())
            yield break;

        PlayAnimation(summonAttackAnimationState);

        float attackStartTime = Time.time;

        float ringDelay = Mathf.Clamp(summonRingDelaySeconds, 0f, summonTotalDurationSeconds);
        if (ringDelay > 0f)
            yield return new WaitForSeconds(ringDelay);

        SelectSummonTargetCells();

        if (_selectedSummonCells.Count > 0)
        {
            SpawnTelegraphRings(_selectedSummonCells);

            float countdown = Mathf.Max(0f, summonCountdownSeconds);
            if (countdown > 0f)
                yield return new WaitForSeconds(countdown);

            SpawnRandomSummonsAtCells(_selectedSummonCells);
            CleanupActiveRings();
        }

        float elapsed = Time.time - attackStartTime;
        float remaining = Mathf.Max(0f, summonTotalDurationSeconds - elapsed);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        PlayAnimation(idleAnimationState);
    }

    private void SelectProjectileTargetCells()
    {
        SelectRandomTargetCells(cellRadius, minProjectiles, maxProjectiles, _selectedProjectileCells);
    }

    private void SelectSummonTargetCells()
    {
        _selectedSummonCells.Clear();

        int remainingSlots = GetRemainingSummonSlots();
        if (remainingSlots <= 0)
            return;

        int cappedMax = Mathf.Min(maxSummons, remainingSlots);
        if (cappedMax <= 0)
            return;

        int cappedMin = Mathf.Min(minSummons, cappedMax);
        SelectRandomTargetCells(summonCellRadius, cappedMin, cappedMax, _selectedSummonCells);
    }

    private void SelectRandomTargetCells(int radius, int minCount, int maxCount, List<Vector3Int> output)
    {
        _candidateCells.Clear();
        output.Clear();

        if (playerTransform == null || groundTilemap == null)
            return;

        Vector3Int playerCell = groundTilemap.WorldToCell(playerTransform.position);
        int safeRadius = Mathf.Max(0, radius);
        int radiusSq = safeRadius * safeRadius;

        for (int y = playerCell.y - safeRadius; y <= playerCell.y + safeRadius; y++)
        {
            for (int x = playerCell.x - safeRadius; x <= playerCell.x + safeRadius; x++)
            {
                int dx = x - playerCell.x;
                int dy = y - playerCell.y;

                if ((dx * dx + dy * dy) > radiusSq)
                    continue;

                if (!IsValidTargetCell(x, y))
                    continue;

                _candidateCells.Add(new Vector3Int(x, y, 0));
            }
        }

        if (_candidateCells.Count == 0)
            return;

        int clampedMin = Mathf.Clamp(minCount, 1, _candidateCells.Count);
        int clampedMax = Mathf.Clamp(maxCount, clampedMin, _candidateCells.Count);
        int count = Random.Range(clampedMin, clampedMax + 1);

        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(i, _candidateCells.Count);

            Vector3Int swap = _candidateCells[i];
            _candidateCells[i] = _candidateCells[pick];
            _candidateCells[pick] = swap;

            output.Add(_candidateCells[i]);
        }
    }

    private bool IsValidTargetCell(int x, int y)
    {
        Vector3Int cell = new Vector3Int(x, y, 0);

        if (groundTilemap == null || groundTilemap.GetTile(cell) == null)
            return false;

        if (collisionTilemap != null && collisionTilemap.GetTile(cell) != null)
            return false;

        return true;
    }

    private void SpawnTelegraphRings(IReadOnlyList<Vector3Int> cells)
    {
        CleanupActiveRings();

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 pos = groundTilemap.GetCellCenterWorld(cells[i]);
            pos.z = 0f;

            GameObject ring = Instantiate(ringPrefab, pos, Quaternion.identity);
            if (ring != null)
                _activeRings.Add(ring);
        }
    }

    private void SpawnProjectilesAtCells(IReadOnlyList<Vector3Int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 pos = groundTilemap.GetCellCenterWorld(cells[i]);
            pos.z = 0f;

            GameObject projectileGo = Instantiate(projectilePrefab, pos, Quaternion.identity);
            if (projectileGo == null)
                continue;

            AegisProjectileImpact impact = projectileGo.GetComponent<AegisProjectileImpact>();
            if (impact == null)
                impact = projectileGo.AddComponent<AegisProjectileImpact>();

            impact.Initialize(projectileDamage, gameObject, projectileLifetimeFallback);
        }
    }

    private void SpawnRandomSummonsAtCells(IReadOnlyList<Vector3Int> cells)
    {
        int remainingSlots = GetRemainingSummonSlots();
        if (remainingSlots <= 0)
            return;

        int spawnCount = Mathf.Min(cells.Count, remainingSlots);
        for (int i = 0; i < spawnCount; i++)
        {
            GameObject summonPrefab = GetRandomSummonPrefab();
            if (summonPrefab == null)
                continue;

            Vector3 pos = groundTilemap.GetCellCenterWorld(cells[i]);
            pos.z = 0f;

            GameObject summon = Instantiate(summonPrefab, pos, Quaternion.identity);
            if (summon == null)
                continue;

            _activeSummons.Add(summon);
            ForceSummonAggro(summon);
        }
    }

    private GameObject GetRandomSummonPrefab()
    {
        if (summonPrefabPool == null || summonPrefabPool.Count == 0)
            return null;

        int start = Random.Range(0, summonPrefabPool.Count);
        for (int i = 0; i < summonPrefabPool.Count; i++)
        {
            int idx = (start + i) % summonPrefabPool.Count;
            GameObject candidate = summonPrefabPool[idx];
            if (candidate != null)
                return candidate;
        }

        return null;
    }

    private void ForceSummonAggro(GameObject summon)
    {
        if (summon == null || playerTransform == null)
            return;

        SlimeNormalAI normal = summon.GetComponent<SlimeNormalAI>();
        if (normal != null)
        {
            normal.ForceAggroTarget(playerTransform);
            return;
        }

        SNightSlimeAI night = summon.GetComponent<SNightSlimeAI>();
        if (night != null)
        {
            night.ForceAggroTarget(playerTransform);
            return;
        }

        summon.SendMessage("ForceAggroTarget", playerTransform, SendMessageOptions.DontRequireReceiver);
    }

    private int GetRemainingSummonSlots()
    {
        if (maxActiveSummons <= 0)
            return 0;

        CleanupDeadSummons();
        return Mathf.Max(0, maxActiveSummons - _activeSummons.Count);
    }

    private void CleanupDeadSummons()
    {
        for (int i = _activeSummons.Count - 1; i >= 0; i--)
        {
            if (_activeSummons[i] == null)
                _activeSummons.RemoveAt(i);
        }
    }

    private void CleanupActiveRings()
    {
        for (int i = 0; i < _activeRings.Count; i++)
        {
            if (_activeRings[i] != null)
                Destroy(_activeRings[i]);
        }

        _activeRings.Clear();
    }

    private void PlayAnimation(string stateName)
    {
        if (aegisAnimator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        aegisAnimator.Play(stateName, 0, 0f);
    }

    private void ResolveRuntimeReferences()
    {
        if (playerTransform == null)
        {
            PlayerTopDown player = FindFirstObjectByType<PlayerTopDown>();
            if (player != null)
                playerTransform = player.transform;
        }

        if (groundTilemap == null)
        {
            Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                Tilemap tilemap = tilemaps[i];
                if (tilemap != null && tilemap.name.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    groundTilemap = tilemap;
                    break;
                }
            }
        }

        if (collisionTilemap == null)
        {
            Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                Tilemap tilemap = tilemaps[i];
                if (tilemap != null && tilemap.name.IndexOf("Collision", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    collisionTilemap = tilemap;
                    break;
                }
            }
        }
    }
}
