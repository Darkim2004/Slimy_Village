using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class AegisProjectileAttackController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap collisionTilemap;
    [SerializeField] private Animator aegisAnimator;
    [SerializeField] private GameObject ringPrefab;
    [SerializeField] private GameObject projectilePrefab;

    [Header("Projectile Attack")]
    [SerializeField, Min(1)] private int minProjectiles = 2;
    [SerializeField, Min(1)] private int maxProjectiles = 4;
    [SerializeField, Min(0)] private int cellRadius = 3;
    [SerializeField, Min(0f)] private float ringTelegraphSeconds = 1f;
    [SerializeField, Min(0f)] private float attackCooldownSeconds = 2f;
    [SerializeField, Min(0f)] private float startupDelaySeconds = 0.5f;
    [SerializeField, Min(0f)] private float preSequenceDelaySeconds = 4.5f;
    [SerializeField, Min(0.1f)] private float attackTotalDurationSeconds = 6f;
    [SerializeField, Min(1)] private int projectileDamage = 1;
    [SerializeField, Min(0.05f)] private float projectileLifetimeFallback = 0.7f;

    [Header("Animation")]
    [SerializeField] private string attackAnimationState = "attack3";
    [SerializeField] private string idleAnimationState = "idle";

    private readonly List<Vector3Int> _candidateCells = new List<Vector3Int>();
    private readonly List<Vector3Int> _selectedCells = new List<Vector3Int>();
    private readonly List<GameObject> _activeRings = new List<GameObject>();

    private Coroutine _attackLoopRoutine;
    private Health _aegisHealth;

    private void Awake()
    {
        _aegisHealth = GetComponent<Health>();

        if (aegisAnimator == null)
            aegisAnimator = GetComponent<Animator>();

        ResolveRuntimeReferences();
    }

    private void OnEnable()
    {
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
    }

    private IEnumerator AttackLoopRoutine()
    {
        if (startupDelaySeconds > 0f)
            yield return new WaitForSeconds(startupDelaySeconds);

        while (true)
        {
            ResolveRuntimeReferences();

            if (CanExecuteAttack())
                yield return ExecuteProjectileAttackRoutine();

            float cooldown = Mathf.Max(0f, attackCooldownSeconds);
            if (cooldown > 0f)
                yield return new WaitForSeconds(cooldown);
            else
                yield return null;
        }
    }

    private bool CanExecuteAttack()
    {
        if (_aegisHealth != null && _aegisHealth.IsDead)
            return false;

        return playerTransform != null
            && groundTilemap != null
            && ringPrefab != null
            && projectilePrefab != null;
    }

    private IEnumerator ExecuteProjectileAttackRoutine()
    {
        PlayAnimation(attackAnimationState);

        float attackStartTime = Time.time;

        float preDelay = Mathf.Max(0f, preSequenceDelaySeconds);
        if (preDelay > 0f)
            yield return new WaitForSeconds(preDelay);

        // Campiona la posizione player al termine del pre-delay (4.5s di default).
        SelectTargetCells();

        if (_selectedCells.Count > 0)
        {
            SpawnTelegraphRings();

            float telegraph = Mathf.Max(0f, ringTelegraphSeconds);
            if (telegraph > 0f)
                yield return new WaitForSeconds(telegraph);

            SpawnProjectilesAtSelectedCells();
            CleanupActiveRings();
        }

        float elapsed = Time.time - attackStartTime;
        float remaining = Mathf.Max(0f, attackTotalDurationSeconds - elapsed);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        PlayAnimation(idleAnimationState);
    }

    private void SelectTargetCells()
    {
        _candidateCells.Clear();
        _selectedCells.Clear();

        Vector3Int playerCell = groundTilemap.WorldToCell(playerTransform.position);
        int radius = Mathf.Max(0, cellRadius);
        int radiusSq = radius * radius;

        for (int y = playerCell.y - radius; y <= playerCell.y + radius; y++)
        {
            for (int x = playerCell.x - radius; x <= playerCell.x + radius; x++)
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

        int minCount = Mathf.Clamp(minProjectiles, 1, _candidateCells.Count);
        int maxCount = Mathf.Clamp(maxProjectiles, minCount, _candidateCells.Count);
        int count = Random.Range(minCount, maxCount + 1);

        for (int i = 0; i < count; i++)
        {
            int pick = Random.Range(i, _candidateCells.Count);

            Vector3Int swap = _candidateCells[i];
            _candidateCells[i] = _candidateCells[pick];
            _candidateCells[pick] = swap;

            _selectedCells.Add(_candidateCells[i]);
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

    private void SpawnTelegraphRings()
    {
        CleanupActiveRings();

        for (int i = 0; i < _selectedCells.Count; i++)
        {
            Vector3 pos = groundTilemap.GetCellCenterWorld(_selectedCells[i]);
            pos.z = 0f;

            GameObject ring = Instantiate(ringPrefab, pos, Quaternion.identity);
            if (ring != null)
                _activeRings.Add(ring);
        }
    }

    private void SpawnProjectilesAtSelectedCells()
    {
        for (int i = 0; i < _selectedCells.Count; i++)
        {
            Vector3 pos = groundTilemap.GetCellCenterWorld(_selectedCells[i]);
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
