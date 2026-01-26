using UnityEngine;

[CreateAssetMenu(menuName = "Game/Entities/Slime Definition")]
public class SlimeDefinition : EntityDefinition
{
    [Header("Attack (placeholder)")]
    [Tooltip("Identificatore dell'attacco (verrà usato più avanti).")]
    public string attackId = "none";

    // In futuro puoi aggiungere:
    // - danno
    // - resistenze
    // - colore / tint
    // - effetti speciali
}
