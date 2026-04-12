using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AegisPillarDamageable : MonoBehaviour
{
    private const string OverlayShaderName = "Isometric/2D/HarvestableHitFlashOverlay";
    private const string HitFlashOverlayName = "__HitFlashOverlay";
    private static bool _loggedMissingOverlayShader;

    [Header("Hits")]
    [SerializeField, Min(1)] private int hitsToDisable = 6;

    [Header("Runes")]
    [SerializeField] private bool autoCollectRunesFromChildren = true;
    [SerializeField] private List<GameObject> runes = new List<GameObject>();

    [Header("Hit Flash")]
    [SerializeField] private bool enableHitFlash = true;
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.12f;
    [SerializeField, Range(0f, 1f)] private float hitFlashStrength = 0.9f;

    public event Action<AegisPillarDamageable> OnPillarDisabled;

    public bool IsDisabled => _isDisabled;
    public int HitsTaken => _hitsTaken;
    public int HitsRemaining => Mathf.Max(0, hitsToDisable - _hitsTaken);

    private readonly List<GameObject> _sortedRunesDescending = new List<GameObject>();
    private SpriteRenderer[] _flashRenderers;
    private SpriteRenderer[] _flashOverlays;
    private Coroutine _flashRoutine;
    private Material _flashOverlayMaterial;
    private int _hitsTaken;
    private bool _isDisabled;

    private void Awake()
    {
        InitializeRuntimeState();
    }

    private void OnEnable()
    {
        SyncRuneVisuals();
    }

    private void OnDisable()
    {
        SetOverlayAlpha(0f);
    }

    private void OnDestroy()
    {
        if (_flashOverlayMaterial != null)
            Destroy(_flashOverlayMaterial);
    }

    private void OnValidate()
    {
        if (hitsToDisable < 1)
            hitsToDisable = 1;

        if (hitFlashDuration < 0f)
            hitFlashDuration = 0f;
    }

    public bool TryRegisterHit(GameObject attacker)
    {
        if (_isDisabled)
            return false;

        PlayHitFlash();

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

    public void RestoreState(int hitsTaken, bool disabled)
    {
        _hitsTaken = Mathf.Clamp(hitsTaken, 0, hitsToDisable);
        _isDisabled = disabled || _hitsTaken >= hitsToDisable;

        if (_isDisabled)
            _hitsTaken = hitsToDisable;

        SyncRuneVisuals();
    }

    private void InitializeRuntimeState()
    {
        CollectRunes();
        CacheFlashRenderers();

        _hitsTaken = Mathf.Clamp(_hitsTaken, 0, hitsToDisable);
        _isDisabled = _hitsTaken >= hitsToDisable;

        SyncRuneVisuals();
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
        OnPillarDisabled?.Invoke(this);
    }

    private void CacheFlashRenderers()
    {
        _flashRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (_flashRenderers == null || _flashRenderers.Length == 0)
        {
            _flashOverlays = null;
            return;
        }

        _flashOverlays = new SpriteRenderer[_flashRenderers.Length];
        for (int i = 0; i < _flashRenderers.Length; i++)
        {
            SpriteRenderer source = _flashRenderers[i];
            if (source == null)
                continue;

            _flashOverlays[i] = GetOrCreateOverlay(source);
            SyncOverlayFromSource(i);
        }
    }

    private void PlayHitFlash()
    {
        if (!enableHitFlash || hitFlashDuration <= 0f)
            return;

        if (_flashRenderers == null || _flashRenderers.Length == 0)
            CacheFlashRenderers();

        if (_flashRenderers == null || _flashRenderers.Length == 0)
            return;

        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
            SetOverlayAlpha(0f);
        }

        _flashRoutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        float totalDuration = Mathf.Max(0.0001f, hitFlashDuration);
        float t = 0f;

        while (t < totalDuration)
        {
            float phase = t / totalDuration;
            float triangle = phase <= 0.5f
                ? phase / 0.5f
                : (1f - phase) / 0.5f;

            SetOverlayAlpha(triangle * hitFlashStrength);
            t += Time.deltaTime;
            yield return null;
        }

        SetOverlayAlpha(0f);
        _flashRoutine = null;
    }

    private SpriteRenderer GetOrCreateOverlay(SpriteRenderer source)
    {
        if (source == null)
            return null;

        Transform overlayTransform = source.transform.Find(HitFlashOverlayName);
        SpriteRenderer overlay = overlayTransform != null
            ? overlayTransform.GetComponent<SpriteRenderer>()
            : null;

        if (overlay == null)
        {
            GameObject overlayGo = new GameObject(HitFlashOverlayName);
            overlayGo.transform.SetParent(source.transform, false);
            overlay = overlayGo.AddComponent<SpriteRenderer>();
        }

        overlay.color = new Color(1f, 1f, 1f, 0f);
        overlay.enabled = false;

        if (_flashOverlayMaterial == null)
            _flashOverlayMaterial = CreateOverlayMaterial();

        if (_flashOverlayMaterial != null)
            overlay.sharedMaterial = _flashOverlayMaterial;

        return overlay;
    }

    private Material CreateOverlayMaterial()
    {
        Shader shader = Shader.Find(OverlayShaderName);
        if (shader == null && !_loggedMissingOverlayShader)
        {
            Debug.LogWarning("[AegisPillarDamageable] Shader overlay flash non trovato: " + OverlayShaderName + ". Uso fallback standard.", this);
            _loggedMissingOverlayShader = true;
        }

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        Material material = new Material(shader);
        material.name = "AegisPillarHitFlashOverlay";
        material.hideFlags = HideFlags.HideAndDontSave;

        if (material.HasProperty("_FlashColor"))
            material.SetColor("_FlashColor", Color.white);

        return material;
    }

    private void SyncOverlayFromSource(int index)
    {
        if (_flashRenderers == null || _flashOverlays == null)
            return;

        if (index < 0 || index >= _flashRenderers.Length)
            return;

        SpriteRenderer source = _flashRenderers[index];
        SpriteRenderer overlay = _flashOverlays[index];
        if (source == null || overlay == null)
            return;

        overlay.sprite = source.sprite;
        overlay.flipX = source.flipX;
        overlay.flipY = source.flipY;
        overlay.drawMode = source.drawMode;
        overlay.size = source.size;
        overlay.tileMode = source.tileMode;
        overlay.maskInteraction = source.maskInteraction;
        overlay.sortingLayerID = source.sortingLayerID;
        overlay.sortingOrder = source.sortingOrder + 1;
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (_flashRenderers == null || _flashOverlays == null)
            return;

        float clampedAlpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < _flashRenderers.Length; i++)
        {
            SpriteRenderer source = _flashRenderers[i];
            SpriteRenderer overlay = _flashOverlays[i];
            if (source == null || overlay == null)
                continue;

            SyncOverlayFromSource(i);

            float finalAlpha = clampedAlpha * source.color.a;
            overlay.color = new Color(1f, 1f, 1f, finalAlpha);
            overlay.enabled = finalAlpha > 0.001f && source.enabled;
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
