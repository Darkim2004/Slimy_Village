using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class AegisFirstEntryIntroCutscene : MonoBehaviour
{
    private const string BossBattleSceneName = "BossBattle";

    [Header("References")]
    [SerializeField] private PlayerTopDown player;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform aegisRoot;
    [SerializeField] private Animator aegisAnimator;
    [SerializeField] private AegisProjectileAttackController aegisAttackController;
    [SerializeField] private CinemachineCamera bossCamera;

    [Header("Animation")]
    [SerializeField] private string enterAnimationState = "enter";
    [SerializeField] private string idleAnimationState = "idle";
    [SerializeField, Min(0f)] private float enterDurationFallbackSeconds = 1f;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float playerHoldSeconds = 1f;
    [SerializeField, Min(0.1f)] private float panToAegisSeconds = 1.5f;
    [SerializeField, Min(0f)] private float bossHoldAfterEnterSeconds = 1f;
    [SerializeField, Min(0.1f)] private float panBackToPlayerSeconds = 1.2f;

    [Header("Camera")]
    [SerializeField] private float introStartOrthographicSize = 4f;
    [SerializeField] private float bossBattleOrthographicSize = 7f;

    private Coroutine _sequenceRoutine;
    private Transform _cameraPivot;
    private bool _inputLockedByCutscene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        EnsureInstanceInBossBattleScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !IsBossBattleScene(scene.name))
            return;

        EnsureInstanceInBossBattleScene();
    }

    private static void EnsureInstanceInBossBattleScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !IsBossBattleScene(activeScene.name))
            return;

        if (FindFirstObjectByType<AegisFirstEntryIntroCutscene>() != null)
            return;

        var go = new GameObject("AegisFirstEntryIntroCutscene");
        go.AddComponent<AegisFirstEntryIntroCutscene>();
    }

    private static bool IsBossBattleScene(string sceneName)
    {
        return string.Equals(sceneName, BossBattleSceneName, StringComparison.OrdinalIgnoreCase);
    }

    private void OnEnable()
    {
        if (!IsBossBattleScene(SceneManager.GetActiveScene().name))
            return;

        if (_sequenceRoutine == null)
            _sequenceRoutine = StartCoroutine(RunSequenceWhenReady());
    }

    private void OnDisable()
    {
        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }

        ReleaseInputLockIfNeeded();
        DestroyPivot();
    }

    private IEnumerator RunSequenceWhenReady()
    {
        while (WorldSaveSystem.Instance == null || !WorldSaveSystem.Instance.IsInitialized)
            yield return null;

        ResolveReferences();

        WorldSaveSystem saveSystem = WorldSaveSystem.Instance;
        if (saveSystem == null || saveSystem.IsAegisDefeated || saveSystem.IsAegisIntroPlayed)
        {
            _sequenceRoutine = null;
            yield break;
        }

        if (playerTransform == null || aegisRoot == null || bossCamera == null)
        {
            Debug.LogWarning("[AegisFirstEntryIntroCutscene] Missing references, skipping intro cutscene.", this);
            _sequenceRoutine = null;
            yield break;
        }

        bool combatNeedsReenable = false;

        try
        {
            if (aegisAttackController != null)
            {
                aegisAttackController.SetCombatEnabled(false);
                combatNeedsReenable = true;
            }

            SetInputLocked(true);

            EnsurePivot();
            _cameraPivot.position = playerTransform.position;
            _cameraPivot.rotation = playerTransform.rotation;
            bossCamera.Follow = _cameraPivot;

            SetCameraOrthographicSize(introStartOrthographicSize);

            if (aegisRoot.gameObject.activeSelf)
                aegisRoot.gameObject.SetActive(false);

            if (playerHoldSeconds > 0f)
                yield return new WaitForSeconds(playerHoldSeconds);

            yield return PanPivotAndZoom(
                playerTransform.position,
                aegisRoot.position,
                Mathf.Max(0.1f, panToAegisSeconds),
                introStartOrthographicSize,
                bossBattleOrthographicSize);

            SetCameraOrthographicSize(bossBattleOrthographicSize);

            aegisRoot.gameObject.SetActive(true);
            yield return null;

            ResolveReferences();

            if (aegisAttackController != null)
                aegisAttackController.SetCombatEnabled(false);

            PlayAnimation(aegisAnimator, enterAnimationState);
            float enterDuration = GetAnimationClipLengthOrFallback(aegisAnimator, enterAnimationState, enterDurationFallbackSeconds);
            if (enterDuration > 0f)
                yield return new WaitForSeconds(enterDuration);

            PlayAnimation(aegisAnimator, idleAnimationState);

            if (bossHoldAfterEnterSeconds > 0f)
                yield return new WaitForSeconds(bossHoldAfterEnterSeconds);

            yield return PanPivotAndZoom(
                aegisRoot.position,
                playerTransform.position,
                Mathf.Max(0.1f, panBackToPlayerSeconds),
                bossBattleOrthographicSize,
                bossBattleOrthographicSize);

            bossCamera.Follow = playerTransform;

            saveSystem.SetAegisIntroPlayed(true, saveImmediately: true);

            SetInputLocked(false);

            if (aegisAttackController != null)
            {
                aegisAttackController.SetCombatEnabled(true);
                combatNeedsReenable = false;
            }
        }
        finally
        {
            SetCameraOrthographicSize(bossBattleOrthographicSize);

            if (bossCamera != null)
                bossCamera.Follow = playerTransform != null ? playerTransform : _cameraPivot;

            ReleaseInputLockIfNeeded();

            if (combatNeedsReenable && aegisAttackController != null)
                aegisAttackController.SetCombatEnabled(true);

            DestroyPivot();
            _sequenceRoutine = null;
        }
    }

    private IEnumerator PanPivotAndZoom(Vector3 from, Vector3 to, float duration, float fromOrtho, float toOrtho)
    {
        if (_cameraPivot == null)
            yield break;

        _cameraPivot.position = from;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            _cameraPivot.position = Vector3.Lerp(from, to, t);
            SetCameraOrthographicSize(Mathf.Lerp(fromOrtho, toOrtho, t));

            yield return null;
        }

        _cameraPivot.position = to;
        SetCameraOrthographicSize(toOrtho);
    }

    private void ResolveReferences()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerTopDown>();

        if (playerTransform == null && player != null)
            playerTransform = player.transform;

        if (aegisAttackController == null)
            aegisAttackController = FindFirstObjectByType<AegisProjectileAttackController>();

        if (aegisRoot == null && aegisAttackController != null)
            aegisRoot = aegisAttackController.transform;

        if (aegisAnimator == null && aegisRoot != null)
            aegisAnimator = aegisRoot.GetComponent<Animator>();

        if (bossCamera == null)
            bossCamera = FindFirstObjectByType<CinemachineCamera>();
    }

    private void EnsurePivot()
    {
        if (_cameraPivot != null)
            return;

        var pivotGo = new GameObject("AegisIntroCameraPivot");
        _cameraPivot = pivotGo.transform;
    }

    private void DestroyPivot()
    {
        if (_cameraPivot == null)
            return;

        Destroy(_cameraPivot.gameObject);
        _cameraPivot = null;
    }

    private void SetCameraOrthographicSize(float size)
    {
        if (bossCamera == null)
            return;

        var lens = bossCamera.Lens;
        lens.OrthographicSize = size;
        bossCamera.Lens = lens;
    }

    private void SetInputLocked(bool locked)
    {
        if (player == null)
            return;

        player.SetInputLocked(locked);
        _inputLockedByCutscene = locked;
    }

    private void ReleaseInputLockIfNeeded()
    {
        if (!_inputLockedByCutscene)
            return;

        if (player != null)
            player.SetInputLocked(false);

        _inputLockedByCutscene = false;
    }

    private static void PlayAnimation(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        int stateHash = Animator.StringToHash(stateName);
        if (animator.HasState(0, stateHash))
            animator.Play(stateHash, 0, 0f);
    }

    private static float GetAnimationClipLengthOrFallback(Animator animator, string stateName, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(stateName))
            return Mathf.Max(0f, fallback);

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null)
                continue;

            if (string.Equals(clip.name, stateName, StringComparison.OrdinalIgnoreCase))
                return Mathf.Max(0f, clip.length);
        }

        return Mathf.Max(0f, fallback);
    }
}
