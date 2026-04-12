using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestisce l'interazione speciale della ritual platform:
/// - prima attivazione: richiede key nello slot hotbar attivo, consuma 1 key, attiva le rune in sequenza.
/// - da quel momento in poi: teletrasporto diretto a BossBattle senza key.
/// </summary>
public class RitualPlatformInteraction : MonoBehaviour
{
    private enum InteractionMode
    {
        ToBossBattle,
        ReturnToNormalWorld
    }

    [Header("Requirement")]
    [SerializeField] private string requiredKeyItemId = "key";
    [SerializeField] private bool consumeKeyOnFirstActivation = true;

    [Header("Runes")]
    [SerializeField] private bool autoCollectRunesFromChildren = true;
    [SerializeField] private List<GameObject> runes = new List<GameObject>();
    [SerializeField] private float secondsBetweenRuneActivations = 1f;

    [Header("Scene Transition")]
    [SerializeField] private InteractionMode interactionMode = InteractionMode.ToBossBattle;
    [SerializeField] private string bossBattleSceneName = "BossBattle";
    [SerializeField] private string normalWorldSceneName = "Game";
    [SerializeField] private string preferredNormalWorldSpawnPointName = "SpawnPoint";
    [SerializeField] private float spawnWaitTimeoutSeconds = 10f;

    private bool interactionInProgress;
    private HotbarHUD cachedHotbarHUD;
    private InventoryModel cachedInventoryModel;

    private void Awake()
    {
        CollectRunesIfNeeded();
        ApplyRuneStateFromSave();
    }

    private void OnEnable()
    {
        // Riallinea lo stato visivo quando l'oggetto torna attivo.
        ApplyRuneStateFromSave();
    }

    private IEnumerator Start()
    {
        // In alcune scene il SaveSystem imposta lo stato poco dopo Awake: facciamo un breve retry.
        float elapsed = 0f;
        while (elapsed < 2f)
        {
            if (IsRitualUnlocked())
            {
                SetAllRunesActive(true);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyRuneStateFromSave();
    }

    public bool TryInteract(PlayerTopDown player)
    {
        if (player == null)
            return true;

        if (interactionInProgress)
            return true;

        StartCoroutine(HandleInteractionRoutine(player));
        return true;
    }

    private IEnumerator HandleInteractionRoutine(PlayerTopDown player)
    {
        interactionInProgress = true;
        player.SetInputLocked(true);

        try
        {
            if (interactionMode == InteractionMode.ReturnToNormalWorld)
            {
                yield return TransitionToNormalWorld(player);
                yield break;
            }

            bool alreadyUnlocked = IsRitualUnlocked();

            if (!alreadyUnlocked)
            {
                if (!TryConsumeRequiredKeyFromActiveSlot(player))
                {
                    Debug.Log("[RitualPlatform] Interazione bloccata: key non presente nello slot attivo della hotbar.", this);
                    yield break;
                }

                yield return PlayRuneActivationSequence();

                var saveSystem = WorldSaveSystem.Instance;
                if (saveSystem != null)
                    saveSystem.SetRitualPlatformUnlocked(true, saveImmediately: true);

                SetAllRunesActive(true);
            }
            else
            {
                SetAllRunesActive(true);
            }

            yield return TransitionToBossBattle(player);
        }
        finally
        {
            if (player != null)
                player.SetInputLocked(false);

            interactionInProgress = false;
        }
    }

    public void ConfigureReturnToWorld(string targetSceneName, string preferredSpawnPointName)
    {
        interactionMode = InteractionMode.ReturnToNormalWorld;

        if (!string.IsNullOrWhiteSpace(targetSceneName))
            normalWorldSceneName = targetSceneName.Trim();

        preferredNormalWorldSpawnPointName = string.IsNullOrWhiteSpace(preferredSpawnPointName)
            ? string.Empty
            : preferredSpawnPointName.Trim();
    }

    private IEnumerator PlayRuneActivationSequence()
    {
        var runtimeRunes = GetRuntimeRunes();
        if (runtimeRunes.Count == 0)
            yield break;

        SetRuneListActive(runtimeRunes, false);

        float stepDelay = Mathf.Max(0.01f, secondsBetweenRuneActivations);
        for (int i = 0; i < runtimeRunes.Count; i++)
        {
            runtimeRunes[i].SetActive(true);

            if (i < runtimeRunes.Count - 1)
                yield return new WaitForSeconds(stepDelay);
        }
    }

    private IEnumerator TransitionToBossBattle(PlayerTopDown player)
    {
        if (string.IsNullOrWhiteSpace(bossBattleSceneName))
        {
            Debug.LogWarning("[RitualPlatform] Nome scena BossBattle non configurato.", this);
            yield break;
        }

        WorldSaveSystem.Instance?.SaveNow("ritual-platform-transition");

        PersistRuntimeObjects(player);

        AsyncOperation loadOperation;
        try
        {
            loadOperation = SceneManager.LoadSceneAsync(bossBattleSceneName, LoadSceneMode.Single);
        }
        catch (Exception ex)
        {
            Debug.LogError("[RitualPlatform] Impossibile caricare la scena '" + bossBattleSceneName + "': " + ex.Message, this);
            yield break;
        }

        if (loadOperation == null)
        {
            Debug.LogError("[RitualPlatform] LoadSceneAsync ha restituito null.", this);
            yield break;
        }

        while (!loadOperation.isDone)
            yield return null;

        float timeout = Mathf.Max(1f, spawnWaitTimeoutSeconds);
        float elapsed = 0f;
        WorldGenTilemap bossWorldGen = null;

        while (elapsed < timeout)
        {
            bossWorldGen = FindFirstObjectByType<WorldGenTilemap>();
            if (bossWorldGen != null && bossWorldGen.HasGenerated)
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (player == null)
            yield break;

        Vector3 targetSpawn = player.transform.position;
        if (bossWorldGen != null && bossWorldGen.HasGenerated)
            targetSpawn = bossWorldGen.WorldSpawnPoint;
        else
            Debug.LogWarning("[RitualPlatform] WorldSpawnPoint BossBattle non disponibile entro timeout: uso posizione corrente player.", this);

        player.transform.position = targetSpawn;
        player.SetRespawnPoint(targetSpawn);
    }

    private IEnumerator TransitionToNormalWorld(PlayerTopDown player)
    {
        if (string.IsNullOrWhiteSpace(normalWorldSceneName))
        {
            Debug.LogWarning("[RitualPlatform] Nome scena mondo normale non configurato.", this);
            yield break;
        }

        WorldSaveSystem.Instance?.SaveNow("ritual-platform-return-transition");

        PersistRuntimeObjects(player);

        AsyncOperation loadOperation;
        try
        {
            loadOperation = SceneManager.LoadSceneAsync(normalWorldSceneName, LoadSceneMode.Single);
        }
        catch (Exception ex)
        {
            Debug.LogError("[RitualPlatform] Impossibile caricare la scena '" + normalWorldSceneName + "': " + ex.Message, this);
            yield break;
        }

        if (loadOperation == null)
        {
            Debug.LogError("[RitualPlatform] LoadSceneAsync ha restituito null.", this);
            yield break;
        }

        while (!loadOperation.isDone)
            yield return null;

        float timeout = Mathf.Max(1f, spawnWaitTimeoutSeconds);
        float elapsed = 0f;
        WorldGenTilemap worldGen = null;

        while (elapsed < timeout)
        {
            worldGen = FindFirstObjectByType<WorldGenTilemap>();
            if (worldGen != null && worldGen.HasGenerated)
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (player == null)
            yield break;

        Vector3 targetSpawn = player.transform.position;

        if (TryResolvePreferredNormalWorldSpawnPoint(out Vector3 preferredSpawn))
        {
            targetSpawn = preferredSpawn;
        }
        else if (worldGen != null && worldGen.HasGenerated)
        {
            targetSpawn = worldGen.WorldSpawnPoint;
        }
        else
        {
            Debug.LogWarning("[RitualPlatform] Spawn point normale non trovato e WorldSpawnPoint non disponibile entro timeout: uso posizione corrente player.", this);
        }

        player.transform.position = targetSpawn;
        player.SetRespawnPoint(targetSpawn);
    }

    private bool TryResolvePreferredNormalWorldSpawnPoint(out Vector3 spawn)
    {
        spawn = default;

        if (string.IsNullOrWhiteSpace(preferredNormalWorldSpawnPointName))
            return false;

        GameObject preferredSpawnObject = GameObject.Find(preferredNormalWorldSpawnPointName);
        if (preferredSpawnObject == null)
            return false;

        spawn = preferredSpawnObject.transform.position;
        return true;
    }

    private bool TryConsumeRequiredKeyFromActiveSlot(PlayerTopDown player)
    {
        if (string.IsNullOrWhiteSpace(requiredKeyItemId))
            return true;

        ResolveInventoryReferences(player);

        if (cachedHotbarHUD == null || cachedInventoryModel == null || cachedInventoryModel.Hotbar == null)
            return false;

        int selectedIndex = cachedHotbarHUD.SelectedIndex;
        var selectedStack = cachedInventoryModel.Hotbar.GetSlot(selectedIndex);

        if (!IsMatchingRequiredKey(selectedStack))
            return false;

        if (!consumeKeyOnFirstActivation)
            return true;

        selectedStack.amount -= 1;

        if (selectedStack.amount <= 0)
            cachedInventoryModel.Hotbar.SetSlot(selectedIndex, null);
        else
            cachedInventoryModel.Hotbar.SetSlot(selectedIndex, selectedStack);

        return true;
    }

    private bool IsMatchingRequiredKey(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty || stack.def == null)
            return false;

        string candidateId = stack.def.id;
        if (string.IsNullOrWhiteSpace(candidateId))
            return false;

        return string.Equals(candidateId.Trim(), requiredKeyItemId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void ResolveInventoryReferences(PlayerTopDown player)
    {
        if (cachedHotbarHUD == null)
            cachedHotbarHUD = FindFirstObjectByType<HotbarHUD>();

        if (cachedInventoryModel == null)
            cachedInventoryModel = player.GetComponentInParent<InventoryModel>();

        if (cachedInventoryModel == null)
            cachedInventoryModel = FindFirstObjectByType<InventoryModel>();
    }

    private void PersistRuntimeObjects(PlayerTopDown player)
    {
        if (player != null)
            DontDestroyOnLoad(player.gameObject);

        if (cachedHotbarHUD == null)
            cachedHotbarHUD = FindFirstObjectByType<HotbarHUD>();

        if (cachedHotbarHUD != null)
        {
            Canvas rootCanvas = cachedHotbarHUD.GetComponentInParent<Canvas>();
            if (rootCanvas != null)
                DontDestroyOnLoad(rootCanvas.gameObject);
        }

        if (EventSystem.current != null)
            DontDestroyOnLoad(EventSystem.current.gameObject);
    }

    private bool IsRitualUnlocked()
    {
        return WorldSaveSystem.Instance != null && WorldSaveSystem.Instance.IsRitualPlatformUnlocked;
    }

    private void ApplyRuneStateFromSave()
    {
        if (interactionInProgress)
            return;

        CollectRunesIfNeeded();
        SetAllRunesActive(IsRitualUnlocked());
    }

    private void CollectRunesIfNeeded()
    {
        runes = GetRuntimeRunes();
    }

    private void SetAllRunesActive(bool active)
    {
        SetRuneListActive(GetRuntimeRunes(), active);
    }

    private void SetRuneListActive(List<GameObject> targetRunes, bool active)
    {
        if (targetRunes == null || targetRunes.Count == 0)
            return;

        for (int i = 0; i < targetRunes.Count; i++)
            targetRunes[i].SetActive(active);
    }

    private List<GameObject> GetRuntimeRunes()
    {
        var collected = new List<GameObject>();

        if (autoCollectRunesFromChildren)
        {
            // Preferiamo i child che sembrano rune (nome + sprite), includendo anche inattivi.
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
                    collected.Add(childObject);
            }

            // Fallback: se non troviamo nomi rune, usiamo i child diretti con SpriteRenderer.
            if (collected.Count == 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child == null)
                        continue;

                    GameObject childObject = child.gameObject;
                    if (childObject != null && childObject.GetComponent<SpriteRenderer>() != null)
                        collected.Add(childObject);
                }
            }
        }
        else if (runes != null)
        {
            for (int i = 0; i < runes.Count; i++)
            {
                if (runes[i] != null)
                    collected.Add(runes[i]);
            }
        }

        collected.Sort(CompareRuneObjects);
        return collected;
    }

    private static int CompareRuneObjects(GameObject left, GameObject right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return 1;
        if (right == null) return -1;

        int leftNum = ExtractTrailingNumber(left.name);
        int rightNum = ExtractTrailingNumber(right.name);

        int numberCompare = leftNum.CompareTo(rightNum);
        if (numberCompare != 0)
            return numberCompare;

        return string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
    }

    private static int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
            return int.MaxValue;

        int end = value.Length - 1;
        while (end >= 0 && char.IsDigit(value[end]))
            end--;

        int start = end + 1;
        if (start >= value.Length)
            return int.MaxValue;

        string numberPart = value.Substring(start);
        return int.TryParse(numberPart, out int parsed) ? parsed : int.MaxValue;
    }
}
