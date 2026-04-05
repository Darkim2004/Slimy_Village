using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class HarvestableNode : MonoBehaviour
{
    private const string OverlayShaderName = "Isometric/2D/HarvestableHitFlashOverlay";
    private static bool _loggedMissingOverlayShader;

    [Header("Health")]
    [Min(1)] public int maxHp = 3;

    [Header("Loot (optional)")]
    [Tooltip("LootTable usata quando il nodo viene distrutto.")]
    public LootTable lootTable;

    [Header("Rules")]
    [Tooltip("Se true, riceve danno solo se l'attaccante ha un tool valido equipaggiato.")]
    public bool requireHarvestTool = true;

    [Min(0)]
    [Tooltip("Livello di raccolta richiesto per danneggiare questo nodo. Se > 0, sovrascrive requireHarvestTool.")]
    public int requiredHarvestLevel = 0;

    [Tooltip("Se true, distrugge il GameObject quando gli HP arrivano a zero.")]
    public bool destroyOnDeath = true;

    [Header("Hit Flash")]
    [Tooltip("Se true, il nodo flasha di bianco quando subisce danno.")]
    public bool enableHitFlash = true;

    [Min(0f)]
    [Tooltip("Durata totale del flash in secondi.")]
    public float hitFlashDuration = 0.12f;

    [Range(0f, 1f)]
    [Tooltip("Intensita' del bianco del flash.")]
    public float hitFlashStrength = 0.9f;

    [Tooltip("Se true, sul colpo mortale ritarda la distruzione per mostrare il flash.")]
    public bool delayDestroyForHitFlash = true;

    public bool IsDestroyed { get; private set; }

    private int _currentHp;
    private SpriteRenderer[] _flashRenderers;
    private SpriteRenderer[] _flashOverlays;
    private Coroutine _flashRoutine;
    private Material _flashOverlayMaterial;

    private const string HitFlashOverlayName = "__HitFlashOverlay";

    private void Awake()
    {
        _currentHp = Mathf.Max(1, maxHp);
        IsDestroyed = false;
        CacheFlashRenderers();
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

    public void SetMaxHpAndReset(int hp)
    {
        maxHp = Mathf.Max(1, hp);
        _currentHp = maxHp;
        IsDestroyed = false;
    }

    public bool TryTakeDamage(int amount, GameObject attacker)
    {
        if (IsDestroyed) return false;

        int effectiveRequired = requiredHarvestLevel > 0
            ? requiredHarvestLevel
            : (requireHarvestTool ? 1 : 0);

        if (effectiveRequired > 0 && GetAttackerHarvestToolLevel(attacker) < effectiveRequired)
            return false;

        int effective = Mathf.Max(1, Mathf.Abs(amount));
        _currentHp -= effective;

        PlayHitFlash();

        if (_currentHp <= 0)
        {
            _currentHp = 0;
            IsDestroyed = true;

            if (lootTable != null)
                lootTable.SpawnLoot(transform.position);

            if (destroyOnDeath)
                Destroy(gameObject, GetDestroyDelay());
        }

        return true;
    }

    private int GetAttackerHarvestToolLevel(GameObject attacker)
    {
        if (attacker == null) return 0;

        HotbarEffectManager manager = attacker.GetComponentInChildren<HotbarEffectManager>();
        if (manager == null)
            manager = attacker.GetComponentInParent<HotbarEffectManager>();

        if (manager == null) return 0;
        return manager.HarvestToolLevel;
    }

    private void OnValidate()
    {
        if (maxHp < 1) maxHp = 1;
        if (requiredHarvestLevel < 0) requiredHarvestLevel = 0;
        if (hitFlashDuration < 0f) hitFlashDuration = 0f;
    }

    private float GetDestroyDelay()
    {
        if (!enableHitFlash || !delayDestroyForHitFlash)
            return 0f;

        return Mathf.Max(0f, hitFlashDuration);
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
            if (_flashRenderers[i] == null) continue;
            _flashOverlays[i] = GetOrCreateOverlay(_flashRenderers[i]);
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
        if (source == null) return null;

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
            Debug.LogWarning("[HarvestableNode] Shader overlay flash non trovato: " + OverlayShaderName + ". Uso fallback standard.", this);
            _loggedMissingOverlayShader = true;
        }

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        Material mat = new Material(shader);
        mat.name = "HarvestableHitFlashOverlay";
        mat.hideFlags = HideFlags.HideAndDontSave;

        if (mat.HasProperty("_FlashColor"))
            mat.SetColor("_FlashColor", Color.white);

        return mat;
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

        float clamped = Mathf.Clamp01(alpha);

        for (int i = 0; i < _flashRenderers.Length; i++)
        {
            SpriteRenderer source = _flashRenderers[i];
            SpriteRenderer overlay = _flashOverlays[i];
            if (source == null || overlay == null)
                continue;

            SyncOverlayFromSource(i);

            float finalAlpha = clamped * source.color.a;
            overlay.color = new Color(1f, 1f, 1f, finalAlpha);
            overlay.enabled = finalAlpha > 0.001f && source.enabled;
        }
    }
}
