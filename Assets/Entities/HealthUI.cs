using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visualizza gli HP del giocatore come cuori nella UI.
/// Ogni cuore rappresenta 2 HP: cuore intero = 2 HP, mezzo cuore = 1 HP, vuoto = 0 HP.
/// </summary>
public class HealthUI : MonoBehaviour
{
    [Header("Binding")]
    [Tooltip("Componente Health da osservare. Se null, cerca sul Player.")]
    [SerializeField] private Health health;

    [Header("Heart Sprites")]
    [Tooltip("Sprite cuore intero (2 HP).")]
    [SerializeField] private Sprite fullHeartSprite;
    [Tooltip("Sprite mezzo cuore (1 HP).")]
    [SerializeField] private Sprite halfHeartSprite;
    [Tooltip("Sprite cuore vuoto (0 HP).")]
    [SerializeField] private Sprite emptyHeartSprite;

    [Header("Layout")]
    [Tooltip("Container dove verranno istanziati i cuori.")]
    [SerializeField] private Transform heartsContainer;
    [Tooltip("Dimensione di ogni cuore in pixel.")]
    [SerializeField] private Vector2 heartSize = new Vector2(32, 32);

    private Image[] heartImages;

    private void OnEnable()
    {
        if (heartsContainer == null)
        {
            Debug.LogWarning("[HealthUI] Hearts container non assegnato.", this);
            return;
        }

        if (health == null)
        {
            var player = FindFirstObjectByType<PlayerTopDown>();
            if (player != null)
                health = player.GetComponent<Health>();
        }

        if (health != null)
        {
            health.onHpChanged += UpdateHearts;
            BuildHearts(health.MaxHp);
            UpdateHearts(health.CurrentHp, health.MaxHp);
        }
    }

    private void OnDisable()
    {
        if (health != null)
            health.onHpChanged -= UpdateHearts;
    }

    private void BuildHearts(int maxHp)
    {
        // Rimuovi cuori precedenti
        if (heartImages != null)
        {
            for (int i = 0; i < heartImages.Length; i++)
            {
                if (heartImages[i] != null)
                    Destroy(heartImages[i].gameObject);
            }
        }

        int heartCount = Mathf.CeilToInt(maxHp / 2f);
        heartImages = new Image[heartCount];

        for (int i = 0; i < heartCount; i++)
        {
            var go = new GameObject($"Heart_{i}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(heartsContainer, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = heartSize;

            var layout = go.GetComponent<LayoutElement>();
            layout.minWidth = heartSize.x;
            layout.minHeight = heartSize.y;
            layout.preferredWidth = heartSize.x;
            layout.preferredHeight = heartSize.y;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            var img = go.GetComponent<Image>();
            img.sprite = fullHeartSprite;
            img.raycastTarget = false;

            heartImages[i] = img;
        }
    }

    private void UpdateHearts(int currentHp, int maxHp)
    {
        int expectedHeartCount = Mathf.CeilToInt(maxHp / 2f);
        if (heartImages == null || heartImages.Length != expectedHeartCount)
            BuildHearts(maxHp);

        for (int i = 0; i < heartImages.Length; i++)
        {
            // Ogni cuore i rappresenta HP (i*2)+1 e (i*2)+2
            int hpForThisHeart = currentHp - (i * 2);

            if (hpForThisHeart >= 2)
                heartImages[i].sprite = fullHeartSprite;
            else if (hpForThisHeart == 1)
                heartImages[i].sprite = halfHeartSprite;
            else
                heartImages[i].sprite = emptyHeartSprite;
        }
    }
}
