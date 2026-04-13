using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemActionSfxAction
{
    AttackSwing,
    Consume,
    HarvestHit,
    BuildPlace
}

public enum ItemActionSfxItemMatchMode
{
    AnyItem,
    EquippedItem,
    ItemId,
    Category
}

[DisallowMultipleComponent]
public class ItemActionSfxDispatcher : MonoBehaviour
{
    [Serializable]
    public class Rule
    {
        public string ruleName = "Sword Swing";
        public bool enabled = true;

        [Header("Action Filter")]
        public ItemActionSfxAction action = ItemActionSfxAction.AttackSwing;

        [Header("Item Filter")]
        public ItemActionSfxItemMatchMode itemMatchMode = ItemActionSfxItemMatchMode.ItemId;
        public string itemId = "sword";
        public ItemCategory itemCategory = ItemCategory.Weapon;

        [Header("Audio")]
        public AudioClip[] clips;

        [Range(0f, 1f)]
        public float volume = 1f;

        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        [Min(0f)]
        public float minInterval = 0.03f;
    }

    [Header("Rules")]
    [Tooltip("Regole di dispatch SFX per azione+item. Match piu specifico vince (ItemId > Category > EquippedItem > AnyItem).")]
    [SerializeField] private List<Rule> rules = new List<Rule> { new Rule() };

    [Header("Debug")]
    [Tooltip("Logga warning quando non esiste alcuna regola per l'azione richiesta.")]
    [SerializeField] private bool warnWhenNoMatchingRule;

    [Tooltip("Logga warning quando una regola matcha ma non ha clip valide.")]
    [SerializeField] private bool warnWhenRuleHasNoClips = true;

    private readonly Dictionary<Rule, float> _lastPlayTimeByRule = new Dictionary<Rule, float>();
    private readonly HashSet<Rule> _loggedMissingClipRules = new HashSet<Rule>();

    public bool TryPlay(ItemActionSfxAction action, ItemDefinition itemDef, Vector3 worldPosition)
    {
        Rule bestRule = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < rules.Count; i++)
        {
            Rule rule = rules[i];
            if (!TryGetMatchScore(rule, action, itemDef, out int score))
                continue;

            if (score > bestScore)
            {
                bestScore = score;
                bestRule = rule;
            }
        }

        if (bestRule == null)
        {
            if (warnWhenNoMatchingRule)
            {
                string itemId = itemDef != null ? itemDef.id : "<none>";
                Debug.LogWarning("[ItemActionSfxDispatcher] Nessuna regola per action=" + action + " itemId=" + itemId + ".", this);
            }

            return false;
        }

        float now = Time.time;
        float minInterval = Mathf.Max(0f, bestRule.minInterval);
        if (_lastPlayTimeByRule.TryGetValue(bestRule, out float lastPlayTime) && now < lastPlayTime + minInterval)
            return false;

        AudioClip clip = PickRandomValidClip(bestRule.clips);
        if (clip == null)
        {
            if (warnWhenRuleHasNoClips && !_loggedMissingClipRules.Contains(bestRule))
            {
                Debug.LogWarning("[ItemActionSfxDispatcher] Regola '" + bestRule.ruleName + "' senza clip valide.", this);
                _loggedMissingClipRules.Add(bestRule);
            }

            return false;
        }

        float minPitch = Mathf.Max(0.1f, Mathf.Min(bestRule.pitchRange.x, bestRule.pitchRange.y));
        float maxPitch = Mathf.Max(minPitch, Mathf.Max(bestRule.pitchRange.x, bestRule.pitchRange.y));
        float pitch = UnityEngine.Random.Range(minPitch, maxPitch);

        GlobalAudioVolume.PlaySfx2D(clip, worldPosition, Mathf.Clamp01(bestRule.volume), pitch);
        _lastPlayTimeByRule[bestRule] = now;
        return true;
    }

    private static AudioClip PickRandomValidClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int first = UnityEngine.Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            int index = (first + i) % clips.Length;
            AudioClip clip = clips[index];
            if (clip != null)
                return clip;
        }

        return null;
    }

    private static bool TryGetMatchScore(Rule rule, ItemActionSfxAction action, ItemDefinition itemDef, out int score)
    {
        score = int.MinValue;

        if (rule == null || !rule.enabled)
            return false;

        if (rule.action != action)
            return false;

        switch (rule.itemMatchMode)
        {
            case ItemActionSfxItemMatchMode.AnyItem:
                score = 100;
                return true;

            case ItemActionSfxItemMatchMode.EquippedItem:
                if (itemDef == null)
                    return false;

                score = 200;
                return true;

            case ItemActionSfxItemMatchMode.Category:
                if (itemDef == null)
                    return false;

                if (itemDef.category != rule.itemCategory)
                    return false;

                score = 300;
                return true;

            case ItemActionSfxItemMatchMode.ItemId:
                if (itemDef == null)
                    return false;

                string ruleItemId = string.IsNullOrWhiteSpace(rule.itemId) ? string.Empty : rule.itemId.Trim();
                if (ruleItemId.Length == 0)
                    return false;

                if (!string.Equals(ruleItemId, itemDef.id, StringComparison.OrdinalIgnoreCase))
                    return false;

                score = 400;
                return true;

            default:
                return false;
        }
    }
}
