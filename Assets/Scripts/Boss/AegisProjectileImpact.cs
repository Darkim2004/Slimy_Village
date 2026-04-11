using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AegisProjectileImpact : MonoBehaviour
{
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0f)] private float hitboxActiveSeconds = 0.08f;
    [SerializeField, Min(0.05f)] private float fallbackLifetimeSeconds = 0.7f;

    private Collider2D _collider;
    private Animator _animator;
    private GameObject _attacker;
    private bool _hasDamagedPlayer;

    public void Initialize(int projectileDamage, GameObject attacker, float fallbackLifetime)
    {
        damage = Mathf.Max(1, projectileDamage);
        _attacker = attacker;
        fallbackLifetimeSeconds = Mathf.Max(0.05f, fallbackLifetime);
    }

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _animator = GetComponent<Animator>();

        if (_collider != null)
            _collider.isTrigger = true;
    }

    private void OnEnable()
    {
        _hasDamagedPlayer = false;
        if (_collider != null)
            _collider.enabled = true;

        StartCoroutine(HitboxWindowRoutine());
        StartCoroutine(DestroyAtAnimationEndRoutine());
    }

    private System.Collections.IEnumerator HitboxWindowRoutine()
    {
        float activeWindow = Mathf.Max(0f, hitboxActiveSeconds);
        if (activeWindow > 0f)
            yield return new WaitForSeconds(activeWindow);

        if (_collider != null)
            _collider.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (_hasDamagedPlayer || other == null)
            return;

        PlayerTopDown player = other.GetComponentInParent<PlayerTopDown>();
        if (player == null)
            return;

        Health playerHealth = other.GetComponentInParent<Health>();
        if (playerHealth == null || playerHealth.IsDead)
            return;

        playerHealth.TakeDamage(damage, _attacker);
        _hasDamagedPlayer = true;
    }

    private System.Collections.IEnumerator DestroyAtAnimationEndRoutine()
    {
        float lifetime = ResolveLifetimeSeconds();
        yield return new WaitForSeconds(lifetime);

        if (gameObject != null)
            Destroy(gameObject);
    }

    private float ResolveLifetimeSeconds()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
            return fallbackLifetimeSeconds;

        AnimationClip[] clips = _animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
            return fallbackLifetimeSeconds;

        float maxLength = 0f;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.length > maxLength)
                maxLength = clip.length;
        }

        if (maxLength <= 0f)
            return fallbackLifetimeSeconds;

        return maxLength;
    }
}
