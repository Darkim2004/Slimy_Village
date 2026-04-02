using UnityEngine;

public enum SlimeAttackMode
{
    AoE,
    Directional
}

[CreateAssetMenu(menuName = "Game/Entities/Slime Definition")]
public class SlimeDefinition : EntityDefinition
{
    [Header("Attack")]
    [Tooltip("Modalità di attacco dello slime.")]
    public SlimeAttackMode attackMode = SlimeAttackMode.AoE;

    [Tooltip("Danno base dell'attacco AoE.")]
    public int attackDamage = 1;

    [Tooltip("Raggio dell'attacco ad area (unità world).")]
    public float attackRadius = 1.2f;

    [Tooltip("Secondi di ritardo (dall'inizio dell'animazione) prima che il danno venga applicato.")]
    public float attackHitboxDelay = 0.15f;

    [Tooltip("Distanza massima di inseguimento prima di perdere l'aggro.")]
    public float aggroRange = 5f;

    [Tooltip("Cooldown tra un attacco e l'altro (secondi).")]
    public float attackCooldown = 1.5f;

    [Tooltip("Velocità di corsa quando insegue l'attaccante.")]
    public float chaseSpeed = 2.5f;

    [Header("Directional Attack")]
    [Tooltip("Lunghezza dell'hitbox direzionale davanti allo slime.")]
    public float directionalLength = 1.6f;

    [Tooltip("Larghezza dell'hitbox direzionale.")]
    public float directionalWidth = 0.9f;

    [Tooltip("Offset extra in avanti oltre alla metà della lunghezza.")]
    public float directionalForwardOffset = 0f;
}
