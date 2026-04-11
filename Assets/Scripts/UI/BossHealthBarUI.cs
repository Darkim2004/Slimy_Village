using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BossHealthBarUI : MonoBehaviour
{
    [SerializeField] private Health bossHealth;
    [SerializeField] private Image fillerImage;
    [SerializeField, Min(0f)] private float smoothingSpeed = 0f;

    private static Sprite _runtimeWhiteSprite;

    private bool _isSubscribed;
    private float _targetFill = 1f;
    private float _displayFill = 1f;

    private void Awake()
    {
        TryAutoResolveReferences();
    }

    private void OnEnable()
    {
        EnsureFillerSupportsFill();
        SubscribeToHealth();
        RefreshFromCurrentHealth();
    }

    private void OnDisable()
    {
        UnsubscribeFromHealth();
    }

    private void Update()
    {
        if (fillerImage == null)
            return;

        if (smoothingSpeed <= 0f)
        {
            _displayFill = _targetFill;
            ApplyDisplayFill();
            return;
        }

        _displayFill = Mathf.MoveTowards(
            _displayFill,
            _targetFill,
            smoothingSpeed * Time.deltaTime);

        ApplyDisplayFill();
    }

    public void Initialize(Health health, Image filler)
    {
        bool healthChanged = bossHealth != health;

        if (healthChanged)
            UnsubscribeFromHealth();

        bossHealth = health;
        fillerImage = filler;

        TryAutoResolveReferences();
        EnsureFillerSupportsFill();
        SubscribeToHealth();
        RefreshFromCurrentHealth();
    }

    private void TryAutoResolveReferences()
    {
        if (fillerImage == null)
        {
            Transform fillerTransform = transform.Find("Filler");
            if (fillerTransform != null)
                fillerImage = fillerTransform.GetComponent<Image>();
        }

        if (bossHealth == null)
        {
            GameObject aegis = GameObject.Find("Aegis");
            if (aegis != null)
                bossHealth = aegis.GetComponent<Health>();
        }
    }

    private void SubscribeToHealth()
    {
        if (_isSubscribed)
            return;

        if (bossHealth == null)
            return;

        bossHealth.onHpChanged += HandleBossHpChanged;
        _isSubscribed = true;
    }

    private void UnsubscribeFromHealth()
    {
        if (!_isSubscribed)
            return;

        if (bossHealth != null)
            bossHealth.onHpChanged -= HandleBossHpChanged;

        _isSubscribed = false;
    }

    private void RefreshFromCurrentHealth()
    {
        if (bossHealth == null)
        {
            SetTargetFill(0f);
            ApplyImmediateFill();
            return;
        }

        HandleBossHpChanged(bossHealth.CurrentHp, bossHealth.MaxHp);
        ApplyImmediateFill();
    }

    private void HandleBossHpChanged(int currentHp, int maxHp)
    {
        float target = maxHp > 0 ? (float)currentHp / maxHp : 0f;
        SetTargetFill(target);
    }

    private void SetTargetFill(float value)
    {
        _targetFill = Mathf.Clamp01(value);
    }

    private void ApplyImmediateFill()
    {
        _displayFill = _targetFill;
        ApplyDisplayFill();
    }

    private void ApplyDisplayFill()
    {
        if (fillerImage == null)
            return;

        fillerImage.fillAmount = Mathf.Clamp01(_displayFill);
    }

    private void EnsureFillerSupportsFill()
    {
        if (fillerImage == null)
            return;

        if (fillerImage.sprite == null)
            fillerImage.sprite = GetRuntimeWhiteSprite();

        fillerImage.type = Image.Type.Filled;
        fillerImage.fillMethod = Image.FillMethod.Horizontal;
        fillerImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillerImage.fillClockwise = true;
    }

    private static Sprite GetRuntimeWhiteSprite()
    {
        if (_runtimeWhiteSprite != null)
            return _runtimeWhiteSprite;

        Texture2D texture = Texture2D.whiteTexture;
        Rect rect = new Rect(0f, 0f, texture.width, texture.height);
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        _runtimeWhiteSprite = Sprite.Create(texture, rect, pivot, 100f);
        _runtimeWhiteSprite.name = "RuntimeWhiteUISprite";
        return _runtimeWhiteSprite;
    }
}
