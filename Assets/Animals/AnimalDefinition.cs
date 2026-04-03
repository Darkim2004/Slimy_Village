using UnityEngine;

[CreateAssetMenu(menuName = "Game/Entities/Animal Definition")]
public class AnimalDefinition : EntityDefinition
{
    [Header("Wander")]
    [Tooltip("Raggio massimo del wander rispetto alla posizione home.")]
    public float wanderRadius = 4.5f;

    [Tooltip("Range secondi di idle tra una decisione e l'altra.")]
    public Vector2 idleTimeRange = new Vector2(0.6f, 1.8f);

    [Tooltip("Range secondi di movimento verso un target wander.")]
    public Vector2 moveTimeRange = new Vector2(0.8f, 2.1f);

    [Range(0f, 1f)]
    [Tooltip("Probabilità di usare la corsa durante wander.")]
    public float wanderRunChance = 0.2f;

    [Header("Flee")]
    [Tooltip("Secondi di fuga dopo aver ricevuto danno.")]
    public float fleeDuration = 3f;

    [Tooltip("Moltiplicatore della runSpeed durante la fuga.")]
    public float fleeSpeedMultiplier = 1.5f;

    [Tooltip("Distanza minima desiderata dalla minaccia durante la fuga.")]
    public float fleePreferredDistance = 5f;

    [Tooltip("Secondi minimi prima di ri-triggerare la fuga da un nuovo colpo.")]
    public float fleeRetriggerCooldown = 0.1f;
}
